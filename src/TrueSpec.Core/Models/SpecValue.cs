namespace TrueSpec.Core.Models;

/// <summary>
/// One label/value row on a component detail page.
/// <paramref name="Source"/> names where the reading came from so the UI can
/// show it on hover. <paramref name="IsEstimate"/> marks values that are
/// derived rather than directly measured.
/// <para>
/// <paramref name="NumericValue"/> is the typed measurement behind the
/// display string, when the value is a number: analyzers, health rules and
/// buyer checks read this instead of re-parsing <see cref="Value"/>
/// ("12,400 hours", "85.5%", "3,500 MHz"). <paramref name="NumericUnit"/>
/// names the unit ("hours", "%", "MHz", "C", "GB") so a consumer never has
/// to guess it from the display text. Both are null when the value is not
/// a number, or when the collector could not measure it: an unreported
/// number is <see cref="SpecLabels.NotReportedByHardware"/> with no typed
/// value, never a zero. Older snapshots and collectors that do not set the
/// typed value yet still work: <see cref="TrueSpec.Core.Health.SpecText"/>
/// falls back to parsing the display string.
/// </para>
/// </summary>
public sealed record SpecValue(
    string Label,
    string Value,
    string Source,
    bool IsEstimate = false,
    double? NumericValue = null,
    string? NumericUnit = null)
{
    public string DisplayValue => IsEstimate ? $"{Value} (ESTIMATE)" : Value;

    /// <summary>True when the hardware did not report this value.</summary>
    public bool IsNotReported =>
        string.Equals(Value, SpecLabels.NotReportedByHardware, StringComparison.Ordinal);

    /// <summary>A spec the hardware did not report: the honest value, never a guess.</summary>
    public static SpecValue NotReported(string label, string source) =>
        new(label, SpecLabels.NotReportedByHardware, source);
}
