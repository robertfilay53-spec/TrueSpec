using TrueSpec.Core.Comparison;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

/// <summary>
/// Tests for the P4 snapshot comparison: score deltas, per-component
/// health changes, changed specifications and added/removed components.
/// </summary>
public class ComparisonTests
{
    [Fact]
    public void Compare_IdenticalSnapshots_HasZeroDeltaAndNoChanges()
    {
        var snapshot = TestSnapshots.Healthy();

        var result = SnapshotComparer.Compare(snapshot, snapshot);

        Assert.Equal(0, result.ScoreDelta, 3);
        Assert.Empty(result.ChangedComponents);
        Assert.Empty(result.AddedComponents);
        Assert.Empty(result.RemovedComponents);
        Assert.Contains("unchanged", result.Summary);
    }

    [Fact]
    public void Compare_WorseCpuHealth_ReportsNegativeDeltaAndChangedComponent()
    {
        var before = TestSnapshots.Healthy();
        var afterComponents = before.Components.Select(c =>
            c.Id == "cpu"
                ? c with
                {
                    HealthPercent = 40d,
                    Specs = [new SpecValue("Package temperature", "95 C", "LibreHardwareMonitor")],
                }
                : c).ToList();
        // The snapshot score is computed at scan time, so a changed
        // component needs a fresh score, exactly like a real re-scan.
        var after = before with
        {
            Components = afterComponents,
            Score = new ScoringEngine().Score(afterComponents),
        };

        var result = SnapshotComparer.Compare(before, after);

        Assert.True(result.ScoreDelta < 0);
        var cpu = Assert.Single(result.ChangedComponents);
        Assert.Equal("CPU", cpu.ComponentName);
        Assert.Equal(95, cpu.HealthBefore);
        Assert.Equal(40, cpu.HealthAfter);
        Assert.Equal(-55, cpu.HealthDelta);
        var temp = Assert.Single(cpu.ChangedSpecs);
        Assert.Equal("Package temperature", temp.Label);
        Assert.Equal("55 C", temp.Before);
        Assert.Equal("95 C", temp.After);
        Assert.Contains("worsened", result.Summary);
    }

    [Fact]
    public void Compare_ChangedRamSpec_ReportsSpecChangeWithoutHealthDelta()
    {
        var before = TestSnapshots.Healthy();
        var after = TestSnapshots.Healthy() with
        {
            Components = before.Components.Select(c =>
                c.Id == "ram"
                    ? c with { Specs = [new SpecValue("Total memory", "32 GB", "CIM Win32_PhysicalMemory")] }
                    : c).ToList(),
        };

        var result = SnapshotComparer.Compare(before, after);

        var ram = Assert.Single(result.ChangedComponents);
        Assert.Equal("RAM", ram.ComponentName);
        Assert.Null(ram.HealthDelta);
        var spec = Assert.Single(ram.ChangedSpecs);
        Assert.Equal("Total memory", spec.Label);
        Assert.Equal("16 GB", spec.Before);
        Assert.Equal("32 GB", spec.After);
    }

    [Fact]
    public void Compare_AddedAndRemovedComponents_AreListed()
    {
        var before = TestSnapshots.Healthy();
        var after = TestSnapshots.Healthy() with
        {
            Components = before.Components
                .Where(c => c.Id != "gpu")
                .Concat([TestSnapshots.Network("2026-01-01") with { Id = "wifi", Name = "Wi-Fi" }])
                .ToList(),
        };

        var result = SnapshotComparer.Compare(before, after);

        Assert.Contains("Wi-Fi", result.AddedComponents);
        Assert.Contains("GPU", result.RemovedComponents);
        Assert.DoesNotContain(result.ChangedComponents, c => c.ComponentName == "GPU");
    }

    [Fact]
    public void Compare_RequiresSameMachine()
    {
        var before = TestSnapshots.Healthy();
        var after = TestSnapshots.Healthy() with { MachineName = "Different Laptop" };

        Assert.Throws<ArgumentException>(() => SnapshotComparer.Compare(before, after));
    }

    [Fact]
    public void Compare_NullSnapshot_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SnapshotComparer.Compare(null!, TestSnapshots.Healthy()));
    }
}
