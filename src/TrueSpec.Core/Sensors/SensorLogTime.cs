using System.Globalization;

namespace TrueSpec.Core.Sensors;

/// <summary>
/// Formats sensor-log timestamps for display. The log stores every sample
/// timestamp in UTC (see <see cref="SensorLogWriter"/>), so a stored time
/// shown as-is reads as UTC; the History chart must show local time
/// instead. The time zone is a parameter (rather than
/// <see cref="TimeZoneInfo.Local"/> baked in) so the conversion is
/// testable with a fixed zone on any machine.
/// </summary>
public static class SensorLogTime
{
    /// <summary>
    /// Formats <paramref name="timestamp"/> as a HH:mm time in
    /// <paramref name="timeZone"/>. A UTC timestamp of 08:45 formats as
    /// 13:45 in a UTC+5 zone.
    /// </summary>
    public static string FormatLocalTime(DateTimeOffset timestamp, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        return TimeZoneInfo.ConvertTime(timestamp, timeZone).ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
