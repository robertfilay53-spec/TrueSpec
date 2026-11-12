namespace TrueSpec.Collectors.Windows.Collectors;

using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// Crash forensics (U3): reads the Windows System event log read-only and
/// decodes the accident history into plain English: blue screens, hardware
/// errors, unexpected shutdowns and disk errors. Counts are exact log counts
/// in a stated 90-day window; first/last cover all time. The free-text
/// message is never parsed for facts. If the channel cannot be read, the
/// component is NotAvailable with the reason, never a failed collector.
/// No elevation is needed: the System channel is standard-user readable on a
/// normal machine.
/// </summary>
public sealed class CrashCollector : ICollector
{
    public string Id => "crash";

    public string Name => "Crash history";

    public string Category => "Diagnostics";

    public TimeSpan RefreshInterval => TimeSpan.FromHours(1);

    public bool RequiresAdministrator => false;

    private const string LogName = "System";
    private const string LogSource = "Windows Event Log (System)";

    /// <summary>
    /// One XPath filter per provider group. The cap is applied per class in
    /// the parser; the reader stops at the same cap so a noisy log cannot
    /// make the scan hang.
    /// </summary>
    private static readonly (string Provider, string Filter)[] QueryFilters =
    {
        ("Microsoft-Windows-WER-SystemErrorReporting",
            "*[System[Provider[@Name='Microsoft-Windows-WER-SystemErrorReporting'] and EventID=1001]]"),
        ("Microsoft-Windows-WHEA-Logger",
            "*[System[Provider[@Name='Microsoft-Windows-WHEA-Logger'] and (EventID=18 or EventID=19 or EventID=20)]]"),
        ("Microsoft-Windows-Kernel-Power",
            "*[System[Provider[@Name='Microsoft-Windows-Kernel-Power'] and EventID=41]]"),
        ("Disk",
            "*[System[Provider[@Name='Disk'] and (EventID=7 or EventID=11 or EventID=51)]]"),
        ("Ntfs",
            "*[System[Provider[@Name='Ntfs'] and (EventID=55 or EventID=98 or EventID=140)]]"),
        ("Microsoft-Windows-Ntfs",
            "*[System[Provider[@Name='Microsoft-Windows-Ntfs'] and EventID=140]]"),
    };

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var rows = new List<CrashHistoryParser.RawCrashEvent>();
            var anyQuerySucceeded = false;

