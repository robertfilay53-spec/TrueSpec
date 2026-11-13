namespace TrueSpec.App.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Storage;

/// <summary>
/// Technician mode, fleet page (plan: Beyond v2.0, item 1): one row per
/// machine in the local snapshot store, lowest score first, with the
/// worst component named. A technician carries the portable app on a USB
/// stick, scans each machine, and every scan lands in the same store on
/// the stick — this page is the fleet summary of that store. Every value
/// comes from a real saved snapshot; a machine whose latest scan is
/// unreadable is skipped, never shown with invented numbers.
/// </summary>
public sealed partial class FleetViewModel : ViewModelBase
{
    private readonly ISnapshotStore? _store;
    private readonly Func<FleetMachineRow, SystemSnapshot, Task>? _exportMachineCsv;
    private readonly Dictionary<string, SystemSnapshot> _snapshotsByMachine = new(StringComparer.OrdinalIgnoreCase);

    public FleetViewModel(
        ISnapshotStore? store,
        ICommand? exportFleetCsvCommand = null,
        Func<FleetMachineRow, SystemSnapshot, Task>? exportMachineCsv = null)
    {
        _store = store;
        _exportMachineCsv = exportMachineCsv;
        ExportFleetCsvCommand = exportFleetCsvCommand;
        ExportMachineCsvCommand = new AsyncRelayCommand<FleetRowViewModel>(ExportMachineCsvAsync);
        _ = LoadAsync();
    }

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
                StatusMessage = "No saved scans yet. Scan a machine, or run the portable app on each machine in the fleet, and its latest scan appears here.";
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
}
