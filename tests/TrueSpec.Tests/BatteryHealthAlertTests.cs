using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

/// <summary>
/// Battery-health alerts (v1.1): a band drop or a fast fall across saved
/// scans, from the trend data the History page already has. Bands align with
/// HardwareMath (Normal 80-100%, Worn 60-79%, Replace below 60%); no second
/// scale is invented here.
/// </summary>
public class BatteryHealthAlertTests
{
    [Fact]
    public void SteadyNormalHealth_HasNoAlert()
    {
        var trend = Analyze("90.0%", "88.0%", "87.5%");

        Assert.Null(trend.BatteryAlert);
    }

    [Fact]
    public void BandDrop_NormalToWornAlertsWithBothBandsNamed()
    {
        var trend = Analyze("85.0%", "78.0%");

        Assert.NotNull(trend.BatteryAlert);
        Assert.Contains("dropped from Normal (85%) to Worn (78%)", trend.BatteryAlert, StringComparison.Ordinal);
    }

    [Fact]
    public void BandDrop_NormalToReplaceAlertsBandDropAndFastFall()
    {
        var trend = Analyze("85.0%", "55.0%");

        Assert.NotNull(trend.BatteryAlert);
        Assert.Contains("dropped from Normal (85%) to Replace (55%)", trend.BatteryAlert, StringComparison.Ordinal);
        Assert.Contains("fell 30 points", trend.BatteryAlert, StringComparison.Ordinal);
    }

    [Fact]
    public void FastFall_WithinOneBandStillAlerts()
    {
        // Both readings are Worn, so no band crossed, but an 8-point fall in
        // one interval is far above normal scan-to-scan variation.
        var trend = Analyze("78.0%", "70.0%");

        Assert.NotNull(trend.BatteryAlert);
        Assert.Contains("fell 8 points", trend.BatteryAlert, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped from", trend.BatteryAlert, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleMeasuredValue_HasNoAlert()
    {
        var trend = Analyze("55.0%");

        Assert.Null(trend.BatteryAlert);
    }

    [Fact]
    public void NoBatteryHealth_HasNoAlert()
    {
        var trend = HistoryTrendAnalyzer.Analyze(Array.Empty<SystemSnapshot>());

        Assert.Null(trend.BatteryAlert);
    }

    [Fact]
    public void GapInTheMiddle_IsSkippedNeverTreatedAsZero()
    {
        // Middle scan did not report health: the alert compares the first and
        // latest measured values, not the gap.
        var trend = Analyze("85.0%", SpecLabels.NotReportedByHardware, "78.0%");

        Assert.NotNull(trend.BatteryAlert);
        Assert.Contains("dropped from Normal (85%) to Worn (78%)", trend.BatteryAlert, StringComparison.Ordinal);
    }

    [Fact]
    public void AlreadyReplaceOnFirstScan_SlowFallHasNoAlert()
    {
        // No band was crossed (Replace to Replace) and the fall is small:
        // the Battery page already names the Replace band, so the History
        // page has nothing new to say.
        var trend = Analyze("55.0%", "52.0%");

        Assert.Null(trend.BatteryAlert);
    }

    [Fact]
    public void Boundary_Exactly80StaysNormalAndFivePointsIsNotAFastFall()
    {
        // 80% is the bottom of Normal, and a 5-point fall is the threshold
        // itself, not above it: neither rule fires.
        var trend = Analyze("85.0%", "80.0%");

        Assert.Null(trend.BatteryAlert);
    }

    private static HistoryTrend Analyze(params string[] healthValues)
    {
        var snapshots = healthValues
            .Select((health, index) => BatterySnapshot(health, DateTimeOffset.UtcNow.AddDays(index - healthValues.Length)))
            .ToArray();
        return HistoryTrendAnalyzer.Analyze(snapshots);
    }

    private static SystemSnapshot BatterySnapshot(string health, DateTimeOffset capturedAt)
    {
        var components = new[]
        {
            new ComponentSnapshot(
                "battery", "Battery", "Battery", "battery", "Test battery", null,
                new[]
                {
                    new SpecValue("Health", health, "Full charge capacity / design capacity"),
                },
                "Battery health compares full charge capacity with design capacity.",
                capturedAt),
        };
        return new SystemSnapshot(
            Guid.NewGuid(),
            capturedAt,
            "Test Laptop",
            "Windows 11 Pro",
            components,
            ScoreResult.Empty,
            new BuyerCheckEvaluator().Evaluate(components, null));
    }
}
