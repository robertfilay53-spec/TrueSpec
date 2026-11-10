namespace TrueSpec.Core.Models;

using TrueSpec.Core.Scoring;

/// <summary>
/// One complete scan of the machine: every component reading, the score with
/// its inputs, and the buyer-mode checklist. This is the object saved to the
/// snapshot store and turned into a report.
/// </summary>
public sealed record SystemSnapshot(
    Guid Id,
    DateTimeOffset CapturedAt,
    string MachineName,
    string OperatingSystem,
    IReadOnlyList<ComponentSnapshot> Components,
    ScoreResult Score,
    BuyerCheckResult BuyerChecks)
{
    public ComponentSnapshot? Find(string componentId) =>
        Components.FirstOrDefault(c =>
            string.Equals(c.Id, componentId, StringComparison.OrdinalIgnoreCase));
}
