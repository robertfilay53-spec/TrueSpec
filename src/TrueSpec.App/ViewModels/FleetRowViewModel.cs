namespace TrueSpec.App.ViewModels;

using System;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using TrueSpec.Core.History;

/// <summary>
/// One row of the Fleet table, pre-formatted for the view. The text
/// matches the fleet CSV export exactly, so the file and the table agree.
/// The row also carries its source row and the per-machine commands, so
/// the Export button on the row can write that one machine's scan as its
/// own CSV and the Remove button can take the machine out of the fleet.
/// </summary>
public sealed partial class FleetRowViewModel : ViewModelBase
{
    public FleetRowViewModel(
        FleetMachineRow row,
        ICommand? exportMachineCsvCommand = null,
        ICommand? removeMachineCommand = null)
    {
        ArgumentNullException.ThrowIfNull(row);

        Row = row;
        ExportMachineCsvCommand = exportMachineCsvCommand;
        RemoveMachineCommand = removeMachineCommand;
        MachineName = row.MachineName;
        ScanCountText = row.ScanCount.ToString(CultureInfo.InvariantCulture);
        LastScanText = row.LastScan.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        ScoreText = row.LatestScore is null
            ? "Not measurable"
            : row.LatestScore.Value.ToString("0.#", CultureInfo.InvariantCulture);
        VerdictText = row.Verdict;
        WorstComponentText = row.WorstComponent is null
            ? "Not reported"
            : row.WorstComponentHealth is null
                ? row.WorstComponent
                : string.Create(CultureInfo.InvariantCulture, $"{row.WorstComponent} — {row.WorstComponentHealth.Value:0.#}%");
    }

    public FleetMachineRow Row { get; }

    /// <summary>The per-machine export command, supplied by the Fleet page. Null when no caller supplied one.</summary>
    public ICommand? ExportMachineCsvCommand { get; }

    /// <summary>The per-machine remove command, supplied by the Fleet page. Null when no caller supplied one.</summary>
    public ICommand? RemoveMachineCommand { get; }

    public string MachineName { get; }

    public string ScanCountText { get; }

    public string LastScanText { get; }

    public string ScoreText { get; }

    public string VerdictText { get; }

    public string WorstComponentText { get; }

    /// <summary>
    /// True after the first click on Remove: the button reads "Confirm
    /// remove" until the second click deletes the machine. The app has
    /// no modal confirm dialog, so the two-step button IS the
    /// confirmation — a single misclick can never delete a machine's
    /// whole history. The flag resets whenever the fleet reloads, which
    /// rebuilds the rows.
    /// </summary>
    [ObservableProperty]
    public partial bool IsRemoveArmed { get; set; }

    /// <summary>"Remove" normally, "Confirm remove" once armed.</summary>
    public string RemoveButtonText => IsRemoveArmed ? "Confirm remove" : "Remove";

    partial void OnIsRemoveArmedChanged(bool value) => OnPropertyChanged(nameof(RemoveButtonText));
}
