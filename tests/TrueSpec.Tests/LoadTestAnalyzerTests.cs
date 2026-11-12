using TrueSpec.Core.Benchmarks;

namespace TrueSpec.Tests;

/// <summary>
/// Pure-function tests for the throttle verdict (Blueprint 2). Every rule is
/// pinned here: throttled needs a slow clock AND heat, a slow clock with a
/// cool CPU is power-limited, gaps are never interpolated, and the 80% /
/// 85 C boundary is tested in both directions.
/// </summary>
public class LoadTestAnalyzerTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static LoadTestSample GetSample(
        double? clockMHz,
        double? tempC,
        int secondOffset,
        bool isGap = false) => new(
        BaseTime.AddSeconds(secondOffset),
        tempC,
        clockMHz,
        clockMHz,
        2400d,
        45d,
        55d,
        isGap);

    private static List<LoadTestSample> GetSamples(double clockMHz, double tempC, int seconds = 35)
    {
        var samples = new List<LoadTestSample>(seconds);
        for (var i = 0; i < seconds; i++)
        {
            samples.Add(GetSample(clockMHz, tempC, i));
        }

        return samples;
    }

    [Fact]
    public void ThrottledCase_SlowClockAndHotCpu_IsThrottled()
    {
        var result = LoadTestAnalyzer.Analyze(GetSamples(1800d, 97d), 4200d, null, 60);

        Assert.True(result.Throttled);
        Assert.False(result.PowerLimited);
        Assert.Equal(1800d, result.AvgClockMHz);
        Assert.Equal(97d, result.PeakTempC);
        Assert.Equal(1800d / 4200d * 100d, result.ClockHeldPercent!.Value, precision: 1);
        Assert.Contains("throttled", result.VerdictText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("43%", result.VerdictText, StringComparison.Ordinal);
    }

    [Fact]
    public void PowerLimitedCase_SlowClockAndCoolCpu_IsNotThrottled()
    {
        var result = LoadTestAnalyzer.Analyze(GetSamples(1800d, 65d), 4200d, null, 60);

        Assert.False(result.Throttled);
        Assert.True(result.PowerLimited);
        Assert.Contains("power-limited or firmware-limited", result.VerdictText, StringComparison.Ordinal);
        Assert.DoesNotContain("throttled under load", result.VerdictText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CleanCase_HeldClock_IsNotThrottled()
    {
        var result = LoadTestAnalyzer.Analyze(GetSamples(3900d, 88d), 4200d, null, 60);

        Assert.False(result.Throttled);
        Assert.False(result.PowerLimited);
        Assert.Contains("No throttling detected", result.VerdictText, StringComparison.Ordinal);
        Assert.Contains("93%", result.VerdictText, StringComparison.Ordinal);
    }

    [Fact]
    public void AbortCase_TemperatureAbort_KeepsReasonAndSaysSo()
    {
        var result = LoadTestAnalyzer.Analyze(GetSamples(1800d, 96d), 4200d, LoadTestResult.AbortTemperature, 60);

        Assert.Equal(LoadTestResult.AbortTemperature, result.AbortReason);
        Assert.Contains("stopped automatically", result.VerdictText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("96", result.VerdictText, StringComparison.Ordinal);
    }

    [Fact]
    public void AbortCase_SensorLost_SaysNoVerdictCanBeGiven()
    {
        var result = LoadTestAnalyzer.Analyze(GetSamples(3900d, 70d, 5), 4200d, LoadTestResult.AbortSensorLost, 60);

        Assert.Equal(LoadTestResult.AbortSensorLost, result.AbortReason);
        Assert.Contains("sensors stopped reporting", result.VerdictText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AbortCase_Cancelled_SaysResultIsIncomplete()
    {
        var result = LoadTestAnalyzer.Analyze(GetSamples(3900d, 70d, 5), 4200d, LoadTestResult.AbortCancelled, 60);

        Assert.Equal(LoadTestResult.AbortCancelled, result.AbortReason);
        Assert.Contains("cancelled", result.VerdictText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SensorGapCase_GapsAreCountedAndNeverInterpolated()
    {
        var samples = GetSamples(3900d, 70d, 35);
        // Three gap seconds in the middle: no reading at all, recorded as gaps.
        samples[10] = GetSample(null, null, 10, isGap: true);
        samples[11] = GetSample(null, null, 11, isGap: true);
        samples[12] = GetSample(null, null, 12, isGap: true);

        var result = LoadTestAnalyzer.Analyze(samples, 4200d, null, 60);

        Assert.Equal(3, result.GapCount);
        Assert.Equal(3900d, result.AvgClockMHz);
        Assert.Equal(70d, result.PeakTempC);
        Assert.False(result.Throttled);
        Assert.Contains("No throttling detected", result.VerdictText, StringComparison.Ordinal);
    }

    [Theory]
    // clockPct, peakTempC, expected throttled: the boundary is strict on both
    // sides (below 80% AND above 85 C), so exactly 80% or exactly 85 C is not throttling.
    [InlineData(79d, 86d, true)]
    [InlineData(80d, 86d, false)]
    [InlineData(81d, 86d, false)]
    [InlineData(79d, 85d, false)]
    [InlineData(80d, 85d, false)]
    [InlineData(79d, 84d, false)]
    public void Boundary_ClockAndTemperature_BothDirections(double clockPercent, double peakTempC, bool expectedThrottled)
    {
        var ratedMax = 4000d;
        var clock = ratedMax * clockPercent / 100d;

        var result = LoadTestAnalyzer.Analyze(GetSamples(clock, peakTempC), ratedMax, null, 60);

        Assert.Equal(expectedThrottled, result.Throttled);
    }

    [Fact]
    public void NoRatedMax_CannotJudgeThrottling()
    {
        var result = LoadTestAnalyzer.Analyze(GetSamples(1800d, 97d), null, null, 60);

        Assert.False(result.Throttled);
        Assert.False(result.PowerLimited);
        Assert.Null(result.ClockHeldPercent);
        Assert.Contains("Rated clock was not reported", result.VerdictText, StringComparison.Ordinal);
    }

    [Fact]
    public void NoSamples_ReportsNothingToJudge()
    {
        var result = LoadTestAnalyzer.Analyze(Array.Empty<LoadTestSample>(), 4200d, null, 60);

        Assert.False(result.Throttled);
        Assert.Equal(0, result.GapCount);
        Assert.Null(result.PeakTempC);
        Assert.Null(result.AvgClockMHz);
        Assert.Contains("recorded no samples", result.VerdictText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoTemperatureReadings_PeakIsNullAndClockAloneDoesNotThrottle()
    {
        var samples = GetSamples(1800d, 0d).Select(s => s with { CpuTempC = null }).ToList();

        var result = LoadTestAnalyzer.Analyze(samples, 4200d, null, 60);

        Assert.Null(result.PeakTempC);
        Assert.False(result.Throttled);
        Assert.False(result.PowerLimited);
        Assert.Contains("no temperature reading", result.VerdictText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShortTest_JudgesOnAllSamplesNotAnEmptyWindow()
    {
        var result = LoadTestAnalyzer.Analyze(GetSamples(1800d, 97d, 10), 4200d, null, 10);

        Assert.True(result.Throttled);
        Assert.Equal(1800d, result.AvgClockMHz);
    }

    [Fact]
    public void FailedMemoryCheck_IsNotedInTheVerdict()
    {
        var result = LoadTestAnalyzer.Analyze(GetSamples(3900d, 70d), 4200d, null, 60, memoryPatternVerified: false);

        Assert.False(result.MemoryPatternVerified);
        Assert.Contains("memory pattern check did not verify", result.VerdictText, StringComparison.OrdinalIgnoreCase);
    }
}
