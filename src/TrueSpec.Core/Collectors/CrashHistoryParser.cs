namespace TrueSpec.Core.Collectors;

using System.Globalization;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;

/// <summary>
/// Pure crash-history parsing: raw event rows in, per-class summaries out.
/// The parser relies on provider + event id + timestamp ONLY. It never reads
/// the free-text message for facts (message wording varies by Windows
/// version), and it never invents a number: counts are exact log counts in a
/// stated window. A cleared log is reported as a gap, not a clean record.
/// </summary>
public static class CrashHistoryParser
{
    /// <summary>How far back the per-class counts look.</summary>
    public static readonly TimeSpan CountWindow = TimeSpan.FromDays(90);

    /// <summary>Most events kept per class, newest first.</summary>
    public const int MaxEventsPerClass = 500;

    /// <summary>
    /// The recent-crash window, in days: a BugCheck or WHEA fatal inside
    /// this window fires the buyer-check failure and the Crash history
    /// alert card (Blueprint 3). One constant, shared by the evaluator
    /// and the Crash history page.
    /// </summary>
    public const int RecentWindowDays = 30;

    /// <summary>
    /// One raw System-channel row. <paramref name="Message"/> is accepted so
    /// callers can pass the record through unchanged, but it is never read:
    /// no fact in the output comes from message text.
    /// </summary>
    public sealed record RawCrashEvent(
        string Provider,
        int EventId,
        DateTimeOffset OccurredAt,
        string? Message);

    /// <summary>
    /// The fixed plain-meaning table. One line per class, no diagnosis
    /// beyond it.
    /// </summary>
    public static string PlainMeaning(CrashClass crashClass) => crashClass switch
    {
        CrashClass.BugCheck => "Windows stopped unexpectedly (blue screen)",
        CrashClass.WheaFatal => "a hardware component reported a fatal error",
        CrashClass.WheaCorrected => "a hardware error was corrected automatically",
        CrashClass.UnexpectedShutdown => "the machine lost power or shut down unexpectedly",
        CrashClass.DiskError => "the disk reported an error",
        _ => "an error was recorded in the event log",
    };

    /// <summary>
    /// Summarises raw rows into a <see cref="CrashHistory"/>. Counts cover
    /// the last 90 days; first/last cover all time within the per-class cap.
    /// </summary>
    /// <param name="rows">Raw System-channel rows, any order.</param>
    /// <param name="now">Reference time for the 90-day window.</param>
    /// <param name="installDate">
    /// Windows install date, or null when it is not reported. Needed for the
    /// cleared-log rule only.
    /// </param>
    /// <param name="earliestLogEntry">
    /// Oldest entry of the whole System channel, when the caller read it.
    /// When null, the oldest crash row is used instead.
    /// </param>
    public static CrashHistory Parse(
        IReadOnlyList<RawCrashEvent> rows,
        DateTimeOffset now,
        DateTimeOffset? installDate,
        DateTimeOffset? earliestLogEntry = null)
    {
        var cutoff = now - CountWindow;

        var events = rows
            .Select(Classify)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        var summaries = events
            .GroupBy(e => e.Class)
            .OrderBy(g => g.Key)
            .Select(group =>
            {
                // Newest first, capped: a machine that logs thousands of
                // corrected errors still reports an exact, bounded count.
                var kept = group
                    .OrderByDescending(e => e.OccurredAt)
                    .Take(MaxEventsPerClass)
                    .ToList();

                return new CrashClassSummary(
                    group.Key,
                    kept.Count(e => e.OccurredAt >= cutoff),
                    kept.Min(e => e.OccurredAt),
                    kept.Max(e => e.OccurredAt),
                    PlainMeaning(group.Key));
            })
            .ToList();

        var earliest = earliestLogEntry
            ?? (rows.Count == 0 ? null : rows.Min(r => r.OccurredAt));

        return new CrashHistory(
            summaries,
            earliest,
            IsLogClearedSuspected(earliest, installDate));
    }

    /// <summary>
    /// The cleared-log rule: the log is suspected cleared when its earliest
    /// entry is newer than the Windows install date plus the grace period.
    /// Missing dates never suspect anything. The grace period is the
    /// authenticity engine's threshold (U1 R6,
    /// <see cref="AuthenticityAnalyzer.LogGapThreshold"/>): there is exactly
    /// one definition of the 7-day value, owned by the analyzer.
    /// </summary>
    public static bool IsLogClearedSuspected(
        DateTimeOffset? earliestLogEntry,
        DateTimeOffset? installDate)
    {
        if (earliestLogEntry is null || installDate is null)
        {
            return false;
        }

        return earliestLogEntry.Value > installDate.Value + AuthenticityAnalyzer.LogGapThreshold;
    }

