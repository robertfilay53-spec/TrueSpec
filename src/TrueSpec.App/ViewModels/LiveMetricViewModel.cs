using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// One sparkline cell. Values are replaced as an array on every refresh so the
/// Sparkline control redraws immediately.
/// </summary>
public sealed class LiveMetricViewModel : ViewModelBase
{
    private const int MaxSamples = 40;

    private readonly double _min;
    private readonly double _max;
    private readonly List<double> _samples = new();
    private readonly Random _random = new();

    private double _current;

    public LiveMetricViewModel(string name, string unit, double min, double max, double seed)
    {
        Name = name;
        Unit = unit;
        _min = min;
        _max = max;
        _current = seed;

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

    public string CurrentText => string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", _current, Unit);

    public IReadOnlyList<double> Values { get; private set; } = Array.Empty<double>();

    public void Seed()
    {
        for (var i = 0; i < 24; i++)
        {
            _current = NextValue(_current);
            _samples.Add(_current);
        }

        Publish();
    }

    public void AddRandomSample()
    {
        _current = NextValue(_current);
        _samples.Add(_current);
        if (_samples.Count > MaxSamples)
        {
            _samples.RemoveAt(0);
        }

        Publish();
    }

    private double NextValue(double current)
    {
        var span = _max - _min;
        var drift = (_random.NextDouble() - 0.5d) * span * 0.22d;
        return Math.Clamp(current + drift, _min, _max);
    }

    private void Publish()
    {
        Values = _samples.ToArray();
        OnPropertyChanged(nameof(Values));
        OnPropertyChanged(nameof(CurrentText));
    }
}
