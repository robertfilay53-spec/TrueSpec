using TrueSpec.Core.Benchmarks;

namespace TrueSpec.Tests;

/// <summary>
/// Pure-function tests for the U5 fan-health rules (plan U5). Every rule is
/// pinned here: 0 RPM is a fault only while the same component is hot, a
/// stopped fan at idle is fan-stop mode and never fires, a flat RPM while
/// the temperature climbs is a fault, a rising RPM is not, and a machine
/// with no fan sensors gets an honest "not reported" state instead of an
/// invented fan. Boundaries are tested in both directions.
/// </summary>
public class FanHealthAnalyzerTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static LoadTestSample Sample(
        int secondOffset,
        double? cpuTempC,
        double? gpuTempC,
        params FanReading[] fans) => new(
        BaseTime.AddSeconds(secondOffset),
        cpuTempC,
        3900d,
        3900d,
        fans.FirstOrDefault()?.Rpm,
        45d,
        gpuTempC,
        false,
        fans);

    /// <summary>One fan at a fixed RPM while the CPU temperature ramps from <paramref name="fromTempC"/> to <paramref name="toTempC"/>.</summary>
    private static List<LoadTestSample> RampSamples(
        string fanName,
        double fanRpm,
        double fromTempC,
        double toTempC,
        int seconds = 10,
        double? idleRpm = null,
        double? idleTempC = null)
    {
        var samples = new List<LoadTestSample>(seconds);
        for (var i = 0; i < seconds; i++)
        {
            var temp = fromTempC + (toTempC - fromTempC) * i / (seconds - 1);
            samples.Add(Sample(i, temp, 55d, new FanReading(fanName, fanRpm)));
        }

        return samples;
    }

    private static FanHealthResult AnalyzeRamp(
        string fanName,
        double fanRpm,
        double fromTempC,
        double toTempC,
        double? idleRpm = null,
        double? idleTempC = null)
    {
        var idleFans = idleRpm is { } rpm
            ? new[] { new FanReading(fanName, rpm) }
            : null;
        return FanHealthAnalyzer.Analyze(
            RampSamples(fanName, fanRpm, fromTempC, toTempC),
            idleFans,
            idleTempC);
    }

    [Fact]
    public void StoppedFanWhileHot_IsFlaggedWithTemperature()
    {
        var result = FanHealthAnalyzer.Analyze(
            RampSamples("CPU Fan #1", 0d, 45d, 81d),
            new[] { new FanReading("CPU Fan #1", 1200d) },
            45d);

        Assert.True(result.HasFault);
        Assert.Single(result.Findings);
        Assert.Contains("CPU Fan #1: 0 RPM at 81 C. This fan is not spinning.", result.Findings[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StoppedFanWhileCool_IsNotFlagged_FanStopMode()
    {
        var result = FanHealthAnalyzer.Analyze(
            RampSamples("CPU Fan #1", 0d, 35d, 45d),
            new[] { new FanReading("CPU Fan #1", 0d) },
            35d);

        Assert.False(result.HasFault);
        Assert.Empty(result.Findings);
        Assert.True(result.HasFanSensors);
    }

    [Fact]
    public void StoppedFan_BoundaryJustBelowHot_IsNotFlagged()
    {
        var result = FanHealthAnalyzer.Analyze(
            RampSamples("CPU Fan #1", 0d, 45d, 69.9d),
            new[] { new FanReading("CPU Fan #1", 1200d) },
            45d);

        Assert.False(result.HasFault);
    }

    [Fact]
    public void StoppedFan_BoundaryAtHot_IsFlagged()
    {
        var result = FanHealthAnalyzer.Analyze(
            RampSamples("CPU Fan #1", 0d, 45d, 70d),
            new[] { new FanReading("CPU Fan #1", 1200d) },
            45d);

        Assert.True(result.HasFault);
    }

    [Fact]
    public void FlatRpmWhileTemperatureClimbs_IsFlagged()
    {
        var result = AnalyzeRamp("CPU Fan #1", 1200d, 45d, 82d, idleRpm: 1200d, idleTempC: 45d);

        Assert.True(result.HasFault);
        Assert.Single(result.Findings);
        Assert.Contains("not speeding up under load", result.Findings[0].Message, StringComparison.Ordinal);
        Assert.Contains("45 C to 82 C", result.Findings[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RpmRisesWithTemperature_IsNotFlagged()
    {
        var samples = new List<LoadTestSample>();
        for (var i = 0; i < 10; i++)
        {
            var temp = 45d + 37d * i / 9;
            var rpm = 1200d + 1200d * i / 9;
            samples.Add(Sample(i, temp, 55d, new FanReading("CPU Fan #1", rpm)));
        }

        var result = FanHealthAnalyzer.Analyze(
            samples,
            new[] { new FanReading("CPU Fan #1", 1200d) },
            45d);

        Assert.False(result.HasFault);
        Assert.Contains("responded to load", result.SummaryText, StringComparison.Ordinal);
    }

    [Fact]
    public void FlatRpmWithSmallTemperatureRise_IsNotFlagged()
    {
        // A 10 C rise is inside normal warm-up noise; rule 2 needs a 20 C rise.
        var result = AnalyzeRamp("CPU Fan #1", 1200d, 45d, 55d, idleRpm: 1200d, idleTempC: 45d);

        Assert.False(result.HasFault);
    }

    [Fact]
    public void NoFanSensors_HonestNotReportedState()
    {
        var samples = new List<LoadTestSample>();
        for (var i = 0; i < 5; i++)
        {
            samples.Add(Sample(i, 80d, 70d));
        }

        var result = FanHealthAnalyzer.Analyze(samples);

        Assert.False(result.HasFanSensors);
        Assert.False(result.HasFault);
        Assert.Empty(result.Fans);
        Assert.Empty(result.Findings);
        Assert.Contains("No fan sensors were reported by hardware", result.SummaryText, StringComparison.Ordinal);
    }

    [Fact]
    public void GpuFan_IsJudgedAgainstGpuTemperature_NotCpu()
    {
        // CPU stays cool while the GPU is hot and its fan is stopped: the
        // GPU fan must fire, and the CPU fan (spinning) must not.
        var samples = new List<LoadTestSample>();
        for (var i = 0; i < 5; i++)
        {
            samples.Add(Sample(i, 45d, 81d,
                new FanReading("CPU Fan #1", 2000d),
                new FanReading("GPU Fan #1", 0d)));
        }

        var result = FanHealthAnalyzer.Analyze(samples);

        Assert.True(result.HasFault);
        Assert.Single(result.Findings);
        Assert.Equal("GPU Fan #1", result.Findings[0].FanName);
    }

    [Fact]
    public void CpuFanStoppedWhileOnlyGpuIsHot_IsNotFlagged()
    {
        var samples = new List<LoadTestSample>();
        for (var i = 0; i < 5; i++)
        {
            samples.Add(Sample(i, 45d, 81d,
                new FanReading("CPU Fan #1", 0d),
                new FanReading("GPU Fan #1", 2500d)));
        }

        var result = FanHealthAnalyzer.Analyze(samples);

        Assert.False(result.HasFault);
    }

    [Fact]
    public void StoppedFan_DoesNotAlsoFireFlatRpmRule()
    {
        var result = FanHealthAnalyzer.Analyze(
            RampSamples("CPU Fan #1", 0d, 45d, 81d),
            new[] { new FanReading("CPU Fan #1", 1200d) },
            45d);

        Assert.Single(result.Findings);
        Assert.Contains("not spinning", result.Findings[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SummaryRows_ReportIdleAndLoadRpm()
    {
        var result = AnalyzeRamp("CPU Fan #1", 2400d, 45d, 82d, idleRpm: 1200d, idleTempC: 45d);

        Assert.Single(result.Fans);
        Assert.Equal(1200d, result.Fans[0].IdleRpm);
        Assert.Equal(2400d, result.Fans[0].LoadRpm);
        Assert.Equal(82d, result.Fans[0].LoadTempC);
        Assert.Contains("idle 1200 RPM", result.Fans[0].DisplayText, StringComparison.Ordinal);
        Assert.Contains("under load 2400 RPM", result.Fans[0].DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public void FanHealth_FlowsThroughLoadTestAnalyzer()
    {
        var result = LoadTestAnalyzer.Analyze(
            RampSamples("CPU Fan #1", 0d, 45d, 81d),
            4200d,
            null,
            60,
            true,
            null,
            new[] { new FanReading("CPU Fan #1", 1200d) },
            45d);

        Assert.NotNull(result.FanHealth);
        Assert.True(result.FanHealth!.HasFault);
        Assert.Contains("not spinning", result.FanHealth.Findings[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoFanSensors_FlowsThroughLoadTestAnalyzer_AsHonestEmptyState()
    {
        var samples = new List<LoadTestSample>();
        for (var i = 0; i < 5; i++)
        {
            samples.Add(Sample(i, 80d, 70d));
        }

        var result = LoadTestAnalyzer.Analyze(samples, 4200d, null, 60);

        Assert.NotNull(result.FanHealth);
        Assert.False(result.FanHealth!.HasFanSensors);
    }
}
