using System.Windows.Input;
using TrueSpec.App.ViewModels;
using TrueSpec.Core;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// P8: the Overview and the Report must always agree on the score and the
/// verdict, because both read the same snapshot. The AppOnFly case that
/// found this: Overview said "Not measurable / -- out of 100" while the
/// Report said 100 "Used - healthy" on the same machine. The Overview was
/// a page built from the loading snapshot; it is now one live instance,
/// updated in place after every scan, and while a scan runs it says
/// "Scanning..." instead of "Not measurable".
/// </summary>
public class ScoreAgreementTests
{
    [Fact]
    public async Task AppOnFlyVm_OverviewAndReportAgreeOnScoreVerdictAndExplanation()
    {
        var snapshot = await ScanAppOnFlyVmAsync();

        // The scan itself reproduces the observed Report screen exactly.
        Assert.Equal(100d, snapshot.Score.OverallScore);
        Assert.Equal(30d, snapshot.Score.MeasuredWeight);
        Assert.Equal(Verdict.UsedHealthy, snapshot.Score.Verdict);
        Assert.Equal(BuyerCheckState.Fail, snapshot.BuyerChecks.OverallState);
        // One check passes: authenticity. Its only runnable rule (R4, BIOS
        // date vs install date) ran clean on this VM, and the other rules
        // skipped for missing signals; the entry mirrors the analyzer's
        // "No signs of prior use" verdict and names the coverage gap in its
        // detail. The crash row is NotApplicable (no crash component).
        Assert.Equal(1, snapshot.BuyerChecks.Checks.Count(c => c.IsPass));

        using var overview = new OverviewViewModel(snapshot, _ => { }, new TestCommand(), new TestCommand(), new UnavailableSensorMonitor());
        var report = new ReportViewModel(snapshot, new TestCommand(), new TestCommand(), new TestCommand(), "/tmp");

        Assert.Equal("100", overview.ScoreText);
        Assert.Equal(report.ScoreText, overview.ScoreText);
        Assert.Equal(report.VerdictText, overview.VerdictText);
        Assert.Equal(report.ScoreExplanation, overview.ScoreExplanation);
        Assert.Equal("Used - healthy", overview.VerdictText);
    }

    [Fact]
    public void OverviewShowsScanningStateNotNotMeasurableWhileScanRuns()
    {
        var empty = new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Test Machine",
            "Windows 11 Pro",
            Array.Empty<ComponentSnapshot>(),
            ScoreResult.Empty,
            new BuyerCheckEvaluator().Evaluate(Array.Empty<ComponentSnapshot>(), null));

        using var overview = new OverviewViewModel(empty, _ => { }, new TestCommand(), new TestCommand(), new UnavailableSensorMonitor(), isScanning: true);

        Assert.Equal("…", overview.ScoreText);
        Assert.Equal("Scanning…", overview.VerdictText);
        Assert.Contains("still running", overview.ScoreExplanation, StringComparison.OrdinalIgnoreCase);

        // The same instance then takes the finished scan: no rebuild, no swap.
        var healthy = TestSnapshots.Healthy();
        var report = new ReportViewModel(healthy, new TestCommand(), new TestCommand(), new TestCommand(), "/tmp");
        overview.UpdateSnapshot(healthy, isScanning: false);

