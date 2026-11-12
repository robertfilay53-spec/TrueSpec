namespace TrueSpec.Core.History;

using System.Globalization;
using TrueSpec.Core.Health;

/// <summary>Which wearing part a forecast is about.</summary>
public enum ForecastMetric
{
    Battery,
    SsdWear,
}

/// <summary>What a forecast attempt concluded.</summary>
public enum ForecastStatus
{
    /// <summary>Fewer than 3 measured points, or a window under 30 days.</summary>
    NotEnoughData,

    /// <summary>Enough data, but the fitted slope is zero or improving.</summary>
    NoMeasurableDecline,

    /// <summary>A range could be fitted from the measured points.</summary>
    Ready,
}

/// <summary>
/// One measured point on a forecast line. <see cref="Value"/> is null when
/// the snapshot did not report that measurement, so a gap in the line is a
/// gap in the data, never a zero.
/// </summary>
public sealed record ForecastPoint(DateTimeOffset Date, double Value);

/// <summary>
/// A degradation forecast: a range of months until the part reaches its
/// Replace threshold, fitted from measured points only, never a single
/// date. <see cref="ForecastLowMonths"/> is the soonest the threshold is
/// reached (fastest rate within the measurement error) and
/// <see cref="ForecastHighMonths"/> the latest; the high end is null when
/// the slowest rate within the error is flat or improving, so the threshold
/// might never be reached. Both are null when there is no forecast (see
/// <see cref="Status"/>). A forecast is information, not a measurement: it
/// never feeds the score.
/// </summary>
public sealed record DegradationForecast(
    ForecastMetric Metric,
    ForecastStatus Status,
    int PointsUsed,
    double WindowDays,
    double SlopePerDay,
    int? ForecastLowMonths,
    int? ForecastHighMonths,
    double Threshold,
    string BasisText,
    string? StepChangeNote,
    int ScansNeeded)
{
    /// <summary>The threshold is already reached: both ends of the range are zero.</summary>
    public bool AlreadyAtThreshold => Status == ForecastStatus.Ready
        && ForecastLowMonths == 0
        && ForecastHighMonths == 0;
}

/// <summary>
/// Degradation forecasting over saved history (Blueprint 5). Pure function,
/// no hardware access, no I/O, no external data, no model of "typical"
/// drives or batteries: the slope is an ordinary least-squares fit over the
/// measured points, and the range comes from the slope's standard error.
/// </summary>
public static class DegradationForecaster
{
    /// <summary>Minimum measured points for a forecast.</summary>
    public const int MinimumPoints = 3;

    /// <summary>Minimum window, in days, for a forecast.</summary>
    public const double MinimumWindowDays = 30d;

    /// <summary>
    /// A jump of more than this many points between consecutive measured
    /// scans means the part was replaced (or the sensor changed): the
    /// earlier scans no longer describe this part, so the window restarts
    /// at the step.
    /// </summary>
    public const double StepChangePoints = 20d;

    /// <summary>
    /// Minimum range width as a fraction of the midpoint, so a very clean
    /// fit does not pretend to be precise.
    /// </summary>
    public const double MinimumRangeWidthFraction = 0.25d;

    /// <summary>Average days in a month (365.25 / 12).</summary>
    private const double DaysPerMonth = 365.25d / 12d;

    /// <summary>Slopes this small are measurement noise, not a decline.</summary>
    private const double NegligibleSlopePerDay = 1e-9d;

    public static DegradationForecast ForBattery(IReadOnlyList<TrendPoint> points) =>
        Forecast(points, ForecastMetric.Battery);

    public static DegradationForecast ForStorageWear(IReadOnlyList<TrendPoint> points) =>
        Forecast(points, ForecastMetric.SsdWear);

