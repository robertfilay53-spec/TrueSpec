using System.Diagnostics;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Runner behaviour with a fake sensor monitor, so the abort and cancel
/// paths are deterministic on any machine (no hot hardware needed): the
/// forced temperature abort, the sensor-lost abort and the cancel path.
/// The burn, memory pattern and disk read still run for real. The safety
/// property is asserted after every run: no worker thread is left alive.
/// </summary>
public class LoadTestRunnerTests
{
    /// <summary>A scripted sensor monitor: a fixed temperature/clock, or empty readings after N reads.</summary>
    private sealed class FakeSensorMonitor : ISensorMonitor
    {
        private readonly double? _tempC;
        private readonly double? _clockMHz;
        private readonly int _goodReads;
        private int _reads;

        public FakeSensorMonitor(double? tempC, double? clockMHz, int goodReads = int.MaxValue)
        {
            _tempC = tempC;
            _clockMHz = clockMHz;
            _goodReads = goodReads;
        }

        public bool IsAvailable => true;

        public string StatusText => "Fake sensor monitor (test only)";

        public Task<SensorSnapshot> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _reads++;
            if (_reads > _goodReads)
            {
                return Task.FromResult(SensorSnapshot.Unavailable("Sensors stopped reporting (test script)."));
            }

            var readings = new List<SensorReading>();
            if (_tempC is { } temp)
            {
                readings.Add(new SensorReading("CPU", "Package", SensorKind.Temperature, "C", temp, "Test"));
            }

            if (_clockMHz is { } clock)
            {
                readings.Add(new SensorReading("CPU", "Core #1", SensorKind.Clock, "MHz", clock, "Test"));
                readings.Add(new SensorReading("CPU", "Core #2", SensorKind.Clock, "MHz", clock, "Test"));
            }

            return Task.FromResult(new SensorSnapshot(true, "Test", DateTimeOffset.Now, readings));
        }
    }

    [Fact]
    public async Task ForcedAbort_TemperatureThresholdOf1C_AbortsWithTemperatureReason()
    {
        var runner = new LoadTestRunner(new FakeSensorMonitor(50d, 3000d));
        var config = new LoadTestConfig(DurationSeconds: 60, AbortTemperatureC: 1d, RatedMaxMHz: 4000d);

        var result = await runner.RunAsync(config);

        Assert.Equal(LoadTestResult.AbortTemperature, result.AbortReason);
        Assert.True(result.Samples.Count >= 1, "At least the first hot sample must be recorded.");
        Assert.True(result.Samples.Count <= 3, $"Abort must fire at the first sample over the limit, saw {result.Samples.Count} samples.");
        Assert.Equal(50d, result.PeakTempC);
        Assert.Equal(0, runner.ActiveWorkerCount);
    }

    [Fact]
    public async Task SensorLost_ReadingsStopMidTest_AbortsWithSensorLostReason()
    {
        var runner = new LoadTestRunner(new FakeSensorMonitor(50d, 3000d, goodReads: 2));
        var config = new LoadTestConfig(DurationSeconds: 60, RatedMaxMHz: 4000d);

        var result = await runner.RunAsync(config);

        Assert.Equal(LoadTestResult.AbortSensorLost, result.AbortReason);
        Assert.True(result.Samples.Count >= 5, $"Two good samples plus three missed samples are needed, saw {result.Samples.Count}.");
        Assert.True(result.GapCount >= 3, $"Missed samples must be recorded as gaps, saw {result.GapCount}.");
        Assert.Equal(0, runner.ActiveWorkerCount);
    }

    [Fact]
    public async Task Cancel_ReturnsWithin2Seconds_WithCancelledReasonAndNoThreadsLeft()
    {
        var runner = new LoadTestRunner(new FakeSensorMonitor(50d, 3000d));
        var config = new LoadTestConfig(DurationSeconds: 60, RatedMaxMHz: 4000d);
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

    [Fact]
    public async Task NoSensorsAtAll_RunsWithoutSensorLostAbort_AndSaysSo()
    {
        var runner = new LoadTestRunner(new FakeSensorMonitor(null, null));
        var config = new LoadTestConfig(DurationSeconds: 3, RatedMaxMHz: 4000d);

        var result = await runner.RunAsync(config);

        Assert.Null(result.AbortReason);
        Assert.True(result.Samples.Count >= 2, $"A 3 second test must record samples, saw {result.Samples.Count}.");
        Assert.Equal(result.Samples.Count, result.GapCount);
        Assert.Null(result.PeakTempC);
        Assert.Contains("could not be judged", result.VerdictText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, runner.ActiveWorkerCount);
    }

    [Fact]
    public async Task ShortRun_MemoryPatternVerifies_AndDiskReadIsMeasured()
    {
        var runner = new LoadTestRunner(new FakeSensorMonitor(50d, 3900d));
        var config = new LoadTestConfig(DurationSeconds: 3, RatedMaxMHz: 4000d);

        var result = await runner.RunAsync(config);

        Assert.Null(result.AbortReason);
        Assert.True(result.MemoryPatternVerified, "The 256 MB memory pattern must verify.");
        Assert.NotNull(result.DiskReadMBps);
        Assert.True(result.DiskReadMBps > 0, "The 128 MB sequential disk read must measure a speed.");
        Assert.Equal(0, runner.ActiveWorkerCount);
    }
}
