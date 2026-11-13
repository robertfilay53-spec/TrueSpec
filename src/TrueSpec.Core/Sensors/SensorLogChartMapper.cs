namespace TrueSpec.Core.Sensors;

using TrueSpec.Core.Settings;

/// <summary>One plotted point: horizontal position as a fraction of the chart's time span, and the measured value.</summary>
public sealed record SensorLogChartPoint(double XFraction, double Value);

/// <summary>
/// Maps sensor-log points to chart segments on a time-proportional axis.
/// The History chart used to plot samples by index, so two samples an hour
/// apart (the app was closed, the machine slept) looked adjacent, and a
/// burst of samples a second apart looked stretched: the line's shape lied
/// about time. Segments now carry their position as a fraction of the
/// measured time span, so the horizontal axis is time, not sample count.
/// A segment breaks where the old index chart broke (a sample where the
/// sensor did not report) and where logging was not running at all: a
/// stretch with no rows between two measured samples. The line never
/// connects across a gap of either kind.
/// </summary>
public static class SensorLogChartMapper
{
    /// <summary>
    /// A stretch longer than this between two consecutive measured samples
    /// means logging was not running (the app was closed or the machine
    /// slept): the line breaks there instead of connecting across hours of
    /// no data. Twice the maximum refresh interval (<see
    /// cref="AppSettings.MaxRefreshSeconds"/> = 30 seconds): samples land
    /// at most 30 seconds apart while logging runs, so anything over 60
    /// seconds is a logging gap, not a slow tick.
    /// </summary>
    public const double LoggingGapSeconds = AppSettings.MaxRefreshSeconds * 2d;

    /// <summary>
    /// Builds the chart segments for <paramref name="points"/>: each
    /// segment is a run of measured samples with no sensor gap and no
    /// logging gap inside it, positioned by time. A segment with fewer
    /// than two points cannot draw a line and is dropped, so a lone
    /// measured sample between two gaps renders nothing, exactly as the
    /// old index chart did. Returns no segments for fewer than two
    /// measured points. Never throws on hand-edited input: points are
    /// sorted by time (a row with an out-of-order timestamp cannot invert
    /// the axis), and fractions are clamped to the span.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<SensorLogChartPoint>> BuildSegments(
        IReadOnlyList<SensorLogPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        // Time order, stable: equal timestamps keep file order, and a
        // hand-edited row with an earlier timestamp lands where it
        // belongs in time instead of breaking the axis.
        var ordered = points.OrderBy(p => p.Timestamp).ToList();
        var measured = ordered.Where(p => p.Value is not null).ToList();
        if (measured.Count < 2)
        {
            return Array.Empty<IReadOnlyList<SensorLogChartPoint>>();
        }

        var first = measured[0].Timestamp;
        var last = measured[^1].Timestamp;
        var spanSeconds = (last - first).TotalSeconds;

        // All samples at the same instant (a hand-edited log, or a burst
        // inside one second): time gives no axis, so fall back to even
        // spacing by measured order, the old index behaviour.
        var timeBased = spanSeconds > 0d;

        var segments = new List<List<SensorLogChartPoint>>();
        var current = new List<SensorLogChartPoint>();
        SensorLogPoint? previousMeasured = null;
        var measuredIndex = 0;
        foreach (var point in ordered)
        {
            if (point.Value is null)
            {
                // A sample where the sensor did not report ends the run:
                // the line never connects across it.
                Flush();
                previousMeasured = null;
                continue;
            }

            if (previousMeasured is not null
                && (point.Timestamp - previousMeasured.Timestamp).TotalSeconds > LoggingGapSeconds)
            {
                // Logging was not running between these two samples.
                Flush();
            }

            var fraction = timeBased
                ? (point.Timestamp - first).TotalSeconds / spanSeconds
                : (double)measuredIndex / (measured.Count - 1);
            current.Add(new SensorLogChartPoint(
                Math.Clamp(fraction, 0d, 1d),
                point.Value.Value));
            previousMeasured = point;
            measuredIndex++;
        }

        Flush();
        return segments;

        void Flush()
        {
            if (current.Count >= 2)
            {
                segments.Add(current);
            }

            current = new List<SensorLogChartPoint>();
        }
    }
}
