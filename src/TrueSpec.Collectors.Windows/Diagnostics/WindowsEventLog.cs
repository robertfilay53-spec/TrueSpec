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

    public static int? CountCriticalErrors(TimeSpan window)
    {
        try
        {
            using var log = new EventLog("System");
            var cutoff = DateTime.Now - window;
            var count = 0;
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
                    count++;
                }
            }

            return count;
        }
        catch
        {
            return null;
        }
    }

    public static int? CountMemoryErrors(TimeSpan window)
    {
        try
        {
            using var log = new EventLog("System");
            var cutoff = DateTime.Now - window;
            var count = 0;
            var examined = 0;

            for (var i = log.Entries.Count - 1; i >= 0 && examined < MaxEntriesExamined; i--, examined++)
            {
                var entry = log.Entries[i];
                if (entry.TimeGenerated < cutoff)
                {
                    break;
                }

                if (entry.EntryType is not EventLogEntryType.Error and not EventLogEntryType.Warning)
                {
                    continue;
                }

                var source = entry.Source ?? string.Empty;
                if (source.Contains("MemoryDiagnostic", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
                else if (source.Contains("WHEA-Logger", StringComparison.OrdinalIgnoreCase) &&
                         IsWheaMemoryError(entry))
                {
                    count++;
                }
            }

            return count;
        }
        catch
        {
            return null;
        }
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