    public static DegradationForecast Forecast(IReadOnlyList<TrendPoint> points, ForecastMetric metric)
    {
        ArgumentNullException.ThrowIfNull(points);

        // Gaps skipped, never zero: only measured, finite values fit.
        // Ordered oldest first even if the caller passed newest first.
        var measured = points
            .Where(p => p.Value is not null && double.IsFinite(p.Value.Value))
            .OrderBy(p => p.CapturedAt)
            .Select(p => new ForecastPoint(p.CapturedAt, p.Value!.Value))
            .ToList();

        var (used, stepChangeNote) = RestartAfterStepChange(measured, metric);
        var windowDays = used.Count < 2
            ? 0d
            : (used[^1].Date - used[0].Date).TotalDays;
        var scansNeeded = Math.Max(0, MinimumPoints - used.Count);

        if (used.Count < MinimumPoints || windowDays < MinimumWindowDays)
        {
            return new DegradationForecast(
                metric,
                ForecastStatus.NotEnoughData,
                used.Count,
                windowDays,
                0d,
                null,
                null,
                ThresholdFor(metric),
                BuildNotEnoughDataText(metric, used.Count, windowDays, scansNeeded, stepChangeNote),
                stepChangeNote,
                scansNeeded);
        }

        var (slopePerDay, slopeStandardError) = LeastSquaresDeclineRate(used, metric);

        if (slopePerDay <= NegligibleSlopePerDay)
        {
            return new DegradationForecast(
                metric,
                ForecastStatus.NoMeasurableDecline,
                used.Count,
                windowDays,
                slopePerDay,
                null,
                null,
                ThresholdFor(metric),
                BuildNoDeclineText(metric, used, windowDays, stepChangeNote),
                stepChangeNote,
                0);
        }

        var threshold = ThresholdFor(metric);
        var latest = used[^1].Value;
        var remaining = metric == ForecastMetric.Battery
            ? latest - threshold
            : threshold - latest;

        if (remaining <= 0d)
        {
            return new DegradationForecast(
                metric,
                ForecastStatus.Ready,
                used.Count,
                windowDays,
                slopePerDay,
                0,
                0,
                threshold,
                BuildAlreadyReachedText(metric, used, windowDays, threshold, stepChangeNote),
                stepChangeNote,
                0);
        }

        var fastRate = slopePerDay + slopeStandardError;
        var slowRate = slopePerDay - slopeStandardError;

        // Low is the soonest the threshold is reached (fastest rate
        // within the error); high is the latest (slowest rate). The
        // clamp works on the raw month values, before whole-month rounding.
        var lowMonths = remaining / fastRate / DaysPerMonth;
        double? highMonths = slowRate <= 0d
            ? null
            : remaining / slowRate / DaysPerMonth;

        if (highMonths is not null)
        {
            var midpoint = (lowMonths + highMonths.Value) / 2d;
            if (highMonths.Value - lowMonths < MinimumRangeWidthFraction * midpoint)
            {
                lowMonths = midpoint * (1d - (MinimumRangeWidthFraction / 2d));
                highMonths = midpoint * (1d + (MinimumRangeWidthFraction / 2d));
            }
        }

        var lowRounded = (int)Math.Round(lowMonths, MidpointRounding.AwayFromZero);
        int? highRounded = highMonths is null
            ? null
            : (int)Math.Round(highMonths.Value, MidpointRounding.AwayFromZero);

        // A positive forecast is never "0 months": under a month still
        // reads as within a month. This clamp runs BEFORE the range
        // widening below: the old order widened first (0 to 1) and then
        // raised the low end to 1, collapsing the range to "1 to 1 months".
        if (lowRounded < 1)
        {
            lowRounded = 1;
        }

        if (highRounded is not null && highRounded.Value <= lowRounded)
        {
            // Whole-month rounding can collapse a short range to one
            // number; keep a visible range.
            highRounded = lowRounded + 1;
        }

        return new DegradationForecast(
            metric,
            ForecastStatus.Ready,
            used.Count,
            windowDays,
            slopePerDay,
            lowRounded,
            highRounded,
            threshold,
            BuildReadyText(metric, used, windowDays, threshold, lowRounded, highRounded, stepChangeNote),
            stepChangeNote,
            0);
    }

