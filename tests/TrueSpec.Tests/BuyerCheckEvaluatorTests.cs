using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

public class BuyerCheckEvaluatorTests
{
    [Fact]
    public void SampleData_FailsBuyerChecksBecauseItIsUsed()
    {
        var result = new BuyerCheckEvaluator().Evaluate(SampleData.All());

        Assert.Equal(BuyerCheckState.Fail, result.OverallState);
        Assert.Contains(result.Checks, c => c.Id == "ssd-wear" && c.State == BuyerCheckState.Fail);
        Assert.Contains(result.Checks, c => c.Id == "battery-cycles" && c.State == BuyerCheckState.Fail);
        Assert.Contains(result.Checks, c => c.Id == "power-on-hours" && c.State == BuyerCheckState.Fail);
        Assert.Contains(result.Checks, c => c.Id == "ram-size" && c.State == BuyerCheckState.Pass);
    }

    [Fact]
    public void CleanMachine_PassesEveryBuyerCheck()
    {
        var today = DateTimeOffset.UtcNow;
        var components = new[]
        {
            System(today),
            Ram("16 GB", "16 GB"),
            Storage("0%", "0"),
            Battery("2"),
        };

        var result = new BuyerCheckEvaluator().Evaluate(components);

        Assert.Equal(BuyerCheckState.Pass, result.OverallState);
        Assert.All(result.Checks, c => Assert.Equal(BuyerCheckState.Pass, c.State));
    }

    [Fact]
    public void MissingReadings_AreNotApplicableNotFailures()
    {
        var result = new BuyerCheckEvaluator().Evaluate(Array.Empty<ComponentSnapshot>());

        Assert.All(result.Checks, c => Assert.Equal(BuyerCheckState.NotApplicable, c.State));
        Assert.Equal(BuyerCheckState.NotApplicable, result.OverallState);
    }

    [Theory]
    [InlineData("32 GB", "32 GB", BuyerCheckState.Pass)]
    [InlineData("16 GB", "32 GB", BuyerCheckState.Fail)]
    public void RamSizeComparison_FlagsMismatch(string actual, string advertised, BuyerCheckState expected)
    {
        var result = new BuyerCheckEvaluator().Evaluate(new[]
        {
            Ram(actual, advertised),
        });

        var check = Assert.Single(result.Checks, c => c.Id == "ram-size");
        Assert.Equal(expected, check.State);
    }

    private static ComponentSnapshot System(DateTimeOffset now) => new(
        "system", "System", "System", "system", "Test",
        null,
        new[]
        {
            new SpecValue("BIOS date", now.AddDays(-20).ToString("yyyy-MM-dd"), "test"),
            new SpecValue("Windows install date", now.AddDays(-20).ToString("yyyy-MM-dd"), "test"),
            new SpecValue("Critical errors (event log)", "0", "test"),
            new SpecValue("Signed drivers only", "Yes", "test"),
        },
        "test",
        now);

    private static ComponentSnapshot Ram(string actual, string advertised) => new(
        "ram", "RAM", "RAM", "ram", "Test", null,
        new[]
        {
            new SpecValue("Total memory", actual, "test"),
            new SpecValue("Advertised memory", advertised, "test"),
        },
        "test",
        DateTimeOffset.Now);

    private static ComponentSnapshot Storage(string wear, string hours) => new(
        "storage", "Storage", "Storage", "storage", "Test", 100d,
        new[]
        {
            new SpecValue("Wear level", wear, "test"),
            new SpecValue("Power-on hours", hours, "test"),
        },
        "test",
        DateTimeOffset.Now);

    private static ComponentSnapshot Battery(string cycles) => new(
        "battery", "Battery", "Battery", "battery", "Test", 100d,
        new[] { new SpecValue("Cycle count", cycles, "test") },
        "test",
        DateTimeOffset.Now);
}
