using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Sensors;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// The "Load test" card on the Tests page (Blueprint 2): duration selector,
/// Start / Cancel, live temperature and clock readouts during the run, and a
/// result card with the throttle verdict and a temperature sparkline.
///
/// The warnings are shown before Start, never after the fact: the test loads
/// the CPU for the selected duration and stops automatically at 95 C, a
/// short test is a functional check and not a burn-in certificate, and a
/// machine on battery may report power-limited results.
/// </summary>
public sealed partial class LoadTestViewModel : ViewModelBase
{
    private readonly ISensorMonitor _sensors;
    private readonly double? _ratedMaxMHz;
    private readonly Action<LoadTestResult>? _onCompleted;
    private readonly SynchronizationContext? _uiContext;

    private LoadTestRunner? _runner;
    private CancellationTokenSource? _cts;

    public LoadTestViewModel(
        ISensorMonitor sensors,
        double? ratedMaxMHz,
        bool isOnBattery,
        Action<LoadTestResult>? onCompleted)
    {
        _sensors = sensors;
        _ratedMaxMHz = ratedMaxMHz;
        _onCompleted = onCompleted;
        _uiContext = SynchronizationContext.Current;
        IsOnBattery = isOnBattery;
    }

    public bool IsOnBattery { get; }

    public IReadOnlyList<int> Durations { get; } = new[] { 30, 60, 120 };

    [ObservableProperty]
    public partial int SelectedDurationSeconds { get; set; } = 60;

    /// <summary>The warning shown before Start; it names the selected duration.</summary>
    public string WarningText => string.Create(
        CultureInfo.InvariantCulture,
        $"This will load your CPU for {SelectedDurationSeconds} seconds. It stops automatically if the temperature exceeds 95 C.");

    public string FunctionalCheckText => string.Create(
        CultureInfo.InvariantCulture,
        $"A {SelectedDurationSeconds}-second test is a functional check, not a burn-in certificate.");

    public bool CanStart => !IsRunning;

    public bool CanCancel => IsRunning;

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Not run yet.";

    [ObservableProperty]
    public partial string LiveTemperatureText { get; set; } = "Temperature: not running";

    [ObservableProperty]
    public partial string LiveClockText { get; set; } = "Clock: not running";

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial string VerdictText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultPeakText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultClockText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultPercentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<double> TemperatureValues { get; set; } = Array.Empty<double>();

    partial void OnSelectedDurationSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(WarningText));
        OnPropertyChanged(nameof(FunctionalCheckText));
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanCancel));
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _runner = new LoadTestRunner(_sensors);
        IsRunning = true;
        HasResult = false;
        TemperatureValues = Array.Empty<double>();
        StatusText = "Running...";
        ElapsedText = "0 s";
        LiveTemperatureText = "Temperature: measuring...";
        LiveClockText = "Clock: measuring...";
        VerdictText = string.Empty;

        try
        {
            var config = new LoadTestConfig(SelectedDurationSeconds, RatedMaxMHz: _ratedMaxMHz);
            var progress = new Progress<LoadTestProgress>(OnProgress);
            var result = await _runner.RunAsync(config, progress, _cts.Token);

            StatusText = result.AbortReason switch
            {
                LoadTestResult.AbortTemperature => "Stopped automatically: temperature limit reached.",
                LoadTestResult.AbortSensorLost => "Stopped: sensors stopped reporting.",
                LoadTestResult.AbortCancelled => "Cancelled.",
                _ => "Load test complete.",
            };
            HasResult = true;
            VerdictText = result.VerdictText;
            ResultPeakText = result.PeakTempC is { } peak
                ? string.Create(CultureInfo.InvariantCulture, $"Peak temperature: {peak:0.#} C")
                : "Peak temperature: not reported by hardware";
            ResultClockText = result.AvgClockMHz is { } clock
                ? string.Create(CultureInfo.InvariantCulture, $"Average clock (last 30 s): {clock:0} MHz")
                : "Average clock: not reported by hardware";
            ResultPercentText = result.RatedMaxMHz is { } rated && result.ClockHeldPercent is { } percent
                ? string.Create(CultureInfo.InvariantCulture, $"Held {percent:0}% of rated {rated:0} MHz max")
                : "Rated clock: not reported by hardware";
            TemperatureValues = result.Samples
                .Where(s => s.CpuTempC.HasValue)
                .Select(s => s.CpuTempC!.Value)
                .ToList();
            ElapsedText = string.Create(
                CultureInfo.InvariantCulture,
                $"Finished in {(result.CompletedAt - result.StartedAt).TotalSeconds:0} s.");
            LiveTemperatureText = "Temperature: test finished";
            LiveClockText = "Clock: test finished";

            _onCompleted?.Invoke(result);
        }
        catch (Exception exception)
        {
            StatusText = "Test failed";
            VerdictText = $"No result is shown because the test did not complete: {exception.Message}";
        }
        finally
        {
            IsRunning = false;
            _runner = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private void OnProgress(LoadTestProgress progress)
    {
        // Progress<T> captures the UI synchronization context, so this
        // already runs on the UI thread in the app. The explicit post is
        // belt and braces for a caller without a captured context.
        if (_uiContext is not null && SynchronizationContext.Current != _uiContext)
        {
            _uiContext.Post(_ => ApplyProgress(progress), null);
            return;
        }

        ApplyProgress(progress);
    }

    private void ApplyProgress(LoadTestProgress progress)
    {
        ElapsedText = string.Create(CultureInfo.InvariantCulture, $"{progress.ElapsedSeconds} s");
        LiveTemperatureText = progress.CpuTempC is { } temp
            ? string.Create(CultureInfo.InvariantCulture, $"Temperature: {temp:0.#} C")
            : "Temperature: not reported by hardware";
        LiveClockText = progress.AvgClockMHz is { } clock
            ? string.Create(CultureInfo.InvariantCulture, $"Clock: {clock:0} MHz")
            : "Clock: not reported by hardware";
    }
}
