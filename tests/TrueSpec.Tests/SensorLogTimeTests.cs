using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// F6: sensor-log timestamps are stored as UTC in sensor-log.csv, but the
/// History chart must display them in local time. A scan that ran
/// 13:45-13:48 PKT (UTC+5) was shown as 08:45-08:48. These tests pin the
/// conversion with fixed zones, so they hold on any machine regardless of
/// its own time zone.
/// </summary>
public class SensorLogTimeTests
{
    private static TimeZoneInfo FixedZone(string id, TimeSpan offset) =>
        TimeZoneInfo.CreateCustomTimeZone(id, offset, id, id);

    [Fact]
    public void UtcTimestampFormatsAsLocalTimeInPlusFiveZone()
    {
        // The laptop-test case: 08:45 UTC is 13:45 in PKT (UTC+5).
        var timestamp = new DateTimeOffset(2026, 11, 12, 8, 45, 0, TimeSpan.Zero);

        Assert.Equal("13:45", SensorLogTime.FormatLocalTime(timestamp, FixedZone("PKT", TimeSpan.FromHours(5))));
    }

    [Fact]
    public void UtcTimestampFormatsAsLocalTimeInMinusFiveZone()
    {
        var timestamp = new DateTimeOffset(2026, 11, 12, 8, 45, 0, TimeSpan.Zero);

        Assert.Equal("03:45", SensorLogTime.FormatLocalTime(timestamp, FixedZone("EST-fixed", TimeSpan.FromHours(-5))));
    }

    [Fact]
    public void UtcTimestampIsUnchangedInUtcZone()
    {
        var timestamp = new DateTimeOffset(2026, 11, 12, 8, 45, 0, TimeSpan.Zero);

        Assert.Equal("08:45", SensorLogTime.FormatLocalTime(timestamp, TimeZoneInfo.Utc));
    }

    [Fact]
    public void TimestampWithNonUtcOffsetIsConvertedNotTakenAsIs()
    {
        // A timestamp already carrying a +02:00 offset (10:45 local there)
        // is 08:45 UTC, so it is 13:45 in the UTC+5 zone.
        var timestamp = new DateTimeOffset(2026, 11, 12, 10, 45, 0, TimeSpan.FromHours(2));

        Assert.Equal("13:45", SensorLogTime.FormatLocalTime(timestamp, FixedZone("PKT", TimeSpan.FromHours(5))));
    }
}
