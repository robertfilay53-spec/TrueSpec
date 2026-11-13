namespace TrueSpec.Core.History;

using TrueSpec.Core.Models;
using TrueSpec.Core.Storage;

/// <summary>
/// One row of the technician fleet table (plan: Beyond v2.0, item 1):
/// one machine's latest scan, its score, and its worst component. Every
/// value comes from a real saved snapshot; a value the latest scan did
/// not report is null, never a zero and never an older scan's value.
/// </summary>
public sealed record FleetMachineRow(
    string MachineName,
    int ScanCount,
    DateTimeOffset LastScan,
    double? LatestScore,
    string Verdict,
    string? WorstComponent,
    double? WorstComponentHealth);

/// <summary>
/// One machine's input to the fleet table: the store's latest row for the
/// machine plus the full latest snapshot it points at. The snapshot may
/// be null when the latest scan is unreadable (corrupt payload); the
/// builder skips such a machine instead of inventing a row for it, the
/// same rule the History page uses for unreadable snapshots.
/// </summary>
public sealed record FleetMachineInput(
    MachineLatestSummary Latest,
    SystemSnapshot? Snapshot);

/// <summary>
/// Technician mode fleet summary, pure functions over saved snapshots.
/// No hardware access, no I/O. The fleet table is a triage list: the
/// lowest score first, so the machine that needs attention is at the
/// top; a machine with no measurable score sorts last, never as zero.
/// </summary>
public static class FleetSummary
{
    /// <summary>
    /// The worst component of a snapshot: the scored breakdown row with
    /// the lowest health. The breakdown already holds the worst health
    /// per category (the scoring engine never averages a dying second
    /// drive away), so the minimum over the breakdown is the worst
    /// component overall. Non-finite health values are not measurements
    /// (the same guard the scoring engine uses) and are ignored. Null
    /// when the snapshot has no measurable component.
    /// </summary>
    public static (string? Name, double? Health) WorstComponent(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var worst = snapshot.Score.Components
            .Where(c => double.IsFinite(c.HealthPercent))
            .OrderBy(c => c.HealthPercent)
            .ThenBy(c => c.Component, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return worst is null ? (null, null) : (worst.Component, worst.HealthPercent);
    }

    /// <summary>
    /// Builds the fleet rows from one input per machine. A machine whose
    /// latest snapshot could not be loaded is skipped, never shown with
    /// invented numbers. Rows sort lowest score first (triage order);
    /// machines with no measurable score sort last, then by machine name.
    /// </summary>
    public static IReadOnlyList<FleetMachineRow> Build(IReadOnlyList<FleetMachineInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var rows = new List<FleetMachineRow>();
        foreach (var input in inputs)
        {
            if (input.Snapshot is null)
            {
                continue;
            }

            var snapshot = input.Snapshot;
            var (worstName, worstHealth) = WorstComponent(snapshot);
            rows.Add(new FleetMachineRow(
                input.Latest.MachineName,
                input.Latest.ScanCount,
                input.Latest.CapturedAt,
                snapshot.Score.HasMeasurableComponents ? snapshot.Score.OverallScore : null,
                snapshot.Score.Verdict.ToString(),
                worstName,
                worstHealth));
        }

        // Lowest score first: the machine that needs attention is at the
        // top. A null score is not a zero, so it sorts last, never first.
        rows.Sort(static (a, b) =>
        {
            var score = (a.LatestScore, b.LatestScore) switch
            {
                (null, null) => 0,
                (null, _) => 1,
                (_, null) => -1,
                var (x, y) => x.Value.CompareTo(y.Value),
            };
            return score != 0
                ? score
                : string.Compare(a.MachineName, b.MachineName, StringComparison.OrdinalIgnoreCase);
        });

        return rows;
    }
}
