using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: the collector reads the real registry and live CIM.
/// On Linux this file is excluded from the build (see
/// TrueSpec.Tests.csproj), like StartupCollectorTests.
/// </summary>
public class SoftwareCollectorTests
{
    [Fact]
    public async Task SoftwareCollector_ListsProgramsWithoutElevationOrThrowing()
    {
        var collector = new SoftwareCollector();

        Assert.Equal("software", collector.Id);
        Assert.Equal("Software", collector.Name);
        Assert.Equal("System", collector.Category);
        Assert.False(collector.RequiresAdministrator);

        var snapshot = await collector.CollectAsync();

        Assert.Equal("software", snapshot.Id);
        Assert.Null(snapshot.HealthPercent);
        Assert.Contains(snapshot.Specs, spec => spec.Label == SpecLabels.InstalledPrograms);
        Assert.Contains(snapshot.Specs, spec => spec.Label == SpecLabels.WindowsActivation);
        Assert.Contains(snapshot.Specs, spec => spec.Label == SpecLabels.LicenseChannel);
        Assert.Contains(snapshot.Specs, spec => spec.Label == SpecLabels.InstalledRuntimes);
        Assert.Contains(snapshot.Specs, spec => spec.Label == SpecLabels.KeysRead);
        Assert.All(snapshot.Specs, spec => Assert.False(string.IsNullOrWhiteSpace(spec.Source)));
        Assert.Contains("product key", snapshot.WhatThisMeans, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SoftwareCollector_NeverExposesAProductKey()
    {
        var snapshot = await new SoftwareCollector().CollectAsync();

        Assert.All(snapshot.Specs, spec =>
        {
            Assert.DoesNotContain("PartialProductKey", spec.Label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PartialProductKey", spec.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ProductKey", spec.Label, StringComparison.OrdinalIgnoreCase);
        });
        Assert.DoesNotContain("Win32_Product", snapshot.Headline, StringComparison.Ordinal);
    }
}
