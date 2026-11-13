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
/// Every value comes from a real saved snapshot; a machine whose latest
/// scan is unreadable is skipped, never shown with invented numbers.
/// </summary>
public sealed partial class FleetViewModel : ViewModelBase
{
    private readonly ISnapshotStore? _store;
    private readonly Func<FleetMachineRow, SystemSnapshot, Task>? _exportMachineCsv;
    private readonly Dictionary<string, SystemSnapshot> _snapshotsByMachine = new(StringComparer.OrdinalIgnoreCase);
    private bool _importing;

    public FleetViewModel(
        ISnapshotStore? store,
        ICommand? exportFleetCsvCommand = null,
        Func<FleetMachineRow, SystemSnapshot, Task>? exportMachineCsv = null)
    {
        _store = store;
        _exportMachineCsv = exportMachineCsv;
        ExportFleetCsvCommand = exportFleetCsvCommand;
        ExportMachineCsvCommand = new AsyncRelayCommand<FleetRowViewModel>(ExportMachineCsvAsync);
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
                Rows.Add(new FleetRowViewModel(row, ExportMachineCsvCommand));
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
