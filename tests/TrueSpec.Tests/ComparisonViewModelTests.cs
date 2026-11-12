using TrueSpec.App.ViewModels;
using TrueSpec.Core.Models;
using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

/// <summary>
/// Tests for the comparison page ViewModel (hardening item 7).
/// Windows leg only (it drives TrueSpec.App's ViewModels), like
/// HistoryForecastViewModelTests: excluded from the net10.0 leg in the csproj.
/// </summary>
public class ComparisonViewModelTests
{
    private static async Task<SqliteSnapshotStore> StoreWithTwoSnapshots(
        string databasePath, SystemSnapshot older, SystemSnapshot newer)
    {
        var store = new SqliteSnapshotStore(databasePath);
        await store.InitializeAsync();
        await store.SaveAsync(older);
        await store.SaveAsync(newer);
        return store;
    }

    private static async Task WaitForAsync(Func<bool> ready, string what, int seconds = 10)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(seconds);
        while (!ready() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.True(ready(), what + " never became ready.");
    }

    [Fact]
    public async Task TwoSnapshots_AutoComparesAndShowsTheChange()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");
        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(-30);
            var older = TestSnapshots.HistorySnapshot(82, "90%", "120", "4", "12400", start);
            var newer = TestSnapshots.HistorySnapshot(70, "75%", "300", "12", "45000", start.AddDays(30));
            var store = await StoreWithTwoSnapshots(databasePath, older, newer);

            var viewModel = new ComparisonViewModel(store, newer);

            await WaitForAsync(() => viewModel.History.Count == 2, "History did not load two snapshots.");
            await WaitForAsync(() => viewModel.HasComparison, "Comparison never completed.");

            Assert.Equal(2, viewModel.History.Count);
            Assert.NotNull(viewModel.SelectedBefore);
            Assert.NotNull(viewModel.SelectedAfter);
            Assert.NotEqual(viewModel.SelectedBefore.Id, viewModel.SelectedAfter.Id);
            Assert.True(viewModel.HasChanges, "Wear 4% to 12% must register as a change.");
            Assert.NotEmpty(viewModel.Rows);
            Assert.Contains("Compared", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(viewModel.SummaryText));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SameSnapshotSelected_CompareIsDisabled()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");
        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(-30);
            var older = TestSnapshots.HistorySnapshot(82, "90%", "120", "4", "12400", start);
            var newer = TestSnapshots.HistorySnapshot(70, "75%", "300", "12", "45000", start.AddDays(30));
            var store = await StoreWithTwoSnapshots(databasePath, older, newer);

            var viewModel = new ComparisonViewModel(store, newer);
            await WaitForAsync(() => viewModel.History.Count == 2, "History did not load two snapshots.");

            viewModel.SelectedBefore = viewModel.History[0];
            viewModel.SelectedAfter = viewModel.History[0];

            Assert.False(viewModel.CompareCommand.CanExecute(null));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task OneSnapshot_ExplainsThatAnotherScanIsNeeded()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");
        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();
            var only = TestSnapshots.HistorySnapshot(82, "90%", "120", "4", "12400", DateTimeOffset.UtcNow);
            await store.SaveAsync(only);

            var viewModel = new ComparisonViewModel(store, only);
            await WaitForAsync(
                () => viewModel.StatusMessage != "Loading snapshot history...",
                "Status never left loading.");

            Assert.Single(viewModel.History);
            Assert.False(viewModel.HasComparison);
            Assert.Contains("Only one snapshot", viewModel.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task NoStore_SaysHistoryIsUnavailable()
    {
        var viewModel = new ComparisonViewModel(null, TestSnapshots.Healthy());
        await WaitForAsync(
            () => viewModel.StatusMessage != "Loading snapshot history...",
            "Status never left loading.");

        Assert.Empty(viewModel.History);
        Assert.False(viewModel.HasComparison);
        Assert.Contains("unavailable", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }
}
