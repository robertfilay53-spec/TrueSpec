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
using TrueSpec.Core.Localization;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;
using TrueSpec.Core.Storage;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// History page: score, battery-health and storage-wear trends across the
/// saved scans in the local SQLite store. Every point comes from a real
/// saved snapshot; a value a scan did not report is a gap in the line and
/// in the table, never a zero. History is shown for one machine at a time:
/// the picker switches the trend, and the compare card puts two machines
/// side by side without ever mixing them into one trend line.
///
/// Localisation, phase 6 (v2.1.12): the page chrome renders in the
/// current language — the headings, the machine picker, the trend-card
/// titles, the sensor-log sentences, the saved-scans table headers and
/// remove buttons, the compare and value sections, and the status
/// lines — so the page follows the language the user picked in
/// Settings, with no restart. The data the page shows stays English:
/// machine names, the trend summary and the battery / storage /
/// driver-age forecast texts are Core-owned wordings produced by the
/// trend analyzer with no language parameter yet; the value verdict
/// sentence and its no-verdict reason are Core-owned wordings produced
/// by ValueComparer; and the table's cell fallbacks ("Not measurable" /
/// "Not reported") are measured-data fallbacks that the CSV export
/// agrees with row for row, so the CSV contract stays English.
/// Transient status lines are re-derived from small stored fields
/// (which message is showing, its scan date, its error), so a language
/// switch re-resolves them instead of silently resetting them. The
/// shell re-languages this long-lived page through
/// <see cref="ApplyLanguage"/> when the user picks a language in
/// Settings, the same in-place pattern the Overview page uses.
/// </summary>
public sealed partial class HistoryViewModel : ViewModelBase
{
    /// <summary>Newest samples kept for the log chart, so a very long session does not slow the render.</summary>
    public const int MaxLogChartPoints = 2000;

    private readonly ISnapshotStore? _store;
    private readonly SystemSnapshot _current;
    private int _loadGeneration;
    private string? _displayedMachine;
    private bool _suppressSelectionReload;
    private bool _removing;
    private string? _sensorLogPath;
    private bool _sensorLoggingEnabled;
    private double? _cpuTemperatureThresholdC;
    private readonly TimeZoneInfo _displayTimeZone;

    private string _languageCode;

    /// <summary>U9: the raw (verdict-free) comparison the value layer sits on top of. Null until two machines are compared.</summary>
    private MachineComparison? _comparison;

    /// <summary>U9: each machine's latest snapshot, for the per-component winners only. Never written to.</summary>
    private SystemSnapshot? _latestSnapshotA;

    /// <summary>U9: each machine's latest snapshot, for the per-component winners only. Never written to.</summary>
    private SystemSnapshot? _latestSnapshotB;

    /// <summary>U9: the last value-comparison result, kept so ApplyLanguage can rebuild the value rows and winners in the new language.</summary>
    private ValueComparisonResult? _lastValueResult;

    /// <summary>Which transient status line is showing, so ApplyLanguage can re-resolve it instead of resetting it.</summary>
    private HistoryStatusKind _statusKind = HistoryStatusKind.Loading;

    /// <summary>The exception message of a read/remove failure status, or null when the current status carries none.</summary>
    private string? _statusError;

    /// <summary>The scan date of a remove-confirmation or removed-scan status, or null when the current status carries none.</summary>
    private string? _statusScanDate;

    /// <summary>The remaining scan count of a removed-scan status, or the scan count of a scans-count status.</summary>
    private int _statusRemaining;

    /// <summary>The machine names of the last successful comparison, so ApplyLanguage can re-resolve the compared sentence.</summary>
    private string? _comparedMachineA;

    /// <summary>The machine names of the last successful comparison, so ApplyLanguage can re-resolve the compared sentence.</summary>
    private string? _comparedMachineB;

    /// <summary>The exception message of a failed comparison, or null when no comparison failure is showing.</summary>
    private string? _compareFailedMessage;

    /// <summary>The exception message of a failed history load, or null when the load did not fail.</summary>
    private string? _comparisonLoadError;

    /// <summary>Which transient value status is showing, so ApplyLanguage can re-resolve it instead of resetting it.</summary>
    private HistoryValueStatusKind _valueKind = HistoryValueStatusKind.Prompt;

    /// <summary>The Core-owned no-verdict reason, shown verbatim when the value status is <see cref="HistoryValueStatusKind.NoVerdict"/>.</summary>
    private string? _valueNoVerdictReason;

    /// <summary>The transient status lines the History page can show. Stored so a language switch re-resolves the line instead of resetting it.</summary>
    private enum HistoryStatusKind
    {
        Loading,
        Unavailable,
        NoScans,
        OneScan,
        ScansCount,
        ReadFailed,
        RemoveUnavailable,
        RemoveConfirm,
        RemoveGone,
        RemovedNone,
        RemovedOne,
        RemovedMany,
        RemoveFailed,
    }

    /// <summary>The transient value statuses the compare-value section can show. Stored so a language switch re-resolves the line instead of resetting it.</summary>
    private enum HistoryValueStatusKind
    {
        Prompt,
        CompareFirst,
        BadPrice,
        HonestyNote,
        NoVerdict,
    }

