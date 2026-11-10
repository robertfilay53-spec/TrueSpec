using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// One sparkline cell. Only real sensor samples are appended; a missing sensor
/// shows "Not available" and leaves the history empty.
/// </summary>
public sealed class LiveMetricViewModel : ViewModelBase
{
    private const int MaxSamples = 40;

    private readonly List<double> _samples = new();

    private double? _current;

    public LiveMetricViewModel(string name, string unit)
    {
        Name = name;
        Unit = unit;

        var color = name switch
        {
            "CPU temp" or "GPU temp" => Color.Parse("#22C55E"),
            "CPU usage" => Color.Parse("#60A5FA"),
            _ => Color.Parse("#F59E0B"),
        };
        Stroke = new SolidColorBrush(color);
        Fill = new SolidColorBrush(color, 0.16d);
    }

    public string Name { get; }

    public string Unit { get; }

    public IBrush Stroke { get; }

    public IBrush Fill { get; }

    public bool HasValue => _current is not null;

    public string CurrentText => _current is { } value
        ? string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", value, Unit)
        : "Not available";

    public IReadOnlyList<double> Values { get; private set; } = Array.Empty<double>();

    public void AddSample(double? value)
    {
        if (value is null || double.IsNaN(value.Value))
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
