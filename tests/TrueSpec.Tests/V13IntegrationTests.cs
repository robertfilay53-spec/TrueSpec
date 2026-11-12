using System.Windows.Input;
using TrueSpec.App;
using TrueSpec.App.ViewModels;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// v1.3 integration tests (Windows leg only: they drive the App's view
/// models, so this file is excluded from the net10.0 leg in
/// TrueSpec.Tests.csproj, like NavigationTests). Covers the New Laptop Check
/// Authenticity section and banner drops, the Crash history page, the System
/// page Age signals group, and the crash nav item + route.
/// </summary>
public class V13IntegrationTests
{
    private static ComponentSnapshot SystemWithDates(DateTimeOffset install, DateTimeOffset bios) => new(
        "system",
        "System",
        "System",
        "system",
        "Test Laptop",
        null,
        new[]
        {
            new SpecValue("Operating system", "Windows 11 Pro", "CIM Win32_OperatingSystem"),
            new SpecValue("BIOS date", bios.ToString("yyyy-MM-dd"), "CIM Win32_BIOS"),
            new SpecValue("Windows install date", install.ToString("yyyy-MM-dd"), "CIM Win32_OperatingSystem"),
            new SpecValue("Critical errors (event log)", "0", "Windows Event Log"),
            new SpecValue("Signed drivers only", "Yes", "CIM Win32_PnPSignedDriver"),
        },
        "System information comes from Windows.",
        DateTimeOffset.UtcNow);

