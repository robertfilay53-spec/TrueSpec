using Avalonia.Media;
using TrueSpec.App.ViewModels;

namespace TrueSpec.Tests;

/// <summary>
/// Tests for one live-metric sparkline cell (hardening item 7).
/// Windows leg only (it drives TrueSpec.App's ViewModels), like
/// LoadTestViewModelTests: excluded from the net10.0 leg in the csproj.
/// </summary>
public class LiveMetricViewModelTests
{
    [Fact]
    public void StartsWithNoValue()
    {
        var metric = new LiveMetricViewModel("CPU temp", "C");

        Assert.Equal("CPU temp", metric.Name);
        Assert.Equal("C", metric.Unit);
        Assert.False(metric.HasValue);
        Assert.Equal("Not available", metric.CurrentText);
        Assert.Empty(metric.Values);
    }

    [Fact]
    public void AddSample_ShowsTheValueAndKeepsHistory()
    {
        var metric = new LiveMetricViewModel("CPU temp", "C");

        metric.AddSample(72.5);
        metric.AddSample(71);

        Assert.True(metric.HasValue);
        Assert.Equal("71 C", metric.CurrentText);
        Assert.Equal(new[] { 72.5, 71d }, metric.Values);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AddSample_NonValue_ClearsTheCurrentReadingButKeepsHistory(double? value)
    {
        // A missing sensor is a gap, never a zero: the current reading
        // clears, but the samples already recorded stay on the sparkline.
        var metric = new LiveMetricViewModel("CPU temp", "C");
        metric.AddSample(72.5);

        metric.AddSample(value);

        Assert.False(metric.HasValue);
        Assert.Equal("Not available", metric.CurrentText);
        Assert.Equal(new[] { 72.5 }, metric.Values);
    }

    [Fact]
    public void AddSample_KeepsOnlyTheNewest40Samples()
    {
        var metric = new LiveMetricViewModel("CPU temp", "C");
        for (var i = 1; i <= 45; i++)
        {
            metric.AddSample(i);
        }

        Assert.Equal(40, metric.Values.Count);
        Assert.Equal(6d, metric.Values[0]);
        Assert.Equal(45d, metric.Values[^1]);
        Assert.Equal("45 C", metric.CurrentText);
    }

    [Theory]
    [InlineData("CPU temp", "#22C55E")]
    [InlineData("GPU temp", "#22C55E")]
    [InlineData("CPU usage", "#60A5FA")]
    [InlineData("Something else", "#F59E0B")]
    public void Colour_FollowsTheMetricName(string name, string expectedHex)
    {
        var metric = new LiveMetricViewModel(name, "C");

        var stroke = Assert.IsType<SolidColorBrush>(metric.Stroke);
        Assert.Equal(Color.Parse(expectedHex), stroke.Color);
        var fill = Assert.IsType<SolidColorBrush>(metric.Fill);
        Assert.Equal(Color.Parse(expectedHex), fill.Color);
    }

    [Fact]
    public void KeyConstructor_ResolvesTheDisplayNameInEnglish()
    {
        // Localisation, phase 5 (v2.1.11): a metric built from its
        // stable key resolves its name through the localizer, so the
        // Overview page's live strip follows the page language.
        var metric = new LiveMetricViewModel(LiveMetricViewModel.CpuTempKey, "C", null);

        Assert.Equal(LiveMetricViewModel.CpuTempKey, metric.MetricKey);
        Assert.Equal("CPU temp", metric.Name);
        Assert.Equal("Not available", metric.CurrentText);
        Assert.Equal("No sensor reading", metric.NoSensorReadingText);
    }

    [Fact]
    public void KeyConstructor_ResolvesTheDisplayNameInGerman()
    {
        var cpu = new LiveMetricViewModel(LiveMetricViewModel.CpuTempKey, "C", "de");
        var gpu = new LiveMetricViewModel(LiveMetricViewModel.GpuTempKey, "C", "de");
        var usage = new LiveMetricViewModel(LiveMetricViewModel.CpuUsageKey, "%", "de");
        var power = new LiveMetricViewModel(LiveMetricViewModel.BatteryPowerKey, "W", "de");

        Assert.Equal("CPU-Temperatur", cpu.Name);
        Assert.Equal("GPU-Temperatur", gpu.Name);
        Assert.Equal("CPU-Auslastung", usage.Name);
        Assert.Equal("Akkuleistung", power.Name);
        Assert.Equal("Nicht verfügbar", cpu.CurrentText);
        Assert.Equal("Kein Sensormesswert", cpu.NoSensorReadingText);
    }

    [Fact]
    public void KeyConstructor_ColourFollowsTheKeyNotTheTranslatedName()
    {
        var cpu = new LiveMetricViewModel(LiveMetricViewModel.CpuTempKey, "C", "de");
        var usage = new LiveMetricViewModel(LiveMetricViewModel.CpuUsageKey, "%", "de");
        var power = new LiveMetricViewModel(LiveMetricViewModel.BatteryPowerKey, "W", "de");

        Assert.Equal(Color.Parse("#22C55E"), Assert.IsType<SolidColorBrush>(cpu.Stroke).Color);
        Assert.Equal(Color.Parse("#60A5FA"), Assert.IsType<SolidColorBrush>(usage.Stroke).Color);
        Assert.Equal(Color.Parse("#F59E0B"), Assert.IsType<SolidColorBrush>(power.Stroke).Color);
    }

    [Fact]
    public void ApplyLanguage_ReResolvesTheNameAndTheEmptyStates()
    {
        var metric = new LiveMetricViewModel(LiveMetricViewModel.CpuTempKey, "C", null);

        metric.ApplyLanguage("de");
        Assert.Equal("CPU-Temperatur", metric.Name);
        Assert.Equal("Nicht verfügbar", metric.CurrentText);
        Assert.Equal("Kein Sensormesswert", metric.NoSensorReadingText);

        metric.ApplyLanguage("en");
        Assert.Equal("CPU temp", metric.Name);
        Assert.Equal("Not available", metric.CurrentText);
    }

    [Fact]
    public void ApplyLanguage_UnknownCodeFallsBackToEnglish()
    {
        var metric = new LiveMetricViewModel(LiveMetricViewModel.CpuTempKey, "C", "de");

        metric.ApplyLanguage("fr");

        Assert.Equal("CPU temp", metric.Name);
        Assert.Equal("Not available", metric.CurrentText);
    }

    [Fact]
    public void ApplyLanguage_LegacyMetricIsUntouched()
    {
        // A metric built with the legacy (name, unit) constructor has
        // no key to resolve from, so a language switch is a no-op for
        // it — existing callers keep their exact behaviour.
        var metric = new LiveMetricViewModel("CPU temp", "C");

        metric.ApplyLanguage("de");

        Assert.Null(metric.MetricKey);
        Assert.Equal("CPU temp", metric.Name);
        Assert.Equal("Not available", metric.CurrentText);
    }

    [Fact]
    public void KeyConstructor_UnknownKeyShowsTheKey()
    {
        var metric = new LiveMetricViewModel("no-such-key", "C", "de");

        Assert.Equal("no-such-key", metric.Name);
        Assert.Equal("no-such-key", metric.MetricKey);
    }
}