    /// <summary>
    /// Reads a crash history back out of the specs a <see cref="CrashCollector"/>
    /// wrote (the label contract in <see cref="SpecLabels"/>): one summary
    /// per class that has a count spec, plus the earliest-log-entry and
    /// log-gap specs. Returns null when the component is missing or carries
    /// no crash specs at all (a NotAvailable component only has a Status
    /// spec): no usable crash data, never an invented empty history. The
    /// collector always writes the earliest-entry spec when it succeeds,
    /// even when no crash class was recorded.
    /// </summary>
    public static CrashHistory? FromComponent(ComponentSnapshot? component)
    {
        if (component is null)
        {
            return null;
        }

        var specs = component.Specs.ToDictionary(
            s => s.Label,
            s => s.Value,
            StringComparer.OrdinalIgnoreCase);

        var summaries = new List<CrashClassSummary>();
        foreach (var (crashClass, display) in ClassDisplayNames)
        {
            if (!specs.TryGetValue(display + SpecLabels.CrashCountSuffix, out var countText) ||
                !int.TryParse(countText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                continue;
            }

            summaries.Add(new CrashClassSummary(
                crashClass,
                count,
                ReadSpecDate(specs, display + SpecLabels.CrashFirstSuffix),
                ReadSpecDate(specs, display + SpecLabels.CrashRecentSuffix),
                PlainMeaning(crashClass)));
        }

        var earliest = ReadSpecDate(specs, SpecLabels.EarliestEventLogEntry);
        var suspected = specs.ContainsKey(SpecLabels.CrashEventLogGap);

        if (summaries.Count == 0 && earliest is null && !suspected)
        {
            return null;
        }

        return new CrashHistory(summaries, earliest, suspected);
    }

    /// <summary>
    /// True when the class's most recent event falls inside the recent
    /// window (<see cref="RecentWindowDays"/> days before <paramref name="now"/>).
    /// A missing summary or a missing last date is never recent. The
    /// comparison is by calendar date: the specs this reads carry dates,
    /// not times, so an event dated exactly 30 days ago counts as recent.
    /// </summary>
    public static bool HasRecentEvent(CrashHistory history, CrashClass crashClass, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(history);

        var last = history.Summaries
            .FirstOrDefault(s => s.Class == crashClass)?.LastAt;
        return last is not null &&
            last.Value.UtcDateTime.Date >= now.UtcDateTime.Date.AddDays(-RecentWindowDays);
    }

    /// <summary>The spec display name per class, as written by the collector.</summary>
    public static string DisplayName(CrashClass crashClass) =>
        ClassDisplayNames.FirstOrDefault(pair => pair.Class == crashClass).Display
        ?? "Recorded errors";

    /// <summary>The spec display name per class, as written by the collector.</summary>
    internal static (CrashClass Class, string Display)[] ClassDisplayNames { get; } = new[]
    {
        (CrashClass.BugCheck, SpecLabels.CrashBlueScreens),
        (CrashClass.WheaFatal, SpecLabels.CrashFatalHardwareErrors),
        (CrashClass.WheaCorrected, SpecLabels.CrashCorrectedHardwareErrors),
        (CrashClass.UnexpectedShutdown, SpecLabels.CrashUnexpectedShutdowns),
        (CrashClass.DiskError, SpecLabels.CrashDiskErrors),
    };

    private static DateTimeOffset? ReadSpecDate(
        IReadOnlyDictionary<string, string> specs,
        string label)
    {
        if (!specs.TryGetValue(label, out var text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Values are "yyyy-MM-dd" or "Not reported by hardware". A value
        // that is neither is corrupt input, not a date: return null.
        return DateTimeOffset.TryParseExact(
            text.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static CrashEvent? Classify(RawCrashEvent row)
    {
        var crashClass = (row.Provider, row.EventId) switch
        {
            ("Microsoft-Windows-WER-SystemErrorReporting", 1001) => CrashClass.BugCheck,
            ("Microsoft-Windows-WHEA-Logger", 18) => CrashClass.WheaFatal,
            ("Microsoft-Windows-WHEA-Logger", 19) => CrashClass.WheaCorrected,
            // Event 20 is the informational form of a corrected hardware
            // error; it counts with the other corrected errors.
            ("Microsoft-Windows-WHEA-Logger", 20) => CrashClass.WheaCorrected,
            ("Microsoft-Windows-Kernel-Power", 41) => CrashClass.UnexpectedShutdown,
            ("Disk", 7 or 11 or 51) => CrashClass.DiskError,
            ("Ntfs", 55 or 98 or 140) => CrashClass.DiskError,
            ("Microsoft-Windows-Ntfs", 140) => CrashClass.DiskError,
            _ => (CrashClass?)null,
        };

        if (crashClass is null)
        {
            return null;
        }

        // Detail is provider + event id only: factual, and stable across
        // Windows versions, unlike the free-text message.
        return new CrashEvent(
            row.OccurredAt,
            crashClass.Value,
            $"Event {row.EventId}",
            row.Provider);
    }
}
