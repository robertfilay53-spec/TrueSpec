namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using Microsoft.Win32;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// Programs Windows launches at sign-in, read from the Run / RunOnce
/// registry keys. Read-only, no elevation: HKCU is the current user and
/// HKLM Run is world-readable. Nothing is started, stopped or changed.
/// The enabled/disabled state comes from Explorer's StartupApproved keys,
/// the same state Task Manager shows; when Windows does not record a state
/// the entry says so instead of guessing.
/// </summary>
public sealed class StartupCollector : ICollector
{
    public string Id => "startup";

    public string Name => "Startup";

    public string Category => "System";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(30);

    public bool RequiresAdministrator => false;

    // Cap the list: a Run key with hundreds of values is either broken or
    // hostile, and the detail page should not render all of them.
    private const int MaxEntries = 100;

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var programs = new List<StartupProgram>();
        var keysRead = new List<string>();
        var keysUnreadable = new List<string>();

        foreach (var key in RunKeys())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadKey(key, programs, keysRead, keysUnreadable);
            if (programs.Count >= MaxEntries)
            {
                break;
            }
        }

        if (keysRead.Count == 0)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "No Run registry key could be read." +
                (keysUnreadable.Count == 0 ? string.Empty : $" Unreadable: {string.Join("; ", keysUnreadable)}."))
                );
        }

        var enabled = programs.Count(p => p.IsEnabled is true);
        var disabled = programs.Count(p => p.IsEnabled is false);
        var unknown = programs.Count - enabled - disabled;

        var headline = programs.Count == 0
            ? "No startup programs found"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{programs.Count} startup program{(programs.Count == 1 ? string.Empty : "s")} - {enabled} enabled, {disabled} disabled");

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec("Startup programs", programs.Count.ToString(CultureInfo.InvariantCulture), "Registry Run / RunOnce keys"),
            CollectorSupport.Spec("Enabled", enabled.ToString(CultureInfo.InvariantCulture), "Explorer StartupApproved state"),
            CollectorSupport.Spec("Disabled", disabled.ToString(CultureInfo.InvariantCulture), "Explorer StartupApproved state"),
            CollectorSupport.Spec("State not reported", unknown.ToString(CultureInfo.InvariantCulture), "Explorer StartupApproved state"),
            CollectorSupport.Spec("Keys read", string.Join("; ", keysRead), "Registry Run / RunOnce keys"),
        };
        if (keysUnreadable.Count > 0)
        {
            specs.Add(CollectorSupport.Spec(
                "Keys not readable",
                string.Join("; ", keysUnreadable),
                "Registry access"));
        }

        if (programs.Count >= MaxEntries)
        {
            specs.Add(CollectorSupport.Spec(
                "List truncated",
                $"Showing the first {MaxEntries} entries.",
                "StartupCollector display cap"));
        }

        var usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var program in programs)
        {
            var label = program.Name;
            var numbered = label;
            var n = 2;
            while (!usedLabels.Add(numbered))
            {
                numbered = $"{label} #{n++}";
            }

            specs.Add(CollectorSupport.Spec(
                numbered,
                string.Create(CultureInfo.InvariantCulture, $"{program.Command} [{program.EnabledText}]"),
                program.Location));
        }

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            headline,
            specs,
            "These are the programs Windows launches when you sign in. A few entries are normal. " +
            "Many entries slow sign-in, and an entry you do not recognise is worth asking the seller about " +
            "before you buy. TrueSpec only lists them: it never disables or removes anything."));
    }

    private static IEnumerable<RunKey> RunKeys() => new[]
    {
        new RunKey(RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Current user - Run", ApprovedRun: true),
        new RunKey(RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "Current user - RunOnce", ApprovedRun: false),
        new RunKey(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "All users - Run", ApprovedRun: false),
        new RunKey(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "All users - Run (32-bit)", ApprovedRun: false),
        new RunKey(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "All users - RunOnce", ApprovedRun: false),
        new RunKey(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "All users - RunOnce (32-bit)", ApprovedRun: false),
    };

    private static void ReadKey(
        RunKey key,
        List<StartupProgram> programs,
        List<string> keysRead,
        List<string> keysUnreadable)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(key.Hive, key.View);
            using var runKey = baseKey.OpenSubKey(key.Path, writable: false);
            if (runKey is null)
            {
                // A missing Run key is normal (nothing registered there):
                // it counts as read, with zero entries.
                keysRead.Add($"{key.Location} (key absent)");
                return;
            }

            // StartupApproved only exists for the current user's Run key.
            // HKLM entries have no per-user approved state.
            Dictionary<string, byte[]>? approved = null;
            if (key.ApprovedRun)
            {
                approved = ReadApproved(baseKey);
            }

            keysRead.Add(key.Location);
            foreach (var valueName in runKey.GetValueNames())
            {
                if (programs.Count >= MaxEntries)
                {
                    return;
                }

                var kind = runKey.GetValueKind(valueName);
                if (kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
                {
                    continue;
                }

                var raw = runKey.GetValue(valueName) as string;
                byte[]? approvedBytes = null;
                approved?.TryGetValue(valueName, out approvedBytes);
                var program = StartupProgramParser.Build(valueName, raw, key.Location, approvedBytes);
                if (program is not null)
                {
                    programs.Add(program);
                }
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or ArgumentException)
        {
            keysUnreadable.Add($"{key.Location} ({exception.GetType().Name})");
        }
    }

    private static Dictionary<string, byte[]> ReadApproved(RegistryKey baseKey)
    {
        var approved = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = baseKey.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                writable: false);
            if (key is null)
            {
                return approved;
            }

            foreach (var valueName in key.GetValueNames())
            {
                if (key.GetValue(valueName) is byte[] bytes)
                {
                    approved[valueName] = bytes;
                }
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or ArgumentException)
        {
            // No approved state is not a failure: entries just report
            // "Not reported by Windows" for their state.
        }

        return approved;
    }

    private sealed record RunKey(
        RegistryHive Hive,
        RegistryView View,
        string Path,
        string Location,
        bool ApprovedRun);
}
