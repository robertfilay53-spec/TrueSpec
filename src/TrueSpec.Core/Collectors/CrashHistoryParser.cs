namespace TrueSpec.Core.Collectors;

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
    /// Grace period for the cleared-log rule: the earliest log entry is
    /// expected within a week of the Windows install date (a fresh install
    /// starts a fresh log). This is the same rule and grace period as the
    /// authenticity engine (U1 R6); the two constants are defined separately
    /// for now and should be unified to one shared constant after merge.
    /// </summary>
    public static readonly TimeSpan LogClearedGracePeriod = TimeSpan.FromDays(7);

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
    /// Missing dates never suspect anything.
    /// </summary>
    public static bool IsLogClearedSuspected(
        DateTimeOffset? earliestLogEntry,
        DateTimeOffset? installDate)
    {
        if (earliestLogEntry is null || installDate is null)
        {
            return false;
        }

        return earliestLogEntry.Value > installDate.Value + LogClearedGracePeriod;
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
