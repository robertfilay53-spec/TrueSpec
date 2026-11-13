namespace TrueSpec.App.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.App.Services;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;
using TrueSpec.Core.Storage;

/// <summary>
/// Technician mode, fleet page (plan: Beyond v2.0, item 1): one row per
/// machine in the local snapshot store, lowest score first, with the
/// worst component named. A technician carries the portable app on a USB
/// stick, scans each machine, and every scan lands in the same store on
/// the stick — this page is the fleet summary of that store. Machines
/// scanned with the installed app instead can be brought in with the
/// Import button, which reads the JSON reports the Report page exports.
/// A machine leaves the fleet with the Remove button on its row (two
/// clicks: arm, then confirm), which deletes all of that machine's
/// saved scans from this data folder — the undo for a wrong import.
/// Export all machines writes one CSV per machine into the export
/// folder, so the whole fleet can be filed at once instead of one
/// Export click per row. Every value comes from a real saved snapshot;
/// a machine whose latest scan is unreadable is skipped, never shown
/// with invented numbers.
/// </summary>
public sealed partial class FleetViewModel : ViewModelBase
{
    private readonly ISnapshotStore? _store;
    private readonly Func<FleetMachineRow, SystemSnapshot, Task>? _exportMachineCsv;
    private readonly Func<IReadOnlyList<FleetMachineRow>, IReadOnlyDictionary<string, SystemSnapshot>, Task<ExportAllMachinesResult>>? _exportAllMachinesCsv;
    private readonly Dictionary<string, SystemSnapshot> _snapshotsByMachine = new(StringComparer.OrdinalIgnoreCase);
    private bool _importing;
    private bool _removing;
    private bool _exportingAll;

    public FleetViewModel(
        ISnapshotStore? store,
        ICommand? exportFleetCsvCommand = null,
        Func<FleetMachineRow, SystemSnapshot, Task>? exportMachineCsv = null,
        Func<IReadOnlyList<FleetMachineRow>, IReadOnlyDictionary<string, SystemSnapshot>, Task<ExportAllMachinesResult>>? exportAllMachinesCsv = null)
    {
        _store = store;
        _exportMachineCsv = exportMachineCsv;
        _exportAllMachinesCsv = exportAllMachinesCsv;
        ExportFleetCsvCommand = exportFleetCsvCommand;
        ExportMachineCsvCommand = new AsyncRelayCommand<FleetRowViewModel>(ExportMachineCsvAsync);
        ExportAllMachinesCommand = new AsyncRelayCommand(ExportAllMachinesAsync);
        RemoveMachineCommand = new AsyncRelayCommand<FleetRowViewModel>(RemoveMachineAsync);
        ImportReportsCommand = new AsyncRelayCommand(ImportReportsAsync);
        _ = LoadAsync();
    }

    /// <summary>
    /// Supplied by the Fleet view (mirrors how SettingsViewModel receives
    /// its folder picker): opens the file picker and returns the JSON
    /// report paths the technician chose, or an empty list when the
    /// picker was cancelled. Null until the view wires it up.
    /// </summary>
    public Func<Task<IReadOnlyList<string>>>? ReportFilePicker { get; set; }

    /// <summary>
    /// Imports the JSON reports the technician picked into this store,
    /// then reloads the fleet. One bad file never stops the rest: each
    /// file is reported by name in the status line.
    /// </summary>
    public IAsyncRelayCommand ImportReportsCommand { get; }

    /// <summary>
    /// Supplied by MainViewModel (mirrors how HistoryViewModel receives
    /// the export commands): writes the currently shown fleet table to
    /// the configured export folder via FleetCsvExporter. Null when no
    /// caller supplied one.
    /// </summary>
    public ICommand? ExportFleetCsvCommand { get; }

    /// <summary>
    /// One machine's latest scan as its own CSV, invoked from the Export
    /// button on that machine's row. The snapshot comes from the map this
    /// page built while loading; the row alone cannot identify it.
    /// </summary>
    public IAsyncRelayCommand<FleetRowViewModel> ExportMachineCsvCommand { get; }

    /// <summary>
    /// Every machine in the fleet as its own CSV, invoked from the
    /// "Export all machines" button above the table: one file per
    /// machine in the export folder, each written exactly like the
    /// per-row export. A machine whose latest scan cannot be read, or
    /// has no score, is skipped and named in the status line, and a
    /// write failure for one machine never stops the rest.
    /// </summary>
    public IAsyncRelayCommand ExportAllMachinesCommand { get; }

