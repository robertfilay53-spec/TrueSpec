namespace TrueSpec.Core.Scoring;

using TrueSpec.Core.Models;

/// <summary>One component's contribution to the overall score.</summary>
public sealed record ComponentScore(
    string Component,
    double HealthPercent,
    double Weight,
    double NormalizedWeight,
    double Contribution);

/// <summary>
/// The result of the scoring engine. Every score is explainable: the component
/// list contains the inputs, weights and math used to reach the total.
/// </summary>
public sealed record ScoreResult(
    double OverallScore,
    Verdict Verdict,
    IReadOnlyList<ComponentScore> Components,
    double MeasuredWeight)
{
    public bool HasMeasurableComponents => Components.Count > 0;

    public static ScoreResult Empty { get; } = new(
        0d,
        Verdict.NotMeasurable,
        Array.Empty<ComponentScore>(),
        0d);

    public string Explanation =>
        HasMeasurableComponents
            ? $"Measured weight {MeasuredWeight:0.#} of 100. Only components with a real sensor are included."
            : "No component exposes a measurable health value, so no score is invented.";
}
