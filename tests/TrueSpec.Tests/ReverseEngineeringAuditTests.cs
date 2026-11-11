using Microsoft.Data.Sqlite;
using TrueSpec.Core;
using TrueSpec.Core.Comparison;
using TrueSpec.Core.Diagnostics;
using TrueSpec.Core.Health;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Security;
using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

/// <summary>
/// Regression tests for the deep reverse-engineering audit (Nov 11, 2026):
/// each test pins a behavior the audit proved wrong by probing the real
/// code, not just by reading it.
/// </summary>
public class ReverseEngineeringAuditTests
{
    private static ComponentSnapshot Component(string id, string category, double? health) =>
        new(id, id, category, id, id, health, Array.Empty<SpecValue>(), string.Empty, DateTimeOffset.Now);

    private static ComponentSnapshot SpecComponent(string id, params SpecValue[] specs) =>
        new(id, id, id, id, id, null, specs, string.Empty, DateTimeOffset.Now);

    private static SystemSnapshot Snapshot(string machine, params ComponentSnapshot[] components) =>
        new(Guid.NewGuid(), DateTimeOffset.Now, machine, "Windows",
            components,
            new ScoreResult(0d, Verdict.NotMeasurable, Array.Empty<ComponentScore>(), 0d),
            new BuyerCheckResult(Array.Empty<BuyerCheck>()));

    [Fact]
    public void ScoringEngine_IgnoresInfiniteHealth()
    {
        // An Infinity health used to clamp to 100 and count toward the score.
        var result = new ScoringEngine().Score(new[]
        {
            Component("storage", "Storage", double.PositiveInfinity),
            Component("battery", "Battery", 50d),
        });

        Assert.Single(result.Components);
        Assert.Equal(50d, result.OverallScore);
    }

    [Fact]
    public void ScoringEngine_CountsEachCategoryWeightOnce()
    {
        // Two storage components used to double-count the 25-point storage
        // weight (100 + 0 storage with a 100 battery scored 66.7).
        var result = new ScoringEngine().Score(new[]
        {
            Component("s1", "Storage", 100d),
            Component("s2", "Storage", 0d),
            Component("b1", "Battery", 100d),
        });

        Assert.Equal(50d, result.MeasuredWeight);
        Assert.Equal(50d, result.OverallScore);
    }

    [Fact]
    public void NetworkHealth_ReturnsNullForFutureDriverDate()
    {
        // A driver dated years in the future used to score 100%.
        var future = DateTimeOffset.UtcNow.AddYears(5).ToString("yyyy-MM-dd");
        var health = ComponentHealthRules.Evaluate(
            SpecComponent("network", new SpecValue("Driver date", future, "test")));
        Assert.Null(health);
    }

    [Fact]
    public void BuyerCheck_FutureBiosAndInstallDatesAreNotApplicable()
    {
        var future = DateTimeOffset.UtcNow.AddYears(5).ToString("yyyy-MM-dd");
        var system = SpecComponent("system",
            new SpecValue("BIOS date", future, "test"),
            new SpecValue("Windows install date", future, "test"));
        var checks = new BuyerCheckEvaluator().Evaluate(new[] { system }).Checks;

        Assert.Equal(BuyerCheckState.NotApplicable, checks.First(c => c.Id == "bios-date").State);
        Assert.Equal(BuyerCheckState.NotApplicable, checks.First(c => c.Id == "windows-install").State);
    }

    [Theory]
    [InlineData("12,5%", 12.5)]
    [InlineData("1,000", 1000d)]
    [InlineData("12.5%", 12.5)]
    public void TryParseLeadingNumber_HandlesDecimalCommas(string text, double expected)
    {
        Assert.True(SpecText.TryParseLeadingNumber(text, out var value));
        Assert.Equal(expected, value, 3);
    }

