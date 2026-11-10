using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

public class BuyerCheckEvaluatorTests
{
    [Fact]
    public void HealthyMachine_WithAdvertisedRam_PassesEveryBuyerCheck()
    {
        var snapshot = TestSnapshots.Healthy();

        Assert.Equal(BuyerCheckState.Pass, snapshot.BuyerChecks.OverallState);
        Assert.All(snapshot.BuyerChecks.Checks, check =>
            Assert.Equal(BuyerCheckState.Pass, check.State));
    }

    [Fact]
    public void MissingReadings_AreNotApplicableNotFailures()
    {
        var result = new BuyerCheckEvaluator().Evaluate(Array.Empty<ComponentSnapshot>());

        Assert.NotEmpty(result.Checks);
        Assert.All(result.Checks, check =>
            Assert.Equal(BuyerCheckState.NotApplicable, check.State));
        Assert.Equal(BuyerCheckState.NotApplicable, result.OverallState);
        Assert.Equal("Not enough data for a verdict", result.BannerText);
    }

    [Fact]
    public void OnePassedCheckAmongMissingReadings_IsAPassNotNotApplicable()
    {
        // Only the RAM check has data and it passes; the rest are missing.
        // One real pass is enough for an overall Pass.
        var result = new BuyerCheckEvaluator().Evaluate(
            new[] { TestSnapshots.Ram("16 GB") },
            new BuyerCheckContext("16 GB"));

        Assert.Equal(BuyerCheckState.Pass, result.Checks.Single(c => c.Id == "ram-size").State);
        Assert.Equal(BuyerCheckState.Pass, result.OverallState);
    }

    [Fact]
    public void UsedDriveAndBattery_FailTheNewLaptopChecks()
    {
        var components = new[]
        {
            TestSnapshots.Storage("9", "120"),
            TestSnapshots.Battery(40),
            TestSnapshots.Ram("16 GB"),
        };

        var result = new BuyerCheckEvaluator().Evaluate(components);

        Assert.Equal(BuyerCheckState.Fail, result.Checks.Single(c => c.Id == "ssd-wear").State);
        Assert.Equal(BuyerCheckState.Fail, result.Checks.Single(c => c.Id == "power-on-hours").State);
        Assert.Equal(BuyerCheckState.Fail, result.Checks.Single(c => c.Id == "battery-cycles").State);
    }

    [Theory]
    [InlineData("16 GB", "16 GB", BuyerCheckState.Pass)]
    [InlineData("16 GB", "32 GB", BuyerCheckState.Fail)]
    [InlineData("16 GB", "16GB", BuyerCheckState.Pass)]
    public void AdvertisedRamComparison_FlagsMismatch(
        string actual,
        string advertised,
        BuyerCheckState expected)
    {
        var result = new BuyerCheckEvaluator().Evaluate(
            new[] { TestSnapshots.Ram(actual) },
            new BuyerCheckContext(advertised));

        var check = Assert.Single(result.Checks, c => c.Id == "ram-size");
        Assert.Equal(expected, check.State);
    }
}
