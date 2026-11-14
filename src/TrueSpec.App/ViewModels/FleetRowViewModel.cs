namespace TrueSpec.App.ViewModels;

using System;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using TrueSpec.Core.History;
using TrueSpec.Core.Localization;

/// <summary>
/// One row of the Fleet table, pre-formatted for the view. The text
/// matches the fleet CSV export exactly, so the file and the table agree.
/// The row also carries its source row and the per-machine commands, so
/// the Export button on the row can write that one machine's scan as its
/// own CSV and the Remove button can take the machine out of the fleet.
///
/// Localisation, phase 8 (v2.1.16): the two button labels render in
/// the current language — "Remove" / "Confirm remove" and "Export
/// CSV" — through <see cref="ApplyLanguage"/>, the same in-place
/// pattern the other pages use. The cell values stay English: the
/// machine name, the scan count, the last-scan date, the score (with
/// its "Not measurable" fallback, like the History table's), the
/// verdict text and the worst component (with its "Not reported"
/// fallback) are measured data, not page chrome.
/// </summary>
public sealed partial class FleetRowViewModel : ViewModelBase
{
    private string _languageCode;

    public FleetRowViewModel(
        FleetMachineRow row,
        ICommand? exportMachineCsvCommand = null,
        ICommand? removeMachineCommand = null,
        string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(row);

        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
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

    /// <summary>"Export CSV" in the page language.</summary>
    public string ExportButtonText =>
        StringLocalizer.Get(AppStringKeys.FleetRowExportButton, _languageCode);

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

    /// <summary>
    /// "Remove" normally, "Confirm remove" once armed, in the page
    /// language. The unarmed label reuses the table's "Remove" column
    /// header key instead of a second copy of the same word.
    /// </summary>
    public string RemoveButtonText => IsRemoveArmed
        ? StringLocalizer.Get(AppStringKeys.FleetRowConfirmRemoveButton, _languageCode)
        : StringLocalizer.Get(AppStringKeys.FleetHeaderRemove, _languageCode);

    partial void OnIsRemoveArmedChanged(bool value) => OnPropertyChanged(nameof(RemoveButtonText));

    /// <summary>
    /// Re-renders the row's button labels in the given language, in
    /// place. Called by the Fleet page when the user picks a language
    /// in Settings; a row created after the switch is already built
    /// in the new language by the constructor. An unknown code falls
    /// back to English through the localizer, never to a blank. The
    /// cell values are untouched.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        OnPropertyChanged(nameof(ExportButtonText));
        OnPropertyChanged(nameof(RemoveButtonText));
    }
}
