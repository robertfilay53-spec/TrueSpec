using System.Globalization;
using TrueSpec.Core.Benchmarks;

namespace TrueSpec.Tests;

/// <summary>
/// U10 extended benchmarks. Two layers, following the existing pattern:
/// fake-harness tests for the pure decision logic (concurrent-load note,
/// latency percentiles) and time-boxed real runs in the serialized
/// LoadTestBurn collection. No test asserts an absolute speed: a benchmark
/// that is fast on one machine and slow on another is correct on both, so
/// tests assert success, units, and that every result names its workload.
/// </summary>
[Collection("LoadTestBurn")]
public class ExtendedBenchmarkTests
{
    // Fake harness: the concurrent-load note is a pure function of the
    // sampled load, so it is tested with fake loads, never a real sample.

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(10d)]
    [InlineData(24.9d)]
    public void ConcurrentLoadNote_IdleOrUnsampledLoad_HasNoNote(double? load)
    {
        Assert.Null(BenchmarkService.ConcurrentLoadNote(load));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ConcurrentLoadNote_UnusableLoad_HasNoNote(double load)
    {
        Assert.Null(BenchmarkService.ConcurrentLoadNote(load));
    }

    [Theory]
    [InlineData(25d)]
    [InlineData(42d)]
    [InlineData(100d)]
    public void ConcurrentLoadNote_BusyLoad_NamesTheSampledLoad(double load)
    {
        var note = BenchmarkService.ConcurrentLoadNote(load);

        Assert.NotNull(note);
        Assert.Contains("another program was using the CPU", note, StringComparison.Ordinal);
        Assert.Contains(((int)load).ToString(CultureInfo.InvariantCulture), note, StringComparison.Ordinal);
    }

    // Fake harness: latency percentiles are pure, so they are tested with
    // fake latencies with exact expected values (nearest-rank method).

    [Fact]
    public void LatencyPercentiles_EmptyOrNull_ReturnsNull()
    {
        Assert.Null(BenchmarkService.LatencyPercentiles(Array.Empty<double>()));
        Assert.Null(BenchmarkService.LatencyPercentiles(null!));
        Assert.Null(BenchmarkService.LatencyPercentiles(new[] { double.NaN, double.PositiveInfinity, -1d }));
    }

    [Fact]
    public void LatencyPercentiles_KnownValues_ReturnsNearestRank()
    {
        var latencies = Enumerable.Range(1, 100).Select(v => (double)v).ToArray();

        var percentiles = BenchmarkService.LatencyPercentiles(latencies);

        Assert.NotNull(percentiles);
        Assert.Equal(50d, percentiles.Value.P50);
        Assert.Equal(90d, percentiles.Value.P90);
        Assert.Equal(99d, percentiles.Value.P99);
    }

    [Fact]
    public void LatencyPercentiles_UnsortedInput_IsSortedFirst()
    {
        var percentiles = BenchmarkService.LatencyPercentiles(new[] { 9d, 1d, 5d, 3d, 7d });

        Assert.NotNull(percentiles);
        Assert.Equal(5d, percentiles.Value.P50);
        Assert.Equal(9d, percentiles.Value.P90);
        Assert.Equal(9d, percentiles.Value.P99);
    }

    [Fact]
    public void LatencyPercentiles_SingleValue_AllPercentilesAreThatValue()
    {
        var percentiles = BenchmarkService.LatencyPercentiles(new[] { 2.5d });

        Assert.NotNull(percentiles);
        Assert.Equal(2.5d, percentiles.Value.P50);
        Assert.Equal(2.5d, percentiles.Value.P90);
        Assert.Equal(2.5d, percentiles.Value.P99);
    }

    [Fact]
    public void SampleSystemCpuLoadPercent_WhenSampled_IsAValidPercent()
    {
        var load = BenchmarkService.SampleSystemCpuLoadPercent();

        // The sample may legitimately be unavailable (no permission to query
        // processes); when it is available it must be a percent, never a
        // made-up or out-of-range number.
        Assert.True(load is null || (load >= 0d && load <= 100d), $"Sampled load was {load}.");
    }

    // Real runs: time-boxed by the service itself (2 s disk phases, best-of-3
    // CPU/memory passes), serialized by LoadTestBurn so they never run
    // alongside another burn.

    [Fact]
    public async Task CpuMultiThread_ReturnsAMeasuredResultNamingItsWorkload()
    {
        var result = await new BenchmarkService(() => 0d).RunCpuMultiThreadAsync();

        Assert.True(result.Success, result.Summary);
        Assert.NotNull(result.Value);
        Assert.True(result.Value > 0);
        Assert.Equal("ops/s", result.Unit);
        Assert.Equal("CPU multi-thread", result.Name);
        Assert.Contains("threads", result.Detail, StringComparison.Ordinal);
        Assert.Contains("xorshift", result.Detail, StringComparison.Ordinal);
        Assert.Contains("all logical cores", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiskRandom4K_ReturnsMeasuredIopsNamingItsWorkload()
    {
        var result = await new BenchmarkService(() => 0d).RunDiskRandom4KAsync();

        Assert.True(result.Success, result.Summary);
        Assert.NotNull(result.Value);
        Assert.True(result.Value > 0);
        Assert.Equal("IOPS", result.Unit);
        Assert.Equal("Disk random 4K", result.Name);
        Assert.Contains("4 KB", result.Detail, StringComparison.Ordinal);
        Assert.Contains("queue depth 1", result.Detail, StringComparison.Ordinal);
        Assert.Contains("random reads", result.Detail, StringComparison.Ordinal);
        Assert.Contains("random writes", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MemoryCopy_ReturnsMeasuredBandwidthNamingItsWorkload()
    {
        var result = await new BenchmarkService(() => 0d).RunMemoryCopyAsync();

        Assert.True(result.Success, result.Summary);
        Assert.NotNull(result.Value);
        Assert.True(result.Value > 0);
        Assert.Equal("GB/s", result.Unit);
        Assert.Equal("Memory copy", result.Name);
        Assert.Contains("64 MB", result.Detail, StringComparison.Ordinal);
        Assert.Contains("Buffer.BlockCopy", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiskLatency_ReturnsPercentilesNamingItsWorkload()
    {
        var result = await new BenchmarkService(() => 0d).RunDiskLatencyAsync();

        Assert.True(result.Success, result.Summary);
        Assert.NotNull(result.Value);
        Assert.True(result.Value > 0);
        Assert.Equal("ms", result.Unit);
        Assert.Equal("Disk latency under load", result.Name);
        Assert.Contains("p50", result.Summary, StringComparison.Ordinal);
        Assert.Contains("p90", result.Summary, StringComparison.Ordinal);
        Assert.Contains("p99", result.Summary, StringComparison.Ordinal);
        Assert.Contains("4 KB", result.Detail, StringComparison.Ordinal);
        Assert.Contains("queue depth 1", result.Detail, StringComparison.Ordinal);
        Assert.Contains("continuous load", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunUnderConcurrentLoad_CarriesTheLoadNote()
    {
        var result = await new BenchmarkService(() => 80d).RunMemoryCopyAsync();

        Assert.True(result.Success, result.Summary);
        Assert.Contains("another program was using the CPU", result.Detail, StringComparison.Ordinal);
        Assert.Contains("80%", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunOnIdleMachine_CarriesNoLoadNote()
    {
        var result = await new BenchmarkService(() => 5d).RunMemoryCopyAsync();

        Assert.True(result.Success, result.Summary);
        Assert.DoesNotContain("another program", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedSequentialRead_UsedByTheLoadTest_CarriesNoLoadNote()
    {
        // The load test runs this measurement while its own burn workers
        // saturate the CPU; a concurrent-load note here would blame another
        // program for the test itself.
        var result = BenchmarkService.MeasureDiskSequentialRead(16 * 1024 * 1024, CancellationToken.None);

        Assert.True(result.Success, result.Summary);
        Assert.DoesNotContain("another program", result.Detail, StringComparison.Ordinal);
    }

    // Cancel paths: a token cancelled before the run starts must cancel the
    // run instead of producing a result.

    [Fact]
    public async Task CpuMultiThread_PreCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new BenchmarkService(() => 0d).RunCpuMultiThreadAsync(cts.Token));
    }

    [Fact]
    public async Task MemoryCopy_PreCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new BenchmarkService(() => 0d).RunMemoryCopyAsync(cts.Token));
    }

    [Fact]
    public async Task DiskRandom4K_PreCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new BenchmarkService(() => 0d).RunDiskRandom4KAsync(cts.Token));
    }

    [Fact]
    public async Task DiskLatency_PreCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new BenchmarkService(() => 0d).RunDiskLatencyAsync(cts.Token));
    }
}
