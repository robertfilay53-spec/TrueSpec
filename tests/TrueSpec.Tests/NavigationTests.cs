using TrueSpec.App.Services;
using TrueSpec.App.ViewModels;
using TrueSpec.Core;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Regression tests for the v1.0.1 defect where the History page was
/// unreachable. The page itself existed (HistoryView / HistoryViewModel),
/// was registered in the nav list and was routed in MainViewModel — but it
/// sat second-to-last in the list, and the left rail overflows at the
/// default 1280x800 window height, so History (and Settings) rendered below
/// the fold and were effectively invisible without scrolling the rail. That
/// is why a live check listed 16 nav items and no History. History now sits
/// inside the visible region, and these tests pin it there.
///
/// The same defect recurred in v1.4 for Settings (last of 20 items): the
/// durable fix is a scrollable left rail with an always-visible scrollbar,
/// so no item is unreachable regardless of how many the list grows to.
/// These tests pin that: Settings routes to its page, and the shell XAML
/// declares the nav list scrollable.
/// </summary>
public class NavigationTests
{
    private static string FindRepoFile(params string[] relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = relativePath.Aggregate(dir.FullName, Path.Combine);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not find " + string.Join(Path.DirectorySeparatorChar, relativePath)
            + " by walking up from " + AppContext.BaseDirectory);
    }
    [Fact]
    public void Navigation_ContainsHistoryPage()
    {
        var items = NavItemViewModel.CreateDefault();
        var history = items.FirstOrDefault(i => i.Key == "history");

        Assert.NotNull(history);
        Assert.Equal("History", history.Label);
    }

    [Fact]
    public void Navigation_HistoryIsWithinVisibleRegion()
    {
        // The default window is 1280x800 and the rail fits about 16 items
        // before it overflows; anything past that is below the fold. History
        // must stay inside the visible region so it is reachable without
        // scrolling the rail.
        var items = NavItemViewModel.CreateDefault().ToList();
        var index = items.FindIndex(i => i.Key == "history");

        Assert.InRange(index, 0, 15);
    }

    [Fact]
    public void Navigation_ContainsUpdatesPage()
    {
        var items = NavItemViewModel.CreateDefault();
        var updates = items.FirstOrDefault(i => i.Key == "updates");

        Assert.NotNull(updates);
        Assert.Equal("Updates", updates.Label);
    }

    [Fact]
    public void Navigation_UpdatesRoutesToComponentDetail()
    {
        var main = new MainViewModel(
            MachineScanService.CreateDefault(),
            new UnavailableSensorMonitor(),
            new BuyerCheckEvaluator());

        main.SelectedNavItem = main.NavItems.First(i => i.Key == "updates");

        Assert.IsType<ComponentDetailViewModel>(main.CurrentPage);
    }

    [Fact]
    public void Navigation_HistoryRoutesToHistoryPage()
    {
        var main = new MainViewModel(
            MachineScanService.CreateDefault(),
            new UnavailableSensorMonitor(),
            new BuyerCheckEvaluator());

        main.SelectedNavItem = main.NavItems.First(i => i.Key == "history");

        Assert.IsType<HistoryViewModel>(main.CurrentPage);
    }

    [Fact]
    public void Navigation_ContainsSoftwarePage()
    {
        var items = NavItemViewModel.CreateDefault();
        var software = items.FirstOrDefault(i => i.Key == "software");

        Assert.NotNull(software);
        Assert.Equal("Software", software.Label);
    }

    [Fact]
    public void Navigation_SoftwareSitsAfterHistorySoHistoryStaysVisible()
    {
        // U8 left-rail decision: the rail fits ~16 items and History must
        // stay inside the visible region (index 0-15, pinned by
        // Navigation_HistoryIsWithinVisibleRegion), so Software sits
        // directly after History instead of with the other collectors.
        // The rail scrolls with an always-visible scrollbar, so Software
        // stays reachable.
        var items = NavItemViewModel.CreateDefault().ToList();
        var history = items.FindIndex(i => i.Key == "history");
        var software = items.FindIndex(i => i.Key == "software");

        Assert.InRange(history, 0, 15);
        Assert.True(software > history, "Software must sit after History so History keeps its visible slot.");
    }

    [Fact]
    public void Navigation_SoftwareRoutesToComponentDetail()
    {
        var main = new MainViewModel(
            MachineScanService.CreateDefault(),
            new UnavailableSensorMonitor(),
            new BuyerCheckEvaluator());

        main.SelectedNavItem = main.NavItems.First(i => i.Key == "software");

        Assert.IsType<ComponentDetailViewModel>(main.CurrentPage);
    }

    [Fact]
    public void Navigation_ContainsSettingsPage()
    {
        var items = NavItemViewModel.CreateDefault();
        var settings = items.FirstOrDefault(i => i.Key == "settings");

        Assert.NotNull(settings);
        Assert.Equal("Settings", settings.Label);
    }

    [Fact]
    public void Navigation_SettingsRoutesToSettingsPage()
    {
        var main = new MainViewModel(
            MachineScanService.CreateDefault(),
            new UnavailableSensorMonitor(),
            new BuyerCheckEvaluator());

        main.SelectedNavItem = main.NavItems.First(i => i.Key == "settings");

        Assert.IsType<SettingsViewModel>(main.CurrentPage);
    }

    [Fact]
    public void Navigation_NavRailIsScrollable()
    {
        // The v1.4 recurrence of the v1.0.1 defect: Settings is the last
        // of 21 nav items and the rail fits about 16 at the default
        // 1280x800 window, so it rendered below the fold and was
        // effectively unreachable. The rail must scroll, and the scrollbar
        // must be always visible (not auto/overlay) so the scrollability
        // is discoverable and every item is reachable.
        var path = FindRepoFile("src", "TrueSpec.App", "Views", "MainWindow.axaml");
        var lines = File.ReadAllLines(path);
        var listBoxStart = Array.FindIndex(lines, line => line.Contains("<ListBox", StringComparison.Ordinal));
        Assert.True(listBoxStart >= 0, "MainWindow.axaml must contain the nav ListBox.");

        var tag = new List<string>();
        for (var i = listBoxStart; i < lines.Length; i++)
        {
            tag.Add(lines[i]);
            if (lines[i].Contains('>'))
            {
                break;
            }
        }

        var tagText = string.Join(" ", tag);
        Assert.Contains("Classes=\"nav\"", tagText, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Visible\"", tagText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Lives here because this is a Windows-leg file that can reference the
    /// App: the single-instance mutex name must be the base name plus a
    /// per-user hash suffix, stable across calls, and must never contain
    /// the plain username. A fixed well-known name lets any local process
    /// squat the mutex and block TrueSpec from launching. The hash itself
    /// is covered in HardeningTests.
    /// </summary>
    [Fact]
    public void SingleInstanceGuard_DefaultMutexNameIsPerUser()
    {
        var first = SingleInstanceGuard.DefaultMutexName();
        var second = SingleInstanceGuard.DefaultMutexName();

        Assert.Equal(first, second);
        Assert.StartsWith(SingleInstanceGuard.MutexName + ".", first, StringComparison.Ordinal);

        // A 1-3 character username can appear in 16 hex characters by
        // coincidence, so only a longer name proves a leak.
        if (Environment.UserName.Length >= 4)
        {
            Assert.DoesNotContain(Environment.UserName, first, StringComparison.OrdinalIgnoreCase);
        }
    }
}
