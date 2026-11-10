namespace TrueSpec.Core.Scoring;

using TrueSpec.Core.Models;

public interface IScoringEngine
{
    ScoreResult Score(IEnumerable<ComponentSnapshot> components);
}

/// <summary>
/// Weighted average over components that expose a measured health value.
/// Components with no sensor are left out entirely and their weight is
/// redistributed across the measured components, never faked.
/// Weights (v1): Storage 25, Battery 25, RAM 15, CPU 15, GPU 10, Network 10.
/// </summary>
public sealed class ScoringEngine : IScoringEngine
{
    public static readonly IReadOnlyDictionary<string, double> DefaultWeights =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Storage"] = 25d,
            ["Battery"] = 25d,
            ["RAM"] = 15d,
            ["CPU"] = 15d,
            ["GPU"] = 10d,
            ["Network"] = 10d,
        };

    private readonly IReadOnlyDictionary<string, double> _weights;

    public ScoringEngine(IReadOnlyDictionary<string, double>? weights = null)
    {
        _weights = weights ?? DefaultWeights;
    }

    public ScoreResult Score(IEnumerable<ComponentSnapshot> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var measured = components
            .Where(c => c.HealthPercent is not null && !double.IsNaN(c.HealthPercent.Value))
            .Select(c => new
            {
                Component = c,
                Health = Math.Clamp(c.HealthPercent!.Value, 0d, 100d),
                Weight = _weights.TryGetValue(c.Category, out var weight) ? weight : 0d,
            })
            .Where(x => x.Weight > 0d)
            .ToList();

        var measuredWeight = measured.Sum(x => x.Weight);
        if (measured.Count == 0 || measuredWeight <= 0d)
        {
            return ScoreResult.Empty;
        }

        var scores = measured
            .Select(x =>
            {
                var normalized = x.Weight / measuredWeight;
                return new ComponentScore(
                    x.Component.Name,
                    x.Health,
                    x.Weight,
                    normalized,
                    x.Health * normalized);
            })
            .OrderByDescending(s => s.Weight)
            .ToList();

        var overall = Math.Clamp(scores.Sum(s => s.Contribution), 0d, 100d);
        return new ScoreResult(
            Math.Round(overall, 1),
            VerdictFor(overall),
            scores,
            Math.Round(measuredWeight, 1));
    }

    public static Verdict VerdictFor(double score) => score switch
    {
        >= 90d => Verdict.LikeNew,
        >= 80d => Verdict.UsedHealthy,
        >= 60d => Verdict.Worn,
        >= 0d => Verdict.ReplaceSoon,
        _ => Verdict.NotMeasurable,
    };
}
