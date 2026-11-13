using TrueSpec.App.ViewModels;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
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

    [Fact]
    public async Task ExportMachineCsvCommand_CallsExportWithRowAndSnapshot()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("SHOP-PC-GOOD", 92d, "8%", baseTime),
                ("SHOP-PC-BAD", 55d, "45%", baseTime.AddHours(1)));

            var calls = new List<(FleetMachineRow Row, SystemSnapshot Snapshot)>();
            var viewModel = new FleetViewModel(
                store,
                exportMachineCsv: (row, snapshot) =>
                {
                    calls.Add((row, snapshot));
                    return Task.CompletedTask;
                });

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!viewModel.HasFleet && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(viewModel.HasFleet, "Fleet never loaded from the store.");
            Assert.Equal(2, viewModel.Rows.Count);

            // The row button exports that one machine: the lowest-score
            // row first, with its own saved snapshot attached.
            await viewModel.ExportMachineCsvCommand.ExecuteAsync(viewModel.Rows[0]);

            var call = Assert.Single(calls);
            Assert.Equal("SHOP-PC-BAD", call.Row.MachineName);
            Assert.Equal("SHOP-PC-BAD", call.Snapshot.MachineName);
            Assert.Equal(55d, call.Row.LatestScore);
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
    public async Task ImportReportsCommand_ImportsPickedReportsIntoTheFleet()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var databasePath = Path.Combine(directory, "snapshots.db");
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var goodPath = Path.Combine(directory, "good.json");
            var badPath = Path.Combine(directory, "bad.json");
            Directory.CreateDirectory(directory);
            var snapshot = TestSnapshots.Healthy() with { MachineName = "SHOP-PC-IMPORTED" };
            await using (var stream = File.Create(goodPath))
            {
                await new TrueSpec.Core.Reports.JsonReportExporter().ExportAsync(
                    TrueSpec.Core.Reports.ReportModel.FromSnapshot(snapshot), stream);
            }

            await File.WriteAllTextAsync(badPath, "not a report at all");
            var viewModel = new FleetViewModel(store)
            {
                ReportFilePicker = () => Task.FromResult<IReadOnlyList<string>>(new[] { goodPath, badPath }),
            };

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (viewModel.StatusMessage == "Loading fleet..." && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            await viewModel.ImportReportsCommand.ExecuteAsync(null);

            deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!viewModel.HasFleet && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(viewModel.HasFleet, "Imported machine never appeared in the fleet.");
            Assert.Single(viewModel.Rows);
            Assert.Equal("SHOP-PC-IMPORTED", viewModel.Rows[0].MachineName);
            Assert.Contains("Imported 1 report", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("SHOP-PC-IMPORTED", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("Skipped 1", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("bad.json", viewModel.StatusMessage, StringComparison.Ordinal);
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
    public async Task ImportReportsCommand_WithoutPicker_ReportsUnavailable()
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

            await viewModel.ImportReportsCommand.ExecuteAsync(null);

            Assert.Equal("Import is unavailable: no file picker is available.", viewModel.StatusMessage);
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
    public async Task ImportReportsCommand_CancelledPicker_LeavesFleetAlone()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var databasePath = Path.Combine(directory, "snapshots.db");
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var viewModel = new FleetViewModel(store)
            {
                ReportFilePicker = () => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>()),
            };

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (viewModel.StatusMessage == "Loading fleet..." && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            var before = viewModel.StatusMessage;
            await viewModel.ImportReportsCommand.ExecuteAsync(null);

            Assert.Equal(before, viewModel.StatusMessage);
            Assert.False(viewModel.HasFleet);
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
    public async Task RemoveMachineCommand_FirstClickArms_SecondClickDeletes()
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

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!viewModel.HasFleet && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(viewModel.HasFleet, "Fleet never loaded from the store.");
            var badRow = viewModel.Rows[0];
            Assert.Equal("SHOP-PC-BAD", badRow.MachineName);

            // First click only arms: nothing is deleted, and the button
            // now reads "Confirm remove".
            await viewModel.RemoveMachineCommand.ExecuteAsync(badRow);

            Assert.True(badRow.IsRemoveArmed);
            Assert.Equal("Confirm remove", badRow.RemoveButtonText);
            Assert.Equal(2, viewModel.Rows.Count);
            Assert.Contains("SHOP-PC-BAD", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("2 saved scans", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Equal(2, (await store.GetLatestPerMachineAsync()).Count);

            // Second click deletes both of the machine's scans and the
            // fleet reloads: only the good machine remains.
            await viewModel.RemoveMachineCommand.ExecuteAsync(badRow);

            Assert.Single(viewModel.Rows);
            Assert.Equal("SHOP-PC-GOOD", viewModel.Rows[0].MachineName);
            Assert.Contains("Removed SHOP-PC-BAD (2 saved scans)", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Equal(new[] { "SHOP-PC-GOOD" }, await store.GetMachineNamesAsync());
            Assert.Single(await store.GetLatestPerMachineAsync());
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
    public async Task RemoveMachineCommand_AlreadyGone_ReportsNothingToRemove()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("SHOP-PC-BAD", 55d, "45%", baseTime));

            var viewModel = new FleetViewModel(store);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!viewModel.HasFleet && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            var row = viewModel.Rows[0];
            await viewModel.RemoveMachineCommand.ExecuteAsync(row);
            Assert.True(row.IsRemoveArmed);

            // The scans vanished between the arm and the confirm (another
            // copy of the app, or a re-import cycle): the confirm must
            // say so honestly instead of claiming a deletion.
            Assert.Equal(0, await store.DeleteMachineAsync("SHOP-PC-BAD"));
            await viewModel.RemoveMachineCommand.ExecuteAsync(row);

            Assert.False(viewModel.HasFleet);
            Assert.Empty(viewModel.Rows);
            Assert.Contains("Nothing to remove", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("SHOP-PC-BAD", viewModel.StatusMessage, StringComparison.Ordinal);
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
    public async Task RemoveMachineCommand_NullStore_ReportsUnavailable()
    {
        var viewModel = new FleetViewModel(null);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (viewModel.StatusMessage == "Loading fleet..." && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        // No store, so no rows exist: drive the command with a row built
        // by hand, the way the view would pass one.
        var row = new FleetRowViewModel(
            new FleetMachineRow("SHOP-PC", 1, DateTimeOffset.UtcNow, 80d, "Used - healthy", null, null),
            removeMachineCommand: viewModel.RemoveMachineCommand);

        await viewModel.RemoveMachineCommand.ExecuteAsync(row);

        Assert.Equal("Remove is unavailable: snapshot history is unavailable on this machine.", viewModel.StatusMessage);
    }

    [Fact]
    public void FleetRowViewModel_RemoveButtonText_FollowsArmedState()
    {
        var row = new FleetRowViewModel(
            new FleetMachineRow("SHOP-PC", 3, DateTimeOffset.UtcNow, 80d, "Used - healthy", null, null));

        Assert.Equal("Remove", row.RemoveButtonText);
        Assert.False(row.IsRemoveArmed);

        row.IsRemoveArmed = true;

        Assert.Equal("Confirm remove", row.RemoveButtonText);
    }
}
