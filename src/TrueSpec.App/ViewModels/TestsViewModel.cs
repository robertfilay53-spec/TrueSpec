using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.Core.Benchmarks;

namespace TrueSpec.App.ViewModels;

/// <summary>Tests page: real local benchmarks plus honest unavailable entries.</summary>
public sealed class TestsViewModel : ViewModelBase
{
    public TestsViewModel()
    {
        var benchmarks = new BenchmarkService();
        Tests = new[]
        {
            new TestItemViewModel(
                "CPU single-thread",
                "Runs a fixed single-thread workload and reports real measured operations per second.",
                benchmarks.RunCpuSingleThreadAsync),
            new TestItemViewModel(
                "Disk read speed",
                "Reads a real 64 MB temporary file and reports measured sequential read speed.",
                benchmarks.RunDiskSequentialReadAsync),
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
            new TestItemViewModel(
                "Stress test",
                "Not in this build: no load test runs until this test is implemented.",
                null),
        };
    }

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

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial string ResultText { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

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
