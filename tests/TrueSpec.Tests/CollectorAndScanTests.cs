using TrueSpec.Core;
using TrueSpec.Core.Collectors;

namespace TrueSpec.Tests;

public class CollectorAndScanTests
{
    [Fact]
    public void StubCatalog_ExposesOneCollectorPerDashboardComponent()
    {
        var collectors = StubCollectorCatalog.CreateAll();

        Assert.Equal(10, collectors.Count);
        Assert.Equal(collectors.Count, collectors.Select(c => c.Id).Distinct().Count());
        Assert.All(collectors, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Category));
            Assert.True(c.RefreshInterval > TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task EveryStubCollector_ReturnsSpecsAndAHeadline()
    {
        foreach (var collector in StubCollectorCatalog.CreateAll())
        {
            var snapshot = await collector.CollectAsync();

            Assert.Equal(collector.Id, snapshot.Id);
            Assert.False(string.IsNullOrWhiteSpace(snapshot.Headline));
            Assert.NotEmpty(snapshot.Specs);
            Assert.False(string.IsNullOrWhiteSpace(snapshot.WhatThisMeans));
            Assert.All(snapshot.Specs, spec => Assert.False(string.IsNullOrWhiteSpace(spec.Source)));
        }
    }

    [Fact]
    public async Task ScanService_CombinesReadingsScoreAndBuyerChecks()
    {
        var service = MachineScanService.CreateDefaultStub();

        var snapshot = await service.ScanAsync();

        Assert.NotEqual(Guid.Empty, snapshot.Id);
        Assert.Equal(10, snapshot.Components.Count);
        Assert.True(snapshot.Score.HasMeasurableComponents);
        Assert.NotEmpty(snapshot.BuyerChecks.Checks);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.MachineName));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.OperatingSystem));
    }
}