    /// <summary>
    /// Takes one machine out of the fleet, invoked from the Remove button
    /// on that machine's row. Two clicks by design (see
    /// <see cref="RemoveMachineAsync"/>): the app has no modal confirm
    /// dialog, so the first click arms the row and only the second
    /// deletes. The row alone identifies the machine, by its exact
    /// stored name.
    /// </summary>
    public IAsyncRelayCommand<FleetRowViewModel> RemoveMachineCommand { get; }

    /// <summary>The rows currently shown on the page, for the CSV export command.</summary>
    public IReadOnlyList<FleetMachineRow>? CurrentRows { get; private set; }

    public ObservableCollection<FleetRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Loading fleet...";

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasFleet { get; set; }

    private async Task LoadAsync()
    {
        if (_store is null)
        {
            StatusMessage = "Snapshot history is unavailable on this machine, so there is no fleet to show yet.";
            return;
        }

        try
        {
            var latest = await _store.GetLatestPerMachineAsync();
            var inputs = new List<FleetMachineInput>();
            _snapshotsByMachine.Clear();
            foreach (var summary in latest)
            {
                var snapshot = await _store.GetSnapshotAsync(summary.Id);
                inputs.Add(new FleetMachineInput(summary, snapshot));
                if (snapshot is not null)
                {
                    _snapshotsByMachine[summary.MachineName] = snapshot;
                }
            }

            var rows = FleetSummary.Build(inputs);
            CurrentRows = rows;
            Rows.Clear();
            foreach (var row in rows)
            {
                Rows.Add(new FleetRowViewModel(row, ExportMachineCsvCommand, RemoveMachineCommand));
            }

            HasFleet = rows.Count > 0;
            if (rows.Count == 0)
            {
                StatusMessage = "No saved scans yet. Scan a machine, or run the portable app on each machine in the fleet, and its latest scan appears here. You can also import JSON reports exported from other machines with the Import button.";
                SummaryText = string.Empty;
                return;
            }

            var totalScans = rows.Sum(r => r.ScanCount);
            var lowest = rows[0];
            SummaryText = string.Create(
                CultureInfo.InvariantCulture,
                $"{rows.Count} machine{(rows.Count == 1 ? string.Empty : "s")} in this data folder, {totalScans} saved scan{(totalScans == 1 ? string.Empty : "s")}. " +
                $"Lowest score {(lowest.LatestScore is null ? "not measurable" : lowest.LatestScore.Value.ToString("0.#", CultureInfo.InvariantCulture))} ({lowest.MachineName}).");
            StatusMessage = "Fleet loaded.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Fleet could not be read: {exception.Message}";
            HasFleet = false;
        }
    }

    private async Task ExportMachineCsvAsync(FleetRowViewModel? rowViewModel)
    {
        if (rowViewModel is null)
        {
            return;
        }

        if (_exportMachineCsv is null)
        {
            StatusMessage = "Machine export is unavailable.";
            return;
        }

        if (!_snapshotsByMachine.TryGetValue(rowViewModel.MachineName, out var snapshot) || snapshot is null)
        {
            StatusMessage = $"Machine export failed: no saved scan was found for {rowViewModel.MachineName}.";
            return;
        }

        await _exportMachineCsv(rowViewModel.Row, snapshot);
    }

    /// <summary>
    /// Exports every machine in the fleet as its own CSV (v2.1.4). The
    /// rows and their snapshots come from this page's load; the
    /// orchestrator writes the files and reports the outcome, and this
    /// method renders the status line from it: how many machines were
    /// exported, the folder they landed in, and any machine skipped by
    /// name with its reason. A second click while an export is running
    /// does nothing (the guard, not a second export).
    /// </summary>
    private async Task ExportAllMachinesAsync()
    {
        if (_exportingAll)
        {
            return;
        }

        if (_exportAllMachinesCsv is null)
        {
            StatusMessage = "Export all is unavailable.";
            return;
        }

        var rows = CurrentRows;
        if (rows is null || rows.Count == 0)
        {
            StatusMessage = "Export all failed: there is no fleet to export yet.";
            return;
        }

        _exportingAll = true;
        try
        {
            var result = await _exportAllMachinesCsv(rows, _snapshotsByMachine);
            StatusMessage = BuildExportAllStatus(result, rows.Count);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Export all failed: {exception.Message}";
        }
        finally
        {
            _exportingAll = false;
        }
    }

    private static string BuildExportAllStatus(ExportAllMachinesResult result, int totalRows)
    {
        var exported = result.ExportedPaths.Count;
        if (exported == 0)
        {
            var skipped = result.Skipped.Count == 0
                ? "there is no fleet to export yet."
                : $"Skipped {result.Skipped.Count}: {string.Join("; ", result.Skipped.Select(s => $"{s.MachineName}: {s.Reason}"))}.";
            return $"Exported 0 machines. {skipped}";
        }

        var summary = string.Create(
            CultureInfo.InvariantCulture,
            $"Exported {exported} of {totalRows} machine{(totalRows == 1 ? string.Empty : "s")} to {result.Folder}.");
        if (result.Skipped.Count > 0)
        {
            summary += $" Skipped {result.Skipped.Count}: {string.Join("; ", result.Skipped.Select(s => $"{s.MachineName}: {s.Reason}"))}.";
        }

        return summary;
    }

    /// <summary>
    /// Removes one machine from the fleet. Two clicks by design: the app
    /// has no modal confirm dialog, so a single misclick must never
    /// delete a machine's whole history. The first click arms the row
    /// (its button reads "Confirm remove") and names how many scans
    /// would go; the second click deletes every saved scan for that
    /// machine from this data folder and reloads the fleet. The count in
    /// the status line is the store's own deleted-row count, so the
    /// message states what actually happened — including 0, when the
    /// scans were already gone. Deleting never touches the machine
    /// itself, only TrueSpec's own history file.
    /// </summary>
    private async Task RemoveMachineAsync(FleetRowViewModel? rowViewModel)
    {
        if (rowViewModel is null || _removing)
        {
            return;
        }

        if (_store is null)
        {
            StatusMessage = "Remove is unavailable: snapshot history is unavailable on this machine.";
            return;
        }

        if (!rowViewModel.IsRemoveArmed)
        {
            rowViewModel.IsRemoveArmed = true;
            var scans = rowViewModel.Row.ScanCount;
            StatusMessage = string.Create(
                CultureInfo.InvariantCulture,
                $"Remove {rowViewModel.MachineName}? This deletes all {scans} saved scan{(scans == 1 ? string.Empty : "s")} for this machine from this data folder. Click 'Confirm remove' to delete — nothing is deleted until then.");
            return;
        }

        _removing = true;
        try
        {
            var removed = await _store.DeleteMachineAsync(rowViewModel.MachineName);
            await LoadAsync();
            if (!StatusMessage.StartsWith("Fleet could not be read", StringComparison.Ordinal))
            {
                StatusMessage = removed == 0
                    ? $"Nothing to remove: no saved scans were found for {rowViewModel.MachineName}."
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"Removed {rowViewModel.MachineName} ({removed} saved scan{(removed == 1 ? string.Empty : "s")}).");
            }
        }
        catch (Exception exception)
        {
            StatusMessage = $"Remove failed: {exception.Message}";
        }
        finally
        {
            _removing = false;
        }
    }

