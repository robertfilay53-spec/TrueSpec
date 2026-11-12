namespace TrueSpec.Core.History;

using System.Globalization;
using TrueSpec.Core.Models;

/// <summary>
/// Which side of a value comparison a judgement favours. A verdict is only
/// ever a judgement on the buyer's own numbers: the measured score and the
/// price the buyer typed in. Nothing here is a market price.
/// </summary>
public enum ValueWinner
{
    MachineA,
    MachineB,
    Tie,
    NotDecidable,
}

/// <summary>The winner of one shared component, decided on measured health only.</summary>
public enum ComponentWinner
{
    MachineA,
    MachineB,
    Tie,
    NotMeasurable,
}

/// <summary>
/// One shared component's value-comparison row: the measured health on each
/// side and which side wins it. A component measurable on only one side has
/// no winner: crowning a winner on missing data would invent a judgement.
/// </summary>
public sealed record ComponentValueWinner(
    string ComponentName,
    ComponentWinner Winner,
    double? HealthA,
    double? HealthB);

/// <summary>
/// One side of a value comparison: the measured facts from
/// <see cref="MachineHistoryComparer"/> plus the asking price the buyer
/// typed in. The price is user input only: it is never scraped, never
/// fetched, never assumed, and it is stored with the comparison only, never
/// in the snapshot (precedent: <c>BuyerCheckContext</c> advertised RAM).
/// </summary>
public sealed record ValueSide(
    string MachineName,
    double? LatestScore,
    decimal? AskingPrice,
    string CurrencySymbol)
{
    /// <summary>True when the buyer entered a usable asking price (above zero).</summary>
    public bool HasPrice => AskingPrice is not null && AskingPrice.Value > 0m;
}

/// <summary>
/// The result of a U9 value comparison. The score is measured, the price is
/// user input, and the per-currency figure is arithmetic on those two:
/// <see cref="IsEstimate"/> is therefore always true whenever a
/// per-currency figure exists, and the UI renders it as an ESTIMATE, never
/// as a measured market value. No market-price data is claimed anywhere.
/// </summary>
public sealed record ValueComparisonResult(
    ValueSide MachineA,
    ValueSide MachineB,
    double? ScorePerCurrencyA,
    double? ScorePerCurrencyB,
    bool IsEstimate,
    decimal? PriceDifference,
    ValueWinner CheaperMachine,
    ValueWinner BetterBuy,
    IReadOnlyList<ComponentValueWinner> ComponentWinners,
    string? VerdictSentence,
    string? NoVerdictReason)
{
    /// <summary>The one-sentence buyer verdict, or the reason there is none.</summary>
    public string DisplayText => VerdictSentence ?? NoVerdictReason ?? string.Empty;
}

/// <summary>
/// U9 value comparison: a separate layer on top of the raw two-machine
/// comparison, never a rewrite of it. <see cref="MachineHistoryComparer"/>
/// explicitly gives "no verdict, no which is better" for the raw facts, and
/// this class does not touch it: it takes the already-summarized
/// <see cref="MachineComparison"/> and adds the buyer decision (asking
/// prices the buyer typed in, measured health score per unit of currency,
/// per-component winner, one verdict sentence). Pure functions, no hardware
/// access, no I/O, no network.
/// </summary>
public static class ValueComparer
{
    /// <summary>
    /// The display precision of the per-currency figure. Two machines whose
    /// per-currency figures agree to this precision are tied: a smaller
    /// difference is display noise, not a buying signal.
    /// </summary>
    public const int ScorePerCurrencyPrecision = 4;

    /// <summary>
    /// The health gap that counts as a real component win. The same 0.05
    /// threshold <see cref="Comparison.SnapshotComparer"/> uses for a
    /// health change, so a win is never declared on rounding noise.
    /// </summary>
    public const double ComponentWinThreshold = 0.05;

    /// <summary>The label the UI shows next to every asking-price field.</summary>
    public const string AskingPriceLabel = "Asking price (you enter this, not measured)";

