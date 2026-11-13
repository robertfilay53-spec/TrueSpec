namespace TrueSpec.App.ViewModels;

using System;
using System.Globalization;
using System.Windows.Input;
using TrueSpec.Core.History;

/// <summary>
/// One row of the Fleet table, pre-formatted for the view. The text
/// matches the fleet CSV export exactly, so the file and the table agree.
/// The row also carries its source row and the per-machine export command,
/// so the Export button on the row can write that one machine's scan as
/// its own CSV.
/// </summary>
public sealed class FleetRowViewModel
{
    public FleetRowViewModel(FleetMachineRow row, ICommand? exportMachineCsvCommand = null)
    {
        ArgumentNullException.ThrowIfNull(row);

        Row = row;
        ExportMachineCsvCommand = exportMachineCsvCommand;
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

    public string MachineName { get; }

    public string ScanCountText { get; }

    public string LastScanText { get; }

    public string ScoreText { get; }

    public string VerdictText { get; }

    public string WorstComponentText { get; }
}
