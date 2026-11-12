using System.Diagnostics;
using TrueSpec.Collectors.Windows.Sensors;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: runs the real load test on the real machine with the
/// real LibreHardwareMonitor pipeline. On Linux this file is excluded from
/// the build (see TrueSpec.Tests.csproj), like CrashCollectorTests. No
/// sensor values are asserted: they depend on the machine the test runs on.
/// Long-running: a 10-second burn plus the memory and disk exercise.
/// </summary>
[Trait("Category", "LongRunning")]
[Collection("LoadTestBurn")]
public class LoadTestIntegrationTests
{
    [Fact]
    public async Task RealMachine_10SecondTest_RecordsSamples_AndLeavesNoThreadsRunning()
    {
        var runner = new LoadTestRunner(new LibreHardwareMonitorSensorMonitor());
        var config = new LoadTestConfig(DurationSeconds: 10, RatedMaxMHz: null);

        var result = await runner.RunAsync(config);

        // A LibreHardwareMonitor sweep on a virtual machine can take over
        // a second, so 1 Hz is best-effort: samples are recorded as fast as
        // reads complete (the CI runner recorded 4 in 10 seconds). What
        // matters is that the test ran its full duration and recorded real
        // samples instead of aborting early.
        Assert.Null(result.AbortReason);
        Assert.True(result.Samples.Count >= 3, $"A 10 second test must record samples, saw {result.Samples.Count}.");
        Assert.True((result.CompletedAt - result.StartedAt).TotalSeconds >= 9,
            $"The test must run its full 10 second duration, ran {(result.CompletedAt - result.StartedAt).TotalSeconds:0.#} s.");
        Assert.True(result.MemoryPatternVerified, "The 256 MB memory pattern must verify on the real machine.");
        Assert.NotNull(result.DiskReadMBps);
        Assert.True(result.DiskReadMBps > 0, "The 128 MB sequential disk read must measure a speed.");
        Assert.Equal(0, runner.ActiveWorkerCount);
    }

    [Fact]
    public async Task RealMachine_Cancel_ReturnsWithin2Seconds_AndLeavesNoThreadsRunning()
    {
        var runner = new LoadTestRunner(new LibreHardwareMonitorSensorMonitor());
        var config = new LoadTestConfig(DurationSeconds: 60, RatedMaxMHz: null);
        using var cts = new CancellationTokenSource();

        var runTask = runner.RunAsync(config, cancellationToken: cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        cts.Cancel();

        var stopwatch = Stopwatch.StartNew();
        var finished = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(10)));
        stopwatch.Stop();

        Assert.True(finished == runTask, "The load test did not return after cancel.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"Cancel must return within 2 seconds, took {stopwatch.Elapsed.TotalSeconds:0.00} s.");
        var result = await runTask;
        Assert.Equal(LoadTestResult.AbortCancelled, result.AbortReason);
        Assert.Equal(0, runner.ActiveWorkerCount);
    }
}
