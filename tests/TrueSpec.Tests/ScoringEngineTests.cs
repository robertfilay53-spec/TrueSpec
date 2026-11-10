using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

public class ScoringEngineTests
{
    [Fact]
    public void Score_IsWeightedAverageOverMeasurableComponents()
    {
        var engine = new ScoringEngine();
        var cpu = MakeComponent("CPU", "CPU", 80d);
        var ram = MakeComponent("RAM", "RAM", 100d);

        var result = engine.Score(new[] { cpu, ram });

        Assert.True(result.HasMeasurableComponents);
        Assert.Equal(90d, result.OverallScore, 1);
        Assert.Equal(Verdict.LikeNew, result.Verdict);
        Assert.Equal(30d, result.MeasuredWeight, 1);
        Assert.Equal(2, result.Components.Count);
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
    public void CombineVerdict_LikeNewScoreAndAllBuyerChecksPass_ReturnsNew()
    {
        var score = ScoreOf(100d);
        var checks = ChecksOf(BuyerCheckState.Pass);

        Assert.Equal(Verdict.New, ScoringEngine.CombineVerdict(score, checks));
    }

    [Fact]
    public void CombineVerdict_LikeNewScoreAndOneBuyerCheckFails_CapsAtUsedHealthy()
    {
        var score = ScoreOf(100d);
        var checks = ChecksOf(BuyerCheckState.Pass, BuyerCheckState.Fail);

        Assert.Equal(Verdict.UsedHealthy, ScoringEngine.CombineVerdict(score, checks));
    }

    [Fact]
    public void CombineVerdict_LikeNewScoreAndOneBuyerCheckWarns_CapsAtUsedHealthy()
    {
        var score = ScoreOf(100d);
        var checks = ChecksOf(BuyerCheckState.Pass, BuyerCheckState.Warning);

        Assert.Equal(Verdict.UsedHealthy, ScoringEngine.CombineVerdict(score, checks));
    }

    [Fact]
    public void CombineVerdict_LikeNewScoreAndNoBuyerData_KeepsLikeNew()
    {
        var score = ScoreOf(100d);
        var checks = ChecksOf(BuyerCheckState.NotApplicable, BuyerCheckState.NotApplicable);

        Assert.Equal(Verdict.LikeNew, ScoringEngine.CombineVerdict(score, checks));
    }

    [Fact]
    public void CombineVerdict_UsedHealthyScoreAndAllBuyerChecksPass_KeepsUsedHealthy()
    {
        var score = ScoreOf(85d);
        var checks = ChecksOf(BuyerCheckState.Pass);

        Assert.Equal(Verdict.UsedHealthy, ScoringEngine.CombineVerdict(score, checks));
    }

    [Fact]
    public void CombineVerdict_LowScoreAndBuyerCheckFails_KeepsTheLowerScoreVerdict()
    {
        var score = ScoreOf(55d);
        var checks = ChecksOf(BuyerCheckState.Fail);

        Assert.Equal(Verdict.ReplaceSoon, ScoringEngine.CombineVerdict(score, checks));
    }

    [Fact]
    public void CombineVerdict_NoMeasurableScore_ReturnsNotMeasurable()
    {
        var score = ScoreResult.Empty;
        var checks = ChecksOf(BuyerCheckState.Pass);

        Assert.Equal(Verdict.NotMeasurable, ScoringEngine.CombineVerdict(score, checks));
    }

    private static ScoreResult ScoreOf(double health) =>
        new ScoringEngine().Score(new[] { MakeComponent("Storage", "Storage", health) });

    private static BuyerCheckResult ChecksOf(params BuyerCheckState[] states) =>
        new(states.Select((state, index) => new BuyerCheck(
            $"check-{index}",
            $"Check {index}",
            "Test detail.",
            state,
            "Nothing to ask.")).ToList());

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
