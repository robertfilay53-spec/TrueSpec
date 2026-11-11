namespace TrueSpec.Core.Comparison;

using TrueSpec.Core.Models;

/// <summary>
/// One component's change between two snapshots.
/// </summary>
public sealed record ComponentChange(
    string ComponentId,
    string ComponentName,
    double? HealthBefore,
    double? HealthAfter,
    double? HealthDelta,
    IReadOnlyList<SpecChange> ChangedSpecs);

/// <summary>
/// One specification whose value changed between two snapshots.
/// </summary>
public sealed record SpecChange(string Label, string? Before, string? After);

/// <summary>
/// The result of comparing two snapshots side by side. Scores and health
/// are compared as measured numbers; a component that was not measurable in
/// either snapshot is reported as such, never as zero.
/// </summary>
public sealed record SnapshotComparison(
    SystemSnapshot Before,
    SystemSnapshot After,
    double ScoreDelta,
    IReadOnlyList<ComponentChange> ChangedComponents,
    IReadOnlyList<string> AddedComponents,
    IReadOnlyList<string> RemovedComponents)
{
    public string Summary
    {
        get
        {
            var direction = ScoreDelta switch
            {
                > 0.05 => "improved",
                < -0.05 => "worsened",
                _ => "unchanged",
            };
            return $"Overall score {direction} by {Math.Abs(ScoreDelta):0.#} points " +
                   $"({Before.Score.OverallScore:0.#} to {After.Score.OverallScore:0.#}). " +
                   $"{ChangedComponents.Count} component(s) changed, " +
                   $"{AddedComponents.Count} added, {RemovedComponents.Count} removed.";
        }
    }
}

/// <summary>
/// Pure comparison of two snapshots. No hardware access, no I/O.
/// </summary>
public static class SnapshotComparer
{
    public static SnapshotComparison Compare(SystemSnapshot before, SystemSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        // Comparing two different machines is meaningless: a spec that
        // differs between them is not a change, it is a different computer.
        if (!string.Equals(before.MachineName, after.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Snapshots must be from the same machine to compare.", nameof(after));
        }

        // A duplicate component id or spec label must never crash the
        // comparison: keep the first entry for a key and carry on.
        var beforeById = before.Components
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var afterById = after.Components
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var changes = new List<ComponentChange>();
        foreach (var afterComponent in after.Components)
        {
            if (!beforeById.TryGetValue(afterComponent.Id, out var beforeComponent))
            {
                continue;
            }

            var changedSpecs = ChangedSpecs(beforeComponent, afterComponent);
            var delta = afterComponent.HealthPercent is not null && beforeComponent.HealthPercent is not null
                ? Math.Round(afterComponent.HealthPercent.Value - beforeComponent.HealthPercent.Value, 1)
                : (double?)null;

            // A health value that did not move is not a delta: report null
            // so a spec-only change does not look like a zero health change.
            // (The 0.05 threshold is the same one that decides whether a
            // component counts as health-changed below.)
            if (delta is not null && Math.Abs(delta.Value) <= 0.05)
            {
                delta = null;
            }

            var healthChanged = delta is not null;
            if (healthChanged || changedSpecs.Count > 0)
            {
                changes.Add(new ComponentChange(
                    afterComponent.Id,
                    afterComponent.Name,
                    beforeComponent.HealthPercent,
                    afterComponent.HealthPercent,
                    delta,
                    changedSpecs));
            }
        }

        var added = after.Components
            .Where(c => !beforeById.ContainsKey(c.Id))
            .Select(c => c.Name)
            .ToList();
        var removed = before.Components
            .Where(c => !afterById.ContainsKey(c.Id))
            .Select(c => c.Name)
            .ToList();

        return new SnapshotComparison(
            before,
            after,
            Math.Round(after.Score.OverallScore - before.Score.OverallScore, 1),
            changes,
            added,
            removed);
    }

    private static IReadOnlyList<SpecChange> ChangedSpecs(ComponentSnapshot before, ComponentSnapshot after)
    {
        var beforeSpecs = before.Specs
            .GroupBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var changes = new List<SpecChange>();

        foreach (var spec in after.Specs)
        {
            if (!beforeSpecs.TryGetValue(spec.Label, out var oldSpec))
            {
                changes.Add(new SpecChange(spec.Label, null, spec.DisplayValue));
                continue;
            }

            if (!string.Equals(oldSpec.DisplayValue, spec.DisplayValue, StringComparison.Ordinal))
            {
                changes.Add(new SpecChange(spec.Label, oldSpec.DisplayValue, spec.DisplayValue));
            }
        }

        foreach (var oldSpec in before.Specs)
        {
            if (after.Specs.All(s => !string.Equals(s.Label, oldSpec.Label, StringComparison.OrdinalIgnoreCase)))
            {
                changes.Add(new SpecChange(oldSpec.Label, oldSpec.DisplayValue, null));
            }
        }

        return changes;
    }
}
