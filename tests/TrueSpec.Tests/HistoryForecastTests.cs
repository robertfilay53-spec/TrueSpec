using TrueSpec.Core.History;

namespace TrueSpec.Tests;

/// <summary>
/// U4 degradation forecasting (Blueprint 5): a pure least-squares fit over
/// measured points only, reported as a range, never a single date. The
/// fixtures are synthetic TrendPoint lists so the expected range is
/// hand-computable; no fixture invents a "typical" drive or battery.
/// </summary>
public class HistoryForecastTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>TrendPoint factory: (day offset from a fixed start, value or gap).</summary>
    private static IReadOnlyList<TrendPoint> Points(params (int Day, double? Value)[] points) =>
        points.Select(p => new TrendPoint(Start.AddDays(p.Day), p.Value)).ToList();

    /// <summary>Steady battery decline: 1 point every 30 days, 90 to 87 over 90 days.</summary>
    private static IReadOnlyList<TrendPoint> LinearBatteryDecline() =>
        Points((0, 90d), (30, 89d), (60, 88d), (90, 87d));

    /// <summary>Steady SSD wear rise: 2 points every 30 days, 2% to 8% over 90 days.</summary>
    private static IReadOnlyList<TrendPoint> LinearWearRise() =>
        Points((0, 2d), (30, 4d), (60, 6d), (90, 8d));

    [Theory]
    [MemberData(nameof(NotEnoughDataCases))]
    public void NotEnoughData_RefusesWithScansStillNeeded(
        IReadOnlyList<TrendPoint> points, ForecastMetric metric, int scansNeeded)
    {
        var forecast = DegradationForecaster.Forecast(points, metric);

        Assert.Equal(ForecastStatus.NotEnoughData, forecast.Status);
        Assert.Null(forecast.ForecastLowMonths);
        Assert.Null(forecast.ForecastHighMonths);
        Assert.Equal(scansNeeded, forecast.ScansNeeded);
        Assert.Contains("at least 3 scans", forecast.BasisText, StringComparison.Ordinal);
        Assert.Contains("at least 30 days", forecast.BasisText, StringComparison.Ordinal);
    }

    public static TheoryData<IReadOnlyList<TrendPoint>, ForecastMetric, int> NotEnoughDataCases() => new()
    {
        // Two points over 60 days: one more scan is needed.
        { Points((0, 90d), (60, 88d)), ForecastMetric.Battery, 1 },
        // Three points but only 29 days of window: no more scans needed,
        // more days are. The text still says how many more days.
        { Points((0, 90d), (14, 89d), (29, 88d)), ForecastMetric.Battery, 0 },
        // A single scan can never produce a forecast.
        { Points((0, 90d)), ForecastMetric.Battery, 2 },
        // No measured points at all: three scans are still needed.
        { Points((0, null), (30, null)), ForecastMetric.SsdWear, 3 },
        // Empty history.
        { Array.Empty<TrendPoint>(), ForecastMetric.Battery, 3 },
    };

    [Fact]
    public void Window29Days_RefusalNamesTheDaysStillNeeded()
    {
        var forecast = DegradationForecaster.ForBattery(Points((0, 90d), (14, 89d), (29, 88d)));

        Assert.Equal(ForecastStatus.NotEnoughData, forecast.Status);
        Assert.Contains("1 more day", forecast.BasisText, StringComparison.Ordinal);
    }

    [Fact]
    public void Window30Days_AcceptsExactlyAtTheBoundary()
    {
        var forecast = DegradationForecaster.ForBattery(Points((0, 90d), (15, 89d), (30, 88d)));

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(3, forecast.PointsUsed);
        Assert.Equal(30d, forecast.WindowDays);
        // 28 points above the 60% threshold at 1 point per 15 days:
        // 420 days is 13.8 months, widened to 12 to 16 by the clamp.
        Assert.Equal(12, forecast.ForecastLowMonths);
        Assert.Equal(16, forecast.ForecastHighMonths);
    }

    [Theory]
    [MemberData(nameof(NoMeasurableDeclineCases))]
    public void NoMeasurableDecline_SaysSoInsteadOfForecasting(
        IReadOnlyList<TrendPoint> points, ForecastMetric metric)
    {
        var forecast = DegradationForecaster.Forecast(points, metric);

        Assert.Equal(ForecastStatus.NoMeasurableDecline, forecast.Status);
        Assert.Null(forecast.ForecastLowMonths);
        Assert.Null(forecast.ForecastHighMonths);
        Assert.Contains("no measurable decline", forecast.BasisText, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<IReadOnlyList<TrendPoint>, ForecastMetric> NoMeasurableDeclineCases() => new()
    {
        // Perfectly flat battery health over 90 days.
        { Points((0, 85d), (30, 85d), (60, 85d), (90, 85d)), ForecastMetric.Battery },
        // Battery health improving: a new battery, or a recalibrated sensor.
        { Points((0, 80d), (30, 82d), (60, 84d), (90, 85d)), ForecastMetric.Battery },
        // SSD wear falling: the drive was replaced, or the counter changed.
        // (A fall under 20 points is not a step change; it is just no decline.)
        { Points((0, 10d), (30, 8d), (60, 6d), (90, 5d)), ForecastMetric.SsdWear },
    };

    [Fact]
    public void LinearBatteryDecline_ProducesTheExpectedRange()
    {
        var forecast = DegradationForecaster.ForBattery(LinearBatteryDecline());

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(ForecastMetric.Battery, forecast.Metric);
        Assert.Equal(4, forecast.PointsUsed);
        Assert.Equal(90d, forecast.WindowDays);
        // Ordinary least squares over 90, 89, 88, 87: exactly 1 point
        // of decline per 30 days.
        Assert.Equal(1d / 30d, forecast.SlopePerDay, 6);
        // 27 points above the Replace threshold (battery below 60%) at
        // that rate is 810 days, 26.6 months; a perfect fit has zero
        // standard error, so the minimum-width clamp widens it to 23-30.
        Assert.Equal(60d, forecast.Threshold);
        Assert.Equal(23, forecast.ForecastLowMonths);
        Assert.Equal(30, forecast.ForecastHighMonths);
        Assert.Contains("Battery health fell 3 points", forecast.BasisText, StringComparison.Ordinal);
        Assert.Contains("4 scans (90 days)", forecast.BasisText, StringComparison.Ordinal);
        Assert.Contains("23 to 30 months", forecast.BasisText, StringComparison.Ordinal);
        Assert.Contains("Replace threshold (battery health below 60%)", forecast.BasisText, StringComparison.Ordinal);
    }

    [Fact]
    public void LinearWearRise_ProducesTheExpectedRange()
    {
        var forecast = DegradationForecaster.ForStorageWear(LinearWearRise());

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(ForecastMetric.SsdWear, forecast.Metric);
        Assert.Equal(4, forecast.PointsUsed);
        Assert.Equal(90d, forecast.WindowDays);
        Assert.Equal(2d / 30d, forecast.SlopePerDay, 6);
        // Storage health is 100 minus wear, so the shared 60% Replace
        // boundary is 40% wear. 32 points below it at 2 points per 30
        // days is 480 days, 15.8 months, widened to 14-18 by the clamp.
        Assert.Equal(40d, forecast.Threshold);
        Assert.Equal(14, forecast.ForecastLowMonths);
        Assert.Equal(18, forecast.ForecastHighMonths);
        Assert.Contains("SSD wear rose 6 points", forecast.BasisText, StringComparison.Ordinal);
        Assert.Contains("4 scans (90 days)", forecast.BasisText, StringComparison.Ordinal);
        Assert.Contains("14 to 18 months", forecast.BasisText, StringComparison.Ordinal);
        Assert.Contains("Replace threshold (storage wear above 40%)", forecast.BasisText, StringComparison.Ordinal);
    }

    [Fact]
    public void GapInTheMiddle_IsSkippedNeverZero()
    {
        // The day-30 scan did not report battery health: the fit uses the
        // three measured points, and the window still spans 90 days.
        var forecast = DegradationForecaster.ForBattery(
            Points((0, 90d), (30, null), (60, 88d), (90, 87d)));

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(3, forecast.PointsUsed);
        Assert.Equal(90d, forecast.WindowDays);
        Assert.DoesNotContain("0%", forecast.BasisText, StringComparison.Ordinal);
    }

    [Fact]
    public void MinimumRangeWidthClamp_CleanFitDoesNotPretendToBePrecise()
    {
        var forecast = DegradationForecaster.ForBattery(LinearBatteryDecline());

        Assert.NotNull(forecast.ForecastLowMonths);
        Assert.NotNull(forecast.ForecastHighMonths);
        var midpoint = (forecast.ForecastLowMonths!.Value + forecast.ForecastHighMonths!.Value) / 2d;
        var width = forecast.ForecastHighMonths.Value - forecast.ForecastLowMonths.Value;
        Assert.True(width >= 0.25d * midpoint, $"Range width {width} months is under 25% of midpoint {midpoint}.");
    }

    [Fact]
    public void StepChange_BatteryReplacement_RestartsTheWindow()
    {
        // Health 70, 68, then a jump to 96: the battery was replaced
        // between the day-30 and day-60 scans. Only the three points
        // after the step describe the new battery.
        var forecast = DegradationForecaster.ForBattery(
            Points((0, 70d), (30, 68d), (60, 96d), (90, 95d), (120, 94d)));

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(3, forecast.PointsUsed);
        Assert.Equal(60d, forecast.WindowDays);
        Assert.Equal(1d / 30d, forecast.SlopePerDay, 6);
        Assert.NotNull(forecast.StepChangeNote);
        Assert.Contains("replaced", forecast.StepChangeNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("forecast restarts here", forecast.StepChangeNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("replaced", forecast.BasisText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StepChange_SsdReplacement_RestartsTheWindow()
    {
        // Wear 30%, 32%, then a drop to 2%: the drive was replaced
        // between the day-30 and day-60 scans.
        var forecast = DegradationForecaster.ForStorageWear(
            Points((0, 30d), (30, 32d), (60, 2d), (90, 3d), (120, 4d)));

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(3, forecast.PointsUsed);
        Assert.Equal(60d, forecast.WindowDays);
        Assert.Equal(1d / 30d, forecast.SlopePerDay, 6);
        Assert.NotNull(forecast.StepChangeNote);
        Assert.Contains("replaced", forecast.StepChangeNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StepChange_LeavesTooFewPoints_RefusesWithTheNoteKept()
    {
        // The replacement is detected, but only two points describe the
        // new battery, so there is still no forecast. The note survives:
        // the user should know why the old history no longer counts.
        var forecast = DegradationForecaster.ForBattery(
            Points((0, 70d), (30, 68d), (60, 96d), (90, 95d)));

        Assert.Equal(ForecastStatus.NotEnoughData, forecast.Status);
        Assert.Equal(1, forecast.ScansNeeded);
        Assert.NotNull(forecast.StepChangeNote);
        Assert.Contains("replaced", forecast.BasisText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AlreadyAtThreshold_ReportsZeroMonthsNotANegativeRange()
    {
        var forecast = DegradationForecaster.ForBattery(
            Points((0, 62d), (30, 60d), (60, 58d), (90, 57d)));

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(0, forecast.ForecastLowMonths);
        Assert.Equal(0, forecast.ForecastHighMonths);
        Assert.Contains("already reached", forecast.BasisText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoisyFit_SlowestRateMayNeverReachThreshold()
    {
        // Health wobbles down slowly with large scan-to-scan noise, so
        // the slope's standard error is bigger than the slope itself:
        // the fast end of the range is still a number, but the slow end
        // is unbounded, and the text says so instead of inventing one.
        var forecast = DegradationForecaster.ForBattery(
            Points((0, 90d), (15, 87d), (30, 89.5d), (45, 86.5d), (60, 89d), (75, 86d), (90, 88.5d)));

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.NotNull(forecast.ForecastLowMonths);
        Assert.Null(forecast.ForecastHighMonths);
        Assert.Contains("might never reach", forecast.BasisText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsortedInput_IsOrderedBeforeFitting()
    {
        var newestFirst = LinearBatteryDecline().Reverse().ToList();

        var forecast = DegradationForecaster.ForBattery(newestFirst);

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(23, forecast.ForecastLowMonths);
        Assert.Equal(30, forecast.ForecastHighMonths);
    }

    [Fact]
    public void NullInput_ThrowsLikeTheTrendAnalyzer()
    {
        Assert.Throws<ArgumentNullException>(() => DegradationForecaster.ForBattery(null!));
    }
}
