using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

/// <summary>
/// Technician mode fleet summary (v2.0.3, plan Beyond v2.0 item 1): pure
/// functions over saved snapshots. Runs on both legs.
/// </summary>
public class FleetSummaryTests
{
    private static SystemSnapshot Snapshot(
        string machine,
        DateTimeOffset at,
        double overall,
        params (string Name, double Health)[] components)
    {
        var scores = components
            .Select(c => new ComponentScore(c.Name, c.Health, 10d, 10d, 10d))
            .ToArray();
        var score = new ScoreResult(overall, Verdict.UsedHealthy, scores, 10d);
        return new SystemSnapshot(
            Guid.NewGuid(),
            at,
            machine,
            "Windows 11 Pro",
            Array.Empty<ComponentSnapshot>(),
            score,
            new BuyerCheckEvaluator().Evaluate(Array.Empty<ComponentSnapshot>(), null));
    }

    private static FleetMachineInput Input(
        string machine,
        DateTimeOffset at,
        double overall,
        int scanCount,
        params (string Name, double Health)[] components) =>
        new(
            new MachineLatestSummary(machine, Guid.NewGuid(), at, overall, "UsedHealthy", scanCount),
            Snapshot(machine, at, overall, components));

    [Fact]
    public void WorstComponent_PicksTheLowestHealth()
    {
        var snapshot = Snapshot("A", DateTimeOffset.UtcNow, 75d,
            ("storage", 40d), ("battery", 70d), ("cpu", 90d));

        var (name, health) = FleetSummary.WorstComponent(snapshot);

        Assert.Equal("storage", name);
        Assert.Equal(40d, health);
    }

    [Fact]
    public void WorstComponent_TieBreaksAlphabetically()
    {
        var snapshot = Snapshot("A", DateTimeOffset.UtcNow, 75d,
            ("storage", 50d), ("battery", 50d));

        var (name, _) = FleetSummary.WorstComponent(snapshot);

        Assert.Equal("battery", name);
    }

    [Fact]
    public void WorstComponent_IgnoresNonFiniteHealth()
    {
        var snapshot = Snapshot("A", DateTimeOffset.UtcNow, 75d,
            ("storage", double.NaN), ("battery", double.PositiveInfinity), ("cpu", 80d));

        var (name, health) = FleetSummary.WorstComponent(snapshot);

        Assert.Equal("cpu", name);
        Assert.Equal(80d, health);
    }

    [Fact]
    public void WorstComponent_NullWhenNothingMeasurable()
    {
        var snapshot = new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "A",
            "Windows 11 Pro",
            Array.Empty<ComponentSnapshot>(),
            ScoreResult.Empty,
            new BuyerCheckEvaluator().Evaluate(Array.Empty<ComponentSnapshot>(), null));

        var (name, health) = FleetSummary.WorstComponent(snapshot);

        Assert.Null(name);
        Assert.Null(health);
    }

    [Fact]
    public void Build_SortsLowestScoreFirst()
    {
        var now = DateTimeOffset.UtcNow;
        var rows = FleetSummary.Build(new[]
        {
            Input("GOOD-PC", now, 92d, 2, ("storage", 95d)),
            Input("BAD-PC", now, 55d, 1, ("battery", 40d)),
            Input("MID-PC", now, 78d, 3, ("cpu", 70d)),
        });

        Assert.Equal(new[] { "BAD-PC", "MID-PC", "GOOD-PC" }, rows.Select(r => r.MachineName));
        Assert.Equal("battery", rows[0].WorstComponent);
        Assert.Equal(40d, rows[0].WorstComponentHealth);
    }

    [Fact]
    public void Build_NullScoreSortsLastNeverAsZero()
    {
        var now = DateTimeOffset.UtcNow;
        var unmeasurable = new FleetMachineInput(
            new MachineLatestSummary("NO-SCORE-PC", Guid.NewGuid(), now, 0d, "NotMeasurable", 1),
            new SystemSnapshot(
                Guid.NewGuid(),
                now,
                "NO-SCORE-PC",
                "Windows 11 Pro",
                Array.Empty<ComponentSnapshot>(),
                ScoreResult.Empty,
                new BuyerCheckEvaluator().Evaluate(Array.Empty<ComponentSnapshot>(), null)));

        var rows = FleetSummary.Build(new[]
        {
            unmeasurable,
            Input("SCORED-PC", now, 80d, 1, ("storage", 85d)),
        });

        Assert.Equal(new[] { "SCORED-PC", "NO-SCORE-PC" }, rows.Select(r => r.MachineName));
        Assert.Equal(80d, rows[0].LatestScore);
        Assert.Null(rows[1].LatestScore);
        Assert.Equal("Not measurable", rows[1].Verdict);
        Assert.Null(rows[1].WorstComponent);
    }

    [Fact]
    public void Build_EqualScoresTieBreakByMachineName()
    {
        var now = DateTimeOffset.UtcNow;
        var rows = FleetSummary.Build(new[]
        {
            Input("ZEBRA-PC", now, 80d, 1, ("storage", 85d)),
            Input("ALPHA-PC", now, 80d, 1, ("storage", 85d)),
        });

        Assert.Equal(new[] { "ALPHA-PC", "ZEBRA-PC" }, rows.Select(r => r.MachineName));
    }

    [Fact]
    public void Build_SkipsMachineWhoseLatestSnapshotIsUnreadable()
    {
        var now = DateTimeOffset.UtcNow;
        var rows = FleetSummary.Build(new[]
        {
            new FleetMachineInput(
                new MachineLatestSummary("BROKEN-PC", Guid.NewGuid(), now, 90d, "LikeNew", 1),
                null),
            Input("GOOD-PC", now, 88d, 1, ("storage", 90d)),
        });

        Assert.Single(rows);
        Assert.Equal("GOOD-PC", rows[0].MachineName);
    }

    [Fact]
    public void Build_EmptyInputGivesEmptyRows()
    {
        Assert.Empty(FleetSummary.Build(Array.Empty<FleetMachineInput>()));
    }

    [Fact]
    public void Build_CarriesScanCountAndLastScanFromTheStore()
    {
        var at = new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero);
        var rows = FleetSummary.Build(new[]
        {
            Input("SHOP-PC", at, 82d, 7, ("storage", 88d)),
        });

        Assert.Equal(7, rows[0].ScanCount);
        Assert.Equal(at, rows[0].LastScan);
        Assert.Equal(82d, rows[0].LatestScore);
        Assert.Equal("Used - healthy", rows[0].Verdict);
    }
}
