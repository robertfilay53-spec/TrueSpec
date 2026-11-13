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
    private string? _sensorLogPath;
    private bool _sensorLoggingEnabled;
    private double? _cpuTemperatureThresholdC;
    private readonly TimeZoneInfo _displayTimeZone;

    /// <summary>U9: the raw (verdict-free) comparison the value layer sits on top of. Null until two machines are compared.</summary>
    private MachineComparison? _comparison;

    /// <summary>U9: each machine's latest snapshot, for the per-component winners only. Never written to.</summary>
    private SystemSnapshot? _latestSnapshotA;

    /// <summary>U9: each machine's latest snapshot, for the per-component winners only. Never written to.</summary>
    private SystemSnapshot? _latestSnapshotB;

    public HistoryViewModel(ISnapshotStore? store, SystemSnapshot current, ICommand? exportHistoryCsvCommand = null, string? sensorLogPath = null, bool sensorLoggingEnabled = true, double? cpuTemperatureThresholdC = null, TimeZoneInfo? displayTimeZone = null)
    {
        _store = store;
        _current = current;
        ExportHistoryCsvCommand = exportHistoryCsvCommand;
        _sensorLogPath = sensorLogPath;
        _sensorLoggingEnabled = sensorLoggingEnabled;
        _cpuTemperatureThresholdC = cpuTemperatureThresholdC;
        _displayTimeZone = displayTimeZone ?? TimeZoneInfo.Local;
        SensorLogThresholdC = cpuTemperatureThresholdC;
        MachineSubtitleText = $"How {current.MachineName} changes over time";
        _ = LoadAsync();
    }

    /// <summary>
    /// Supplied by MainViewModel (mirrors how ReportViewModel receives the
    /// export commands): writes the currently shown trend to the configured
    /// export folder via HistoryCsvExporter. Null when no caller supplied one.
    /// </summary>
    public ICommand? ExportHistoryCsvCommand { get; }

    /// <summary>The trend currently shown on the page, for the CSV export command.</summary>
    public HistoryTrend? CurrentTrend { get; private set; }

    public ObservableCollection<HistoryRowViewModel> Rows { get; } = new();

    public ObservableCollection<string> MachineNames { get; } = new();

    public ObservableCollection<MachineComparisonRowViewModel> ComparisonRows { get; } = new();

    /// <summary>U9 value rows (asking price, score per $1) for the two compared machines.</summary>
    public ObservableCollection<MachineComparisonRowViewModel> ValueRows { get; } = new();

    /// <summary>U9 per-component winners for the two compared machines.</summary>
    public ObservableCollection<ValueWinnerRowViewModel> ValueComponentWinners { get; } = new();

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
    public partial string SensorLogStatusText { get; set; } = "Loading sensor log...";

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
            SensorLogStatusText = "Sensor logging is off. Turn it on in Settings and the CPU temperature chart will appear here.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_sensorLogPath) || !File.Exists(_sensorLogPath))
        {
            SensorLogStatusText = "No sensor samples recorded yet. Leave TrueSpec running and the CPU temperature chart will appear here.";
            return;
        }

        try
        {
            var columns = SensorLogReader.ReadColumns(_sensorLogPath);
            var column = SensorLogReader.FindCpuTemperatureColumn(columns);
            if (column is null)
            {
                SensorLogStatusText = "This machine's log has no CPU temperature sensor, so there is nothing to chart.";
                return;
            }

            var series = SensorLogReader.Read(_sensorLogPath, column);
            if (series.RowCount == 0)
            {
                SensorLogStatusText = "No sensor samples recorded yet. Leave TrueSpec running and the CPU temperature chart will appear here.";
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
                SensorLogStatusText = $"No temperature readings were recorded in {series.RowCount} samples: this machine's hardware does not report a CPU temperature sensor. This is expected on machines whose sensor is not exposed, so there is no CPU temperature chart to show.";
                return;
            }

            if (measuredCount == 1)
            {
                SensorLogStatusText = $"Only one temperature reading was recorded in {series.RowCount} samples, so there is not enough data to draw a chart.";
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
                SensorLogStatusText = "The temperature readings are too far apart in time to draw a line: each reading stands alone because logging was not running between them.";
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
                ? "no gaps"
                : series.GapCount == 1
                    ? "1 gap where the sensor did not report"
                    : $"{series.GapCount} gaps where the sensor did not report";
            SensorLogStatusText = $"CPU temperature, {series.RowCount} samples from {first} to {last}.";
            SensorLogSummaryText = points.Count < series.Points.Count
                ? $"Showing the newest {points.Count} of {series.RowCount} samples, {gapText}."
                : $"{series.RowCount} samples, {gapText}.";
        }
        catch (Exception exception)
        {
            SensorLogStatusText = $"Sensor log could not be read: {exception.Message}";
        }
    }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Loading snapshot history...";

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
    public partial string ValueStatusText { get; set; } = "Enter each seller's asking price to compare value.";

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

        if (!IsLoading && MachineNames.Count < 2)
        {
            ComparisonStatusText = "Only one machine is in history so far. Run a scan on a second machine and its history will appear here for comparison.";
        }
        else if (!IsLoading && CompareMachineA is not null && CompareMachineB is not null
            && string.Equals(CompareMachineA, CompareMachineB, StringComparison.OrdinalIgnoreCase))
        {
            ComparisonStatusText = "Pick two different machines.";
        }
    }

    private async Task LoadAsync()
    {
        if (_store is null)
        {
            StatusMessage = "Snapshot history is unavailable on this machine, so there is nothing to trend yet.";
            ComparisonStatusText = "Snapshot history is unavailable on this machine, so there is nothing to compare yet.";
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
            if (CompareMachineA is not null && CompareMachineB is not null)
            {
                ComparisonStatusText = "Pick two machines and compare. Different machines are shown side by side, never mixed in one trend.";
            }

            await LoadTrendForMachineAsync(_current.MachineName);
        }
        catch (Exception exception)
        {
            StatusMessage = $"History could not be read: {exception.Message}";
            ComparisonStatusText = $"History could not be read: {exception.Message}";
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
        MachineSubtitleText = $"How {machineName} changes over time";
        StatusMessage = "Loading snapshot history...";
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
                StatusMessage = "No saved scans yet. Run a scan (refresh) and come back: every scan is saved to local history.";
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
            ScoreRangeText = RangeText("Score", trend.ScorePoints, value => value.ToString("0.#", CultureInfo.InvariantCulture));
            BatteryRangeText = RangeText("Battery health", trend.BatteryHealthPoints, value => string.Create(CultureInfo.InvariantCulture, $"{value:0.#}%"));
            WearRangeText = RangeText("Storage wear", trend.StorageWearPoints, value => string.Create(CultureInfo.InvariantCulture, $"{value:0.#}%"));

            Rows.Clear();
            for (var i = 0; i < trend.ScorePoints.Count; i++)
            {
                Rows.Add(new HistoryRowViewModel(
                    trend.ScorePoints[i].CapturedAt,
                    trend.ScorePoints[i].Value,
                    trend.BatteryHealthPoints[i].Value,
                    trend.BatteryCyclePoints[i].Value,
                    trend.StorageWearPoints[i].Value,
                    trend.StoragePowerOnHoursPoints[i].Value));
            }

            HasHistory = true;
            StatusMessage = trend.SnapshotCount < 2
                ? "One scan saved so far. Run another scan (refresh) and come back for the trend."
                : $"{trend.SnapshotCount} scans in history, oldest first.";
        }
        catch (Exception exception)
        {
            if (generation == _loadGeneration)
            {
                StatusMessage = $"History could not be read: {exception.Message}";
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

    [RelayCommand]
    private async Task CompareMachinesAsync()
    {
        if (_store is null)
        {
            ComparisonStatusText = "Snapshot history is unavailable on this machine, so there is nothing to compare yet.";
            HasMachineComparison = false;
            return;
        }

        if (CompareMachineA is null || CompareMachineB is null)
        {
            ComparisonStatusText = "Pick two machines to compare.";
            HasMachineComparison = false;
            return;
        }

        if (string.Equals(CompareMachineA, CompareMachineB, StringComparison.OrdinalIgnoreCase))
        {
            ComparisonStatusText = "Pick two different machines.";
            HasMachineComparison = false;
            return;
        }

        IsComparing = true;
        ComparisonStatusText = "Comparing machines...";
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
            ComparisonRows.Clear();
            ComparisonRows.Add(new MachineComparisonRowViewModel("Scans in history", ScanCountText(comparison.MachineA), ScanCountText(comparison.MachineB)));
            ComparisonRows.Add(new MachineComparisonRowViewModel("First scan", DateText(comparison.MachineA.FirstScan), DateText(comparison.MachineB.FirstScan)));
            ComparisonRows.Add(new MachineComparisonRowViewModel("Last scan", DateText(comparison.MachineA.LastScan), DateText(comparison.MachineB.LastScan)));
            ComparisonRows.Add(new MachineComparisonRowViewModel("Latest score", ScoreText(comparison.MachineA.LatestScore), ScoreText(comparison.MachineB.LatestScore)));
            ComparisonRows.Add(new MachineComparisonRowViewModel("Latest battery health", PercentText(comparison.MachineA.LatestBatteryHealth), PercentText(comparison.MachineB.LatestBatteryHealth)));
            ComparisonRows.Add(new MachineComparisonRowViewModel("Latest storage wear", PercentText(comparison.MachineA.LatestStorageWear), PercentText(comparison.MachineB.LatestStorageWear)));
            ComparisonRows.Add(new MachineComparisonRowViewModel("Latest oldest driver date", DateText(comparison.MachineA.LatestOldestDriverDate), DateText(comparison.MachineB.LatestOldestDriverDate)));
            ComparisonRows.Add(new MachineComparisonRowViewModel("Last update install", DateText(comparison.MachineA.LastUpdateInstall), DateText(comparison.MachineB.LastUpdateInstall)));

            HasMachineComparison = true;
            ComparisonStatusText = $"Compared {comparison.MachineA.MachineName} with {comparison.MachineB.MachineName}. Different machines are shown side by side, never mixed in one trend.";
        }
        catch (Exception exception)
        {
            ComparisonStatusText = $"Comparison failed: {exception.Message}";
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
            ValueStatusText = "Compare two machines first, then enter asking prices.";
            HasValueComparison = false;
            return;
        }

        if (!TryParseAskingPrice(PriceAText, out var priceA) || !TryParseAskingPrice(PriceBText, out var priceB))
        {
            ValueStatusText = "Enter an asking price above zero for each machine (numbers only, for example 500). The price is yours, not measured.";
            ValueVerdictText = string.Empty;
            ValueRows.Clear();
            ValueComponentWinners.Clear();
            HasValueComparison = false;
            return;
        }

        var result = ValueComparer.Compare(_comparison, priceA, priceB, "$", _latestSnapshotA, _latestSnapshotB);

        ValueRows.Clear();
        ValueRows.Add(new MachineComparisonRowViewModel(
            ValueComparer.AskingPriceLabel,
            PriceText(result.MachineA.AskingPrice, result.MachineA.CurrencySymbol),
            PriceText(result.MachineB.AskingPrice, result.MachineB.CurrencySymbol)));
        ValueRows.Add(new MachineComparisonRowViewModel(
            "Score per $1 (ESTIMATE)",
            PerCurrencyText(result.ScorePerCurrencyA),
            PerCurrencyText(result.ScorePerCurrencyB)));

        ValueComponentWinners.Clear();
        foreach (var winner in result.ComponentWinners)
        {
            ValueComponentWinners.Add(new ValueWinnerRowViewModel(winner));
        }

        ValueVerdictText = result.VerdictSentence ?? string.Empty;
        ValueStatusText = result.VerdictSentence is null
            ? result.NoVerdictReason ?? string.Empty
            : "Prices are yours, not measured. The per-dollar figure is arithmetic on the measured score and the price you entered, labelled ESTIMATE. No market-price data is used.";
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
        PriceAText = string.Empty;
        PriceBText = string.Empty;
        ValueVerdictText = string.Empty;
        ValueStatusText = "Enter each seller's asking price to compare value.";
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

    private static string RangeText(string label, IReadOnlyList<TrendPoint> points, Func<double, string> format)
    {
        var measured = points.Where(p => p.Value is not null).Select(p => p.Value!.Value).ToList();
        if (measured.Count == 0)
        {
            return $"{label}: not reported in any saved scan.";
        }

        if (measured.Count == 1)
        {
            return $"{label}: {format(measured[0])} (one scan only).";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{label}: {format(measured[0])} to {format(measured[^1])} over {measured.Count} scans.");
    }
}

public sealed class HistoryRowViewModel
{
    public HistoryRowViewModel(
        DateTimeOffset capturedAt,
        double? score,
        double? batteryHealth,
        double? batteryCycles,
        double? storageWear,
        double? storagePowerOnHours)
    {
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

    public DateTimeOffset CapturedAt { get; }

    public string ScoreText { get; }

    public string BatteryHealthText { get; }

    public string BatteryCyclesText { get; }

    public string StorageWearText { get; }

    public string StorageHoursText { get; }
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
public sealed class ValueWinnerRowViewModel
{
    public ValueWinnerRowViewModel(ComponentValueWinner winner)
    {
        ComponentName = winner.ComponentName;
        WinnerText = winner.Winner switch
        {
            ComponentWinner.MachineA => $"Machine A wins ({HealthText(winner.HealthA)} vs {HealthText(winner.HealthB)})",
            ComponentWinner.MachineB => $"Machine B wins ({HealthText(winner.HealthA)} vs {HealthText(winner.HealthB)})",
            ComponentWinner.Tie => $"Tie ({HealthText(winner.HealthA)} vs {HealthText(winner.HealthB)})",
            _ => "Not measurable on one or both machines",
        };
    }

    public string ComponentName { get; }

    public string WinnerText { get; }

    private static string HealthText(double? health) =>
        health is null ? "Not measurable" : string.Create(CultureInfo.InvariantCulture, $"{health.Value:0.#}%");
}

