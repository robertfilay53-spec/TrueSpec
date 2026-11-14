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
using TrueSpec.Core.Localization;
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
///
/// Localisation, phase 8 (v2.1.16): the page chrome renders in the
/// current language — the eyebrow, title, intro, the Import button
/// and note, the Machines heading, the two export buttons, the table
/// note, the column headers and the row buttons, plus the transient
/// status lines (loading, no scans, the summary, the export-all /
/// remove / import lines) — so the page follows the language the
/// user picked in Settings, with no restart. The data the page shows
/// stays English: machine names, the row cell values (scan counts,
/// dates, scores with their "Not measurable" fallback, verdicts,
/// worst components with their "Not reported" fallback), file and
/// folder paths, and exception messages. The summary and the
/// export-all / remove / import lines are templates with one/many
/// keys instead of inline plural hacks, and the remove-arm line
/// quotes the translated Confirm-remove label. The page is cached by
/// the shell, so the strings resolve from the stored language code
/// and re-render through <see cref="ApplyLanguage"/>, the same
/// in-place pattern the Overview and History pages use.
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

    private string _languageCode;

    /// <summary>Which transient status line is showing, so ApplyLanguage can re-resolve it instead of resetting it.</summary>
    private FleetStatusKind _statusKind = FleetStatusKind.Loading;

    /// <summary>The exception message of a failed status, or null when the current status carries none.</summary>
    private string? _statusError;

    /// <summary>The machine name of a machine-export / remove status, or null when the current status carries none.</summary>
    private string? _statusMachine;

    /// <summary>The scan count of a remove-arm or removed status.</summary>
    private int _statusCount;

    /// <summary>The export-all result of an export-all status, so ApplyLanguage can rebuild its line.</summary>
    private ExportAllMachinesResult? _lastExportAllResult;

    /// <summary>The fleet size an export-all status was built from.</summary>
    private int _lastExportAllTotal;

    /// <summary>The machine names an import status imported, so ApplyLanguage can rebuild its line.</summary>
    private IReadOnlyList<string> _lastImported = Array.Empty<string>();

    /// <summary>The "file: reason" lines an import status skipped, so ApplyLanguage can rebuild its line.</summary>
    private IReadOnlyList<string> _lastImportSkipped = Array.Empty<string>();

    /// <summary>The transient status lines the Fleet page can show. Stored so a language switch re-resolves the line instead of resetting it.</summary>
    private enum FleetStatusKind
    {
        Loading,
        Unavailable,
        NoScans,
        Loaded,
        ReadFailed,
        MachineExportUnavailable,
        MachineExportNoScan,
        ExportAllUnavailable,
        ExportAllNoFleet,
        ExportAllFailed,
        ExportAllResult,
        RemoveUnavailable,
        RemoveArm,
        RemoveNothing,
        Removed,
        RemoveFailed,
        ImportUnavailable,
        ImportNoPicker,
        ImportResult,
    }

    public FleetViewModel(
        ISnapshotStore? store,
        ICommand? exportFleetCsvCommand = null,
        Func<FleetMachineRow, SystemSnapshot, Task>? exportMachineCsv = null,
        Func<IReadOnlyList<FleetMachineRow>, IReadOnlyDictionary<string, SystemSnapshot>, Task<ExportAllMachinesResult>>? exportAllMachinesCsv = null,
        string? languageCode = null)
    {
        _store = store;
        _exportMachineCsv = exportMachineCsv;
        _exportAllMachinesCsv = exportAllMachinesCsv;
        ExportFleetCsvCommand = exportFleetCsvCommand;
        ExportMachineCsvCommand = new AsyncRelayCommand<FleetRowViewModel>(ExportMachineCsvAsync);
        ExportAllMachinesCommand = new AsyncRelayCommand(ExportAllMachinesAsync);
        RemoveMachineCommand = new AsyncRelayCommand<FleetRowViewModel>(RemoveMachineAsync);
        ImportReportsCommand = new AsyncRelayCommand(ImportReportsAsync);
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        SetStatus(FleetStatusKind.Loading);
        _ = LoadAsync();
    }

    /// <summary>The page eyebrow, in the page language.</summary>
    public string FleetEyebrowText =>
        StringLocalizer.Get(AppStringKeys.FleetEyebrow, _languageCode);

    /// <summary>The page title, in the page language.</summary>
    public string FleetTitleText =>
        StringLocalizer.Get(AppStringKeys.FleetTitle, _languageCode);

    /// <summary>The page intro, in the page language.</summary>
    public string FleetIntroText =>
        StringLocalizer.Get(AppStringKeys.FleetIntro, _languageCode);

    /// <summary>The "Import JSON reports" button label, in the page language.</summary>
    public string FleetImportButtonText =>
        StringLocalizer.Get(AppStringKeys.FleetImportButton, _languageCode);

    /// <summary>The import explainer note, in the page language.</summary>
    public string FleetImportNoteText =>
        StringLocalizer.Get(AppStringKeys.FleetImportNote, _languageCode);

    /// <summary>The "Machines" heading, in the page language.</summary>
    public string FleetMachinesTitleText =>
        StringLocalizer.Get(AppStringKeys.FleetMachinesTitle, _languageCode);

    /// <summary>The "Export all machines" button label, in the page language.</summary>
    public string FleetExportAllButtonText =>
        StringLocalizer.Get(AppStringKeys.FleetExportAllButton, _languageCode);

    /// <summary>The "Export fleet as CSV" button label, in the page language.</summary>
    public string FleetExportFleetCsvButtonText =>
        StringLocalizer.Get(AppStringKeys.FleetExportFleetCsvButton, _languageCode);

    /// <summary>The table note, in the page language.</summary>
    public string FleetTableNoteText =>
        StringLocalizer.Get(AppStringKeys.FleetTableNote, _languageCode);

    /// <summary>The "Machine" column header, in the page language.</summary>
    public string FleetHeaderMachineText =>
        StringLocalizer.Get(AppStringKeys.FleetHeaderMachine, _languageCode);

    /// <summary>The "Scans" column header, in the page language.</summary>
    public string FleetHeaderScansText =>
        StringLocalizer.Get(AppStringKeys.FleetHeaderScans, _languageCode);

    /// <summary>The "Last scan" column header, in the page language.</summary>
    public string FleetHeaderLastScanText =>
        StringLocalizer.Get(AppStringKeys.FleetHeaderLastScan, _languageCode);

    /// <summary>The "Score" column header, in the page language.</summary>
    public string FleetHeaderScoreText =>
        StringLocalizer.Get(AppStringKeys.FleetHeaderScore, _languageCode);

    /// <summary>The "Verdict" column header, in the page language.</summary>
    public string FleetHeaderVerdictText =>
        StringLocalizer.Get(AppStringKeys.FleetHeaderVerdict, _languageCode);

    /// <summary>The "Worst component" column header, in the page language.</summary>
    public string FleetHeaderWorstComponentText =>
        StringLocalizer.Get(AppStringKeys.FleetHeaderWorstComponent, _languageCode);

    /// <summary>The "Export" column header, in the page language.</summary>
    public string FleetHeaderExportText =>
        StringLocalizer.Get(AppStringKeys.FleetHeaderExport, _languageCode);

    /// <summary>The "Remove" column header, in the page language.</summary>
    public string FleetHeaderRemoveText =>
        StringLocalizer.Get(AppStringKeys.FleetHeaderRemove, _languageCode);

    /// <summary>
    /// Re-renders this page in the given language, in place: every
    /// localized text is re-resolved from the stored state, and every
    /// row re-renders its button labels. Called by the shell when the
    /// user picks a language in Settings; a page created after the
    /// switch is already built in the new language by the
    /// constructor. An unknown code falls back to English through the
    /// localizer, never to a blank. Measured data (machine names, row
    /// cell values, file and folder paths) is untouched.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        OnPropertyChanged(nameof(FleetEyebrowText));
        OnPropertyChanged(nameof(FleetTitleText));
        OnPropertyChanged(nameof(FleetIntroText));
        OnPropertyChanged(nameof(FleetImportButtonText));
        OnPropertyChanged(nameof(FleetImportNoteText));
        OnPropertyChanged(nameof(FleetMachinesTitleText));
        OnPropertyChanged(nameof(FleetExportAllButtonText));
        OnPropertyChanged(nameof(FleetExportFleetCsvButtonText));
        OnPropertyChanged(nameof(FleetTableNoteText));
        OnPropertyChanged(nameof(FleetHeaderMachineText));
        OnPropertyChanged(nameof(FleetHeaderScansText));
        OnPropertyChanged(nameof(FleetHeaderLastScanText));
        OnPropertyChanged(nameof(FleetHeaderScoreText));
        OnPropertyChanged(nameof(FleetHeaderVerdictText));
        OnPropertyChanged(nameof(FleetHeaderWorstComponentText));
        OnPropertyChanged(nameof(FleetHeaderExportText));
        OnPropertyChanged(nameof(FleetHeaderRemoveText));
        StatusMessage = ResolveStatusMessage();
        SummaryText = ResolveSummaryText();
        foreach (var row in Rows)
        {
            row.ApplyLanguage(_languageCode);
        }
    }

    /// <summary>Records which status line is showing and renders it in the page language.</summary>
    private void SetStatus(
        FleetStatusKind kind,
        string? error = null,
        string? machine = null,
        int count = 0,
        ExportAllMachinesResult? exportAll = null,
        int exportAllTotal = 0,
        IReadOnlyList<string>? imported = null,
        IReadOnlyList<string>? skipped = null)
    {
        _statusKind = kind;
        _statusError = error;
        _statusMachine = machine;
        _statusCount = count;
        _lastExportAllResult = exportAll;
        _lastExportAllTotal = exportAllTotal;
        _lastImported = imported ?? Array.Empty<string>();
        _lastImportSkipped = skipped ?? Array.Empty<string>();
        StatusMessage = ResolveStatusMessage();
    }

    /// <summary>Resolves the stored status kind to its sentence in the page language.</summary>
    private string ResolveStatusMessage() => _statusKind switch
    {
        FleetStatusKind.Loading => StringLocalizer.Get(AppStringKeys.FleetLoading, _languageCode),
        FleetStatusKind.Unavailable => StringLocalizer.Get(AppStringKeys.FleetUnavailable, _languageCode),
        FleetStatusKind.NoScans => StringLocalizer.Get(AppStringKeys.FleetNoScans, _languageCode),
        FleetStatusKind.Loaded => StringLocalizer.Get(AppStringKeys.FleetLoaded, _languageCode),
        FleetStatusKind.ReadFailed => StringLocalizer.GetFormatted(AppStringKeys.FleetReadFailed, _languageCode, _statusError ?? string.Empty),
        FleetStatusKind.MachineExportUnavailable => StringLocalizer.Get(AppStringKeys.FleetMachineExportUnavailable, _languageCode),
        FleetStatusKind.MachineExportNoScan => StringLocalizer.GetFormatted(AppStringKeys.FleetMachineExportNoScan, _languageCode, _statusMachine ?? string.Empty),
        FleetStatusKind.ExportAllUnavailable => StringLocalizer.Get(AppStringKeys.FleetExportAllUnavailable, _languageCode),
        FleetStatusKind.ExportAllNoFleet => StringLocalizer.Get(AppStringKeys.FleetExportAllNoFleet, _languageCode),
        FleetStatusKind.ExportAllFailed => StringLocalizer.GetFormatted(AppStringKeys.FleetExportAllFailed, _languageCode, _statusError ?? string.Empty),
        FleetStatusKind.ExportAllResult => _lastExportAllResult is null
            ? string.Empty
            : BuildExportAllStatus(_lastExportAllResult, _lastExportAllTotal),
        FleetStatusKind.RemoveUnavailable => StringLocalizer.Get(AppStringKeys.FleetRemoveUnavailable, _languageCode),
        FleetStatusKind.RemoveArm => _statusCount == 1
            ? StringLocalizer.GetFormatted(
                AppStringKeys.FleetRemoveArmOne,
                _languageCode,
                _statusMachine ?? string.Empty,
                StringLocalizer.Get(AppStringKeys.FleetRowConfirmRemoveButton, _languageCode))
            : StringLocalizer.GetFormatted(
                AppStringKeys.FleetRemoveArmMany,
                _languageCode,
                _statusMachine ?? string.Empty,
                _statusCount,
                StringLocalizer.Get(AppStringKeys.FleetRowConfirmRemoveButton, _languageCode)),
        FleetStatusKind.RemoveNothing => StringLocalizer.GetFormatted(AppStringKeys.FleetRemoveNothing, _languageCode, _statusMachine ?? string.Empty),
        FleetStatusKind.Removed => _statusCount == 1
            ? StringLocalizer.GetFormatted(AppStringKeys.FleetRemovedOne, _languageCode, _statusMachine ?? string.Empty)
            : StringLocalizer.GetFormatted(AppStringKeys.FleetRemovedMany, _languageCode, _statusMachine ?? string.Empty, _statusCount),
        FleetStatusKind.RemoveFailed => StringLocalizer.GetFormatted(AppStringKeys.FleetRemoveFailed, _languageCode, _statusError ?? string.Empty),
        FleetStatusKind.ImportUnavailable => StringLocalizer.Get(AppStringKeys.FleetImportUnavailable, _languageCode),
        FleetStatusKind.ImportNoPicker => StringLocalizer.Get(AppStringKeys.FleetImportNoPicker, _languageCode),
        FleetStatusKind.ImportResult => BuildImportSummary(_lastImported, _lastImportSkipped),
        _ => string.Empty,
    };

    /// <summary>
    /// Resolves the fleet summary from the loaded rows in the page
    /// language: how many machines, how many saved scans, and the
    /// lowest score with its machine. Empty when there is no fleet.
    /// The score keeps the app's invariant formatting; a lowest score
    /// that was not measurable reads as the summary's own fallback
    /// word, not as a number.
    /// </summary>
    private string ResolveSummaryText()
    {
        var rows = CurrentRows;
        if (rows is null || rows.Count == 0)
        {
            return string.Empty;
        }

        var totalScans = rows.Sum(r => r.ScanCount);
        var lowest = rows[0];
        var scoreText = lowest.LatestScore is null
            ? StringLocalizer.Get(AppStringKeys.FleetScoreNotMeasurable, _languageCode)
            : lowest.LatestScore.Value.ToString("0.#", CultureInfo.InvariantCulture);
        return (rows.Count, totalScans) switch
        {
            (1, 1) => StringLocalizer.GetFormatted(
                AppStringKeys.FleetSummaryOneOne,
                _languageCode,
                scoreText,
                lowest.MachineName),
            (1, _) => StringLocalizer.GetFormatted(
                AppStringKeys.FleetSummaryOneMany,
                _languageCode,
                totalScans,
                scoreText,
                lowest.MachineName),
            (_, 1) => StringLocalizer.GetFormatted(
                AppStringKeys.FleetSummaryManyOne,
                _languageCode,
                rows.Count,
                scoreText,
                lowest.MachineName),
            _ => StringLocalizer.GetFormatted(
                AppStringKeys.FleetSummaryManyMany,
                _languageCode,
                rows.Count,
                totalScans,
                scoreText,
                lowest.MachineName),
        };
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
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasFleet { get; set; }

    private async Task LoadAsync()
    {
        if (_store is null)
        {
            SetStatus(FleetStatusKind.Unavailable);
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
                Rows.Add(new FleetRowViewModel(row, ExportMachineCsvCommand, RemoveMachineCommand, _languageCode));
            }

            HasFleet = rows.Count > 0;
            if (rows.Count == 0)
            {
                SetStatus(FleetStatusKind.NoScans);
                SummaryText = string.Empty;
                return;
            }

            SummaryText = ResolveSummaryText();
            SetStatus(FleetStatusKind.Loaded);
        }
        catch (Exception exception)
        {
            SetStatus(FleetStatusKind.ReadFailed, error: exception.Message);
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
            SetStatus(FleetStatusKind.MachineExportUnavailable);
            return;
        }

        if (!_snapshotsByMachine.TryGetValue(rowViewModel.MachineName, out var snapshot) || snapshot is null)
        {
            SetStatus(FleetStatusKind.MachineExportNoScan, machine: rowViewModel.MachineName);
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
            SetStatus(FleetStatusKind.ExportAllUnavailable);
            return;
        }

        var rows = CurrentRows;
        if (rows is null || rows.Count == 0)
        {
            SetStatus(FleetStatusKind.ExportAllNoFleet);
            return;
        }

        _exportingAll = true;
        try
        {
            var result = await _exportAllMachinesCsv(rows, _snapshotsByMachine);
            SetStatus(FleetStatusKind.ExportAllResult, exportAll: result, exportAllTotal: rows.Count);
        }
        catch (Exception exception)
        {
            SetStatus(FleetStatusKind.ExportAllFailed, error: exception.Message);
        }
        finally
        {
            _exportingAll = false;
        }
    }

    private string BuildExportAllStatus(ExportAllMachinesResult result, int totalRows)
    {
        var exported = result.ExportedPaths.Count;
        if (exported == 0)
        {
            var skipped = result.Skipped.Count == 0
                ? StringLocalizer.Get(AppStringKeys.FleetExportAllZeroNoFleet, _languageCode)
                : StringLocalizer.GetFormatted(
                    AppStringKeys.FleetSkipped,
                    _languageCode,
                    result.Skipped.Count,
                    string.Join("; ", result.Skipped.Select(s => $"{s.MachineName}: {s.Reason}"))) + ".";
            return StringLocalizer.GetFormatted(AppStringKeys.FleetExportAllZero, _languageCode, skipped);
        }

        var summary = totalRows == 1
            ? StringLocalizer.GetFormatted(
                AppStringKeys.FleetExportAllTotalOne,
                _languageCode,
                exported,
                result.Folder)
            : StringLocalizer.GetFormatted(
                AppStringKeys.FleetExportAllTotalMany,
                _languageCode,
                exported,
                totalRows,
                result.Folder);
        if (result.Skipped.Count > 0)
        {
            summary += " " + StringLocalizer.GetFormatted(
                AppStringKeys.FleetSkipped,
                _languageCode,
                result.Skipped.Count,
                string.Join("; ", result.Skipped.Select(s => $"{s.MachineName}: {s.Reason}"))) + ".";
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
            SetStatus(FleetStatusKind.RemoveUnavailable);
            return;
        }

        if (!rowViewModel.IsRemoveArmed)
        {
            rowViewModel.IsRemoveArmed = true;
            SetStatus(FleetStatusKind.RemoveArm, machine: rowViewModel.MachineName, count: rowViewModel.Row.ScanCount);
            return;
        }

        _removing = true;
        try
        {
            var removed = await _store.DeleteMachineAsync(rowViewModel.MachineName);
            await LoadAsync();
            if (_statusKind != FleetStatusKind.ReadFailed)
            {
                SetStatus(
                    removed == 0 ? FleetStatusKind.RemoveNothing : FleetStatusKind.Removed,
                    machine: rowViewModel.MachineName,
                    count: removed);
            }
        }
        catch (Exception exception)
        {
            SetStatus(FleetStatusKind.RemoveFailed, error: exception.Message);
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
            SetStatus(FleetStatusKind.ImportUnavailable);
            return;
        }

        if (ReportFilePicker is null)
        {
            SetStatus(FleetStatusKind.ImportNoPicker);
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
            if (_statusKind != FleetStatusKind.ReadFailed)
            {
                SetStatus(FleetStatusKind.ImportResult, imported: imported, skipped: skipped);
            }
        }
        finally
        {
            _importing = false;
        }
    }

    private string BuildImportSummary(IReadOnlyList<string> imported, IReadOnlyList<string> skipped)
    {
        var summary = imported.Count switch
        {
            0 => StringLocalizer.Get(AppStringKeys.FleetImportedNone, _languageCode),
            1 => StringLocalizer.GetFormatted(AppStringKeys.FleetImportedOne, _languageCode, imported[0]),
            _ => StringLocalizer.GetFormatted(AppStringKeys.FleetImportedMany, _languageCode, imported.Count, string.Join(", ", imported)),
        };
        summary += skipped.Count > 0
            ? ". " + StringLocalizer.GetFormatted(
                AppStringKeys.FleetSkipped,
                _languageCode,
                skipped.Count,
                string.Join("; ", skipped))
            : ".";
        return summary;
    }
}
