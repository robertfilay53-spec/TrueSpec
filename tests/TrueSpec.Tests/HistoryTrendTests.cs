using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

public class HistoryTrendTests
{
    [Fact]
    public void EmptyHistory_HasNoPointsAndSaysSo()
    {
        var trend = HistoryTrendAnalyzer.Analyze(Array.Empty<SystemSnapshot>());

        Assert.Equal(0, trend.SnapshotCount);
        Assert.Null(trend.ScoreDelta);
        Assert.Null(trend.BatteryHealthDelta);
        Assert.Null(trend.StorageWearDelta);
        Assert.Contains("No scans saved yet", trend.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleSnapshot_HasPointsButNoDelta()
    {
        var trend = HistoryTrendAnalyzer.Analyze(new[] { SnapshotWith(90d, "90.0%", "2", "2%", "100") });

        Assert.Equal(1, trend.SnapshotCount);
        Assert.Single(trend.ScorePoints);
        Assert.Null(trend.ScoreDelta);
        Assert.Contains("one more scan is needed", trend.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TwoSnapshots_ComputesScoreBatteryAndWearDeltas()
    {
        var before = SnapshotWith(90d, "90.0%", "10", "2%", "100");
        var after = SnapshotWith(85d, "85.5%", "12", "5%", "250");

        var trend = HistoryTrendAnalyzer.Analyze(new[] { after, before });

        Assert.Equal(2, trend.SnapshotCount);
        Assert.Equal(-5d, trend.ScoreDelta);
        Assert.Equal(-4.5d, trend.BatteryHealthDelta);
        Assert.Equal(3d, trend.StorageWearDelta);
        // Points are oldest first even when the input is newest first.
        Assert.True(trend.ScorePoints[0].CapturedAt < trend.ScorePoints[1].CapturedAt);
        Assert.Contains("Score 90 to 85", trend.Summary, StringComparison.Ordinal);
        Assert.Contains("Battery health 90 to 85.5", trend.Summary, StringComparison.Ordinal);
        Assert.Contains("Storage wear 2 to 5", trend.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_SnapshotIds_MatchInputScansInPointOrder()
    {
        var before = SnapshotWith(90d, "90.0%", "10", "2%", "100", new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
        var after = SnapshotWith(85d, "85.5%", "12", "5%", "250", new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero));

        var trend = HistoryTrendAnalyzer.Analyze(new[] { after, before });

        // Oldest first, in the same order as the points, so the History
        // table row at index i shows the scan named by SnapshotIds[i].
        Assert.Equal(new[] { before.Id, after.Id }, trend.SnapshotIds);
        Assert.Equal(before.CapturedAt, trend.ScorePoints[0].CapturedAt);
        Assert.Equal(after.CapturedAt, trend.ScorePoints[1].CapturedAt);
    }

    [Fact]
    public void EmptyHistory_SnapshotIdsAreEmpty()
    {
        Assert.Empty(HistoryTrendAnalyzer.Analyze(Array.Empty<SystemSnapshot>()).SnapshotIds);
        Assert.Empty(HistoryTrend.Empty.SnapshotIds);
    }

    [Fact]
    public void UnreportedBatteryHealth_IsAGapNeverAZero()
    {
        var before = SnapshotWith(90d, "90.0%", "10", "2%", "100");
        // Second scan: the battery firmware stopped reporting health.
        var after = SnapshotWith(88d, SpecLabels.NotReportedByHardware, "12", "3%", "200");

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after });

        Assert.Equal(-2d, trend.ScoreDelta);
        Assert.Null(trend.BatteryHealthDelta);
        Assert.Null(trend.BatteryHealthPoints[1].Value);
        Assert.Equal(90d, trend.BatteryHealthPoints[0].Value);
        Assert.Contains("Battery health reported once", trend.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void MixedMachines_ThrowsLikeTheComparer()
    {
        var first = SnapshotWith(90d, "90.0%", "10", "2%", "100");
        var other = SnapshotWith(80d, "80.0%", "5", "1%", "50", machineName: "Other Laptop");

        Assert.Throws<ArgumentException>(() => HistoryTrendAnalyzer.Analyze(new[] { first, other }));
    }

    [Fact]
    public async Task LoadAsync_ReadsSavedSnapshotsFromTheStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();
            await store.SaveAsync(SnapshotWith(90d, "90.0%", "10", "2%", "100", DateTimeOffset.UtcNow.AddDays(-2)));
            await store.SaveAsync(SnapshotWith(85d, "85.0%", "11", "4%", "150", DateTimeOffset.UtcNow.AddDays(-1)));

            var trend = await HistoryTrendAnalyzer.LoadAsync(store, "Test Laptop");

            Assert.Equal(2, trend.SnapshotCount);
            Assert.Equal(-5d, trend.ScoreDelta);
            Assert.Equal(-5d, trend.BatteryHealthDelta);
            Assert.Equal(2d, trend.StorageWearDelta);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_SkipsSnapshotsFromOtherMachines()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();
            await store.SaveAsync(SnapshotWith(90d, "90.0%", "10", "2%", "100"));
            await store.SaveAsync(SnapshotWith(50d, "50.0%", "1", "9%", "9", machineName: "Other Laptop"));

            var trend = await HistoryTrendAnalyzer.LoadAsync(store, "Test Laptop");

            Assert.Equal(1, trend.SnapshotCount);
            Assert.Null(trend.ScoreDelta);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// A snapshot shaped like the real collectors write it: battery health
    /// and storage wear live in spec values ("90.0%", "2%"), not in
    /// HealthPercent, because both collectors are NotMeasurable.
    /// </summary>
    private static SystemSnapshot SnapshotWith(
        double score,
        string batteryHealth,
        string cycles,
        string wear,
        string powerOnHours,
        DateTimeOffset? capturedAt = null,
        string machineName = "Test Laptop")
    {
        var now = capturedAt ?? DateTimeOffset.UtcNow;
        var components = new[]
        {
            new ComponentSnapshot(
                "battery", "Battery", "Battery", "battery", "Test battery", null,
                new[]
                {
                    new SpecValue("Health", batteryHealth, "Full charge capacity / design capacity"),
                    new SpecValue("Cycle count", cycles, "CIM root\\WMI BatteryCycleCount"),
                },
                "Battery health compares full charge capacity with design capacity.",
                now),
            new ComponentSnapshot(
                "storage", "Storage", "Storage", "storage", "Test drive", 100d - ParseWear(wear),
                new[]
                {
                    new SpecValue("Wear level", wear, "Get-StorageReliabilityCounter.Wear"),
                    new SpecValue("Power-on hours", powerOnHours, "Get-StorageReliabilityCounter.PowerOnHours"),
                },
                "Drive health comes from the manufacturer wear counter.",
                now),
        };
        var scoreResult = new ScoreResult(
            score,
            Verdict.UsedHealthy,
            new[] { new ComponentScore("storage", 100d - ParseWear(wear), 10d, 10d, 10d) },
            10d);
        return new SystemSnapshot(
            Guid.NewGuid(),
            now,
            machineName,
            "Windows 11 Pro",
            components,
            scoreResult,
            new BuyerCheckEvaluator().Evaluate(components, null));
    }

    private static double ParseWear(string wear) =>
        double.Parse(wear.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture);
}
