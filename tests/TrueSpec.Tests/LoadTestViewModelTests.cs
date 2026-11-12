using TrueSpec.App.ViewModels;
using TrueSpec.Core.Benchmarks;

namespace TrueSpec.Tests;

/// <summary>
/// v1.4.1 (F13): the rated max and the held % are separate lines on the
/// load-test result card. A known rated max (4100 MHz from WMI) must still
/// show when no clock was measured, instead of the whole line falling
/// back to "Rated clock: not reported by hardware".
/// </summary>
public class LoadTestViewModelTests
{
    [Fact]
    public void RatedMaxKnownButNoClockMeasured_ShowsRatedMaxAndReportsHeldPercentMissing()
    {
        var result = Result(ratedMaxMHz: 4100d, clockHeldPercent: null);

        Assert.Equal("Rated max clock: 4100 MHz", LoadTestViewModel.RatedMaxText(result));
        Assert.Equal("Held % of rated clock: not reported by hardware", LoadTestViewModel.HeldPercentText(result));
    }

    [Fact]
    public void RatedMaxAndHeldPercentBothKnown_ShowsBothLines()
    {
        var result = Result(ratedMaxMHz: 4100d, clockHeldPercent: 97d);

        Assert.Equal("Rated max clock: 4100 MHz", LoadTestViewModel.RatedMaxText(result));
        Assert.Equal("Held 97% of rated clock", LoadTestViewModel.HeldPercentText(result));
    }

    [Fact]
    public void NeitherKnown_ReportsBothLinesAsNotReported()
    {
        var result = Result(ratedMaxMHz: null, clockHeldPercent: null);

        Assert.Equal("Rated max clock: not reported by hardware", LoadTestViewModel.RatedMaxText(result));
        Assert.Equal("Held % of rated clock: not reported by hardware", LoadTestViewModel.HeldPercentText(result));
    }

    private static LoadTestResult Result(double? ratedMaxMHz, double? clockHeldPercent) => new(
        Array.Empty<LoadTestSample>(),
        PeakTempC: 70d,
        AvgClockMHz: null,
        RatedMaxMHz: ratedMaxMHz,
        ClockHeldPercent: clockHeldPercent,
        Throttled: false,
        PowerLimited: false,
        AbortReason: null,
        VerdictText: "Test verdict.",
        DurationSeconds: 60,
        GapCount: 0,
        MemoryPatternVerified: true,
        DiskReadMBps: null,
        StartedAt: DateTimeOffset.UtcNow,
        CompletedAt: DateTimeOffset.UtcNow);
}
