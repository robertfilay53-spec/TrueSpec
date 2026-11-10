namespace TrueSpec.Collectors.Windows.Diagnostics;

using System.Diagnostics;

/// <summary>Reads real event log counts. Unreadable logs return null.</summary>
internal static class WindowsEventLog
{
    public static int? CountCriticalErrors(TimeSpan window)
    {
        try
        {
            using var log = new EventLog("System");
            var cutoff = DateTime.Now - window;
            var count = 0;

            for (var i = log.Entries.Count - 1; i >= 0; i--)
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

            for (var i = log.Entries.Count - 1; i >= 0; i--)
            {
                var entry = log.Entries[i];
                if (entry.TimeGenerated < cutoff)
                {
                    break;
                }

                var source = entry.Source ?? string.Empty;
                if (entry.EntryType is EventLogEntryType.Error or EventLogEntryType.Warning &&
                    (source.Contains("WHEA-Logger", StringComparison.OrdinalIgnoreCase) ||
                     source.Contains("MemoryDiagnostic", StringComparison.OrdinalIgnoreCase)))
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
}