        Assert.Equal(report.ScoreText, overview.ScoreText);
        Assert.Equal(report.VerdictText, overview.VerdictText);
        Assert.Equal(report.ScoreExplanation, overview.ScoreExplanation);
    }

    [Fact]
    public void OverviewShowsNotMeasurableOnlyAfterFinishedScanWithNoData()
    {
        var empty = new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Test Machine",
            "Windows 11 Pro",
            Array.Empty<ComponentSnapshot>(),
            ScoreResult.Empty,
            new BuyerCheckEvaluator().Evaluate(Array.Empty<ComponentSnapshot>(), null));

        using var overview = new OverviewViewModel(empty, _ => { }, new TestCommand(), new TestCommand(), new UnavailableSensorMonitor(), isScanning: true);
        overview.UpdateSnapshot(empty, isScanning: false);

        Assert.Equal("--", overview.ScoreText);
        Assert.Equal("Not measurable", overview.VerdictText);
        Assert.Contains("No component exposes a measurable health value", overview.ScoreExplanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MainViewModelOverviewUpdatesInPlaceAfterScan()
    {
        var service = MachineScanService.Create(AppOnFlyComponents().Select(c => new FixedCollector(c)));
        var main = new MainViewModel(service, new UnavailableSensorMonitor(), new BuyerCheckEvaluator());

        var overviewBefore = main.Overview;
        Assert.Equal("…", overviewBefore.ScoreText);
        Assert.Same(overviewBefore, main.CurrentPage);

        main.BeginInitialScan();

        var landed = false;
        for (var i = 0; i < 100; i++)
        {
            await Task.Delay(100);
            if (main.Overview.ScoreText == "100")
            {
                landed = true;
                break;
            }
        }

        Assert.True(landed, "The Overview did not show the finished scan's score within 10 seconds");
        Assert.Same(overviewBefore, main.Overview);
        Assert.Same(main.Overview, main.CurrentPage);
        Assert.Equal("Used - healthy", main.Overview.VerdictText);

        main.SelectedNavItem = main.NavItems.First(n => n.Key == "report");
        var report = Assert.IsType<ReportViewModel>(main.CurrentPage);
        Assert.Equal("100", report.ScoreText);
        Assert.Equal("Used - healthy", report.VerdictText);

        main.SelectedNavItem = main.NavItems.First(n => n.Key == "overview");
        Assert.Same(main.Overview, main.CurrentPage);
        Assert.Equal("100", main.Overview.ScoreText);
    }

    /// <summary>
    /// The AppOnFly VM from the Nov 11 final verification: Microsoft
    /// Corporation Virtual Machine, AMD EPYC 9174F, 1.1 GB RAM, Msft Virtual
    /// Disk, Hyper-V Video. Only CPU (temperature) and RAM (zero memory
    /// errors) expose a measurable health value: measured weight 30.
    /// </summary>
    private static async Task<SystemSnapshot> ScanAppOnFlyVmAsync()
    {
        var service = MachineScanService.Create(AppOnFlyComponents().Select(c => new FixedCollector(c)));
        return await service.ScanAsync(null, CancellationToken.None, ScanTimeouts.Default);
    }

    private static IReadOnlyList<ComponentSnapshot> AppOnFlyComponents()
    {
        ComponentSnapshot component(string id, string name, string category, string headline, params SpecValue[] specs) =>
            new(id, name, category, id, headline, null, specs, "Test component.", DateTimeOffset.UtcNow);

        SpecValue spec(string label, string value) => new(label, value, "Test");

        return new[]
        {
            component("system", "System", "System", "APPCLOUD-1234",
                spec("Operating system", "Microsoft Windows 11 Pro"),
                spec("Windows install date", "2024-01-10"),
                spec("BIOS date", "2021-06-15"),
                spec("Critical errors (event log)", "Not reported"),
                spec("Signed drivers only", "Unknown")),
            component("cpu", "CPU", "CPU", "AMD EPYC 9174F",
                spec("Package temperature", "55 C")),
            component("ram", "RAM", "RAM", "1.1 GB",
                spec("Total memory", "1.1 GB"),
                spec("Memory errors", "0")),
            component("storage", "Storage", "Storage", "Msft Virtual Disk",
                spec("Model", "Msft Virtual Disk")),
            ComponentSnapshot.NotAvailable("battery", "Battery", "Battery", "battery", "No battery present."),
            component("gpu", "GPU", "GPU", "Hyper-V Video",
                spec("Driver date", "Not reported")),
            component("network", "Network", "Network", "Hyper-V Network Adapter",
                spec("Link speed", "10 Gbps")),
        };
    }

    private sealed class FixedCollector : ICollector
    {
        private readonly ComponentSnapshot _component;

        public FixedCollector(ComponentSnapshot component)
        {
            _component = component;
        }

        public string Id => _component.Id;

        public string Name => _component.Name;

        public string Category => _component.Category;

        public TimeSpan RefreshInterval => TimeSpan.FromSeconds(2);

        public bool RequiresAdministrator => false;

        public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_component);
        }
    }

    private sealed class TestCommand : ICommand
    {
        // Commands in these tests never change executability.
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }
}
