using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Sensors;

namespace TrueSpec.App.ViewModels;

/// <summary>Tests page: real local benchmarks plus honest unavailable entries.</summary>
public sealed class TestsViewModel : ViewModelBase
{
    public TestsViewModel()
        : this(new UnavailableSensorMonitor(), null, false, null)
    {
    }

    public TestsViewModel(
        ISensorMonitor sensors,
        double? ratedMaxMHz,
        bool isOnBattery,
        Action<LoadTestResult>? onLoadTestCompleted)
    {
        LoadTest = new LoadTestViewModel(sensors, ratedMaxMHz, isOnBattery, onLoadTestCompleted);

        var benchmarks = new BenchmarkService();
        Tests = new[]
        {
            new TestItemViewModel(
                "CPU single-thread",
                "Runs a fixed single-thread workload and reports real measured operations per second.",
                benchmarks.RunCpuSingleThreadAsync),
            new TestItemViewModel(
                "CPU multi-thread",
                "Runs the same workload on all logical cores at once and reports real measured operations per second.",
                benchmarks.RunCpuMultiThreadAsync),
            new TestItemViewModel(
                "Disk read speed",
                "Reads a real 64 MB temporary file and reports measured sequential read speed.",
                benchmarks.RunDiskSequentialReadAsync),
            new TestItemViewModel(
                "Disk random 4K",
                "Random 4 KB reads and writes against a real 64 MB temporary file, queue depth 1, up to 2 seconds per phase. This is the number that decides how a machine feels.",
                benchmarks.RunDiskRandom4KAsync),
            new TestItemViewModel(
                "Memory copy",
                "Copies real 64 MB buffers and reports measured copy bandwidth in GB/s.",
                benchmarks.RunMemoryCopyAsync),
            new TestItemViewModel(
                "Disk latency under load",
                "Continuous random 4 KB reads keep the disk busy for up to 2 seconds; reports p50, p90 and p99 read latency in milliseconds.",
                benchmarks.RunDiskLatencyAsync),
            new TestItemViewModel(
                "Quick memory test",
                "Not in this build: the Windows Memory Diagnostic needs a reboot, so run it from Windows yourself for now.",
                null),
            new TestItemViewModel(
                "Battery runtime estimate",
                "Not in this build: no runtime estimate is shown until this test is implemented.",
                null),
            new TestItemViewModel(
                "Wi-Fi speed test",
                "Not in this build: an internet speed test would need the network, and this app is offline first.",
                null),
        };
    }

    /// <summary>
    /// U10 scope note (WORLD-CLASS-PLAN.md U10): these benchmarks measure
    /// this machine only. They do not replace Geekbench (cross-platform
    /// comparison) or 3DMark (GPU rendering).
    /// </summary>
    public string BenchmarkScopeNote =>
        "These benchmarks measure this machine only, with the exact workload shown on each result. " +
        "They do not replace Geekbench (cross-platform comparison) or 3DMark (GPU rendering). " +
        "A result made while another program was using the CPU says so on the result itself.";

    /// <summary>The load-test card: duration, Start/Cancel, live readouts and the result.</summary>
    public LoadTestViewModel LoadTest { get; }

    public IReadOnlyList<TestItemViewModel> Tests { get; }
}

public sealed partial class TestItemViewModel : ViewModelBase
{
    private readonly Func<CancellationToken, Task<BenchmarkResult>>? _run;

    public TestItemViewModel(
        string name,
        string description,
        Func<CancellationToken, Task<BenchmarkResult>>? run)
    {
        Name = name;
        Description = description;
        _run = run;
        Status = run is null ? "Not available in this build" : "Ready";
        ResultText = run is null
            ? "This test is not implemented yet, so no result is shown."
            : "Not run yet.";
    }

    public string Name { get; }

    public string Description { get; }

    public bool CanRun => _run is not null;

    /// <summary>The Run button enables only for an implemented test that is not already running.</summary>
    public bool CanRunNow => CanRun && !IsRunning;

    /// <summary>Button label follows the state: unavailable, running, or ready.</summary>
    public string RunButtonText => !CanRun ? "Not available" : IsRunning ? "Running…" : "Run test";

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial string ResultText { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunNow));
        OnPropertyChanged(nameof(RunButtonText));
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (_run is null || IsRunning)
        {
            return;
        }

        IsRunning = true;
        Status = "Running...";
        ResultText = "Measuring locally. Nothing is uploaded.";
        try
        {
            var result = await _run(CancellationToken.None);
            Status = result.Summary;
            ResultText = $"{result.Detail} Source: {result.Source}.";
        }
        catch (Exception exception)
        {
            Status = "Test failed";
            ResultText = $"No result is shown because the test did not complete: {exception.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