    /// <summary>
    /// Compares two already-summarized machines on value. Prices come only
    /// from the caller (buyer-typed); a null or non-positive price means
    /// "no price entered" and yields no verdict, never a guessed price.
    /// <paramref name="latestSnapshotA"/> and <paramref name="latestSnapshotB"/>
    /// are the two machines' latest snapshots, used only for the
    /// per-component winners; both may be null, in which case the winner
    /// list is empty and the verdict still stands on score and price.
    /// </summary>
    public static ValueComparisonResult Compare(
        MachineComparison comparison,
        decimal? askingPriceA,
        decimal? askingPriceB,
        string currencySymbol = "$",
        SystemSnapshot? latestSnapshotA = null,
        SystemSnapshot? latestSnapshotB = null)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        var symbol = string.IsNullOrWhiteSpace(currencySymbol) ? "$" : currencySymbol.Trim();
        var sideA = new ValueSide(
            comparison.MachineA.MachineName,
            comparison.MachineA.LatestScore,
            ValidPrice(askingPriceA),
            symbol);
        var sideB = new ValueSide(
            comparison.MachineB.MachineName,
            comparison.MachineB.LatestScore,
            ValidPrice(askingPriceB),
            symbol);

        var perCurrencyA = ScorePerCurrency(sideA);
        var perCurrencyB = ScorePerCurrency(sideB);
        var componentWinners = ComponentWinners(latestSnapshotA, latestSnapshotB);

        var priceDifference = sideA.HasPrice && sideB.HasPrice
            ? Math.Abs(sideA.AskingPrice!.Value - sideB.AskingPrice!.Value)
            : (decimal?)null;
        var cheaper = CheaperSide(sideA, sideB);
        var betterBuy = BetterBuySide(perCurrencyA, perCurrencyB);

        var (verdict, reason) = Verdict(sideA, sideB, perCurrencyA, perCurrencyB, priceDifference, cheaper, betterBuy);