    /// <summary>
    /// The Replace threshold for the metric, from the existing band rule:
    /// battery Replace is health below 60% (<see
    /// cref="HardwareMath.BatteryBand"/>), and storage health is 100 minus
    /// wear (<see cref="HardwareMath.StorageHealthPercent"/>), so the same
    /// 60% health boundary is 40% wear. One shared boundary, named in the
    /// output; no second copy of any threshold lives here.
    /// </summary>
    public static double ThresholdFor(ForecastMetric metric) => metric switch
    {
        ForecastMetric.Battery => HardwareMath.ReplaceHealthThresholdPercent,
        ForecastMetric.SsdWear => HardwareMath.StorageReplaceWearThresholdPercent,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown forecast metric."),
    };

    /// <summary>
    /// Finds the last step change (a jump of more than
    /// <see cref="StepChangePoints"/> between consecutive measured scans)
    /// and restarts the window at the scan after it, with a note. Without
    /// this, a replaced part produces a nonsense slope.
    /// </summary>
    private static (IReadOnlyList<ForecastPoint> Used, string? Note) RestartAfterStepChange(
        IReadOnlyList<ForecastPoint> measured, ForecastMetric metric)
    {
        var restartAt = 0;
        string? note = null;
        for (var i = 1; i < measured.Count; i++)
        {
            var jump = measured[i].Value - measured[i - 1].Value;
            if (Math.Abs(jump) <= StepChangePoints)
            {
                continue;
            }

            restartAt = i;
            note = BuildStepChangeNote(metric, measured[i - 1], measured[i], jump);
        }

        return restartAt == 0
            ? (measured, null)
            : (measured.Skip(restartAt).ToList(), note);
    }

