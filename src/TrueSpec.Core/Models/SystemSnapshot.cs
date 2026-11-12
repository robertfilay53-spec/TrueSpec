namespace TrueSpec.Core.Models;

using TrueSpec.Core.Benchmarks;

using TrueSpec.Core.Scoring;

/// <summary>
/// One complete scan of the machine: every component reading, the score with
/// its inputs, and the buyer-mode checklist. This is the object saved to the
/// snapshot store and turned into a report. <see cref="LoadTest"/> is null
/// until the user runs the load test from the Tests page; a refresh keeps the
/// most recent load test result, because it was measured on this machine.
/// </summary>
public sealed record SystemSnapshot(
    Guid Id,
    DateTimeOffset CapturedAt,
    string MachineName,
    string OperatingSystem,
    IReadOnlyList<ComponentSnapshot> Components,
    ScoreResult Score,
    BuyerCheckResult BuyerChecks,
    LoadTestResult? LoadTest = null)
{
    public ComponentSnapshot? Find(string componentId) =>
        Components.FirstOrDefault(c =>
            string.Equals(c.Id, componentId, StringComparison.OrdinalIgnoreCase));
}
