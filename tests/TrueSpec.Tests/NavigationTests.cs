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
/// </summary>
public class NavigationTests
{
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
