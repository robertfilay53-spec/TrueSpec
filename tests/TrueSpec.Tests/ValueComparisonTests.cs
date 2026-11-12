using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

/// <summary>
/// U9 value comparison: buyer-typed asking prices against the measured
/// health score from <see cref="MachineHistoryComparer"/> (which keeps its
/// "no verdict" stance: the verdict lives only in this layer). The score is
/// measured, the price is user input, and the per-currency figure is
/// arithmetic labelled as an estimate. Prices are never scraped, fetched or
/// assumed, and never stored in the snapshot.
/// </summary>
public class ValueComparisonTests
{
    private static MachineComparison Comparison(double? scoreA, double? scoreB) => new(
        new MachineSideSummary("Machine A", 2, null, null, scoreA, null, null, null, null),
        new MachineSideSummary("Machine B", 2, null, null, scoreB, null, null, null, null));

    private static SystemSnapshot SnapshotWithComponents(
        string machineName,
        params (string Id, string Name, double? Health)[] components)
    {
        var now = DateTimeOffset.UtcNow;
        return new SystemSnapshot(
            Guid.NewGuid(),
            now,
            machineName,
            "Windows 11 Pro",
            components.Select(c => new ComponentSnapshot(
                c.Id, c.Name, c.Name, c.Id, c.Name, c.Health,
                Array.Empty<SpecValue>(), "Test component.", now)).ToList(),
            ScoreResult.Empty,
            new BuyerCheckResult(Array.Empty<BuyerCheck>()));
    }

    [Fact]
    public void Compare_SpecExample_ReturnsExactVerdictSentence()
    {
        // The v1.5 plan's own example: B scores 82 vs 71 and costs $120 less.
        var result = ValueComparer.Compare(Comparison(71d, 82d), 620m, 500m);

        Assert.Equal(ValueWinner.MachineB, result.BetterBuy);
        Assert.Equal(ValueWinner.MachineB, result.CheaperMachine);
        Assert.Equal(120m, result.PriceDifference);
        Assert.Equal(
            "Machine B scores 82 vs 71 and costs $120 less. On measured health per dollar, B is the better buy.",
            result.VerdictSentence);
        Assert.Null(result.NoVerdictReason);
    }

    [Fact]
    public void Compare_WinnerCostsMore_SaysCostsMore()
    {
        // B is healthier per dollar (90/600 = 0.15) than A (60/500 = 0.12),
        // even though B costs $100 more outright.
        var result = ValueComparer.Compare(Comparison(60d, 90d), 500m, 600m);

        Assert.Equal(ValueWinner.MachineB, result.BetterBuy);
        Assert.Equal(ValueWinner.MachineA, result.CheaperMachine);
        Assert.Equal(
            "Machine B scores 90 vs 60 and costs $100 more. On measured health per dollar, B is the better buy.",
            result.VerdictSentence);
    }

    [Fact]
    public void Compare_EqualPrices_SaysCostsTheSame()
    {
        var result = ValueComparer.Compare(Comparison(82.5, 90d), 500m, 500m);

        Assert.Equal(ValueWinner.MachineB, result.BetterBuy);
        Assert.Equal(ValueWinner.Tie, result.CheaperMachine);
        Assert.Equal(0m, result.PriceDifference);
        Assert.Equal(
            "Machine B scores 90 vs 82.5 and costs the same. On measured health per dollar, B is the better buy.",
            result.VerdictSentence);
    }

    [Fact]
    public void Compare_EqualHealthPerDollar_ReturnsTieSentence()
    {
        // 80/400 and 100/500 are the same health per dollar.
        var result = ValueComparer.Compare(Comparison(80d, 100d), 400m, 500m);

        Assert.Equal(ValueWinner.Tie, result.BetterBuy);
        Assert.Equal(
            "Machine A scores 80 vs 100 and costs $100 less. On measured health per dollar, the two machines are tied.",
            result.VerdictSentence);
        Assert.Null(result.NoVerdictReason);
    }

