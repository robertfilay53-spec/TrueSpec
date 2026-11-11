using TrueSpec.Core.Benchmarks;

namespace TrueSpec.Tests;

/// <summary>
/// Regression tests for the benchmarks. The CPU benchmark used to fail on
/// every machine: the workload checksum was cast with `checked`, and the sum
/// of 20M full-width xorshift values exceeds long.MaxValue after the first
/// iteration, so it threw OverflowException and the test always reported
/// "Measurement failed".
/// </summary>
public class BenchmarkServiceTests
{
    [Fact]
    public async Task CpuSingleThread_ReturnsAMeasuredResult()
    {
        var result = await new BenchmarkService().RunCpuSingleThreadAsync();

        Assert.True(result.Success, result.Summary);
        Assert.NotNull(result.Value);
        Assert.True(result.Value > 0);
        Assert.Equal("ops/s", result.Unit);
    }

    [Fact]
    public async Task DiskSequentialRead_ReturnsAMeasuredResult()
    {
        var result = await new BenchmarkService().RunDiskSequentialReadAsync();

        Assert.True(result.Success, result.Summary);
        Assert.NotNull(result.Value);
        Assert.True(result.Value > 0);
        Assert.Equal("MB/s", result.Unit);
    }
}
