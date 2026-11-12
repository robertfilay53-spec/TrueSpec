namespace TrueSpec.Core.Models;

/// <summary>
/// A single component reading produced by an <c>ICollector</c>. The headline is
/// the short value shown on a dashboard card, the specs feed the detail table,
/// and <see cref="HealthPercent"/> is null when no sensor exists.
/// </summary>
public sealed record ComponentSnapshot(
    string Id,
    string Name,
    string Category,
    string IconKey,
    string Headline,
    double? HealthPercent,
    IReadOnlyList<SpecValue> Specs,
    string WhatThisMeans,
    DateTimeOffset CapturedAt)
{
    // A health percentage is a measurement between 0 and 100. Anything
    // else is corrupt input (a hand-edited snapshot, a bad deserializer):
    // non-finite values (NaN, +/-Infinity) become "no measurement" and
    // out-of-range finite values are clamped, so no consumer (status band,
    // health text, scoring, trends) ever has to defend against them again.
    // This replaces the synthesized positional property with a validated
    // one; the constructor parameter is still in scope for the initializer.
    public double? HealthPercent { get; init; } = HealthPercent is null || !double.IsFinite(HealthPercent.Value)
        ? null
        : Math.Clamp(HealthPercent.Value, 0d, 100d);

    public HealthStatus Status => HealthStatusExtensions.FromHealth(HealthPercent);

    public string HealthText => HealthPercent is null
        ? "Not measurable"
        : $"{HealthPercent.Value:0}%";

    public static ComponentSnapshot NotMeasurable(
        string id,
        string name,
        string category,
        string iconKey,
        string headline,
        IReadOnlyList<SpecValue> specs,
        string whatThisMeans,
        DateTimeOffset? capturedAt = null) =>
        new(id, name, category, iconKey, headline, null, specs, whatThisMeans, capturedAt ?? DateTimeOffset.Now);

    /// <summary>
    /// A component that could not be read. It carries a reason, never a number.
    /// </summary>
    public static ComponentSnapshot NotAvailable(
        string id,
        string name,
        string category,
        string iconKey,
        string reason) => new(
        id,
        name,
        category,
        iconKey,
        "Not available",
        null,
        new[]
        {
            new SpecValue(SpecLabels.Status, reason, "Platform / collector check"),
        },
        "This reading is not available. TrueSpec does not invent a value when the hardware or Windows does not report one.",
        DateTimeOffset.Now);
}
