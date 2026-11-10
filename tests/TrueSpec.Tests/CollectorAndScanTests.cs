using TrueSpec.Core;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

public class CollectorAndScanTests
{
    [Fact]
    public void UnavailableCatalog_ExposesEveryDashboardComponent()
    {
        var collectors = UnavailableCollectorCatalog.CreateAll();

        Assert.Equal(11, collectors.Count);
        Assert.Equal(collectors.Count, collectors.Select(c => c.Id).Distinct().Count());
        Assert.Contains(collectors, c => c.Id == "system");
        Assert.Contains(collectors, c => c.Id == "drivers");
        Assert.All(collectors, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Category));
            Assert.True(c.RefreshInterval > TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task EveryUnavailableCollector_ReturnsAReasonAndNoHealthNumber()
    {
        foreach (var collector in UnavailableCollectorCatalog.CreateAll())
        {
            var snapshot = await collector.CollectAsync();

            Assert.Equal(collector.Id, snapshot.Id);
            Assert.Equal("Not available", snapshot.Headline);
            Assert.Null(snapshot.HealthPercent);
            Assert.Contains(snapshot.Specs, spec => spec.Label == "Status");
            Assert.All(snapshot.Specs, spec =>
                Assert.False(string.IsNullOrWhiteSpace(spec.Source)));
            Assert.Contains("does not invent", snapshot.WhatThisMeans, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DefaultScan_UsesHonestFallbackAndReportsNoScore()
    {
        var snapshot = await MachineScanService.CreateDefault().ScanAsync();

        Assert.Equal(4, snapshot.Components.Count);
        Assert.All(snapshot.Components, component => Assert.Null(component.HealthPercent));
        Assert.False(snapshot.Score.HasMeasurableComponents);
        Assert.Equal(0d, snapshot.Score.OverallScore);
        Assert.NotEmpty(snapshot.BuyerChecks.Checks);
        Assert.All(snapshot.BuyerChecks.Checks, check =>
            Assert.Equal(BuyerCheckState.NotApplicable, check.State));
    }

    [Fact]
    public async Task ScanService_AppliesMeasuredHealthRulesAndKeepsSources()
    {
        var now = DateTimeOffset.UtcNow;
        var components = new[]
        {
            TestSnapshots.System(now),
            TestSnapshots.Storage("10", "0", health: null),
            TestSnapshots.Battery(2, health: null),
            TestSnapshots.Ram("16 GB"),
        };
        var service = MachineScanService.Create(components.Select(c => new FixedCollector(c)));

        var snapshot = await service.ScanAsync(new BuyerCheckContext("16 GB"));

        Assert.Equal(4, snapshot.Components.Count);
        Assert.Equal(90d, snapshot.Find("storage")!.HealthPercent!.Value, 3);
        Assert.Equal(90d, snapshot.Find("battery")!.HealthPercent!.Value, 3);
        Assert.True(snapshot.Score.HasMeasurableComponents);
        Assert.NotEmpty(snapshot.BuyerChecks.Checks);
        Assert.All(snapshot.Components.SelectMany(c => c.Specs), spec =>
            Assert.False(string.IsNullOrWhiteSpace(spec.Source)));
    }

    private sealed class FixedCollector : ICollector
    {
        private readonly ComponentSnapshot _component;

        public FixedCollector(ComponentSnapshot component)
        {
            _component = component;
        }

        public string Id => "fixed";

        public string Name => "Fixed";

        public string Category => "Test";

        public string IconKey => "system";

        public TimeSpan RefreshInterval => TimeSpan.FromSeconds(2);

        public bool RequiresAdministrator => false;

        public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_component);
        }
    }
}