    private static string BuildStepChangeNote(
        ForecastMetric metric, ForecastPoint before, ForecastPoint after, double jump)
    {
        var label = MetricLabel(metric);
        var unit = "%";
        var direction = metric == ForecastMetric.Battery
            ? jump > 0d
                ? "jumped"
                : "fell sharply"
            : jump < 0d
                ? "dropped"
                : "jumped";
        // A jump in the replacement direction (battery health up, SSD
        // wear down) reads as a new part; a jump the other way is a sharp
        // discontinuity either way, so the earlier scans no longer
        // describe this part. Both restart the window.
        var looksReplaced = metric == ForecastMetric.Battery ? jump > 0d : jump < 0d;
        var cause = looksReplaced
            ? $"the {PartNoun(metric)} looks replaced"
            : $"the {PartNoun(metric)} changed sharply";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{label} {direction} from {before.Value:0.#}{unit} to {after.Value:0.#}{unit} between scans, so {cause}; forecast restarts here.");
    }

    /// <summary>
    /// Ordinary least-squares fit of value against days since the first
    /// measured point. Returns the decline rate in points per day (positive
    /// when the part is wearing: battery health falling, SSD wear rising)
    /// and the standard error of that rate.
    /// </summary>
    private static (double SlopePerDay, double StandardError) LeastSquaresDeclineRate(
        IReadOnlyList<ForecastPoint> used, ForecastMetric metric)
    {
        var first = used[0].Date;
        var xs = used.Select(p => (p.Date - first).TotalDays).ToList();
        var ys = used.Select(p => p.Value).ToList();
        var n = used.Count;

        var meanX = xs.Average();
        var meanY = ys.Average();
        var sxx = xs.Sum(x => (x - meanX) * (x - meanX));
        var sxy = xs.Zip(ys, (x, y) => (x - meanX) * (y - meanY)).Sum();

        // The window guard already guarantees distinct dates, so Sxx > 0.
        var slope = sxy / sxx;
        var intercept = meanY - (slope * meanX);
        var residualSumSquares = xs.Zip(ys, (x, y) =>
        {
            var residual = y - (intercept + (slope * x));
            return residual * residual;
        }).Sum();
        var standardError = Math.Sqrt(residualSumSquares / (n - 2) / sxx);

        // Battery health falls as the battery wears; SSD wear rises.
        return metric == ForecastMetric.Battery
            ? (-slope, standardError)
            : (slope, standardError);
    }

    private static string MetricLabel(ForecastMetric metric) => metric switch
    {
        ForecastMetric.Battery => "Battery health",
        ForecastMetric.SsdWear => "SSD wear",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown forecast metric."),
    };

    private static string PartNoun(ForecastMetric metric) => metric switch
    {
        ForecastMetric.Battery => "battery",
        ForecastMetric.SsdWear => "drive",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown forecast metric."),
    };

    private static string ThresholdText(ForecastMetric metric, double threshold) => metric switch
    {
        ForecastMetric.Battery => string.Create(
            CultureInfo.InvariantCulture, $"Replace threshold (battery health below {threshold:0.#}%)"),
        ForecastMetric.SsdWear => string.Create(
            CultureInfo.InvariantCulture, $"Replace threshold (storage wear above {threshold:0.#}%)"),
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown forecast metric."),
    };

    private static string WithStepNote(string? stepChangeNote, string text) =>
        stepChangeNote is null ? text : stepChangeNote + " " + text;

    private static string BuildNotEnoughDataText(
        ForecastMetric metric, int pointsUsed, double windowDays, int scansNeeded, string? stepChangeNote)
    {
        var label = MetricLabel(metric);
        var text = scansNeeded > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{label} has {pointsUsed} scan{(pointsUsed == 1 ? string.Empty : "s")} over {windowDays:0.#} days. A forecast needs at least 3 scans over at least 30 days: {scansNeeded} more scan{(scansNeeded == 1 ? string.Empty : "s")} needed.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{label} has {pointsUsed} scans over {windowDays:0.#} days. A forecast needs at least 3 scans over at least 30 days: {Math.Ceiling(MinimumWindowDays - windowDays):0} more day{(Math.Ceiling(MinimumWindowDays - windowDays) == 1d ? string.Empty : "s")} of history needed.");
        return WithStepNote(stepChangeNote, text);
    }

    private static string BuildNoDeclineText(
        ForecastMetric metric, IReadOnlyList<ForecastPoint> used, double windowDays, string? stepChangeNote)
    {
        var label = MetricLabel(metric);
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{label} showed no measurable decline over {used.Count} scans ({windowDays:0.#} days): {used[0].Value:0.#}% to {used[^1].Value:0.#}%. No forecast until a decline is measured.");
        return WithStepNote(stepChangeNote, text);
    }

    private static string BuildAlreadyReachedText(
        ForecastMetric metric, IReadOnlyList<ForecastPoint> used, double windowDays, double threshold, string? stepChangeNote)
    {
        var label = MetricLabel(metric);
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{label} has already reached the {ThresholdText(metric, threshold)}: {used[^1].Value:0.#}% on the latest scan ({used.Count} scans, {windowDays:0.#} days).");
        return WithStepNote(stepChangeNote, text);
    }

    private static string BuildReadyText(
        ForecastMetric metric,
        IReadOnlyList<ForecastPoint> used,
        double windowDays,
        double threshold,
        int lowMonths,
        int? highMonths,
        string? stepChangeNote)
    {
        var label = MetricLabel(metric);
        var change = used[^1].Value - used[0].Value;
        var direction = metric == ForecastMetric.Battery
            ? change < 0d ? "fell" : "rose"
            : change > 0d ? "rose" : "fell";
        var firstSentence = string.Create(
            CultureInfo.InvariantCulture,
            $"{label} {direction} {Math.Abs(change):0.#} points over {used.Count} scans ({windowDays:0.#} days).");
        var secondSentence = highMonths is null
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"At that measured rate, it reaches the {ThresholdText(metric, threshold)} in {lowMonths} month{(lowMonths == 1 ? string.Empty : "s")} at the earliest; at the slowest rate within the measurement error, it might never reach it.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"At that measured rate, it reaches the {ThresholdText(metric, threshold)} in {lowMonths} to {highMonths.Value} months.");
        return WithStepNote(stepChangeNote, firstSentence + " " + secondSentence);
    }
}
