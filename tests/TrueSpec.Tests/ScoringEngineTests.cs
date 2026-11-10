using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

public class ScoringEngineTests
{
    [Fact]
    public void SampleData_ProducesWeightedScoreOverMeasurableComponents()
    {
        var engine = new ScoringEngine();

        var result = engine.Score(SampleData.All());

        Assert.True(result.HasMeasurableComponents);
        Assert.Equal(93.6d, result.OverallScore, 1);
        Assert.Equal(Verdict.LikeNew, result.Verdict);
        Assert.Equal(85d, result.MeasuredWeight, 1);
        Assert.DoesNotContain(result.Components, c => c.Component == "RAM");
        Assert.DoesNotContain(result.Components, c => c.Component == "Display");
    }

    [Fact]
    public void NotMeasurableComponents_AreExcludedAndWeightsRedistributed()
    {
        var engine = new ScoringEngine();
        var cpu = MakeComponent("CPU", "CPU", 80d);
        var ram = MakeComponent("RAM", "RAM", null);

        var result = engine.Score(new[] { cpu, ram });

        Assert.Single(result.Components);
        Assert.Equal(80d, result.OverallScore, 1);
        Assert.Equal(15d, result.MeasuredWeight, 1);
        Assert.Equal(1d, result.Components[0].NormalizedWeight, 3);
    }

    [Fact]
    public void NoMeasurableComponents_ReturnsNoScoreInsteadOfZero()
    {
        var engine = new ScoringEngine();
        var result = engine.Score(new[] { MakeComponent("RAM", "RAM", null) });

        Assert.False(result.HasMeasurableComponents);
        Assert.Equal(0d, result.OverallScore);
        Assert.Equal(Verdict.NotMeasurable, result.Verdict);
    }

    [Theory]
    [InlineData(95d, Verdict.LikeNew)]
    [InlineData(90d, Verdict.LikeNew)]
    [InlineData(89d, Verdict.UsedHealthy)]
    [InlineData(80d, Verdict.UsedHealthy)]
    [InlineData(79d, Verdict.Worn)]
    [InlineData(60d, Verdict.Worn)]
    [InlineData(59d, Verdict.ReplaceSoon)]
    public void VerdictBands_MatchTheSpec(double score, Verdict expected)
    {
        Assert.Equal(expected, ScoringEngine.VerdictFor(score));
    }

    [Fact]
    public void HealthStatus_UsesTheSameBandsAsTheUi()
    {
        Assert.Equal(HealthStatus.Healthy, HealthStatusExtensions.FromHealth(80d));
        Assert.Equal(HealthStatus.Watch, HealthStatusExtensions.FromHealth(79.9d));
        Assert.Equal(HealthStatus.Watch, HealthStatusExtensions.FromHealth(60d));
        Assert.Equal(HealthStatus.Problem, HealthStatusExtensions.FromHealth(59.9d));
        Assert.Equal(HealthStatus.NotMeasurable, HealthStatusExtensions.FromHealth(null));
    }

    private static ComponentSnapshot MakeComponent(string id, string category, double? health) =>
        new(id, id, category, id, "test", health, Array.Empty<SpecValue>(), "test", DateTimeOffset.Now);
}
