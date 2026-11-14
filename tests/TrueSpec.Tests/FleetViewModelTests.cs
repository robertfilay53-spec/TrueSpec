using TrueSpec.App.Services;
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
    public async Task ImportReportsCommand_German_ReportsTheSkipReasonInGerman()
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
            var viewModel = new FleetViewModel(store, languageCode: "de")
            {
                ReportFilePicker = () => Task.FromResult<IReadOnlyList<string>>(new[] { goodPath, badPath }),
            };

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (viewModel.StatusMessage == "Gerätepark wird geladen …" && DateTimeOffset.UtcNow < deadline)
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

            // Localisation polish (v2.1.18): the skip reason is a
            // structured failure kind rendered in the page language,
            // so the German line holds no English fragment. The file
            // name stays data.
            Assert.Contains("1 Bericht importiert", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("SHOP-PC-IMPORTED", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("Übersprungen 1", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("bad.json: Die Datei ist kein TrueSpec-JSON-Bericht und wurde übersprungen.", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("could not be imported", viewModel.StatusMessage, StringComparison.Ordinal);

            // The stored entries re-render on a language switch, so
            // the same status line comes back in English and then in
            // German again.
            viewModel.ApplyLanguage("en");
            Assert.Contains("Imported 1 report", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("Skipped 1", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("bad.json: The file is not a TrueSpec JSON report, so it was skipped.", viewModel.StatusMessage, StringComparison.Ordinal);

            viewModel.ApplyLanguage("de");
            Assert.Contains("bad.json: Die Datei ist kein TrueSpec-JSON-Bericht und wurde übersprungen.", viewModel.StatusMessage, StringComparison.Ordinal);
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

            // The scans vanish between the arm and the confirm (another
            // copy of the app, or a re-import cycle): this direct delete
            // removes the one saved scan, so the confirm that follows
            // must say so honestly instead of claiming a deletion.
            Assert.Equal(1, await store.DeleteMachineAsync("SHOP-PC-BAD"));
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

    /// <summary>
    /// Export all machines (v2.1.4): the command hands the delegate the
    /// rows this page loaded and the snapshot map it built while
    /// loading, and the status line reports the outcome — how many
    /// machines were exported, and the folder they landed in.
    /// </summary>
    [Fact]
    public async Task ExportAllMachinesCommand_CallsDelegateWithRowsAndSnapshots()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("SHOP-PC-GOOD", 92d, "8%", baseTime),
                ("SHOP-PC-BAD", 55d, "45%", baseTime.AddHours(1)));

            IReadOnlyList<FleetMachineRow>? seenRows = null;
            IReadOnlyDictionary<string, SystemSnapshot>? seenMap = null;
            var calls = 0;
            var folder = Path.Combine(directory, "exports");
            Task<ExportAllMachinesResult> Delegate(
                IReadOnlyList<FleetMachineRow> rows,
                IReadOnlyDictionary<string, SystemSnapshot> map)
            {
                calls++;
                seenRows = rows;
                seenMap = map;
                return Task.FromResult(new ExportAllMachinesResult(
                    folder,
                    new[]
                    {
                        Path.Combine(folder, "TrueSpec-machine-SHOP-PC-BAD.csv"),
                        Path.Combine(folder, "TrueSpec-machine-SHOP-PC-GOOD.csv"),
                    },
                    Array.Empty<ExportAllSkippedMachine>()));
            }

            var viewModel = new FleetViewModel(store, exportAllMachinesCsv: Delegate);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!viewModel.HasFleet && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(viewModel.HasFleet, "Fleet never loaded from the store.");

            await viewModel.ExportAllMachinesCommand.ExecuteAsync(null);

            Assert.Equal(1, calls);
            Assert.NotNull(seenRows);
            Assert.Equal(2, seenRows!.Count);
            Assert.NotNull(seenMap);
            Assert.Equal(2, seenMap!.Count);
            Assert.True(seenMap.ContainsKey("SHOP-PC-GOOD"));
            Assert.True(seenMap.ContainsKey("SHOP-PC-BAD"));
            Assert.Contains("Exported 2 of 2 machines to", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains(folder, viewModel.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Export all machines (v2.1.4): with no export delegate wired up
    /// (the MainViewModel always wires one; the bare constructor does
    /// not), the command says so instead of failing silently.
    /// </summary>
    [Fact]
    public async Task ExportAllMachinesCommand_NoDelegate_ReportsUnavailable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(directory, ("SHOP-PC-GOOD", 92d, "8%", baseTime));

            var viewModel = new FleetViewModel(store);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!viewModel.HasFleet && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(viewModel.HasFleet, "Fleet never loaded from the store.");

            await viewModel.ExportAllMachinesCommand.ExecuteAsync(null);

            Assert.Equal("Export all is unavailable.", viewModel.StatusMessage);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Export all machines (v2.1.4): an empty fleet has nothing to
    /// export, so the delegate is never called and the status line says
    /// the fleet is empty.
    /// </summary>
    [Fact]
    public async Task ExportAllMachinesCommand_EmptyFleet_DoesNotCallDelegate()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var databasePath = Path.Combine(directory, "snapshots.db");
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var calls = 0;
            Task<ExportAllMachinesResult> Delegate(
                IReadOnlyList<FleetMachineRow> rows,
                IReadOnlyDictionary<string, SystemSnapshot> map)
            {
                calls++;
                return Task.FromResult(new ExportAllMachinesResult(
                    Path.Combine(directory, "exports"),
                    Array.Empty<string>(),
                    Array.Empty<ExportAllSkippedMachine>()));
            }

            var viewModel = new FleetViewModel(store, exportAllMachinesCsv: Delegate);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (viewModel.StatusMessage == "Loading fleet..." && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            await viewModel.ExportAllMachinesCommand.ExecuteAsync(null);

            Assert.Equal(0, calls);
            Assert.Contains("no fleet to export yet", viewModel.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Export all machines (v2.1.4): a partial export still reports the
    /// count, and names the machine that was skipped and why — the
    /// technician can see at a glance which machine needs a re-scan.
    /// </summary>
    [Fact]
    public async Task ExportAllMachinesCommand_PartialExport_NamesSkippedMachine()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("SHOP-PC-GOOD", 92d, "8%", baseTime),
                ("SHOP-PC-BAD", 55d, "45%", baseTime.AddHours(1)));

            var folder = Path.Combine(directory, "exports");
            Task<ExportAllMachinesResult> Delegate(
                IReadOnlyList<FleetMachineRow> rows,
                IReadOnlyDictionary<string, SystemSnapshot> map) =>
                Task.FromResult(new ExportAllMachinesResult(
                    folder,
                    new[] { Path.Combine(folder, "TrueSpec-machine-SHOP-PC-GOOD.csv") },
                    new[] { new ExportAllSkippedMachine("SHOP-PC-BAD", ExportAllSkipReasonKind.NoScore) }));

            var viewModel = new FleetViewModel(store, exportAllMachinesCsv: Delegate);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!viewModel.HasFleet && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(viewModel.HasFleet, "Fleet never loaded from the store.");

            await viewModel.ExportAllMachinesCommand.ExecuteAsync(null);

            Assert.Contains("Exported 1 of 2 machines to", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("SHOP-PC-BAD", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("no score", viewModel.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Export all machines (v2.1.19): the skip reasons are structured
    /// kinds rendered in the page language, so the German line holds no
    /// English fragment. The machine names stay data, and the write
    /// error stays data too — an English exception message passes
    /// through the German line unchanged.
    /// </summary>
    [Fact]
    public async Task ExportAllMachinesCommand_German_ReportsTheSkipReasonsInGerman()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("SHOP-PC-GOOD", 92d, "8%", baseTime),
                ("SHOP-PC-BAD", 55d, "45%", baseTime.AddHours(1)),
                ("SHOP-PC-GONE", 40d, "60%", baseTime.AddHours(2)));

            var folder = Path.Combine(directory, "exports");
            Task<ExportAllMachinesResult> Delegate(
                IReadOnlyList<FleetMachineRow> rows,
                IReadOnlyDictionary<string, SystemSnapshot> map) =>
                Task.FromResult(new ExportAllMachinesResult(
                    folder,
                    new[] { Path.Combine(folder, "TrueSpec-machine-SHOP-PC-GOOD.csv") },
                    new[]
                    {
                        new ExportAllSkippedMachine("SHOP-PC-BAD", ExportAllSkipReasonKind.NoScore),
                        new ExportAllSkippedMachine("SHOP-PC-GONE", ExportAllSkipReasonKind.WriteFailed, "Access denied"),
                    }));

            var viewModel = new FleetViewModel(store, languageCode: "de", exportAllMachinesCsv: Delegate);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!viewModel.HasFleet && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(viewModel.HasFleet, "Fleet never loaded from the store.");

            await viewModel.ExportAllMachinesCommand.ExecuteAsync(null);

            Assert.Contains("1 von 3 Geräten nach", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("Übersprungen 2", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("SHOP-PC-BAD: der neueste Scan hat keine Bewertung zum Exportieren", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("SHOP-PC-GONE: konnte nicht geschrieben werden (Access denied)", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("no score", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("could not be written", viewModel.StatusMessage, StringComparison.Ordinal);

            // The stored result re-renders on a language switch, so
            // the same status line comes back in English and then in
            // German again.
            viewModel.ApplyLanguage("en");
            Assert.Contains("Exported 1 of 3 machines to", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("Skipped 2", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("SHOP-PC-BAD: the latest scan has no score to export", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("SHOP-PC-GONE: could not be written (Access denied)", viewModel.StatusMessage, StringComparison.Ordinal);

            viewModel.ApplyLanguage("de");
            Assert.Contains("SHOP-PC-BAD: der neueste Scan hat keine Bewertung zum Exportieren", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("SHOP-PC-GONE: konnte nicht geschrieben werden (Access denied)", viewModel.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Export all machines (v2.1.4): a second click while an export is
    /// running does nothing — one export, not two sets of files.
    /// </summary>
    [Fact]
    public async Task ExportAllMachinesCommand_Reentrancy_SecondCallWhileRunningDoesNothing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(directory, ("SHOP-PC-GOOD", 92d, "8%", baseTime));

            var folder = Path.Combine(directory, "exports");
            var completion = new TaskCompletionSource<ExportAllMachinesResult>();
            var calls = 0;
            Task<ExportAllMachinesResult> Delegate(
                IReadOnlyList<FleetMachineRow> rows,
                IReadOnlyDictionary<string, SystemSnapshot> map)
            {
                calls++;
                return completion.Task;
            }

            var viewModel = new FleetViewModel(store, exportAllMachinesCsv: Delegate);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!viewModel.HasFleet && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(viewModel.HasFleet, "Fleet never loaded from the store.");

            var first = viewModel.ExportAllMachinesCommand.ExecuteAsync(null);
            await viewModel.ExportAllMachinesCommand.ExecuteAsync(null);

            Assert.Equal(1, calls);

            completion.SetResult(new ExportAllMachinesResult(
                folder,
                new[] { Path.Combine(folder, "TrueSpec-machine-SHOP-PC-GOOD.csv") },
                Array.Empty<ExportAllSkippedMachine>()));
            await first;

            Assert.Equal(1, calls);
            Assert.Contains("Exported 1 of 1 machine to", viewModel.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
