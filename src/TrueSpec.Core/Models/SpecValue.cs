namespace TrueSpec.Core.Models;

/// <summary>
/// One label/value row on a component detail page.
/// <paramref name="Source"/> names where the reading came from so the UI can
/// show it on hover. <paramref name="IsEstimate"/> marks values that are
/// derived rather than directly measured.
/// </summary>
public sealed record SpecValue(
    string Label,
    string Value,
    string Source,
    bool IsEstimate = false)
{
    public string DisplayValue => IsEstimate ? $"{Value} (ESTIMATE)" : Value;
}
