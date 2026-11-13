namespace TrueSpec.Core.Models;

using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Comparison;
using TrueSpec.Core.Scoring;

/// <summary>
/// One complete scan of the machine: every component reading, the score with
/// its inputs, and the buyer-mode checklist. This is the object saved to the
/// snapshot store and turned into a report. <see cref="LoadTest"/> is null
/// until the user runs the load test from the Tests page; a refresh keeps the
/// most recent load test result, because it was measured on this machine.
/// <see cref="MachineDnaHash"/> / <see cref="MachineDnaParts"/> are null on
/// snapshots saved before DNA existed: such a snapshot reads as "DNA not
/// recorded", never as a mismatch against a later scan.
/// </summary>
public sealed record SystemSnapshot(
    Guid Id,
    DateTimeOffset CapturedAt,
    string MachineName,
    string OperatingSystem,
    IReadOnlyList<ComponentSnapshot> Components,
    ScoreResult Score,
    BuyerCheckResult BuyerChecks,
    LoadTestResult? LoadTest = null,
    string? MachineDnaHash = null,
    IReadOnlyList<MachineDnaPart>? MachineDnaParts = null)
{
    public ComponentSnapshot? Find(string componentId) =>
        Components.FirstOrDefault(c =>
            string.Equals(c.Id, componentId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The DNA status for display: the hash when this scan recorded one,
    /// "DNA not available" when the scan recorded DNA but no part reported
    /// a usable serial, "DNA not recorded" for a snapshot saved before
    /// DNA existed. The last two are honest states, never a mismatch.
    /// </summary>
    public string MachineDnaStatus => MachineDnaParts is null
        ? "DNA not recorded"
        : MachineDnaHash ?? "DNA not available";
}