    /// <summary>
    /// Technician mode import (v2.1.0): each picked JSON report is read
    /// through <see cref="SnapshotReportImporter"/> and, when valid, saved
    /// into this store. Saving uses the store's upsert, so importing the
    /// same file twice stores one scan, never a duplicate. A file that
    /// fails validation is named in the status line and the rest still
    /// import. The fleet reloads afterwards so the new machines appear.
    /// </summary>
    private async Task ImportReportsAsync()
    {
        if (_importing)
        {
            return;
        }

        if (_store is null)
        {
            StatusMessage = "Import is unavailable: snapshot history is unavailable on this machine.";
            return;
        }

        if (ReportFilePicker is null)
        {
            StatusMessage = "Import is unavailable: no file picker is available.";
            return;
        }

        _importing = true;
        try
        {
            var paths = await ReportFilePicker();
            if (paths is null || paths.Count == 0)
            {
                return;
            }

            var importer = new SnapshotReportImporter();
            var imported = new List<string>();
            var skipped = new List<string>();
            foreach (var path in paths)
            {
                var fileName = Path.GetFileName(path);
                try
                {
                    await using var stream = File.OpenRead(path);
                    var result = await importer.ImportAsync(stream);
                    if (!result.Success || result.Snapshot is null)
                    {
                        skipped.Add($"{fileName}: {result.Error ?? "could not be imported."}");
                        continue;
                    }

                    await _store.SaveAsync(result.Snapshot);
                    imported.Add(result.Snapshot.MachineName);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    skipped.Add($"{fileName}: could not be read ({exception.Message}).");
                }
            }

            await LoadAsync();
            var summary = imported.Count > 0
                ? $"Imported {imported.Count} report{(imported.Count == 1 ? string.Empty : "s")} ({string.Join(", ", imported)})"
                : "Imported 0 reports";
            summary += skipped.Count > 0
                ? $". Skipped {skipped.Count}: {string.Join("; ", skipped)}"
                : ".";
            if (!StatusMessage.StartsWith("Fleet could not be read", StringComparison.Ordinal))
            {
                StatusMessage = summary;
            }
        }
        finally
        {
            _importing = false;
        }
    }
}
