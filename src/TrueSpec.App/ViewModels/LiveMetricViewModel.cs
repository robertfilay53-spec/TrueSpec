using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;
using TrueSpec.Core.Localization;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// One sparkline cell. Only real sensor samples are appended; a missing sensor
/// shows "Not available" and leaves the history empty.
///
/// Localisation, phase 5 (v2.1.11): the Overview page's live strip renders
/// in the page language. A metric built with a stable key (one of the
/// *Key constants) resolves its display name from the key through the
/// localizer and re-resolves it through <see cref="ApplyLanguage"/>; the
/// key, the unit and the colour never change with the language, so the
/// strip keeps updating the same cell after a switch. A metric built with
/// the legacy (name, unit) constructor keeps the old behaviour exactly:
/// the name is the display name, the colour follows the name, and
/// <see cref="ApplyLanguage"/> is a no-op — existing callers and tests
/// are untouched. "Not available" and "No sensor reading" are page
/// chrome, so they resolve in the metric's language too; the measured
/// value itself never changes.
/// </summary>
public sealed class LiveMetricViewModel : ViewModelBase
{
    private const int MaxSamples = 40;

    /// <summary>Stable keys for the four live metrics the Overview page shows.</summary>
    public const string CpuTempKey = "cpu-temp";
    public const string GpuTempKey = "gpu-temp";
    public const string CpuUsageKey = "cpu-usage";
    public const string BatteryPowerKey = "battery-power";

    private readonly List<double> _samples = new();

    private double? _current;

    private string _name;

    private string? _metricKey;

    private string _languageCode;

    public LiveMetricViewModel(string name, string unit)
    {
        _name = name;
        Unit = unit;
        _metricKey = null;
        _languageCode = LanguagePacks.EnglishCode;
        (Stroke, Fill) = BrushesFor(name);
    }

    /// <summary>
    /// Builds a metric from its stable key: the display name resolves in
    /// <paramref name="languageCode"/> (English when null or unknown) and
    /// the colour follows the key, not the translated name.
    /// </summary>
    public LiveMetricViewModel(string metricKey, string unit, string? languageCode)
    {
        _metricKey = metricKey;
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        Unit = unit;
        // The key itself is the fallback name: an unknown key shows the
        // key, like the localizer shows an unknown key, never a blank.
        _name = metricKey;
        _name = ResolveName();
        (Stroke, Fill) = BrushesFor(metricKey);
    }

    /// <summary>The stable key when built from one, null for a legacy (name, unit) metric.</summary>
    public string? MetricKey => _metricKey;

    public string Name
    {
        get => _name;
        private set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public string Unit { get; }

    public IBrush Stroke { get; }

    public IBrush Fill { get; }

    public bool HasValue => _current is not null;

    public string CurrentText => _current is { } value
        ? string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", value, Unit)
        : StringLocalizer.Get(AppStringKeys.OverviewMetricNotAvailable, _languageCode);

    /// <summary>The empty-strip line under the sparkline, in the metric's language.</summary>
    public string NoSensorReadingText =>
        StringLocalizer.Get(AppStringKeys.OverviewNoSensorReading, _languageCode);

    /// <summary>
    /// Re-renders this metric in the given language, in place: the display
    /// name and the empty-state texts re-resolve. A legacy metric has no
    /// key to resolve from, so this is a no-op for it. An unknown code
    /// falls back to English through the localizer, never to a blank.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        if (_metricKey is null)
        {
            return;
        }

        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        Name = ResolveName();
        OnPropertyChanged(nameof(CurrentText));
        OnPropertyChanged(nameof(NoSensorReadingText));
    }

    private string ResolveName() => _metricKey switch
    {
        CpuTempKey => StringLocalizer.Get(AppStringKeys.OverviewMetricCpuTemp, _languageCode),
        GpuTempKey => StringLocalizer.Get(AppStringKeys.OverviewMetricGpuTemp, _languageCode),
        CpuUsageKey => StringLocalizer.Get(AppStringKeys.OverviewMetricCpuUsage, _languageCode),
        BatteryPowerKey => StringLocalizer.Get(AppStringKeys.OverviewMetricBatteryPower, _languageCode),
        _ => _name,
    };

    private static (IBrush Stroke, IBrush Fill) BrushesFor(string keyOrName)
    {
        var color = keyOrName switch
        {
            "CPU temp" or "GPU temp" or CpuTempKey or GpuTempKey => Color.Parse("#22C55E"),
            "CPU usage" or CpuUsageKey => Color.Parse("#60A5FA"),
            _ => Color.Parse("#F59E0B"),
        };
        var stroke = new SolidColorBrush(color);
        var fill = new SolidColorBrush(color, 0.16d);
        return (stroke, fill);
    }

    public IReadOnlyList<double> Values { get; private set; } = Array.Empty<double>();

    public void AddSample(double? value)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            _current = null;
            Publish();
            return;
        }

        _current = value.Value;
        _samples.Add(value.Value);
        if (_samples.Count > MaxSamples)
        {
            _samples.RemoveAt(0);
        }

        Publish();
    }

    private void Publish()
    {
        Values = _samples.ToArray();
        OnPropertyChanged(nameof(Values));
        OnPropertyChanged(nameof(CurrentText));
        OnPropertyChanged(nameof(HasValue));
    }
}
