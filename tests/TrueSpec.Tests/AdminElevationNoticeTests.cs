using TrueSpec.Core.Health;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

/// <summary>
/// The F3 notice rule, as pure logic over a snapshot: it fires only for a
/// standard user, only on a real Windows scan, and only names readings the
/// admin-gated collectors own.
/// </summary>
public class AdminElevationNoticeTests
{
    [Fact]
    public void ElevatedProcess_NeverShowsNotice()
    {
        var snapshot = WithSilentCpuTemp(TestSnapshots.Healthy());

        Assert.Null(AdminElevationNotice.ForSnapshot(snapshot, isElevated: true));
    }

    [Fact]
    public void StandardUser_FullResults_NoNotice()
    {
        // Healthy() reports a CPU temperature, a GPU temperature and disk
        // wear: nothing admin-gated is missing.
        Assert.Null(AdminElevationNotice.ForSnapshot(TestSnapshots.Healthy(), isElevated: false));
    }

    [Fact]
    public void StandardUser_SilentCpuTemperature_ShowsNoticeNamingIt()
    {
        var snapshot = WithSilentCpuTemp(TestSnapshots.Healthy());

        var notice = AdminElevationNotice.ForSnapshot(snapshot, isElevated: false);

        Assert.NotNull(notice);
        Assert.StartsWith(AdminElevationNotice.NoticeText, notice);
        Assert.Contains("CPU temperature", notice);
    }

    [Fact]
    public void StandardUser_SilentGpuTemperatureAndDiskWear_NamesBoth()
    {
        var snapshot = WithSilentGpuTempAndWear(TestSnapshots.Healthy());

        var notice = AdminElevationNotice.ForSnapshot(snapshot, isElevated: false);

        Assert.NotNull(notice);
        Assert.Contains("GPU temperature", notice);
        Assert.Contains("disk wear", notice);
    }

    [Fact]
    public void StandardUser_AdminGatedComponentNotAvailable_NamesIt()
    {
        var now = DateTimeOffset.UtcNow;
        var components = new[]
        {
            TestSnapshots.System(now),
            TestSnapshots.Cpu(),
            ComponentSnapshot.NotAvailable("storage", "Storage", "Storage", "storage", "SMART needs elevation."),
            TestSnapshots.Gpu(),
        };
        var snapshot = SnapshotWith(components);

        var notice = AdminElevationNotice.ForSnapshot(snapshot, isElevated: false);

        Assert.NotNull(notice);
        Assert.Contains("Storage", notice);
    }

    [Fact]
    public void StandardUser_NoSystemIdentity_NoNotice()
    {
        // The honest-fallback catalogue (Linux, macOS, a VM without hardware
        // access): every component is unavailable for platform reasons, and
        // elevation advice would be nonsense.
        var snapshot = new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Environment.MachineName,
            "Not reported",
            new[]
            {
                ComponentSnapshot.NotAvailable("system", "System", "System", "system", "Not available on this platform."),
                ComponentSnapshot.NotAvailable("cpu", "CPU", "CPU", "cpu", "Not available on this platform."),
                ComponentSnapshot.NotAvailable("storage", "Storage", "Storage", "storage", "Not available on this platform."),
            },
            ScoreResult.Empty,
            new BuyerCheckEvaluator().Evaluate(Array.Empty<ComponentSnapshot>(), null));

        Assert.Null(AdminElevationNotice.ForSnapshot(snapshot, isElevated: false));
    }

    private static SystemSnapshot WithSilentCpuTemp(SystemSnapshot snapshot)
    {
        var components = snapshot.Components.Select(component =>
            string.Equals(component.Id, "cpu", StringComparison.OrdinalIgnoreCase)
                ? component with
                {
                    Specs = new[] { new SpecValue("Package temperature", "Not reported by hardware", "LibreHardwareMonitor") },
                }
                : component).ToList();
        return snapshot with { Components = components };
    }

    private static SystemSnapshot WithSilentGpuTempAndWear(SystemSnapshot snapshot)
    {
        var components = snapshot.Components.Select(component =>
            string.Equals(component.Id, "gpu", StringComparison.OrdinalIgnoreCase)
                ? component with
                {
                    Specs = new[] { new SpecValue("GPU temperature", "Not reported by hardware", "LibreHardwareMonitor") },
                }
                : string.Equals(component.Id, "storage", StringComparison.OrdinalIgnoreCase)
                    ? component with
                    {
                        Specs = new[] { new SpecValue("Wear level", "Not reported by hardware", "SMART / NVMe reliability counters") },
                    }
                    : component).ToList();
        return snapshot with { Components = components };
    }

    private static SystemSnapshot SnapshotWith(IReadOnlyList<ComponentSnapshot> components)
    {
        var score = new ScoringEngine().Score(components);
        var checks = new BuyerCheckEvaluator().Evaluate(components, null);
        return new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Test Laptop",
            "Windows 11 Pro",
            components,
            score,
            checks);
    }
}