            foreach (var (_, filter) in QueryFilters)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TryReadQuery(filter, rows))
                {
                    anyQuerySucceeded = true;
                }
            }

            if (!anyQuerySucceeded)
            {
                return Task.FromResult(ComponentSnapshot.NotAvailable(
                    Id,
                    Name,
                    Category,
                    Id,
                    "The Windows System event log could not be read. Crash history is not available on this machine."));
            }

            var installDate = ReadWindowsInstallDate();
            var earliestLogEntry = TryReadEarliestLogEntry();
            var history = CrashHistoryParser.Parse(
                rows,
                DateTimeOffset.Now,
                installDate,
                earliestLogEntry);

            return Task.FromResult(BuildSnapshot(history));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A collector never fails the scan: an unreadable log is a
            // NotAvailable component with the reason, never an exception.
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                $"The Windows System event log could not be read ({exception.GetType().Name}). Crash history is not available on this machine."));
        }
    }

    /// <summary>
    /// Reads one XPath query, newest first, up to the per-class cap. Returns
    /// false when the query itself could not run; a single unreadable record
    /// ends that query's read instead of failing the whole collector.
    /// </summary>
    private static bool TryReadQuery(string xpath, List<CrashHistoryParser.RawCrashEvent> rows)
    {
        try
        {
            var query = new EventLogQuery(LogName, PathType.LogName, xpath)
            {
                ReverseDirection = true,
            };

            using var reader = new EventLogReader(query);
            var read = 0;

            while (read < CrashHistoryParser.MaxEventsPerClass)
            {
                EventRecord record;
                try
                {
                    record = reader.ReadEvent()!;
                }
                catch
                {
                    break;
                }

                if (record is null)
                {
                    break;
                }

                using (record)
                {
                    if (record.TimeCreated is { } timeCreated &&
                        record.ProviderName is { } provider)
                    {
                        // Only provider + event id + timestamp are kept. The
                        // message is passed as null on purpose: the parser
                        // must never read message text for facts.
                        rows.Add(new CrashHistoryParser.RawCrashEvent(
                            provider,
                            record.Id,
                            new DateTimeOffset(timeCreated),
                            Message: null));
                        read++;
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// The oldest entry of the whole System channel, for the cleared-log
    /// rule. Null when the channel is empty or the oldest entry cannot be
    /// read; the parser then falls back to the oldest crash row.
    /// </summary>
    private static DateTimeOffset? TryReadEarliestLogEntry()
    {
        try
        {
            var query = new EventLogQuery(LogName, PathType.LogName, "*");
            using var reader = new EventLogReader(query);
            using var record = reader.ReadEvent();
            return record?.TimeCreated is { } timeCreated
                ? new DateTimeOffset(timeCreated)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset? ReadWindowsInstallDate()
    {
        var os = Wmi.Query("root\\cimv2", "SELECT InstallDate FROM Win32_OperatingSystem")
            .FirstOrDefault();
        return os?.Date("InstallDate");
    }

    private ComponentSnapshot BuildSnapshot(CrashHistory history)
    {
        var specs = new List<SpecValue>();

        foreach (var summary in history.Summaries)
        {
            var display = CrashHistoryParser.DisplayName(summary.Class);
            specs.Add(CollectorSupport.Spec(
                display + SpecLabels.CrashCountSuffix,
                summary.Count90d.ToString(CultureInfo.InvariantCulture),
                LogSource));
            specs.Add(CollectorSupport.Spec(
                display + SpecLabels.CrashFirstSuffix,
                FormatDate(summary.FirstAt),
                LogSource));
            specs.Add(CollectorSupport.Spec(
                display + SpecLabels.CrashRecentSuffix,
                FormatDate(summary.LastAt),
                LogSource));
        }

        specs.Add(CollectorSupport.Spec(
            SpecLabels.EarliestEventLogEntry,
            FormatDate(history.EarliestLogEntry),
            LogSource));

        if (history.LogClearedSuspected && history.EarliestLogEntry is { } earliest)
        {
            specs.Add(CollectorSupport.Spec(
                SpecLabels.CrashEventLogGap,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"No entries before {earliest:yyyy-MM-dd}. The log may have been cleared, so earlier crashes would not appear here."),
                LogSource));
        }

        var headline = BuildHeadline(history);
        var whatThisMeans = BuildWhatThisMeans(history);

        return ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            headline,
            specs,
            whatThisMeans);
    }

    private static string BuildHeadline(CrashHistory history)
    {
        var parts = history.Summaries
            .Where(s => s.Count90d > 0)
            .Select(s => string.Create(
                CultureInfo.InvariantCulture,
                $"{s.Count90d} {CountNoun(s.Class, s.Count90d)}"))
            .ToList();

        return parts.Count == 0
            ? "No crashes recorded in the last 90 days"
            : $"{string.Join(", ", parts)} in the last 90 days";
    }

    private static string BuildWhatThisMeans(CrashHistory history)
    {
        if (history.Summaries.Count == 0)
        {
            return "No blue screens, hardware errors, unexpected shutdowns or disk errors " +
                "were recorded in the System event log in the last 90 days.";
        }

        var sentences = history.Summaries
            .Select(s => s.PlainMeaning)
            .Select(meaning => string.Create(
                CultureInfo.InvariantCulture,
                $"{char.ToUpper(meaning[0], CultureInfo.InvariantCulture)}{meaning[1..]}."))
            .ToList();

        var text = string.Join(" ", sentences);

        if (history.LogClearedSuspected && history.EarliestLogEntry is { } earliest)
        {
            text += string.Create(
                CultureInfo.InvariantCulture,
                $" The event log has no entries before {earliest:yyyy-MM-dd}, so this history may be incomplete.");
        }

        return text;
    }

    private static string CountNoun(CrashClass crashClass, int count) => (crashClass, count) switch
    {
        (CrashClass.BugCheck, 1) => "blue screen",
        (CrashClass.BugCheck, _) => "blue screens",
        (CrashClass.WheaFatal, 1) => "fatal hardware error",
        (CrashClass.WheaFatal, _) => "fatal hardware errors",
        (CrashClass.WheaCorrected, 1) => "corrected hardware error",
        (CrashClass.WheaCorrected, _) => "corrected hardware errors",
        (CrashClass.UnexpectedShutdown, 1) => "unexpected shutdown",
        (CrashClass.UnexpectedShutdown, _) => "unexpected shutdowns",
        (CrashClass.DiskError, 1) => "disk error",
        (CrashClass.DiskError, _) => "disk errors",
        (_, 1) => "recorded error",
        (_, _) => "recorded errors",
    };

    private static string FormatDate(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        ?? SpecLabels.NotReportedByHardware;
}
