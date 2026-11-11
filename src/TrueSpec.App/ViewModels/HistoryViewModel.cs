using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Storage;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// History page: score, battery-health and storage-wear trends across the
/// saved scans in the local SQLite store. Every point comes from a real
/// saved snapshot; a value a scan did not report is a gap in the line and
/// in the table, never a zero.
/// </summary>
public sealed partial class HistoryViewModel : ViewModelBase
{
    private readonly ISnapshotStore? _store;
    private readonly SystemSnapshot _current;

    public HistoryViewModel(ISnapshotStore? store, SystemSnapshot current)
    {
        _store = store;
        _current = current;
        _ = LoadHistoryAsync();
    }

    public ObservableCollection<HistoryRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Loading snapshot history...";

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasHistory { get; set; }

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

    private async Task LoadHistoryAsync()
    {
        if (_store is null)
        {
            StatusMessage = "Snapshot history is unavailable on this machine, so there is nothing to trend yet.";
            return;
        }

        try
        {
            var trend = await HistoryTrendAnalyzer.LoadAsync(_store, _current.MachineName, 50);
            if (trend.SnapshotCount == 0)
            {
                StatusMessage = "No saved scans yet. Run a scan (refresh) and come back: every scan is saved to local history.";
                return;
            }

            SummaryText = trend.Summary;
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
            StatusMessage = $"History could not be read: {exception.Message}";
        }
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
