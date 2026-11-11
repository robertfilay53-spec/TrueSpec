using TrueSpec.Collectors.Windows.Collectors;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: the collector reads the real registry and WMI. On
/// Linux this file is excluded from the build (see TrueSpec.Tests.csproj),
/// like StartupCollectorTests. No dates or counts are asserted: they depend
/// on the machine the test runs on.
/// </summary>
public class UpdatesCollectorTests
{
    [Fact]
    public async Task UpdatesCollector_ReportsUpdateStateWithoutElevation()
    {
        var collector = new UpdatesCollector();

        Assert.Equal("updates", collector.Id);
        Assert.Equal("Windows Update", collector.Name);
        Assert.False(collector.RequiresAdministrator);

        var snapshot = await collector.CollectAsync();

        Assert.Equal("updates", snapshot.Id);
        Assert.Null(snapshot.HealthPercent);
        Assert.Contains(snapshot.Specs, spec => spec.Label == "Update management");
        Assert.Contains(snapshot.Specs, spec => spec.Label == "Last successful update install");
        Assert.Contains(snapshot.Specs, spec => spec.Label == "Installed updates (hotfixes)");
        Assert.All(snapshot.Specs, spec => Assert.False(string.IsNullOrWhiteSpace(spec.Source)));
        Assert.Contains("WSUS", snapshot.WhatThisMeans, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("managed", snapshot.WhatThisMeans, StringComparison.OrdinalIgnoreCase);
    }
}
