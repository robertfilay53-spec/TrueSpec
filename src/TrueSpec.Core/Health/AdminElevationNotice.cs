namespace TrueSpec.Core.Health;

using TrueSpec.Core.Models;

/// <summary>
/// The "run as administrator" notice for the Overview page.
///
/// As a standard user, the admin-gated readings stay silent: SMART disk
/// wear needs elevation, LibreHardwareMonitor temperatures need elevation,
/// and a whole admin-gated component can come back NotAvailable. Before
/// this notice existed the app left the user guessing why half the screen
/// was blank. The rule is deliberately narrow so it never blames elevation
/// for something elevation cannot fix:
/// <list type="bullet">
/// <item>it only fires when the process is not elevated;</item>
/// <item>it only fires on a real Windows scan (the system identity must
/// have been read — on any other platform every component is unavailable
/// for platform reasons, not elevation);</item>
/// <item>it only names readings the admin-gated collectors own: a
/// NotAvailable CPU, GPU or Storage component, a silent CPU or GPU
/// temperature, or silent disk wear.</item>
/// </list>
/// A reading that is silent for a hardware reason (a drive that reports no
/// wear counter at all) is still named: the notice says to run as
/// administrator "for full results", never that elevation will fix that
/// exact reading. Pure and testable: the ViewModel only renders what this
/// returns.
/// </summary>
public static class AdminElevationNotice
{
    /// <summary>The headline of the notice, shown verbatim in the UI.</summary>
    public const string NoticeText = "Run as administrator for full results";

    // The collectors that need elevation (ICollector.RequiresAdministrator).
    // Keep in sync with CpuCollector, GpuCollector and StorageCollector.
    private static readonly string[] AdminGatedComponentIds = ["cpu", "gpu", "storage"];

    /// <summary>
    /// The full notice text for this snapshot, or null when no notice
    /// applies (elevated process, no admin-gated gaps, or no real Windows
    /// scan to judge). The text names the missing readings so the user can
    /// see what elevation would unlock.
    /// </summary>
    public static string? ForSnapshot(SystemSnapshot snapshot, bool isElevated)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (isElevated)
        {
            return null;
        }

        // No system identity means no real Windows scan: every component is
        // unavailable for platform reasons (Linux, macOS, a VM without
        // hardware access), and elevation advice would be nonsense.
        var system = snapshot.Find("system");
        if (system is null || IsNotAvailable(system))
        {
            return null;
        }

        var gaps = new List<string>();
        foreach (var id in AdminGatedComponentIds)
        {
            var component = snapshot.Find(id);
            if (component is null)
            {
                continue;
            }

            if (IsNotAvailable(component))
            {
                gaps.Add(component.Name);
                continue;
            }

            // Present but silent: the admin-gated sensor readings this
            // component owns did not report.
            var silent = id switch
            {
                "cpu" => SpecIsSilent(component, SpecLabels.PackageTemperature) ? "CPU temperature" : null,
                "gpu" => SpecIsSilent(component, SpecLabels.GPUTemperature) ? "GPU temperature" : null,
                "storage" => SpecIsSilent(component, SpecLabels.WearLevel) ? "disk wear" : null,
                _ => null,
            };
            if (silent is not null)
            {
                gaps.Add(silent);
            }
        }

        if (gaps.Count == 0)
        {
            return null;
        }

        return $"{NoticeText}. Some readings need administrator rights and are missing from this scan: {string.Join(", ", gaps)}. Close TrueSpec and run it as administrator to see them.";
    }

    private static bool IsNotAvailable(ComponentSnapshot component) =>
        string.Equals(component.Headline, "Not available", StringComparison.OrdinalIgnoreCase);

    private static bool SpecIsSilent(ComponentSnapshot component, string label) =>
        component.Specs.Any(spec =>
            string.Equals(spec.Label, label, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(spec.Value, SpecLabels.NotReportedByHardware, StringComparison.Ordinal));
}
