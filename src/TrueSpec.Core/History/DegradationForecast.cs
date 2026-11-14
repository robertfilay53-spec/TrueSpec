namespace TrueSpec.Core.History;

using System.Globalization;
using TrueSpec.Core.Health;
using TrueSpec.Core.Localization;

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

    /// <summary>
    /// Forecasts battery degradation. <paramref name="languageCode"/>
    /// (optional, English when blank or unknown) localizes the basis text
    /// and the step-change note; months, counts and percentages keep the
    /// app's existing formatting. Callers that never pass a language see
    /// the exact English wordings this method always produced.
    /// </summary>
    public static DegradationForecast ForBattery(IReadOnlyList<TrendPoint> points, string? languageCode = null) =>
        Forecast(points, ForecastMetric.Battery, languageCode);

    /// <summary>Forecasts storage wear (see <see cref="ForBattery"/>).</summary>
    public static DegradationForecast ForStorageWear(IReadOnlyList<TrendPoint> points, string? languageCode = null) =>
        Forecast(points, ForecastMetric.SsdWear, languageCode);

    public static DegradationForecast Forecast(IReadOnlyList<TrendPoint> points, ForecastMetric metric, string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        // Gaps skipped, never zero: only measured, finite values fit.
        // Ordered oldest first even if the caller passed newest first.
        var measured = points
            .Where(p => p.Value is not null && double.IsFinite(p.Value.Value))
            .OrderBy(p => p.CapturedAt)
            .Select(p => new ForecastPoint(p.CapturedAt, p.Value!.Value))
            .ToList();

        var (used, stepChangeNote) = RestartAfterStepChange(measured, metric, languageCode);
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
                BuildNotEnoughDataText(metric, used.Count, windowDays, scansNeeded, stepChangeNote, languageCode),
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
                BuildNoDeclineText(metric, used, windowDays, stepChangeNote, languageCode),
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
                BuildAlreadyReachedText(metric, used, windowDays, threshold, stepChangeNote, languageCode),
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
            BuildReadyText(metric, used, windowDays, threshold, lowRounded, highRounded, stepChangeNote, languageCode),
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
        IReadOnlyList<ForecastPoint> measured, ForecastMetric metric, string? languageCode)
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
            note = BuildStepChangeNote(metric, measured[i - 1], measured[i], jump, languageCode);
        }

        return restartAt == 0
            ? (measured, null)
            : (measured.Skip(restartAt).ToList(), note);
    }

    private static string BuildStepChangeNote(
        ForecastMetric metric, ForecastPoint before, ForecastPoint after, double jump, string? languageCode)
    {
        var label = MetricLabel(metric, languageCode);
        var directionKey = metric == ForecastMetric.Battery
            ? jump > 0d
                ? AppStringKeys.HistoryForecastJumped
                : AppStringKeys.HistoryForecastFellSharply
            : jump < 0d
                ? AppStringKeys.HistoryForecastDropped
                : AppStringKeys.HistoryForecastJumped;
        var direction = StringLocalizer.Get(directionKey, languageCode);
        // A jump in the replacement direction (battery health up, SSD
        // wear down) reads as a new part; a jump the other way is a sharp
        // discontinuity either way, so the earlier scans no longer
        // describe this part. Both restart the window.
        var looksReplaced = metric == ForecastMetric.Battery ? jump > 0d : jump < 0d;
        var part = PartNoun(metric, languageCode);
        var cause = looksReplaced
            ? StringLocalizer.GetFormatted(AppStringKeys.HistoryForecastCauseReplaced, languageCode, part)
            : StringLocalizer.GetFormatted(AppStringKeys.HistoryForecastCauseChanged, languageCode, part);
        return StringLocalizer.GetFormatted(
            AppStringKeys.HistoryForecastStepChange,
            languageCode,
            label,
            direction,
            FormatNumber(before.Value),
            FormatNumber(after.Value),
            cause);
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

    private static string MetricLabel(ForecastMetric metric, string? languageCode) => metric switch
    {
        ForecastMetric.Battery => StringLocalizer.Get(AppStringKeys.HistoryBatteryTitle, languageCode),
        ForecastMetric.SsdWear => StringLocalizer.Get(AppStringKeys.HistoryForecastMetricSsdWear, languageCode),
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown forecast metric."),
    };

    /// <summary>
    /// The part noun with its article ("the battery" / "the drive"), so
    /// the step-change cause reads in the language's own article and gender.
    /// </summary>
    private static string PartNoun(ForecastMetric metric, string? languageCode) => metric switch
    {
        ForecastMetric.Battery => StringLocalizer.Get(AppStringKeys.HistoryForecastPartBattery, languageCode),
        ForecastMetric.SsdWear => StringLocalizer.Get(AppStringKeys.HistoryForecastPartStorage, languageCode),
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown forecast metric."),
    };

    private static string ThresholdText(ForecastMetric metric, double threshold, string? languageCode) => metric switch
    {
        ForecastMetric.Battery => StringLocalizer.GetFormatted(
            AppStringKeys.HistoryForecastThresholdBattery, languageCode, FormatNumber(threshold)),
        ForecastMetric.SsdWear => StringLocalizer.GetFormatted(
            AppStringKeys.HistoryForecastThresholdStorage, languageCode, FormatNumber(threshold)),
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown forecast metric."),
    };

    /// <summary>One decimal, invariant culture, for every number passed into a forecast template.</summary>
    private static string FormatNumber(double value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:0.#}");

    private static string WithStepNote(string? stepChangeNote, string text) =>
        stepChangeNote is null ? text : stepChangeNote + " " + text;

    private static string BuildNotEnoughDataText(
        ForecastMetric metric, int pointsUsed, double windowDays, int scansNeeded, string? stepChangeNote, string? languageCode)
    {
        var label = MetricLabel(metric, languageCode);
        var days = FormatNumber(windowDays);
        string text;
        if (scansNeeded > 0)
        {
            text = pointsUsed == 1
                ? StringLocalizer.GetFormatted(AppStringKeys.HistoryForecastNotEnoughUsedOne, languageCode, label, days)
                : scansNeeded == 1
                    ? StringLocalizer.GetFormatted(AppStringKeys.HistoryForecastNotEnoughUsedManyNeededOne, languageCode, label, pointsUsed, days)
                    : StringLocalizer.GetFormatted(AppStringKeys.HistoryForecastNotEnoughUsedManyNeededMany, languageCode, label, pointsUsed, days, scansNeeded);
        }
        else
        {
            var daysNeeded = (int)Math.Ceiling(MinimumWindowDays - windowDays);
            text = daysNeeded == 1
                ? StringLocalizer.GetFormatted(AppStringKeys.HistoryForecastNotEnoughDaysOne, languageCode, label, pointsUsed, days)
                : StringLocalizer.GetFormatted(AppStringKeys.HistoryForecastNotEnoughDaysMany, languageCode, label, pointsUsed, days, daysNeeded);
        }

        return WithStepNote(stepChangeNote, text);
    }

    private static string BuildNoDeclineText(
        ForecastMetric metric, IReadOnlyList<ForecastPoint> used, double windowDays, string? stepChangeNote, string? languageCode)
    {
        var label = MetricLabel(metric, languageCode);
        var text = StringLocalizer.GetFormatted(
            AppStringKeys.HistoryForecastNoDecline,
            languageCode,
            label,
            used.Count,
            FormatNumber(windowDays),
            FormatNumber(used[0].Value),
            FormatNumber(used[^1].Value));
        return WithStepNote(stepChangeNote, text);
    }

    private static string BuildAlreadyReachedText(
        ForecastMetric metric, IReadOnlyList<ForecastPoint> used, double windowDays, double threshold, string? stepChangeNote, string? languageCode)
    {
        var label = MetricLabel(metric, languageCode);
        var text = StringLocalizer.GetFormatted(
            AppStringKeys.HistoryForecastAlreadyReached,
            languageCode,
            label,
            ThresholdText(metric, threshold, languageCode),
            FormatNumber(used[^1].Value),
            used.Count,
            FormatNumber(windowDays));
        return WithStepNote(stepChangeNote, text);
    }

    private static string BuildReadyText(
        ForecastMetric metric,
        IReadOnlyList<ForecastPoint> used,
        double windowDays,
        double threshold,
        int lowMonths,
        int? highMonths,
        string? stepChangeNote,
        string? languageCode)
    {
        var label = MetricLabel(metric, languageCode);
        var change = used[^1].Value - used[0].Value;
        var directionKey = metric == ForecastMetric.Battery
            ? change < 0d ? AppStringKeys.HistoryForecastFell : AppStringKeys.HistoryForecastRose
            : change > 0d ? AppStringKeys.HistoryForecastRose : AppStringKeys.HistoryForecastFell;
        var direction = StringLocalizer.Get(directionKey, languageCode);
        var thresholdText = ThresholdText(metric, threshold, languageCode);
        var firstSentence = StringLocalizer.GetFormatted(
            AppStringKeys.HistoryForecastReadyFirst,
            languageCode,
            label,
            direction,
            FormatNumber(Math.Abs(change)),
            used.Count,
            FormatNumber(windowDays));
        var secondSentence = highMonths is null
            ? lowMonths == 1
                ? StringLocalizer.GetFormatted(AppStringKeys.HistoryForecastReadyOpenOne, languageCode, thresholdText)
                : StringLocalizer.GetFormatted(AppStringKeys.HistoryForecastReadyOpenMany, languageCode, thresholdText, lowMonths)
            : StringLocalizer.GetFormatted(
                AppStringKeys.HistoryForecastReadyRange,
                languageCode,
                thresholdText,
                lowMonths,
                highMonths.Value);
        return WithStepNote(stepChangeNote, firstSentence + " " + secondSentence);
    }
}
