using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Tests for the U6 threshold alert evaluator: a crossing only alerts when
/// it persists for the configured duration, a single-sample spike never
/// alerts, and an episode re-arms only after a measured value falls back
/// below the threshold.
/// </summary>
public class ThresholdAlertEvaluatorTests
{
    private static ThresholdAlertEvaluator CpuEvaluator(double threshold, int durationSeconds)
    {
        var evaluator = new ThresholdAlertEvaluator(
            ThresholdAlertEvaluator.CpuTemperatureLabel,
            "C");
        evaluator.Configure(threshold, durationSeconds);
        return evaluator;
    }

    private static DateTimeOffset At(int hour, int minute, int second) =>
        new(2026, 11, 12, hour, minute, second, TimeSpan.FromHours(2));

    [Fact]
    public void SustainedCrossingProducesTheExactAlertText()
    {
        var evaluator = CpuEvaluator(90, 30);

        Assert.Null(evaluator.Evaluate(95, At(14, 32, 0)));
        Assert.Null(evaluator.Evaluate(96, At(14, 32, 10)));
        Assert.Null(evaluator.Evaluate(94, At(14, 32, 20)));
        var alert = evaluator.Evaluate(93, At(14, 32, 30));

        Assert.NotNull(alert);
        Assert.Equal("CPU temperature exceeded 90 C for 30 seconds at 14:32.", alert.Text);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 14, 32, 30, TimeSpan.FromHours(2)), alert.RaisedAt);
        Assert.Equal(90, alert.Threshold);
        Assert.Equal(30, alert.DurationSeconds);
    }

    [Theory]
    [InlineData(90, 30, "CPU temperature exceeded 90 C for 30 seconds at 09:04.")]
    [InlineData(85.5, 60, "CPU temperature exceeded 85.5 C for 60 seconds at 09:05.")]
    [InlineData(100, 5, "CPU temperature exceeded 100 C for 5 seconds at 09:04.")]
    public void AlertTextFormatsThresholdDurationAndTime(double threshold, int duration, string expected)
    {
        var evaluator = CpuEvaluator(threshold, duration);
        var start = At(9, 4, 0);

        Assert.Null(evaluator.Evaluate(threshold + 1, start));
        ThresholdAlert? alert = null;
        var steps = duration / 5;
        for (var i = 1; i <= steps; i++)
        {
            alert = evaluator.Evaluate(threshold + 1, start.AddSeconds(i * 5));
        }

        Assert.NotNull(alert);
        Assert.Equal(expected, alert.Text);
    }

    [Fact]
    public void SingleSampleSpikeDoesNotAlert()
    {
        var evaluator = CpuEvaluator(90, 30);

        Assert.Null(evaluator.Evaluate(99, At(14, 32, 0)));
        Assert.Null(evaluator.Evaluate(70, At(14, 32, 2)));
        Assert.Null(evaluator.Evaluate(71, At(14, 32, 32)));
        Assert.False(evaluator.EpisodeActive);
        Assert.Empty(evaluator.Alerts);
    }

    [Fact]
    public void BelowThresholdNeverAlerts()
    {
        var evaluator = CpuEvaluator(90, 30);

        for (var i = 0; i < 10; i++)
        {
            Assert.Null(evaluator.Evaluate(89.9, At(14, 32, i * 5)));
        }

        Assert.Empty(evaluator.Alerts);
    }

    [Fact]
    public void ExactlyAtThresholdCountsAsACrossing()
    {
        var evaluator = CpuEvaluator(90, 5);

        Assert.Null(evaluator.Evaluate(90, At(14, 32, 0)));
        var alert = evaluator.Evaluate(90, At(14, 32, 5));

        Assert.NotNull(alert);
        Assert.Equal("CPU temperature exceeded 90 C for 5 seconds at 14:32.", alert.Text);
    }

    [Fact]
    public void NoSecondAlertWhileStillAboveThreshold()
    {
        var evaluator = CpuEvaluator(90, 5);

        evaluator.Evaluate(95, At(14, 32, 0));
        Assert.NotNull(evaluator.Evaluate(95, At(14, 32, 5)));

        for (var i = 1; i <= 6; i++)
        {
            Assert.Null(evaluator.Evaluate(96, At(14, 32, 5 + (i * 5))));
        }

        Assert.Single(evaluator.Alerts);
        Assert.True(evaluator.EpisodeActive);
    }

    [Fact]
    public void AlertReArmsAfterValueFallsBackBelowThreshold()
    {
        var evaluator = CpuEvaluator(90, 5);

        evaluator.Evaluate(95, At(14, 32, 0));
        Assert.NotNull(evaluator.Evaluate(95, At(14, 32, 5)));
        Assert.True(evaluator.EpisodeActive);

        // Falls back: the episode ends and the evaluator re-arms.
        Assert.Null(evaluator.Evaluate(70, At(14, 32, 10)));
        Assert.False(evaluator.EpisodeActive);

        // Rises again: a second, separate alert.
        Assert.Null(evaluator.Evaluate(95, At(14, 32, 20)));
        var second = evaluator.Evaluate(95, At(14, 32, 25));

        Assert.NotNull(second);
        Assert.Equal("CPU temperature exceeded 90 C for 5 seconds at 14:32.", second.Text);
        Assert.Equal(2, evaluator.Alerts.Count);
    }

    [Fact]
    public void GapResetsThePendingTimer()
    {
        var evaluator = CpuEvaluator(90, 30);

        Assert.Null(evaluator.Evaluate(95, At(14, 32, 0)));
        Assert.Null(evaluator.Evaluate(95, At(14, 32, 10)));
        // The sensor stops reporting: the pending crossing cannot be
        // proven to have persisted, so the timer restarts.
        Assert.Null(evaluator.Evaluate(null, At(14, 32, 20)));
        Assert.Null(evaluator.Evaluate(95, At(14, 32, 25)));
        Assert.Null(evaluator.Evaluate(95, At(14, 32, 45)));
        var alert = evaluator.Evaluate(95, At(14, 32, 55));

        Assert.NotNull(alert);
        Assert.Equal("CPU temperature exceeded 90 C for 30 seconds at 14:32.", alert.Text);
    }

    [Fact]
    public void GapDoesNotEndAnActiveEpisode()
    {
        var evaluator = CpuEvaluator(90, 5);

        evaluator.Evaluate(95, At(14, 32, 0));
        Assert.NotNull(evaluator.Evaluate(95, At(14, 32, 5)));

        // Missing readings are not proof the temperature fell back.
        Assert.Null(evaluator.Evaluate(null, At(14, 32, 10)));
        Assert.Null(evaluator.Evaluate(null, At(14, 32, 15)));
        Assert.True(evaluator.EpisodeActive);

        // Still above when the sensor returns: no second alert.
        Assert.Null(evaluator.Evaluate(96, At(14, 32, 20)));
        Assert.Single(evaluator.Alerts);

        // Only a measured value below the threshold re-arms.
        Assert.Null(evaluator.Evaluate(70, At(14, 32, 25)));
        Assert.False(evaluator.EpisodeActive);
    }

    [Fact]
    public void DisabledThresholdNeverAlerts()
    {
        var evaluator = new ThresholdAlertEvaluator(
            ThresholdAlertEvaluator.CpuTemperatureLabel,
            "C");
        evaluator.Configure(null, 30);

        Assert.False(evaluator.IsEnabled);
        for (var i = 0; i < 10; i++)
        {
            Assert.Null(evaluator.Evaluate(120, At(14, 32, i * 5)));
        }

        Assert.Empty(evaluator.Alerts);
    }

    [Fact]
    public void ReconfigureResetsStateAndAppliesTheNewThreshold()
    {
        var evaluator = CpuEvaluator(90, 30);

        Assert.Null(evaluator.Evaluate(95, At(14, 32, 0)));
        evaluator.Configure(80, 5);

        Assert.False(evaluator.EpisodeActive);
        Assert.Null(evaluator.Evaluate(85, At(14, 32, 10)));
        var alert = evaluator.Evaluate(85, At(14, 32, 15));

        Assert.NotNull(alert);
        Assert.Equal("CPU temperature exceeded 80 C for 5 seconds at 14:32.", alert.Text);
    }

    [Fact]
    public void AlertRaisedEventFiresOncePerEpisode()
    {
        var evaluator = CpuEvaluator(90, 5);
        var raised = new List<ThresholdAlert>();
        evaluator.AlertRaised += (_, alert) => raised.Add(alert);

        evaluator.Evaluate(95, At(14, 32, 0));
        evaluator.Evaluate(95, At(14, 32, 5));
        evaluator.Evaluate(95, At(14, 32, 10));

        Assert.Single(raised);
        Assert.Equal("CPU temperature exceeded 90 C for 5 seconds at 14:32.", raised[0].Text);
    }
}