    [Fact]
    public void Compare_MissingScore_HasNoVerdictAndNamesTheGap()
    {
        var result = ValueComparer.Compare(Comparison(null, 82d), 500m, 500m);

        Assert.Null(result.VerdictSentence);
        Assert.Equal(ValueWinner.NotDecidable, result.BetterBuy);
        Assert.Contains("measured health score", result.NoVerdictReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_MissingPrice_HasNoVerdictAndNamesTheGap()
    {
        var result = ValueComparer.Compare(Comparison(71d, 82d), 500m, null);

        Assert.Null(result.VerdictSentence);
        Assert.Equal(ValueWinner.NotDecidable, result.BetterBuy);
        Assert.Contains("asking price", result.NoVerdictReason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Compare_NonPositivePrice_IsTreatedAsNoPrice(decimal price)
    {
        var result = ValueComparer.Compare(Comparison(71d, 82d), price, 500m);

        Assert.Null(result.VerdictSentence);
        Assert.Null(result.ScorePerCurrencyA);
        Assert.False(result.MachineA.HasPrice);
    }

    [Fact]
    public void Compare_PerCurrency_IsArithmeticOnMeasuredScoreAndTypedPrice()
    {
        var result = ValueComparer.Compare(Comparison(71d, 82d), 620m, 500m);

        Assert.Equal(71d / 620d, result.ScorePerCurrencyA!.Value, 9);
        Assert.Equal(82d / 500d, result.ScorePerCurrencyB!.Value, 9);
        // The per-currency figure is arithmetic, never a measurement.
        Assert.True(result.IsEstimate);
    }

    [Fact]
    public void Compare_NoInputs_HasNoPerCurrencyFigureAndIsNotAnEstimate()
    {
        var result = ValueComparer.Compare(Comparison(null, null), null, null);

        Assert.Null(result.ScorePerCurrencyA);
        Assert.Null(result.ScorePerCurrencyB);
        Assert.False(result.IsEstimate);
        Assert.Null(result.PriceDifference);
        Assert.Equal(ValueWinner.NotDecidable, result.CheaperMachine);
    }

    [Fact]
    public void Compare_CurrencySymbol_IsUsedInTheVerdict()
    {
        var result = ValueComparer.Compare(Comparison(71d, 82d), 620m, 500m, "£");

        Assert.Contains("£120 less", result.VerdictSentence, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_DecimalPrices_KeepTheirCentsInTheDifference()
    {
        var result = ValueComparer.Compare(Comparison(71d, 82d), 500.50m, 400.25m);

        Assert.Equal(100.25m, result.PriceDifference);
        Assert.Contains("$100.25 less", result.VerdictSentence, StringComparison.Ordinal);
    }

    [Fact]
    public void ComponentWinners_SharedComponents_DecidedOnMeasuredHealth()
    {
        var latestA = SnapshotWithComponents(
            "Machine A",
            ("cpu", "CPU", 95d),
            ("battery", "Battery", 90d),
            ("storage", "Storage", 100d),
            ("ram", "RAM", null),
            ("gpu", "GPU", 80d));
        var latestB = SnapshotWithComponents(
            "Machine B",
            ("cpu", "CPU", 80d),
            ("battery", "Battery", 90d),
            ("storage", "Storage", 50d),
            ("ram", "RAM", 100d));

        var result = ValueComparer.Compare(
            Comparison(80d, 70d), 500m, 500m, "$", latestA, latestB);

        Assert.Equal(4, result.ComponentWinners.Count);
        Assert.Equal(ComponentWinner.MachineA, Winner("CPU"));
        Assert.Equal(ComponentWinner.Tie, Winner("Battery"));
        Assert.Equal(ComponentWinner.MachineA, Winner("Storage"));
        // Measurable on one side only: no winner is crowned on missing data.
        Assert.Equal(ComponentWinner.NotMeasurable, Winner("RAM"));
        // Present on one machine only: not comparable, not listed.
        Assert.DoesNotContain(result.ComponentWinners, w => w.ComponentName == "GPU");
        Assert.Equal(95d, result.ComponentWinners.First(w => w.ComponentName == "CPU").HealthA);
        Assert.Equal(80d, result.ComponentWinners.First(w => w.ComponentName == "CPU").HealthB);

        ComponentWinner Winner(string name) =>
            result.ComponentWinners.First(w => w.ComponentName == name).Winner;
    }

    [Fact]
    public void ComponentWinners_HealthGapWithinThreshold_IsATie()
    {
        var latestA = SnapshotWithComponents("Machine A", ("cpu", "CPU", 90d));
        var latestB = SnapshotWithComponents("Machine B", ("cpu", "CPU", 90.04));

        var result = ValueComparer.Compare(
            Comparison(80d, 80d), 500m, 500m, "$", latestA, latestB);

        Assert.Single(result.ComponentWinners);
        Assert.Equal(ComponentWinner.Tie, result.ComponentWinners[0].Winner);
    }

    [Fact]
    public void ComponentWinners_DuplicateComponentIds_DoNotThrow()
    {
        var latestA = SnapshotWithComponents(
            "Machine A",
            ("cpu", "CPU", 95d),
            ("cpu", "CPU", 40d));
        var latestB = SnapshotWithComponents("Machine B", ("cpu", "CPU", 80d));

        var result = ValueComparer.Compare(
            Comparison(80d, 70d), 500m, 500m, "$", latestA, latestB);

        Assert.Single(result.ComponentWinners);
        Assert.Equal(ComponentWinner.MachineA, result.ComponentWinners[0].Winner);
    }

    [Fact]
    public void ComponentWinners_NoSnapshots_ReturnsEmptyListAndVerdictStillStands()
    {
        var result = ValueComparer.Compare(Comparison(71d, 82d), 620m, 500m);

        Assert.Empty(result.ComponentWinners);
        Assert.NotNull(result.VerdictSentence);
    }

    [Fact]
    public void Compare_DoesNotChangeTheSnapshotsOrTheRawComparison()
    {
        var comparison = Comparison(71d, 82d);
        var latestA = SnapshotWithComponents("Machine A", ("cpu", "CPU", 95d));
        var latestB = SnapshotWithComponents("Machine B", ("cpu", "CPU", 80d));
        var componentsBefore = latestA.Components.Count;
        var scoreBefore = latestA.Score;

        var result = ValueComparer.Compare(comparison, 620m, 500m, "$", latestA, latestB);

        // The price lives with the comparison result only: the snapshots and
        // the raw (verdict-free) comparison come back untouched.
        Assert.Equal(componentsBefore, latestA.Components.Count);
        Assert.Equal(scoreBefore, latestA.Score);
        Assert.Equal(comparison, Comparison(71d, 82d));
        Assert.DoesNotContain("620", result.MachineA.MachineName, StringComparison.Ordinal);
    }
}