    private static ComponentSnapshot CrashWithBugCheck(DateTimeOffset mostRecent, int count90d) => new(
        "crash",
        "Crash history",
        "Diagnostics",
        "crash",
        $"{count90d} blue screen(s) in the last 90 days",
        null,
        new[]
        {
            new SpecValue("Blue screens (last 90 days)", count90d.ToString(), "Windows Event Log (System)"),
            new SpecValue("Blue screens - first recorded", mostRecent.AddDays(-40).ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
            new SpecValue("Blue screens - most recent", mostRecent.ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
            new SpecValue("Earliest event log entry", mostRecent.AddDays(-100).ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
        },
        "Test component.",
        DateTimeOffset.UtcNow);

    private static ComponentSnapshot CrashWithTwoClasses(DateTimeOffset now) => new(
        "crash",
        "Crash history",
        "Diagnostics",
        "crash",
        "2 blue screens, 1 disk error in the last 90 days",
        null,
        new[]
        {
            new SpecValue("Blue screens (last 90 days)", "2", "Windows Event Log (System)"),
            new SpecValue("Blue screens - first recorded", now.AddDays(-40).ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
            new SpecValue("Blue screens - most recent", now.AddDays(-5).ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
            new SpecValue("Disk errors (last 90 days)", "1", "Windows Event Log (System)"),
            new SpecValue("Disk errors - first recorded", now.AddDays(-40).ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
            new SpecValue("Disk errors - most recent", now.AddDays(-40).ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
            new SpecValue("Earliest event log entry", now.AddDays(-100).ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
        },
        "Test component.",
        now);

    private static ComponentSnapshot[] AllPassBase(DateTimeOffset now) => new[]
    {
        SystemWithDates(now.AddDays(-20), now.AddDays(-20)),
        TestSnapshots.Storage("0", "0"),
        TestSnapshots.Battery(2),
        TestSnapshots.Ram("16 GB"),
        TestSnapshots.Crash(now),
    };

    private static SystemSnapshot SnapshotWith(params ComponentSnapshot[] components)
    {
        var list = components.ToList();
        return new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Test Laptop",
            "Windows 11 Pro",
            list,
            new ScoringEngine().Score(list),
            new BuyerCheckEvaluator().Evaluate(list, new BuyerCheckContext("16 GB")));
    }

    private sealed class TestCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }

    [Fact]
    public void Navigation_ContainsCrashHistoryPage()
    {
        var items = NavItemViewModel.CreateDefault();
        var crash = items.FirstOrDefault(i => i.Key == "crash");

        Assert.NotNull(crash);
        Assert.Equal("Crash history", crash.Label);
    }

    [Fact]
    public void Navigation_HistoryStillWithinVisibleRegionAfterAddingCrash()
    {
        var items = NavItemViewModel.CreateDefault().ToList();
        var index = items.FindIndex(i => i.Key == "history");

        Assert.InRange(index, 0, 15);
    }

    [Fact]
    public void Navigation_CrashIconIsNotTheFallbackIcon()
    {
        Assert.NotEqual(IconLibrary.Get("system"), IconLibrary.Get("crash"));
    }

    [Fact]
    public void NewLaptopCheck_StrongAuthenticity_ShowsSectionAndDropsBanner()
    {
        var now = DateTimeOffset.UtcNow;
        var components = AllPassBase(now).ToList();
        components[0] = SystemWithDates(now.AddDays(-20), now.AddDays(-19));

        var viewModel = new NewLaptopCheckViewModel(
            SnapshotWith(components.ToArray()),
            new TestCommand());

        Assert.True(viewModel.HasAuthenticityChecks);
        var row = Assert.Single(viewModel.AuthenticityChecks);
        Assert.Contains("BIOS date vs Windows install date", row.Title, StringComparison.Ordinal);
        Assert.Equal("Strong", row.SeverityText);
        Assert.False(string.IsNullOrWhiteSpace(row.SourceA));
        Assert.False(string.IsNullOrWhiteSpace(row.SourceB));
        Assert.Equal(BuyerCheckState.Warning, viewModel.OverallState);
        Assert.Equal("WARNING - Signs of prior use (BIOS date vs Windows install date)", viewModel.BannerText);
    }

    [Fact]
    public void NewLaptopCheck_RecentBugCheck_DropsBanner()
    {
        var now = DateTimeOffset.UtcNow;
        var components = AllPassBase(now).ToList();
        components[^1] = CrashWithBugCheck(now.AddDays(-5), 2);

        var viewModel = new NewLaptopCheckViewModel(
            SnapshotWith(components.ToArray()),
            new TestCommand());

        Assert.Equal(BuyerCheckState.Warning, viewModel.OverallState);
        Assert.Equal("WARNING - Recent blue screens recorded", viewModel.BannerText);
    }

    [Fact]
    public void NewLaptopCheck_NoFindings_ShowsEmptyAuthenticityState()
    {
        var viewModel = new NewLaptopCheckViewModel(TestSnapshots.Healthy(), new TestCommand());

        Assert.False(viewModel.HasAuthenticityChecks);
        Assert.Contains("No signs of prior use", viewModel.AuthenticitySummary, StringComparison.Ordinal);
        Assert.Equal(BuyerCheckState.Pass, viewModel.OverallState);
        Assert.StartsWith("PASS", viewModel.BannerText, StringComparison.Ordinal);
    }

    [Fact]
    public void CrashHistoryPage_ShowsTableAndRecentAlert()
    {
        var now = DateTimeOffset.UtcNow;
        var viewModel = new CrashHistoryViewModel(SnapshotWith(CrashWithTwoClasses(now)));

        Assert.True(viewModel.HasHistory);
        Assert.True(viewModel.HasRows);
        Assert.Equal(2, viewModel.Rows.Count);
        var bugCheck = viewModel.Rows[0];
        Assert.Equal("Blue screens", bugCheck.ClassName);
        Assert.Equal(2, bugCheck.Count90d);
        Assert.Contains("blue screen", bugCheck.Meaning, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.HasRecentAlert);
        Assert.Contains("blue screen", viewModel.AlertText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CrashHistoryPage_NoComponent_ShowsEmptyState()
    {
        var viewModel = new CrashHistoryViewModel(SnapshotWith(TestSnapshots.Ram("16 GB")));

        Assert.False(viewModel.HasHistory);
        Assert.False(viewModel.HasRows);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.EmptyMessage));
        Assert.False(viewModel.HasRecentAlert);
    }

    [Fact]
    public void CrashHistoryPage_EmptyLog_ShowsNoCrashesLine()
    {
        var now = DateTimeOffset.UtcNow;
        var viewModel = new CrashHistoryViewModel(SnapshotWith(TestSnapshots.Crash(now)));

        Assert.True(viewModel.HasHistory);
        Assert.False(viewModel.HasRows);
        Assert.Contains("No blue screens", viewModel.EmptyMessage, StringComparison.Ordinal);
        Assert.False(viewModel.HasRecentAlert);
    }

    [Fact]
    public void SystemPage_ShowsAgeSignals_OtherPagesDoNot()
    {
        var healthy = TestSnapshots.Healthy();

        var system = new ComponentDetailViewModel(healthy, "system");
        Assert.True(system.HasAgeSignals);
        Assert.Contains(system.AgeSignals, s => s.Name == "Windows install date");
        Assert.Contains(system.AgeSignals, s => s.Name == "Power-on hours");
        Assert.All(system.AgeSignals, s => Assert.False(string.IsNullOrWhiteSpace(s.Source)));

        var cpu = new ComponentDetailViewModel(healthy, "cpu");
        Assert.False(cpu.HasAgeSignals);
    }

    [Fact]
    public void MainViewModel_NavigateToCrash_OpensCrashHistoryPage()
    {
        var shell = new MainViewModel(
            Array.Empty<TrueSpec.Core.Collectors.ICollector>(),
            new UnavailableSensorMonitor(),
            new BuyerCheckEvaluator());

        shell.SelectedNavItem = NavItemViewModel.CreateDefault().First(i => i.Key == "crash");

        Assert.IsType<CrashHistoryViewModel>(shell.CurrentPage);
    }
}