    public HistoryViewModel(ISnapshotStore? store, SystemSnapshot current, ICommand? exportHistoryCsvCommand = null, string? sensorLogPath = null, bool sensorLoggingEnabled = true, double? cpuTemperatureThresholdC = null, TimeZoneInfo? displayTimeZone = null, string? languageCode = null)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        _store = store;
        _current = current;
        ExportHistoryCsvCommand = exportHistoryCsvCommand;
        RemoveScanCommand = new AsyncRelayCommand<HistoryRowViewModel>(RemoveScanAsync);
        _sensorLogPath = sensorLogPath;
        _sensorLoggingEnabled = sensorLoggingEnabled;
        _cpuTemperatureThresholdC = cpuTemperatureThresholdC;
        _displayTimeZone = displayTimeZone ?? TimeZoneInfo.Local;
        SensorLogThresholdC = cpuTemperatureThresholdC;
        SensorLogStatusText = StringLocalizer.Get(AppStringKeys.HistoryLogLoading, _languageCode);
        ValueStatusText = ResolveValueStatusText();
        MachineSubtitleText = StringLocalizer.GetFormatted(AppStringKeys.HistorySubtitleTemplate, _languageCode, current.MachineName);
        SetStatus(HistoryStatusKind.Loading);
        _ = LoadAsync();
    }

    /// <summary>
    /// Supplied by MainViewModel (mirrors how ReportViewModel receives the
    /// export commands): writes the currently shown trend to the configured
    /// export folder via HistoryCsvExporter. Null when no caller supplied one.
    /// </summary>
    public ICommand? ExportHistoryCsvCommand { get; }

    /// <summary>
    /// Deletes one saved scan, invoked from the Remove button on that
    /// scan's row. Two clicks by design (see
    /// <see cref="RemoveScanAsync"/>): the app has no modal confirm
    /// dialog, so the first click arms the row and only the second
    /// deletes. The row identifies the scan by its snapshot id, so the
    /// delete can never touch another scan of the same machine.
    /// </summary>
    public IAsyncRelayCommand<HistoryRowViewModel> RemoveScanCommand { get; }

    /// <summary>The trend currently shown on the page, for the CSV export command.</summary>
    public HistoryTrend? CurrentTrend { get; private set; }

    public ObservableCollection<HistoryRowViewModel> Rows { get; } = new();

    public ObservableCollection<string> MachineNames { get; } = new();

    public ObservableCollection<MachineComparisonRowViewModel> ComparisonRows { get; } = new();

    /// <summary>U9 value rows (asking price, score per $1) for the two compared machines.</summary>
    public ObservableCollection<MachineComparisonRowViewModel> ValueRows { get; } = new();

    /// <summary>U9 per-component winners for the two compared machines.</summary>
    public ObservableCollection<ValueWinnerRowViewModel> ValueComponentWinners { get; } = new();

    /// <summary>The page eyebrow, in the page language.</summary>
    public string HistoryEyebrowText =>
        StringLocalizer.Get(AppStringKeys.HistoryEyebrow, _languageCode);

    /// <summary>The page title, in the page language.</summary>
    public string HistoryTitleText =>
        StringLocalizer.Get(AppStringKeys.HistoryTitle, _languageCode);

    /// <summary>The page introduction, in the page language.</summary>
    public string HistoryIntroText =>
        StringLocalizer.Get(AppStringKeys.HistoryIntro, _languageCode);

    /// <summary>The machine-picker card title, in the page language.</summary>
    public string HistoryMachineTitleText =>
        StringLocalizer.Get(AppStringKeys.HistoryMachineTitle, _languageCode);

    /// <summary>The machine-picker placeholder, in the page language.</summary>
    public string HistoryPickMachineText =>
        StringLocalizer.Get(AppStringKeys.HistoryPickMachine, _languageCode);

    /// <summary>The machine-picker note, in the page language.</summary>
    public string HistoryMachineNoteText =>
        StringLocalizer.Get(AppStringKeys.HistoryMachineNote, _languageCode);

    /// <summary>The battery-alert card eyebrow, in the page language.</summary>
    public string HistoryBatteryAlertEyebrowText =>
        StringLocalizer.Get(AppStringKeys.HistoryBatteryAlertEyebrow, _languageCode);

    /// <summary>The overall-score card title, in the page language.</summary>
    public string HistoryScoreTitleText =>
        StringLocalizer.Get(AppStringKeys.HistoryScoreTitle, _languageCode);

    /// <summary>The battery-health card title, in the page language.</summary>
    public string HistoryBatteryTitleText =>
        StringLocalizer.Get(AppStringKeys.HistoryBatteryTitle, _languageCode);

    /// <summary>The storage-wear card title, in the page language.</summary>
    public string HistoryWearTitleText =>
        StringLocalizer.Get(AppStringKeys.HistoryWearTitle, _languageCode);

    /// <summary>The forecast eyebrow under the sparkline cards, in the page language.</summary>
    public string HistoryForecastEyebrowText =>
        StringLocalizer.Get(AppStringKeys.HistoryForecastEyebrow, _languageCode);

    /// <summary>The CPU temperature log card title, in the page language.</summary>
    public string HistoryCpuLogTitleText =>
        StringLocalizer.Get(AppStringKeys.HistoryCpuLogTitle, _languageCode);

    /// <summary>The Refresh chart button, in the page language.</summary>
    public string HistoryRefreshChartText =>
        StringLocalizer.Get(AppStringKeys.HistoryRefreshChart, _languageCode);

    /// <summary>The caption under the sensor-log chart, in the page language.</summary>
    public string HistoryChartCaptionText =>
        StringLocalizer.Get(AppStringKeys.HistoryChartCaption, _languageCode);

    /// <summary>The driver-age card eyebrow, in the page language.</summary>
    public string HistoryDriverAgeEyebrowText =>
        StringLocalizer.Get(AppStringKeys.HistoryDriverAgeEyebrow, _languageCode);

    /// <summary>The saved-scans card title, in the page language.</summary>
    public string HistorySavedScansTitleText =>
        StringLocalizer.Get(AppStringKeys.HistorySavedScansTitle, _languageCode);

    /// <summary>The Export history as CSV button, in the page language.</summary>
    public string HistoryExportCsvText =>
        StringLocalizer.Get(AppStringKeys.HistoryExportCsv, _languageCode);

    /// <summary>The note under the saved-scans table, in the page language.</summary>
    public string HistoryTableNoteText =>
        StringLocalizer.Get(AppStringKeys.HistoryTableNote, _languageCode);

    /// <summary>The Scanned table header, in the page language.</summary>
    public string HistoryHeaderScannedText =>
        StringLocalizer.Get(AppStringKeys.HistoryHeaderScanned, _languageCode);

    /// <summary>The Score table header, in the page language.</summary>
    public string HistoryHeaderScoreText =>
        StringLocalizer.Get(AppStringKeys.HistoryHeaderScore, _languageCode);

    /// <summary>The Cycles table header, in the page language.</summary>
    public string HistoryHeaderCyclesText =>
        StringLocalizer.Get(AppStringKeys.HistoryHeaderCycles, _languageCode);

    /// <summary>The Battery health table header, in the page language (reuses the card title).</summary>
    public string HistoryHeaderBatteryText =>
        StringLocalizer.Get(AppStringKeys.HistoryBatteryTitle, _languageCode);

    /// <summary>The Storage wear table header, in the page language (reuses the card title).</summary>
    public string HistoryHeaderWearText =>
        StringLocalizer.Get(AppStringKeys.HistoryWearTitle, _languageCode);

    /// <summary>The Power-on hours table header, in the page language.</summary>
    public string HistoryHeaderPowerOnHoursText =>
        StringLocalizer.Get(AppStringKeys.HistoryHeaderPowerOnHours, _languageCode);

    /// <summary>The compare card title, in the page language.</summary>
    public string HistoryCompareTitleText =>
        StringLocalizer.Get(AppStringKeys.HistoryCompareTitle, _languageCode);

    /// <summary>The compare card note, in the page language.</summary>
    public string HistoryCompareNoteText =>
        StringLocalizer.Get(AppStringKeys.HistoryCompareNote, _languageCode);

    /// <summary>The Machine A picker label, in the page language.</summary>
    public string HistoryMachineAText =>
        StringLocalizer.Get(AppStringKeys.HistoryMachineA, _languageCode);

    /// <summary>The Machine B picker label, in the page language.</summary>
    public string HistoryMachineBText =>
        StringLocalizer.Get(AppStringKeys.HistoryMachineB, _languageCode);

    /// <summary>The Compare button, in the page language.</summary>
    public string HistoryCompareButtonText =>
        StringLocalizer.Get(AppStringKeys.HistoryCompareButton, _languageCode);

    /// <summary>The value section title, in the page language.</summary>
    public string HistoryValueTitleText =>
        StringLocalizer.Get(AppStringKeys.HistoryValueTitle, _languageCode);

    /// <summary>The value section introduction, in the page language.</summary>
    public string HistoryValueIntroText =>
        StringLocalizer.Get(AppStringKeys.HistoryValueIntro, _languageCode);

    /// <summary>The asking-price label above each price box, in the page language.</summary>
    public string HistoryAskingPriceLabelText =>
        StringLocalizer.Get(AppStringKeys.HistoryAskingPriceLabel, _languageCode);

    /// <summary>The asking-price placeholder, in the page language.</summary>
    public string HistoryPriceExampleText =>
        StringLocalizer.Get(AppStringKeys.HistoryPriceExample, _languageCode);

    /// <summary>The Compare value button, in the page language.</summary>
    public string HistoryCompareValueButtonText =>
        StringLocalizer.Get(AppStringKeys.HistoryCompareValueButton, _languageCode);

    /// <summary>The per-component winners heading, in the page language.</summary>
    public string HistoryWinnersTitleText =>
        StringLocalizer.Get(AppStringKeys.HistoryWinnersTitle, _languageCode);

    /// <summary>
    /// Re-renders this page in the given language, in place: every
    /// localized chrome property is re-resolved, the table rows are
    /// rebuilt from the stored trend, the comparison and value rows
    /// and the winner rows are rebuilt from the stored comparison,
    /// the range texts are re-derived, the transient status lines are
    /// re-resolved from their stored state, and the sensor-log texts
    /// are re-read from the log file. Called by the shell when the
    /// user picks a language in Settings; a page created after the
    /// switch is already built in the new language by the constructor.
    /// An unknown code falls back to English through the localizer,
    /// never to a blank. Measured data (trend values, forecast texts,
    /// verdict sentence, cell fallbacks) is untouched: it is rebuilt
    /// from the same trend and comparison, so it keeps its values.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        MachineSubtitleText = StringLocalizer.GetFormatted(AppStringKeys.HistorySubtitleTemplate, _languageCode, _displayedMachine ?? _current.MachineName);
        if (CurrentTrend is not null)
        {
            RebuildRows(CurrentTrend);
            RecomputeRangeTexts(CurrentTrend);
        }

        if (_comparison is not null)
        {
            RebuildComparisonRows(_comparison);
        }

        if (_lastValueResult is not null)
        {
            RebuildValueRows(_lastValueResult);
        }

        StatusMessage = ResolveStatusMessage();
        ComparisonStatusText = ResolveComparisonStatusText();
        ValueStatusText = ResolveValueStatusText();
        LoadSensorLog();

        OnPropertyChanged(nameof(HistoryEyebrowText));
        OnPropertyChanged(nameof(HistoryTitleText));
        OnPropertyChanged(nameof(HistoryIntroText));
        OnPropertyChanged(nameof(HistoryMachineTitleText));
        OnPropertyChanged(nameof(HistoryPickMachineText));
        OnPropertyChanged(nameof(HistoryMachineNoteText));
        OnPropertyChanged(nameof(HistoryBatteryAlertEyebrowText));
        OnPropertyChanged(nameof(HistoryScoreTitleText));
        OnPropertyChanged(nameof(HistoryBatteryTitleText));
        OnPropertyChanged(nameof(HistoryWearTitleText));
        OnPropertyChanged(nameof(HistoryForecastEyebrowText));
        OnPropertyChanged(nameof(HistoryCpuLogTitleText));
        OnPropertyChanged(nameof(HistoryRefreshChartText));
        OnPropertyChanged(nameof(HistoryChartCaptionText));
        OnPropertyChanged(nameof(HistoryDriverAgeEyebrowText));
        OnPropertyChanged(nameof(HistorySavedScansTitleText));
        OnPropertyChanged(nameof(HistoryExportCsvText));
        OnPropertyChanged(nameof(HistoryTableNoteText));
        OnPropertyChanged(nameof(HistoryHeaderScannedText));
        OnPropertyChanged(nameof(HistoryHeaderScoreText));
        OnPropertyChanged(nameof(HistoryHeaderCyclesText));
        OnPropertyChanged(nameof(HistoryHeaderBatteryText));
        OnPropertyChanged(nameof(HistoryHeaderWearText));
        OnPropertyChanged(nameof(HistoryHeaderPowerOnHoursText));
        OnPropertyChanged(nameof(HistoryCompareTitleText));
        OnPropertyChanged(nameof(HistoryCompareNoteText));
        OnPropertyChanged(nameof(HistoryMachineAText));
        OnPropertyChanged(nameof(HistoryMachineBText));
        OnPropertyChanged(nameof(HistoryCompareButtonText));
        OnPropertyChanged(nameof(HistoryValueTitleText));
        OnPropertyChanged(nameof(HistoryValueIntroText));
        OnPropertyChanged(nameof(HistoryAskingPriceLabelText));
        OnPropertyChanged(nameof(HistoryPriceExampleText));
        OnPropertyChanged(nameof(HistoryCompareValueButtonText));
        OnPropertyChanged(nameof(HistoryWinnersTitleText));
    }

    /// <summary>Records which status line is showing and renders it in the page language.</summary>
    private void SetStatus(HistoryStatusKind kind, string? error = null, string? scanDate = null, int remaining = 0)
    {
        _statusKind = kind;
        _statusError = error;
        _statusScanDate = scanDate;
        _statusRemaining = remaining;
        StatusMessage = ResolveStatusMessage();
    }

    /// <summary>Resolves the stored status kind to its sentence in the page language.</summary>
    private string ResolveStatusMessage() => _statusKind switch
    {
        HistoryStatusKind.Loading => StringLocalizer.Get(AppStringKeys.HistoryLoading, _languageCode),
        HistoryStatusKind.Unavailable => StringLocalizer.Get(AppStringKeys.HistoryUnavailable, _languageCode),
        HistoryStatusKind.NoScans => StringLocalizer.Get(AppStringKeys.HistoryNoScans, _languageCode),
        HistoryStatusKind.OneScan => StringLocalizer.Get(AppStringKeys.HistoryOneScan, _languageCode),
        HistoryStatusKind.ScansCount => StringLocalizer.GetFormatted(
            AppStringKeys.HistoryScansCount,
            _languageCode,
            (CurrentTrend?.SnapshotCount ?? _statusRemaining).ToString(CultureInfo.InvariantCulture)),
        HistoryStatusKind.ReadFailed => StringLocalizer.GetFormatted(AppStringKeys.HistoryReadFailed, _languageCode, _statusError ?? string.Empty),
        HistoryStatusKind.RemoveUnavailable => StringLocalizer.Get(AppStringKeys.HistoryRemoveUnavailable, _languageCode),
        HistoryStatusKind.RemoveConfirm => StringLocalizer.GetFormatted(AppStringKeys.HistoryRemoveConfirm, _languageCode, _statusScanDate ?? string.Empty),
        HistoryStatusKind.RemoveGone => StringLocalizer.Get(AppStringKeys.HistoryRemoveGone, _languageCode),
        HistoryStatusKind.RemovedNone => StringLocalizer.GetFormatted(AppStringKeys.HistoryRemovedNone, _languageCode, _statusScanDate ?? string.Empty),
        HistoryStatusKind.RemovedOne => StringLocalizer.GetFormatted(AppStringKeys.HistoryRemovedOne, _languageCode, _statusScanDate ?? string.Empty),
        HistoryStatusKind.RemovedMany => StringLocalizer.GetFormatted(AppStringKeys.HistoryRemovedMany, _languageCode, _statusScanDate ?? string.Empty, _statusRemaining),
        HistoryStatusKind.RemoveFailed => StringLocalizer.GetFormatted(AppStringKeys.HistoryRemoveFailed, _languageCode, _statusError ?? string.Empty),
        _ => string.Empty,
    };

    /// <summary>
    /// Resolves the comparison status line from the current state in
    /// the page language. The precedence mirrors the old
    /// UpdateCompareState: a failed load or comparison and the
    /// comparing state win first, then the one-machine and
    /// same-machine guards, then the compared sentence, then the
    /// pick-two prompts.
    /// </summary>
    private string ResolveComparisonStatusText()
    {
        if (_store is null)
        {
            return StringLocalizer.Get(AppStringKeys.HistoryCompareUnavailable, _languageCode);
        }

        if (_comparisonLoadError is not null)
        {
            return StringLocalizer.GetFormatted(AppStringKeys.HistoryReadFailed, _languageCode, _comparisonLoadError);
        }

        if (IsComparing)
        {
            return StringLocalizer.Get(AppStringKeys.HistoryComparing, _languageCode);
        }

        if (_compareFailedMessage is not null)
        {
            return StringLocalizer.GetFormatted(AppStringKeys.HistoryCompareFailed, _languageCode, _compareFailedMessage);
        }

        if (!IsLoading && MachineNames.Count < 2)
        {
            return StringLocalizer.Get(AppStringKeys.HistoryOnlyOneMachine, _languageCode);
        }

        if (CompareMachineA is not null && CompareMachineB is not null
            && string.Equals(CompareMachineA, CompareMachineB, StringComparison.OrdinalIgnoreCase))
        {
            return StringLocalizer.Get(AppStringKeys.HistoryPickDifferent, _languageCode);
        }

        if (HasMachineComparison && _comparedMachineA is not null && _comparedMachineB is not null)
        {
            return StringLocalizer.GetFormatted(AppStringKeys.HistoryCompared, _languageCode, _comparedMachineA, _comparedMachineB);
        }

        if (CompareMachineA is not null && CompareMachineB is not null)
        {
            return StringLocalizer.Get(AppStringKeys.HistoryPickTwoCompare, _languageCode);
        }

        if (!IsLoading && (CompareMachineA is null || CompareMachineB is null))
        {
            return StringLocalizer.Get(AppStringKeys.HistoryPickTwo, _languageCode);
        }

        return string.Empty;
    }

    /// <summary>Resolves the stored value status kind to its sentence in the page language.</summary>
    private string ResolveValueStatusText() => _valueKind switch
    {
        HistoryValueStatusKind.Prompt => StringLocalizer.Get(AppStringKeys.HistoryValuePrompt, _languageCode),
        HistoryValueStatusKind.CompareFirst => StringLocalizer.Get(AppStringKeys.HistoryValueCompareFirst, _languageCode),
        HistoryValueStatusKind.BadPrice => StringLocalizer.Get(AppStringKeys.HistoryValueBadPrice, _languageCode),
        HistoryValueStatusKind.HonestyNote => StringLocalizer.Get(AppStringKeys.HistoryValueHonestyNote, _languageCode),
        // Core-owned wording (ValueComparer): shown verbatim, never translated.
        HistoryValueStatusKind.NoVerdict => _valueNoVerdictReason ?? string.Empty,
        _ => string.Empty,
    };

    /// <summary>Rebuilds the saved-scans table rows from a trend, in the page language.</summary>
    private void RebuildRows(HistoryTrend trend)
    {
        Rows.Clear();
        for (var i = 0; i < trend.ScorePoints.Count; i++)
        {
            Rows.Add(new HistoryRowViewModel(
                trend.SnapshotIds[i],
                trend.ScorePoints[i].CapturedAt,
                trend.ScorePoints[i].Value,
                trend.BatteryHealthPoints[i].Value,
                trend.BatteryCyclePoints[i].Value,
                trend.StorageWearPoints[i].Value,
                trend.StoragePowerOnHoursPoints[i].Value,
                RemoveScanCommand,
                _languageCode));
        }
    }

    /// <summary>Re-derives the three range lines under the sparkline cards from a trend, in the page language.</summary>
    private void RecomputeRangeTexts(HistoryTrend trend)
    {
        ScoreRangeText = RangeText(StringLocalizer.Get(AppStringKeys.HistoryHeaderScore, _languageCode), trend.ScorePoints, value => value.ToString("0.#", CultureInfo.InvariantCulture));
        BatteryRangeText = RangeText(StringLocalizer.Get(AppStringKeys.HistoryBatteryTitle, _languageCode), trend.BatteryHealthPoints, value => string.Create(CultureInfo.InvariantCulture, $"{value:0.#}%"));
        WearRangeText = RangeText(StringLocalizer.Get(AppStringKeys.HistoryWearTitle, _languageCode), trend.StorageWearPoints, value => string.Create(CultureInfo.InvariantCulture, $"{value:0.#}%"));
    }

    /// <summary>Rebuilds the side-by-side comparison rows from a comparison, in the page language.</summary>
    private void RebuildComparisonRows(MachineComparison comparison)
    {
        ComparisonRows.Clear();
        ComparisonRows.Add(new MachineComparisonRowViewModel(StringLocalizer.Get(AppStringKeys.HistoryRowScans, _languageCode), ScanCountText(comparison.MachineA), ScanCountText(comparison.MachineB)));
        ComparisonRows.Add(new MachineComparisonRowViewModel(StringLocalizer.Get(AppStringKeys.HistoryRowFirstScan, _languageCode), DateText(comparison.MachineA.FirstScan), DateText(comparison.MachineB.FirstScan)));
        ComparisonRows.Add(new MachineComparisonRowViewModel(StringLocalizer.Get(AppStringKeys.HistoryRowLastScan, _languageCode), DateText(comparison.MachineA.LastScan), DateText(comparison.MachineB.LastScan)));
        ComparisonRows.Add(new MachineComparisonRowViewModel(StringLocalizer.Get(AppStringKeys.HistoryRowLatestScore, _languageCode), ScoreText(comparison.MachineA.LatestScore), ScoreText(comparison.MachineB.LatestScore)));
        ComparisonRows.Add(new MachineComparisonRowViewModel(StringLocalizer.Get(AppStringKeys.HistoryRowLatestBattery, _languageCode), PercentText(comparison.MachineA.LatestBatteryHealth), PercentText(comparison.MachineB.LatestBatteryHealth)));
        ComparisonRows.Add(new MachineComparisonRowViewModel(StringLocalizer.Get(AppStringKeys.HistoryRowLatestWear, _languageCode), PercentText(comparison.MachineA.LatestStorageWear), PercentText(comparison.MachineB.LatestStorageWear)));
        ComparisonRows.Add(new MachineComparisonRowViewModel(StringLocalizer.Get(AppStringKeys.HistoryRowLatestDriver, _languageCode), DateText(comparison.MachineA.LatestOldestDriverDate), DateText(comparison.MachineB.LatestOldestDriverDate)));
        ComparisonRows.Add(new MachineComparisonRowViewModel(StringLocalizer.Get(AppStringKeys.HistoryRowLastUpdate, _languageCode), DateText(comparison.MachineA.LastUpdateInstall), DateText(comparison.MachineB.LastUpdateInstall)));
    }

    /// <summary>Rebuilds the value rows and per-component winners from a value result, in the page language.</summary>
    private void RebuildValueRows(ValueComparisonResult result)
    {
        ValueRows.Clear();
        ValueRows.Add(new MachineComparisonRowViewModel(
            ValueComparer.AskingPriceLabel,
            PriceText(result.MachineA.AskingPrice, result.MachineA.CurrencySymbol),
            PriceText(result.MachineB.AskingPrice, result.MachineB.CurrencySymbol)));
        ValueRows.Add(new MachineComparisonRowViewModel(
            StringLocalizer.Get(AppStringKeys.HistoryValueScorePerDollar, _languageCode),
            PerCurrencyText(result.ScorePerCurrencyA),
            PerCurrencyText(result.ScorePerCurrencyB)));

        ValueComponentWinners.Clear();
        foreach (var winner in result.ComponentWinners)
        {
            ValueComponentWinners.Add(new ValueWinnerRowViewModel(winner, _languageCode));
        }
    }

    /// <summary>
    /// CPU temperature chart segments from the sensor log, oldest first.
    /// Positions are fractions of the measured time span (<see
    /// cref="SensorLogChartMapper"/>), so the chart's horizontal axis is
    /// time, not sample count; a segment breaks at a sensor gap and at a
    /// stretch where logging was not running.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<IReadOnlyList<SensorLogChartPoint>>? CpuTemperatureLogSegments { get; set; }

    /// <summary>The user-set CPU temperature threshold, drawn as a dashed line on the log chart.</summary>
    [ObservableProperty]
    public partial double? SensorLogThresholdC { get; set; }

    [ObservableProperty]
    public partial bool HasSensorLog { get; set; }

    [ObservableProperty]
    public partial string SensorLogSummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SensorLogStatusText { get; set; } = string.Empty;

    /// <summary>
    /// Applies new sensor-log settings live (the shell calls this when the
    /// user changes a setting) and re-reads the log.
    /// </summary>
    public void ApplySensorSettings(string? sensorLogPath, bool sensorLoggingEnabled, double? cpuTemperatureThresholdC)
    {
        _sensorLogPath = sensorLogPath;
        _sensorLoggingEnabled = sensorLoggingEnabled;
        _cpuTemperatureThresholdC = cpuTemperatureThresholdC;
        SensorLogThresholdC = cpuTemperatureThresholdC;
        LoadSensorLog();
    }

    /// <summary>Re-reads the sensor log from disk (the Refresh chart button).</summary>
    [RelayCommand]
    private void RefreshSensorLog() => LoadSensorLog();

    /// <summary>
    /// Reads the local sensor log for the CPU temperature chart. Never
    /// throws: a missing or unreadable log is an empty state, not an error.
    /// Runs on the calling thread; the file is capped at 10,000 rows, so
    /// the read is fast.
    /// </summary>
    public void LoadSensorLog()
    {
        HasSensorLog = false;
        CpuTemperatureLogSegments = null;
        SensorLogSummaryText = string.Empty;

        if (!_sensorLoggingEnabled)
        {
            SensorLogStatusText = StringLocalizer.Get(AppStringKeys.HistoryLogOff, _languageCode);
            return;
        }

        if (string.IsNullOrWhiteSpace(_sensorLogPath) || !File.Exists(_sensorLogPath))
        {
            SensorLogStatusText = StringLocalizer.Get(AppStringKeys.HistoryLogNoSamples, _languageCode);
            return;
        }

        try
        {
            var columns = SensorLogReader.ReadColumns(_sensorLogPath);
            var column = SensorLogReader.FindCpuTemperatureColumn(columns);
            if (column is null)
            {
                SensorLogStatusText = StringLocalizer.Get(AppStringKeys.HistoryLogNoCpuSensor, _languageCode);
                return;
            }

            var series = SensorLogReader.Read(_sensorLogPath, column);
            if (series.RowCount == 0)
            {
                SensorLogStatusText = StringLocalizer.Get(AppStringKeys.HistoryLogNoSamples, _languageCode);
                return;
            }

            var points = series.Points;
            if (points.Count > MaxLogChartPoints)
            {
                points = points.Skip(points.Count - MaxLogChartPoints).ToList();
            }

            // The chart cannot draw a line from fewer than two measured
            // points (SensorLogChart renders nothing then), so fewer than
            // two readings is an empty state, not a blank chart that looks
            // broken. Zero readings means the hardware never reported a CPU
            // temperature sensor during the whole log: say so plainly, and
            // that it is expected on machines whose sensor is not exposed.
            // Gaps are never filled or interpolated; a partial gap keeps
            // the existing gap rendering below.
            var measuredCount = points.Count(p => p.Value is not null);
            if (measuredCount == 0)
            {
                SensorLogStatusText = StringLocalizer.GetFormatted(AppStringKeys.HistoryLogNoReadings, _languageCode, series.RowCount);
                return;
            }

            if (measuredCount == 1)
            {
                SensorLogStatusText = StringLocalizer.GetFormatted(AppStringKeys.HistoryLogOneReading, _languageCode, series.RowCount);
                return;
            }

            // Time-proportional segments: the chart's horizontal axis is
            // time, so two samples an hour apart do not look adjacent.
            CpuTemperatureLogSegments = SensorLogChartMapper.BuildSegments(points);
            if (CpuTemperatureLogSegments.Count == 0)
            {
                // Every measured reading stands alone: each is separated
                // from the next by a sensor gap or a stretch where
                // logging was not running, so no line can be drawn. An
                // empty state, not a blank chart that looks broken.
                CpuTemperatureLogSegments = null;
                SensorLogStatusText = StringLocalizer.Get(AppStringKeys.HistoryLogTooFarApart, _languageCode);
                return;
            }

            HasSensorLog = true;
            // Sample timestamps are stored as UTC in sensor-log.csv; the
            // displayed range is local time.
            var first = series.FirstAt is { } firstAt
                ? SensorLogTime.FormatLocalTime(firstAt, _displayTimeZone)
                : "?";
            var last = series.LastAt is { } lastAt
                ? SensorLogTime.FormatLocalTime(lastAt, _displayTimeZone)
                : "?";
            var gapText = series.GapCount == 0
                ? StringLocalizer.Get(AppStringKeys.HistoryLogGapNone, _languageCode)
                : series.GapCount == 1
                    ? StringLocalizer.Get(AppStringKeys.HistoryLogGapOne, _languageCode)
                    : StringLocalizer.GetFormatted(AppStringKeys.HistoryLogGapMany, _languageCode, series.GapCount);
            SensorLogStatusText = StringLocalizer.GetFormatted(AppStringKeys.HistoryLogSummary, _languageCode, series.RowCount, first, last);
            SensorLogSummaryText = points.Count < series.Points.Count
                ? StringLocalizer.GetFormatted(AppStringKeys.HistoryLogSummaryNewest, _languageCode, points.Count, series.RowCount, gapText)
                : StringLocalizer.GetFormatted(AppStringKeys.HistoryLogSummaryAll, _languageCode, series.RowCount, gapText);
        }
        catch (Exception exception)
        {
            SensorLogStatusText = StringLocalizer.GetFormatted(AppStringKeys.HistoryLogReadFailed, _languageCode, exception.Message);
        }
    }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasHistory { get; set; }

    [ObservableProperty]
    public partial string? BatteryAlertText { get; set; }

    [ObservableProperty]
    public partial bool HasBatteryAlert { get; set; }

    /// <summary>
    /// U4 forecast card under the battery sparkline. Null (and the card
    /// hidden) when no saved scan reported battery health; otherwise the
    /// forecast text itself, including the not-enough-data wording.
    /// Core-owned wording: the ViewModel holds no second copy of the rule.
    /// </summary>
    [ObservableProperty]
    public partial string? BatteryForecastText { get; set; }

    [ObservableProperty]
    public partial bool HasBatteryForecast { get; set; }

    /// <summary>U4 forecast card under the storage-wear sparkline; same rule as <see cref="BatteryForecastText"/>.</summary>
    [ObservableProperty]
    public partial string? StorageWearForecastText { get; set; }

    [ObservableProperty]
    public partial bool HasStorageWearForecast { get; set; }

    /// <summary>Core-owned wording: the ViewModel holds no second copy of the rule.</summary>
    [ObservableProperty]
    public partial string? DriverAgeSummaryText { get; set; }

    [ObservableProperty]
    public partial bool HasDriverAgeSummary { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<double>? ScoreValues { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<double>? BatteryHealthValues { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<double>? StorageWearValues { get; set; }

    [ObservableProperty]
    public partial string ScoreRangeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BatteryRangeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WearRangeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MachineSubtitleText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedMachine { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsMachinePickerEnabled { get; set; }

    [ObservableProperty]
    public partial string? CompareMachineA { get; set; }

    [ObservableProperty]
    public partial string? CompareMachineB { get; set; }

    [ObservableProperty]
    public partial bool HasMachineComparison { get; set; }

    [ObservableProperty]
    public partial string ComparisonStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanCompareMachines { get; set; }

    [ObservableProperty]
    public partial bool IsComparing { get; set; }

    [ObservableProperty]
    public partial string MachineAName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MachineBName { get; set; } = string.Empty;

    /// <summary>
    /// U9: the asking price the buyer typed in for Machine A. Buyer input
    /// only, held in memory with this comparison: it is never written to a
    /// snapshot or the store, never scraped, never fetched, never assumed.
    /// </summary>
    [ObservableProperty]
    public partial string PriceAText { get; set; } = string.Empty;

    /// <summary>U9: the asking price the buyer typed in for Machine B. Same rule as <see cref="PriceAText"/>.</summary>
    [ObservableProperty]
    public partial string PriceBText { get; set; } = string.Empty;

    /// <summary>U9: true once a value comparison has run for the current machine pair.</summary>
    [ObservableProperty]
    public partial bool HasValueComparison { get; set; }

    /// <summary>U9: prompt, parse error, missing-data reason, or the honesty note under the verdict.</summary>
    [ObservableProperty]
    public partial string ValueStatusText { get; set; } = string.Empty;

    /// <summary>U9: the one-sentence value verdict, empty when there is none.</summary>
    [ObservableProperty]
    public partial string ValueVerdictText { get; set; } = string.Empty;

    /// <summary>U9: the Compare value button is enabled only while two machines are compared.</summary>
    [ObservableProperty]
    public partial bool CanCompareValue { get; set; }

    partial void OnSelectedMachineChanged(string? value)
    {
        if (_suppressSelectionReload || value is null)
        {
            return;
        }

        if (string.Equals(value, _displayedMachine, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = LoadTrendForMachineAsync(value);
    }

    partial void OnCompareMachineAChanged(string? value) => UpdateCompareState();

    partial void OnCompareMachineBChanged(string? value) => UpdateCompareState();

    partial void OnIsLoadingChanged(bool value) => UpdateCompareState();

    partial void OnIsComparingChanged(bool value)
    {
        UpdateCompareState();
        UpdateValueCompareState();
    }

    partial void OnHasMachineComparisonChanged(bool value) => UpdateValueCompareState();

    private void UpdateValueCompareState()
    {
        CanCompareValue = HasMachineComparison && !IsComparing;
        CompareValueCommand.NotifyCanExecuteChanged();
    }

    private void UpdateCompareState()
    {
        CanCompareMachines = !IsLoading && !IsComparing
            && MachineNames.Count >= 2
            && CompareMachineA is not null
            && CompareMachineB is not null
            && !string.Equals(CompareMachineA, CompareMachineB, StringComparison.OrdinalIgnoreCase);

        ComparisonStatusText = ResolveComparisonStatusText();
    }

    private async Task LoadAsync()
    {
        if (_store is null)
        {
            SetStatus(HistoryStatusKind.Unavailable);
            ComparisonStatusText = ResolveComparisonStatusText();
            return;
        }

        IsLoading = true;
        IsMachinePickerEnabled = false;
        try
        {
            var names = await _store.GetMachineNamesAsync();
            MachineNames.Clear();
            foreach (var name in names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                MachineNames.Add(name);
            }

            // The current machine stays selectable even before its first
            // saved scan, so its empty state is reachable from the picker.
            if (!MachineNames.Contains(_current.MachineName, StringComparer.OrdinalIgnoreCase))
            {
                var index = MachineNames
                    .TakeWhile(n => string.Compare(n, _current.MachineName, StringComparison.OrdinalIgnoreCase) < 0)
                    .Count();
                MachineNames.Insert(index, _current.MachineName);
            }

            _suppressSelectionReload = true;
            try
            {
                SelectedMachine = _current.MachineName;
                CompareMachineA = _current.MachineName;
                CompareMachineB = MachineNames.FirstOrDefault(n =>
                    !string.Equals(n, _current.MachineName, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _suppressSelectionReload = false;
            }

            UpdateCompareState();

            await LoadTrendForMachineAsync(_current.MachineName);
        }
        catch (Exception exception)
        {
            SetStatus(HistoryStatusKind.ReadFailed, error: exception.Message);
            _comparisonLoadError = exception.Message;
            ComparisonStatusText = ResolveComparisonStatusText();
        }
        finally
        {
            IsLoading = false;
            IsMachinePickerEnabled = MachineNames.Count > 0;
            UpdateCompareState();
        }

        // The sensor log does not depend on the snapshot store, so it loads
        // even when history itself failed.
        LoadSensorLog();
    }

    private async Task LoadTrendForMachineAsync(string machineName)
    {
        if (_store is null)
        {
            return;
        }

        var generation = ++_loadGeneration;
        IsLoading = true;
        IsMachinePickerEnabled = false;
        _displayedMachine = machineName;
        MachineSubtitleText = StringLocalizer.GetFormatted(AppStringKeys.HistorySubtitleTemplate, _languageCode, machineName);
        SetStatus(HistoryStatusKind.Loading);
        CurrentTrend = null;
        HasHistory = false;
        HasBatteryAlert = false;
        BatteryAlertText = null;
        HasBatteryForecast = false;
        BatteryForecastText = null;
        HasStorageWearForecast = false;
        StorageWearForecastText = null;
        HasDriverAgeSummary = false;
        DriverAgeSummaryText = null;
        SummaryText = string.Empty;
        ScoreValues = null;
        BatteryHealthValues = null;
        StorageWearValues = null;
        ScoreRangeText = string.Empty;
        BatteryRangeText = string.Empty;
        WearRangeText = string.Empty;
        Rows.Clear();

        try
        {
            var trend = await HistoryTrendAnalyzer.LoadAsync(_store, machineName, 50);
            if (generation != _loadGeneration)
            {
                return;
            }

            CurrentTrend = trend;
            if (trend.SnapshotCount == 0)
            {
                SetStatus(HistoryStatusKind.NoScans);
                return;
            }

            SummaryText = trend.Summary;
            BatteryAlertText = trend.BatteryAlert;
            HasBatteryAlert = trend.BatteryAlert is not null;
            BatteryForecastText = ForecastText(trend.BatteryHealthPoints, DegradationForecaster.ForBattery);
            HasBatteryForecast = BatteryForecastText is not null;
            StorageWearForecastText = ForecastText(trend.StorageWearPoints, DegradationForecaster.ForStorageWear);
            HasStorageWearForecast = StorageWearForecastText is not null;
            DriverAgeSummaryText = trend.DriverAgeSummary;
            HasDriverAgeSummary = trend.DriverAgeSummary is not null;
            ScoreValues = MeasuredValues(trend.ScorePoints);
            BatteryHealthValues = MeasuredValues(trend.BatteryHealthPoints);
            StorageWearValues = MeasuredValues(trend.StorageWearPoints);
            RecomputeRangeTexts(trend);

            RebuildRows(trend);

            HasHistory = true;
            SetStatus(
                trend.SnapshotCount < 2 ? HistoryStatusKind.OneScan : HistoryStatusKind.ScansCount,
                remaining: trend.SnapshotCount);
        }
        catch (Exception exception)
        {
            if (generation == _loadGeneration)
            {
                SetStatus(HistoryStatusKind.ReadFailed, error: exception.Message);
            }
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsLoading = false;
                IsMachinePickerEnabled = true;
                UpdateCompareState();
            }
        }
    }

    /// <summary>
    /// Deletes one saved scan from the History table. Two clicks by
    /// design: the app has no modal confirm dialog, so the first click
    /// arms the row (the button reads "Confirm remove" and the status
    /// line names the scan date) and only the second click deletes.
    /// The store deletes by the row's snapshot id, so the delete can
    /// never touch another scan of the same machine; the status line
    /// states the store's own deleted-row count, including 0 when the
    /// scan was already gone. The trend reloads afterwards, so the
    /// table, the chart and the CSV export all agree on what remains.
    /// Deleting touches only TrueSpec's own history file, never the
    /// machine itself.
    /// </summary>
    private async Task RemoveScanAsync(HistoryRowViewModel? rowViewModel)
    {
        if (rowViewModel is null || _removing)
        {
            return;
        }

        if (_store is null)
        {
            SetStatus(HistoryStatusKind.RemoveUnavailable);
            return;
        }

        var scanDate = rowViewModel.CapturedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (!rowViewModel.IsRemoveArmed)
        {
            rowViewModel.IsRemoveArmed = true;
            SetStatus(HistoryStatusKind.RemoveConfirm, scanDate: scanDate);
            return;
        }

        _removing = true;
        try
        {
            var removed = await _store.DeleteSnapshotAsync(rowViewModel.Id);
            await LoadTrendForMachineAsync(_displayedMachine ?? _current.MachineName);
            if (_statusKind != HistoryStatusKind.ReadFailed)
            {
                if (removed == 0)
                {
                    SetStatus(HistoryStatusKind.RemoveGone);
                }
                else if (CurrentTrend is null || CurrentTrend.SnapshotCount == 0)
                {
                    SetStatus(HistoryStatusKind.RemovedNone, scanDate: scanDate);
                }
                else if (CurrentTrend.SnapshotCount == 1)
                {
                    SetStatus(HistoryStatusKind.RemovedOne, scanDate: scanDate);
                }
                else
                {
                    SetStatus(HistoryStatusKind.RemovedMany, scanDate: scanDate, remaining: CurrentTrend.SnapshotCount);
                }
            }
        }
        catch (Exception exception)
        {
            SetStatus(HistoryStatusKind.RemoveFailed, error: exception.Message);
        }
        finally
        {
            _removing = false;
        }
    }

    [RelayCommand]
    private async Task CompareMachinesAsync()
    {
        if (_store is null)
        {
            ComparisonStatusText = ResolveComparisonStatusText();
            HasMachineComparison = false;
            return;
        }

        if (CompareMachineA is null || CompareMachineB is null)
        {
            _compareFailedMessage = null;
            _comparedMachineA = null;
            _comparedMachineB = null;
            HasMachineComparison = false;
            ComparisonStatusText = StringLocalizer.Get(AppStringKeys.HistoryPickTwo, _languageCode);
            return;
        }

        if (string.Equals(CompareMachineA, CompareMachineB, StringComparison.OrdinalIgnoreCase))
        {
            _compareFailedMessage = null;
            _comparedMachineA = null;
            _comparedMachineB = null;
            HasMachineComparison = false;
            ComparisonStatusText = StringLocalizer.Get(AppStringKeys.HistoryPickDifferent, _languageCode);
            return;
        }

        IsComparing = true;
        ComparisonStatusText = ResolveComparisonStatusText();
        ResetValueComparison();
        try
        {
            var snapshotsA = await LoadSnapshotsForMachineAsync(CompareMachineA);
            var snapshotsB = await LoadSnapshotsForMachineAsync(CompareMachineB);
            var comparison = MachineHistoryComparer.Compare(snapshotsA, snapshotsB);

            // U9 keeps the raw comparison and the two latest snapshots so
            // the value layer can sit on top without re-reading history.
            // The raw comparison itself stays verdict-free.
            _comparison = comparison;
            _latestSnapshotA = LatestSnapshot(snapshotsA);
            _latestSnapshotB = LatestSnapshot(snapshotsB);

            MachineAName = comparison.MachineA.MachineName;
            MachineBName = comparison.MachineB.MachineName;
            RebuildComparisonRows(comparison);

            _comparedMachineA = comparison.MachineA.MachineName;
            _comparedMachineB = comparison.MachineB.MachineName;
            _compareFailedMessage = null;
            HasMachineComparison = true;
            ComparisonStatusText = ResolveComparisonStatusText();
        }
        catch (Exception exception)
        {
            _compareFailedMessage = exception.Message;
            _comparedMachineA = null;
            _comparedMachineB = null;
            ComparisonStatusText = ResolveComparisonStatusText();
            HasMachineComparison = false;
        }
        finally
        {
            IsComparing = false;
        }
    }

    /// <summary>
    /// U9 value comparison, on top of the raw two-machine comparison: the
    /// buyer types each seller's asking price, and TrueSpec shows the
    /// measured health score per unit of currency, the per-component winner,
    /// and one verdict sentence. The prices live only in this ViewModel for
    /// this comparison: they are never written to a snapshot or the store.
    /// The verdict sentence itself is Core-owned wording
    /// (<see cref="ValueComparer"/>); this method holds no second copy.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCompareValue))]
    private void CompareValue()
    {
        if (_comparison is null)
        {
            _valueKind = HistoryValueStatusKind.CompareFirst;
            _valueNoVerdictReason = null;
            ValueStatusText = ResolveValueStatusText();
            HasValueComparison = false;
            return;
        }

        if (!TryParseAskingPrice(PriceAText, out var priceA) || !TryParseAskingPrice(PriceBText, out var priceB))
        {
            _valueKind = HistoryValueStatusKind.BadPrice;
            _valueNoVerdictReason = null;
            ValueStatusText = ResolveValueStatusText();
            ValueVerdictText = string.Empty;
            ValueRows.Clear();
            ValueComponentWinners.Clear();
            HasValueComparison = false;
            return;
        }

        var result = ValueComparer.Compare(_comparison, priceA, priceB, "$", _latestSnapshotA, _latestSnapshotB);
        _lastValueResult = result;
        RebuildValueRows(result);

        ValueVerdictText = result.VerdictSentence ?? string.Empty;
        if (result.VerdictSentence is null)
        {
            _valueKind = HistoryValueStatusKind.NoVerdict;
            _valueNoVerdictReason = result.NoVerdictReason;
        }
        else
        {
            _valueKind = HistoryValueStatusKind.HonestyNote;
            _valueNoVerdictReason = null;
        }

        ValueStatusText = ResolveValueStatusText();
        HasValueComparison = true;
    }

    /// <summary>
    /// U9: clears the value state when the machine pair changes, so a price
    /// typed for one pair is never silently applied to another pair.
    /// </summary>
    private void ResetValueComparison()
    {
        _comparison = null;
        _latestSnapshotA = null;
        _latestSnapshotB = null;
        _lastValueResult = null;
        _comparedMachineA = null;
        _comparedMachineB = null;
        _compareFailedMessage = null;
        PriceAText = string.Empty;
        PriceBText = string.Empty;
        ValueVerdictText = string.Empty;
        _valueKind = HistoryValueStatusKind.Prompt;
        _valueNoVerdictReason = null;
        ValueStatusText = ResolveValueStatusText();
        ValueRows.Clear();
        ValueComponentWinners.Clear();
        HasValueComparison = false;
        UpdateValueCompareState();
    }

    private static SystemSnapshot? LatestSnapshot(IReadOnlyList<SystemSnapshot> snapshots) =>
        snapshots.Count == 0 ? null : snapshots.OrderBy(s => s.CapturedAt).Last();

    /// <summary>
    /// U9: buyer-typed price parsing. Plain numbers; a leading currency
    /// symbol and thousands separators are tolerated, anything else is not
    /// a price. Zero and negative prices are not prices: they mean "no
    /// price entered", never a free or paid-to-take machine.
    /// </summary>
    private static bool TryParseAskingPrice(string? text, out decimal price)
    {
        price = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = text.Trim().TrimStart('$', '€', '£').Trim();
        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out price)
            && !decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.CurrentCulture, out price))
        {
            return false;
        }

        return price > 0m;
    }

    private static string PriceText(decimal? price, string currencySymbol) =>
        price is null
            ? "Not entered"
            : string.Create(CultureInfo.InvariantCulture, $"{currencySymbol}{price.Value:0.##}");

    private static string PerCurrencyText(double? value) =>
        value is null ? "Not measurable" : value.Value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Loads one machine's snapshots the same way
    /// <see cref="HistoryTrendAnalyzer.LoadAsync"/> does: history summaries,
    /// then each snapshot, skipping unreadable ones and any snapshot from a
    /// different machine. No new store method.
    /// </summary>
    private async Task<IReadOnlyList<SystemSnapshot>> LoadSnapshotsForMachineAsync(string machineName)
    {
        var summaries = await _store!.GetHistoryAsync(50);
        var snapshots = new List<SystemSnapshot>();
        foreach (var summary in summaries)
        {
            var snapshot = await _store.GetSnapshotAsync(summary.Id);
            if (snapshot is null)
            {
                continue;
            }

            if (!string.Equals(snapshot.MachineName, machineName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            snapshots.Add(snapshot);
        }

        return snapshots;
    }

    private static string ScanCountText(MachineSideSummary summary) =>
        summary.ScanCount == 0
            ? "Not reported"
            : summary.ScanCount.ToString(CultureInfo.InvariantCulture);

    private static string DateText(DateTimeOffset? value) =>
        value is null ? "Not reported" : value.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string ScoreText(double? value) =>
        value is null ? "Not reported" : value.Value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string PercentText(double? value) =>
        value is null ? "Not reported" : string.Create(CultureInfo.InvariantCulture, $"{value.Value:0.#}%");

    /// <summary>
    /// The U4 forecast card text for one metric, or null when no saved scan
    /// reported that metric at all: the card is hidden entirely then, never
    /// shown empty. Otherwise the forecast's own wording, including the
    /// not-enough-data and no-decline cases.
    /// </summary>
    private static string? ForecastText(
        IReadOnlyList<TrendPoint> points,
        Func<IReadOnlyList<TrendPoint>, DegradationForecast> forecast)
    {
        if (!points.Any(p => p.Value is not null))
        {
            return null;
        }

        return forecast(points).BasisText;
    }

    private static IReadOnlyList<double>? MeasuredValues(IReadOnlyList<TrendPoint> points)
    {
        var values = points
            .Where(p => p.Value is not null)
            .Select(p => p.Value!.Value)
            .ToList();
        return values.Count < 2 ? null : values;
    }

    private string RangeText(string label, IReadOnlyList<TrendPoint> points, Func<double, string> format)
    {
        var measured = points.Where(p => p.Value is not null).Select(p => p.Value!.Value).ToList();
        if (measured.Count == 0)
        {
            return StringLocalizer.GetFormatted(AppStringKeys.HistoryRangeNone, _languageCode, label);
        }

        if (measured.Count == 1)
        {
            return StringLocalizer.GetFormatted(AppStringKeys.HistoryRangeOne, _languageCode, label, format(measured[0]));
        }

        return StringLocalizer.GetFormatted(
            AppStringKeys.HistoryRangeMany,
            _languageCode,
            label,
            format(measured[0]),
            format(measured[^1]),
            measured.Count);
    }
}

/// <summary>
/// One row of the History table, pre-formatted for the view. The text
/// matches the history CSV export exactly, so the file and the table
/// agree. The row also carries its snapshot id and the remove command,
/// so the Remove button on the row can delete exactly that one scan:
/// the id comes from the trend's <c>SnapshotIds</c>, in the same order
/// as the points, so the row and its scan can never drift apart.
/// </summary>
public sealed partial class HistoryRowViewModel : ViewModelBase
{
    private string _languageCode;

    public HistoryRowViewModel(
        Guid id,
        DateTimeOffset capturedAt,
        double? score,
        double? batteryHealth,
        double? batteryCycles,
        double? storageWear,
        double? storagePowerOnHours,
        ICommand? removeScanCommand = null,
        string? languageCode = null)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        Id = id;
        RemoveScanCommand = removeScanCommand;
        CapturedAt = capturedAt;
        ScoreText = score is null ? "Not measurable" : score.Value.ToString("0.#", CultureInfo.InvariantCulture);
        BatteryHealthText = batteryHealth is null
            ? "Not reported"
            : string.Create(CultureInfo.InvariantCulture, $"{batteryHealth.Value:0.#}%");
        BatteryCyclesText = batteryCycles is null
            ? "Not reported"
            : batteryCycles.Value.ToString("0", CultureInfo.InvariantCulture);
        StorageWearText = storageWear is null
            ? "Not reported"
            : string.Create(CultureInfo.InvariantCulture, $"{storageWear.Value:0.#}%");
        StorageHoursText = storagePowerOnHours is null
            ? "Not reported"
            : storagePowerOnHours.Value.ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>The id of the saved snapshot this row shows. The remove command deletes this id, and no other scan.</summary>
    public Guid Id { get; }

    /// <summary>The per-scan remove command, supplied by the History page. Null when no caller supplied one.</summary>
    public ICommand? RemoveScanCommand { get; }

    public DateTimeOffset CapturedAt { get; }

    public string ScoreText { get; }

    public string BatteryHealthText { get; }

    public string BatteryCyclesText { get; }

    public string StorageWearText { get; }

    public string StorageHoursText { get; }

    /// <summary>
    /// True after the first click on Remove: the button reads "Confirm
    /// remove" until the second click deletes the scan. The app has no
    /// modal confirm dialog, so the two-step button IS the
    /// confirmation — a single misclick can never delete a scan. The
    /// flag resets whenever the trend reloads, which rebuilds the rows.
    /// </summary>
    [ObservableProperty]
    public partial bool IsRemoveArmed { get; set; }

    /// <summary>"Remove" normally, "Confirm remove" once armed, in the row's language.</summary>
    public string RemoveButtonText => IsRemoveArmed
        ? StringLocalizer.Get(AppStringKeys.HistoryConfirmRemoveButton, _languageCode)
        : StringLocalizer.Get(AppStringKeys.HistoryRemoveButton, _languageCode);

    /// <summary>Re-resolves the remove button text in the given language, in place.</summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        OnPropertyChanged(nameof(RemoveButtonText));
    }

    partial void OnIsRemoveArmedChanged(bool value) => OnPropertyChanged(nameof(RemoveButtonText));
}

/// <summary>One facts-only row of the two-machine side-by-side table. No verdict.</summary>
public sealed class MachineComparisonRowViewModel
{
    public MachineComparisonRowViewModel(string label, string machineAText, string machineBText)
    {
        Label = label;
        MachineAText = machineAText;
        MachineBText = machineBText;
    }

    public string Label { get; }

    public string MachineAText { get; }

    public string MachineBText { get; }
}

/// <summary>
/// U9: one per-component winner row. The winner is decided on measured
/// health only; the text names the health on both sides so the buyer can
/// see why, and a component measurable on only one side says so instead of
/// crowning a winner on missing data.
/// </summary>
public sealed class ValueWinnerRowViewModel : ViewModelBase
{
    private readonly ComponentWinner _winner;
    private readonly double? _healthA;
    private readonly double? _healthB;
    private string _languageCode;

    public ValueWinnerRowViewModel(ComponentValueWinner winner, string? languageCode = null)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        _winner = winner.Winner;
        _healthA = winner.HealthA;
        _healthB = winner.HealthB;
        ComponentName = winner.ComponentName;
    }

    public string ComponentName { get; }

    /// <summary>The winner sentence, in the row's language. The health figures inside stay as measured.</summary>
    public string WinnerText => _winner switch
    {
        ComponentWinner.MachineA => StringLocalizer.GetFormatted(AppStringKeys.HistoryWinnerA, _languageCode, HealthText(_healthA), HealthText(_healthB)),
        ComponentWinner.MachineB => StringLocalizer.GetFormatted(AppStringKeys.HistoryWinnerB, _languageCode, HealthText(_healthA), HealthText(_healthB)),
        ComponentWinner.Tie => StringLocalizer.GetFormatted(AppStringKeys.HistoryWinnerTie, _languageCode, HealthText(_healthA), HealthText(_healthB)),
        _ => StringLocalizer.Get(AppStringKeys.HistoryWinnerNotMeasurable, _languageCode),
    };

    /// <summary>Re-resolves the winner sentence in the given language, in place.</summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        OnPropertyChanged(nameof(WinnerText));
    }

    private static string HealthText(double? health) =>
        health is null ? "Not measurable" : string.Create(CultureInfo.InvariantCulture, $"{health.Value:0.#}%");
}