    [Theory]
    [InlineData("16,5 GB", 33UL << 29)] // 16.5 GiB, not 165 GiB
    [InlineData("1,000 GB", 1000UL << 30)]
    [InlineData("16 GB", 16UL << 30)]
    public void TryParseMemorySizeBytes_HandlesDecimalCommas(string text, ulong expected)
    {
        Assert.True(SpecText.TryParseMemorySizeBytes(text, out var bytes));
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void SnapshotComparer_DeduplicatesRepeatedComponents()
    {
        var before = Snapshot("M", Component("cpu", "CPU", 80d));
        var after = Snapshot("M", Component("cpu", "CPU", 90d), Component("cpu", "CPU", 90d));
        var comparison = SnapshotComparer.Compare(before, after);
        Assert.Single(comparison.ChangedComponents);
    }

    [Fact]
    public async Task SnapshotHistory_SkipsCorruptScoreRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"truespec-audit-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteSnapshotStore(path);
            await store.InitializeAsync();
            var good = Snapshot("M");
            await store.SaveAsync(good);

            // Pooling=false, like the store itself: a pooled connection
            // keeps the database file locked on Windows, so the cleanup
            // File.Delete below would throw IOException.
            await using var connection = new SqliteConnection($"Data Source={path};Pooling=false");
            await connection.OpenAsync();
            async Task Exec(string sql)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }

            // A TEXT score must not read back as a fake 0.0, and an
            // out-of-range score is not a score this app ever writes.
            await Exec($"INSERT INTO snapshots VALUES ('{Guid.NewGuid():D}', '2026-01-01T00:00:00+00:00', 'M', 'not-a-number', 'Worn', '{{}}')");
            await Exec($"INSERT INTO snapshots VALUES ('{Guid.NewGuid():D}', '2026-01-02T00:00:00+00:00', 'M', 500, 'Worn', '{{}}')");
            await Exec($"INSERT INTO snapshots VALUES ('not-a-guid', 'not-a-date', 'M', 50, 'Worn', '{{}}')");

            var history = await store.GetHistoryAsync(50);
            Assert.Single(history);
            Assert.Equal(good.Id, history[0].Id);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    // Real values Windows reports for a healthy install.
    [InlineData(0x41000u, "On, up to date")] // ESET, enabled + up to date
    [InlineData(0x61100u, "On, up to date")] // Defender, enabled + up to date
    [InlineData(0x41010u, "On, may be out of date")] // enabled, definitions stale
    [InlineData(0x60100u, "Off")] // Defender present but not enabled
    [InlineData(0x42000u, "Snoozed")]
    [InlineData(null, "Not reported by hardware")]
    public void AntivirusState_DecodesTheWscBitmask(uint? state, string expected)
    {
        Assert.Equal(expected, WindowsSecurityProductState.DecodeProductState(state));
    }

    [Fact]
    public async Task MachineScanService_NeverUsesNotAvailableAsMachineName()
    {
        // A failed system collector used to stamp "Not available" as the
        // machine name on the Overview hero, reports and history.
        var service = MachineScanService.Create(new[]
        {
            new FakeCollector(ComponentSnapshot.NotAvailable(
                "system", "System", "System", "system", "CIM failed")),
        });
        var snapshot = await service.ScanAsync();
        Assert.NotEqual("Not available", snapshot.MachineName);
        Assert.Equal(Environment.MachineName, snapshot.MachineName);
    }

    [Fact]
    public void SafeReportFileName_StripsWindowsInvalidCharacters()
    {
        // On Linux ':' is a legal file name character, but the report is
        // written on Windows, where it is not.
        var name = PathSafety.SafeReportFileName("PC:01", "csv", new DateTime(2026, 1, 1));
        Assert.DoesNotContain(":", name);
        Assert.StartsWith("PC01-", name, StringComparison.Ordinal);
    }

    private sealed class FakeCollector : TrueSpec.Core.Collectors.ICollector
    {
        private readonly ComponentSnapshot _snapshot;

        public FakeCollector(ComponentSnapshot snapshot) => _snapshot = snapshot;

        public string Id => _snapshot.Id;
        public string Name => _snapshot.Name;
        public string Category => _snapshot.Category;
        public TimeSpan RefreshInterval => TimeSpan.FromSeconds(1);
        public bool RequiresAdministrator => false;

        public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_snapshot);
    }
}
