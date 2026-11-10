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
        string whatThisMeans) =>
        new(id, name, category, iconKey, headline, null, specs, whatThisMeans, DateTimeOffset.Now);
}