        return new ValueComparisonResult(
            sideA,
            sideB,
            perCurrencyA,
            perCurrencyB,
            IsEstimate: perCurrencyA is not null || perCurrencyB is not null,
            priceDifference,
            cheaper,
            betterBuy,
            componentWinners,
            verdict,
            reason);
    }

    /// <summary>
    /// Measured health score per unit of currency: arithmetic on the
    /// measured score and the buyer-entered price. Null when either input
    /// is missing, so a missing score or price never becomes a zero.
    /// </summary>
    public static double? ScorePerCurrency(ValueSide side)
    {
        if (side.LatestScore is null || !side.HasPrice)
        {
            return null;
        }

        return side.LatestScore.Value / (double)side.AskingPrice!.Value;
    }

    /// <summary>
    /// Per-component winners from the two latest snapshots, matched by
    /// component id (case-insensitive, first entry wins on duplicates, the
    /// same rule <see cref="Comparison.SnapshotComparer"/> uses). Only
    /// components present on both sides are compared; a component missing
    /// from one machine cannot have a winner.
    /// </summary>
    public static IReadOnlyList<ComponentValueWinner> ComponentWinners(
        SystemSnapshot? latestSnapshotA,
        SystemSnapshot? latestSnapshotB)
    {
        if (latestSnapshotA is null || latestSnapshotB is null)
        {
            return Array.Empty<ComponentValueWinner>();
        }

        var byIdA = IndexById(latestSnapshotA);
        var byIdB = IndexById(latestSnapshotB);

        var winners = new List<ComponentValueWinner>();
        foreach (var componentA in byIdA.Values)
        {
            if (!byIdB.TryGetValue(componentA.Id, out var componentB))
            {
                continue;
            }

            var healthA = componentA.HealthPercent;
            var healthB = componentB.HealthPercent;
            var winner = healthA is null || healthB is null
                ? ComponentWinner.NotMeasurable
                : healthA.Value - healthB.Value > ComponentWinThreshold
                    ? ComponentWinner.MachineA
                    : healthB.Value - healthA.Value > ComponentWinThreshold
                        ? ComponentWinner.MachineB
                        : ComponentWinner.Tie;

            winners.Add(new ComponentValueWinner(componentA.Name, winner, healthA, healthB));
        }

        return winners;
    }

    private static Dictionary<string, ComponentSnapshot> IndexById(SystemSnapshot snapshot) =>
        snapshot.Components
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private static decimal? ValidPrice(decimal? price) =>
        // A decimal is always finite, so above zero is the only check:
        // null, zero and negative all mean "no price entered".
        price is { } value && value > 0m ? value : null;

    private static ValueWinner CheaperSide(ValueSide sideA, ValueSide sideB)
    {
        if (!sideA.HasPrice || !sideB.HasPrice)
        {
            return ValueWinner.NotDecidable;
        }

        return sideA.AskingPrice!.Value < sideB.AskingPrice!.Value
            ? ValueWinner.MachineA
            : sideB.AskingPrice!.Value < sideA.AskingPrice!.Value
                ? ValueWinner.MachineB
                : ValueWinner.Tie;
    }

    private static ValueWinner BetterBuySide(double? perCurrencyA, double? perCurrencyB)
    {
        if (perCurrencyA is null || perCurrencyB is null)
        {
            return ValueWinner.NotDecidable;
        }

        var roundedA = Math.Round(perCurrencyA.Value, ScorePerCurrencyPrecision);
        var roundedB = Math.Round(perCurrencyB.Value, ScorePerCurrencyPrecision);
        return roundedA > roundedB
            ? ValueWinner.MachineA
            : roundedB > roundedA
                ? ValueWinner.MachineB
                : ValueWinner.Tie;
    }

    /// <summary>
    /// The one-sentence verdict, in the exact form the v1.5 plan specifies:
    /// "Machine B scores 82 vs 71 and costs $120 less. On measured health
    /// per dollar, B is the better buy." The winner is the machine with the
    /// higher measured health per unit of currency. A tie is still one
    /// sentence, saying so. No verdict is possible without a measured score
    /// and an asking price for both machines, and the reason says which is
    /// missing instead of guessing.
    /// </summary>
    private static (string? Verdict, string? Reason) Verdict(
        ValueSide sideA,
        ValueSide sideB,
        double? perCurrencyA,
        double? perCurrencyB,
        decimal? priceDifference,
        ValueWinner cheaper,
        ValueWinner betterBuy)
    {
        if (sideA.LatestScore is null || sideB.LatestScore is null)
        {
            return (null, "A value verdict needs a measured health score for both machines.");
        }

        // Captured once, so the sentence builders below never re-read a
        // nullable property.
        var scoreA = sideA.LatestScore.Value;
        var scoreB = sideB.LatestScore.Value;

        if (!sideA.HasPrice || !sideB.HasPrice)
        {
            return (null, "Enter an asking price above zero for each machine to compare value.");
        }

        if (betterBuy is ValueWinner.NotDecidable)
        {
            return (null, "A value verdict needs a measured health score and an asking price for both machines.");
        }

        var symbol = sideA.CurrencySymbol;
        var priceText = PriceText(priceDifference, symbol);

        if (betterBuy is ValueWinner.Tie)
        {
            // On a tie the sentence is anchored on the cheaper machine (or
            // Machine A when the prices match), in the same words as a win.
            var tieClause = cheaper is ValueWinner.MachineA
                ? $"Machine A scores {scoreA:0.#} vs {scoreB:0.#} and costs {priceText} less. On measured health per dollar, the two machines are tied."
                : cheaper is ValueWinner.MachineB
                    ? $"Machine B scores {scoreB:0.#} vs {scoreA:0.#} and costs {priceText} less. On measured health per dollar, the two machines are tied."
                    : $"Machine A scores {scoreA:0.#} vs {scoreB:0.#} and costs the same. On measured health per dollar, the two machines are tied.";
            return (tieClause, null);
        }

        var winnerIsA = betterBuy is ValueWinner.MachineA;
        var winnerScore = winnerIsA ? scoreA : scoreB;
        var loserScore = winnerIsA ? scoreB : scoreA;
        var winnerLetter = winnerIsA ? "A" : "B";
        var winnerCheaper = cheaper == betterBuy;
        var costClause = cheaper is ValueWinner.Tie
            ? "costs the same"
            : winnerCheaper
                ? $"costs {priceText} less"
                : $"costs {priceText} more";

        var sentence = string.Create(
            CultureInfo.InvariantCulture,
            $"Machine {winnerLetter} scores {winnerScore:0.#} vs {loserScore:0.#} and {costClause}. On measured health per dollar, {winnerLetter} is the better buy.");
        return (sentence, null);
    }

    private static string PriceText(decimal? priceDifference, string symbol) =>
        priceDifference is null
            ? string.Empty
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{symbol}{priceDifference.Value:0.##}");
}
