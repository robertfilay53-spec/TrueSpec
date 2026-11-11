using TrueSpec.Collectors.Windows.Collectors;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: the collector reads the real registry. On Linux this
/// file is excluded from the build (see TrueSpec.Tests.csproj), like
/// ScoreAgreementTests.
/// </summary>
public class StartupCollectorTests
{
    [Fact]
    public async Task StartupCollector_ListsRunEntriesWithoutElevationOrThrowing()
    {
        var collector = new StartupCollector();

        Assert.Equal("startup", collector.Id);
        Assert.Equal("Startup", collector.Name);
        Assert.False(collector.RequiresAdministrator);

        var snapshot = await collector.CollectAsync();

        Assert.Equal("startup", snapshot.Id);
        Assert.Null(snapshot.HealthPercent);
        Assert.Contains(snapshot.Specs, spec => spec.Label == "Startup programs");
        Assert.Contains(snapshot.Specs, spec => spec.Label == "Keys read");
        Assert.All(snapshot.Specs, spec => Assert.False(string.IsNullOrWhiteSpace(spec.Source)));
        Assert.Contains("never disables", snapshot.WhatThisMeans, StringComparison.OrdinalIgnoreCase);
    }
}
