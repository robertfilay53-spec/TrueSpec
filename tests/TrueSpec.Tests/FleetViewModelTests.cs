using TrueSpec.App.ViewModels;
using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

/// <summary>
/// Technician mode fleet page (v2.0.3): two synthetic machines in a real
/// SQLite store, and the Fleet ViewModel surfaces the triage table.
/// Windows leg only (it drives TrueSpec.App's ViewModel), like
/// HistoryForecastViewModelTests: excluded from the net10.0 leg in the
/// csproj.
/// </summary>
public class FleetViewModelTests
{
    private static async Task<SqliteSnapshotStore> StoreWithAsync(
        string directory,
        params (string Machine, double Score, string Wear, DateTimeOffset At)[] scans)
    {
        var databasePath = Path.Combine(directory, "snapshots.db");
        var store = new SqliteSnapshotStore(databasePath);
        await store.InitializeAsync();
        foreach (var scan in scans)
        {
            await store.SaveAsync(TestSnapshots.HistorySnapshot(
                scan.Score,
                "90.0%",
                "10",
                scan.Wear,
                "100",
                scan.At,
                machineName: scan.Machine));
        }

        return store;
    }

    [Fact]
    public async Task TwoMachines_ViewModelListsLowestScoreFirst()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("SHOP-PC-GOOD", 92d, "8%", baseTime),
                ("SHOP-PC-BAD", 55d, "45%", baseTime.AddHours(1)),
                ("SHOP-PC-BAD", 58d, "42%", baseTime.AddHours(2)));

            var viewModel = new FleetViewModel(store);

            // The ViewModel loads the fleet on a fire-and-forget task
            // from its constructor: wait for it instead of sleeping a
            // fixed time.
            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!viewModel.HasFleet && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(viewModel.HasFleet, "Fleet never loaded from the store.");
            Assert.Equal(2, viewModel.Rows.Count);
            // Lowest score first: the machine that needs attention is
            // at the top, and its worst component is the storage drive.
            Assert.Equal("SHOP-PC-BAD", viewModel.Rows[0].MachineName);
            Assert.Equal("58", viewModel.Rows[0].ScoreText);
            Assert.Equal("2", viewModel.Rows[0].ScanCountText);
            Assert.Equal("storage — 58%", viewModel.Rows[0].WorstComponentText);
            Assert.Equal("SHOP-PC-GOOD", viewModel.Rows[1].MachineName);
            Assert.Equal("92", viewModel.Rows[1].ScoreText);
            Assert.Contains("2 machines", viewModel.SummaryText, StringComparison.Ordinal);
            Assert.Contains("SHOP-PC-BAD", viewModel.SummaryText, StringComparison.Ordinal);
            Assert.NotNull(viewModel.CurrentRows);
            Assert.Equal(2, viewModel.CurrentRows!.Count);
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
    public async Task EmptyStore_ViewModelShowsEmptyState()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var databasePath = Path.Combine(directory, "snapshots.db");
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var viewModel = new FleetViewModel(store);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (viewModel.StatusMessage == "Loading fleet..." && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.False(viewModel.HasFleet);
            Assert.Empty(viewModel.Rows);
            Assert.Contains("No saved scans yet", viewModel.StatusMessage, StringComparison.Ordinal);
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
    public async Task NullStore_ViewModelReportsUnavailable()
    {
        var viewModel = new FleetViewModel(null);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (viewModel.StatusMessage == "Loading fleet..." && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.False(viewModel.HasFleet);
        Assert.Contains("unavailable", viewModel.StatusMessage, StringComparison.Ordinal);
    }
}
