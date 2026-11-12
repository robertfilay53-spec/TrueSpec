namespace TrueSpec.Collectors.Windows.Diagnostics;

using System.Diagnostics;

/// <summary>Reads real event log counts. Unreadable logs return null.</summary>
internal static class WindowsEventLog
{
    // The event log is unbounded. Entries are newest-first and the scan
    // stops at the window cutoff, but a log full of recent entries could
    // still mean walking tens of thousands of COM objects on the scan
    // thread. Past this many examined entries the count stops: the number
    // is already large enough to say "many errors", and the scan must
    // finish. The same cap applies to the memory-error count.
    public const int MaxEntriesExamined = 10_000;

    /// <summary>
    /// The earliest entry still in the System log, or null when the log is
    /// empty or unreadable. Entries are chronological (oldest first), so the
    /// first entry is the earliest. A cleared or rotated log reports the
    /// oldest surviving entry, never a guessed date.
    /// </summary>
    public static DateTimeOffset? EarliestEntry()
    {
        try
        {
            using var log = new EventLog("System");
            if (log.Entries.Count == 0)
            {
                return null;
            }

            return new DateTimeOffset(log.Entries[0].TimeGenerated);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// One pass over the System log: the critical-error count, the memory
    /// error count and the earliest surviving entry, read from a single
    /// <see cref="EventLog"/> open. The System collector used to call
    /// <see cref="CountCriticalErrors"/>, <see cref="CountMemoryErrors"/>
    /// and <see cref="EarliestEntry"/> separately — three opens, each
    /// walking up to 10,000 entries through the legacy Event Log API — and
    /// the RAM collector walked it a fourth time. On a busy machine that
    /// was a large, repeated share of the scan. One open, one walk.
    /// A null field means that reading could not be made, never a guess.
    /// </summary>
    public sealed record SystemLogSummary(
        int? CriticalErrors,
        int? MemoryErrors,
        DateTimeOffset? EarliestEntry);

    public static SystemLogSummary ReadSummary(TimeSpan window)
    {
        try
        {
            using var log = new EventLog("System");
            if (log.Entries.Count == 0)
            {
                return new SystemLogSummary(null, null, null);
            }

            var earliest = new DateTimeOffset(log.Entries[0].TimeGenerated);
            var cutoff = DateTime.Now - window;
            var critical = 0;
            var memory = 0;
            var examined = 0;

            for (var i = log.Entries.Count - 1; i >= 0 && examined < MaxEntriesExamined; i--, examined++)
            {
                var entry = log.Entries[i];
                if (entry.TimeGenerated < cutoff)
                {
                    break;
                }

                if (entry.EntryType == EventLogEntryType.Error)
                {
                    critical++;
                }

                if (entry.EntryType is EventLogEntryType.Error or EventLogEntryType.Warning &&
                    IsMemoryError(entry))
                {
                    memory++;
                }
            }

            return new SystemLogSummary(critical, memory, earliest);
        }
        catch
        {
            return new SystemLogSummary(null, null, null);
        }
    }

    public static int? CountCriticalErrors(TimeSpan window) => ReadSummary(window).CriticalErrors;

    public static int? CountMemoryErrors(TimeSpan window) => ReadSummary(window).MemoryErrors;

    /// <summary>
    /// The memory-error predicate the old separate count used: a
    /// MemoryDiagnostic entry, or a WHEA-Logger entry that is a memory
    /// error (see <see cref="IsWheaMemoryError"/>). Every other error or
    /// warning source is not a memory error.
    /// </summary>
    private static bool IsMemoryError(EventLogEntry entry)
    {
        var source = entry.Source ?? string.Empty;
        if (source.Contains("MemoryDiagnostic", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return source.Contains("WHEA-Logger", StringComparison.OrdinalIgnoreCase) &&
            IsWheaMemoryError(entry);
    }

    /// <summary>
    /// WHEA-Logger covers every hardware error (CPU, PCIe, memory), so an
    /// event only counts when it is a memory error: event ID 47 is the
    /// corrected ECC memory error, and any WHEA event whose message names
    /// Memory as the reporting component also counts. Event 19 (the common
    /// one) is a CPU/bus machine check, not a memory error, and must not
    /// lower the RAM score.
    /// </summary>
    private static bool IsWheaMemoryError(EventLogEntry entry)
    {
        // The low 16 bits of InstanceId are the event ID. EventLogEntry.EventID
        // is obsolete and would raise a compiler warning, so it is not used.
        if ((entry.InstanceId & 0xFFFF) == 47)
        {
            return true;
        }

        try
        {
            var message = entry.Message;
            return !string.IsNullOrEmpty(message) &&
                message.Contains("Reported by component: Memory", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // The message could not be formatted; do not count the event.
            return false;
        }
    }
}
