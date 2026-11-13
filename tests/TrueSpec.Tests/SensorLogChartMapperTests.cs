using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// The History sensor-log chart is time-proportional: the horizontal axis
/// is time, not sample count. These tests pin the mapping (Core, both
/// legs): even and uneven spacing, sensor gaps, logging gaps, and the
/// degenerate inputs a hand-edited log can produce.
/// </summary>
public class SensorLogChartMapperTests
{
    private static readonly DateTimeOffset Base =
        new(2026, 11, 12, 8, 45, 0, TimeSpan.Zero);

    private static SensorLogPoint At(double seconds, double? value) =>
        new(Base.AddSeconds(seconds), value);

    [Fact]
    public void EvenSpacing_MapsToEvenFractions()
    {
        var segments = SensorLogChartMapper.BuildSegments(new[]
        {
            At(0, 70), At(10, 71), At(20, 72),
        });

        var only = Assert.Single(segments);
        Assert.Equal(3, only.Count);
        Assert.Equal(0d, only[0].XFraction);
        Assert.Equal(0.5d, only[1].XFraction);
        Assert.Equal(1d, only[2].XFraction);
        Assert.Equal(70d, only[0].Value);
        Assert.Equal(71d, only[1].Value);
        Assert.Equal(72d, only[2].Value);
    }

    [Fact]
    public void UnevenSpacing_IsTimeProportionalNotIndexProportional()
    {
        // By index these would be 0, 0.5, 1; by time the middle sample
        // sits one third of the way across (10 of 30 seconds).
        var segments = SensorLogChartMapper.BuildSegments(new[]
        {
            At(0, 70), At(10, 71), At(30, 72),
        });

        var only = Assert.Single(segments);
        Assert.Equal(0d, only[0].XFraction);
        Assert.Equal(10d / 30d, only[1].XFraction, precision: 10);
        Assert.Equal(1d, only[2].XFraction);
    }

    [Fact]
    public void NullValue_BreaksSegmentAndLonePointIsDropped()
    {
        // The 70 stands alone before the gap, so it renders nothing;
        // only the run after the gap draws.
        var segments = SensorLogChartMapper.BuildSegments(new[]
        {
            At(0, 70), At(10, null), At(20, 72), At(30, 73),
        });

        var only = Assert.Single(segments);
        Assert.Equal(2, only.Count);
        Assert.Equal(20d / 30d, only[0].XFraction, precision: 10);
        Assert.Equal(72d, only[0].Value);
        Assert.Equal(1d, only[1].XFraction);
        Assert.Equal(73d, only[1].Value);
    }

    [Fact]
    public void LoggingGap_BreaksSegment()
    {
        // Two measured samples 120 seconds apart with no rows between
        // them: logging was not running, so each stands alone and
        // neither draws.
        var segments = SensorLogChartMapper.BuildSegments(new[]
        {
            At(0, 70), At(120, 72),
        });

        Assert.Empty(segments);
    }

    [Fact]
    public void LoggingGap_AfterTheGapStartsANewSegment()
    {
        var segments = SensorLogChartMapper.BuildSegments(new[]
        {
            At(0, 70), At(10, 71), At(200, 72), At(210, 73),
        });

        Assert.Equal(2, segments.Count);
        Assert.Equal(2, segments[0].Count);
        Assert.Equal(2, segments[1].Count);
        Assert.Equal(70d, segments[0][0].Value);
        Assert.Equal(71d, segments[0][1].Value);
        Assert.Equal(72d, segments[1][0].Value);
        Assert.Equal(73d, segments[1][1].Value);
        // Fractions are over the whole measured span (0 to 210 seconds).
        Assert.Equal(0d, segments[0][0].XFraction);
        Assert.Equal(200d / 210d, segments[1][0].XFraction, precision: 10);
        Assert.Equal(1d, segments[1][1].XFraction);
    }

    [Theory]
    [InlineData(60d, 1)]
    [InlineData(61d, 2)]
    public void LoggingGapBoundary_SixtySecondsDoesNotBreak(double gapSeconds, int expectedSegments)
    {
        // Exactly twice the maximum 30-second refresh interval is still
        // a slow tick; one second past it is a logging gap.
        var segments = SensorLogChartMapper.BuildSegments(new[]
        {
            At(0, 70), At(10, 71), At(10 + gapSeconds, 72), At(20 + gapSeconds, 73),
        });

        Assert.Equal(expectedSegments, segments.Count);
    }

    [Fact]
    public void FewerThanTwoMeasuredPoints_ReturnsNoSegments()
    {
        Assert.Empty(SensorLogChartMapper.BuildSegments(Array.Empty<SensorLogPoint>()));
        Assert.Empty(SensorLogChartMapper.BuildSegments(new[] { At(0, 70) }));
        Assert.Empty(SensorLogChartMapper.BuildSegments(new[] { At(0, 70), At(10, null) }));
        Assert.Empty(SensorLogChartMapper.BuildSegments(new[] { At(0, null), At(10, null) }));
    }

    [Fact]
    public void AllTimestampsEqual_FallsBackToIndexSpacing()
    {
        // Time gives no axis when every sample shares one instant, so
        // the points spread evenly by measured order instead.
        var segments = SensorLogChartMapper.BuildSegments(new[]
        {
            At(0, 70), At(0, 71), At(0, 72),
        });

        var only = Assert.Single(segments);
        Assert.Equal(0d, only[0].XFraction);
        Assert.Equal(0.5d, only[1].XFraction);
        Assert.Equal(1d, only[2].XFraction);
    }

    [Fact]
    public void UnsortedInput_IsSortedByTime()
    {
        // A hand-edited row with an out-of-order timestamp cannot invert
        // the axis: the points land in time order.
        var segments = SensorLogChartMapper.BuildSegments(new[]
        {
            At(20, 72), At(0, 70), At(10, 71),
        });

        var only = Assert.Single(segments);
        Assert.Equal(70d, only[0].Value);
        Assert.Equal(71d, only[1].Value);
        Assert.Equal(72d, only[2].Value);
        Assert.Equal(0d, only[0].XFraction);
        Assert.Equal(0.5d, only[1].XFraction);
        Assert.Equal(1d, only[2].XFraction);
    }

    [Fact]
    public void ValuesPassThroughExactly()
    {
        var segments = SensorLogChartMapper.BuildSegments(new[]
        {
            At(0, 70.25), At(30, 71.5),
        });

        var only = Assert.Single(segments);
        Assert.Equal(70.25d, only[0].Value);
        Assert.Equal(71.5d, only[1].Value);
    }
}
