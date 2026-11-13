using TrueSpec.Core.Collectors;

namespace TrueSpec.Tests;

/// <summary>
/// Linux collector parity study (v2.1.6, plan Beyond v2.0 item 3): the
/// catalogue is data, so the tests pin it — every collector has exactly
/// one row, every row names a real source, and the study's headline
/// findings cannot drift silently. Runs on both legs.
/// </summary>
public class LinuxCollectorParityTests
{
    [Fact]
    public void EveryCollectorHasExactlyOneRow()
    {
        var collectorIds = UnavailableCollectorCatalog.CreateAll().Select(c => c.Id).ToArray();
        var rows = LinuxCollectorParity.All;

        Assert.Equal(collectorIds.Length, rows.Count);
        Assert.Equal(
            collectorIds.OrderBy(id => id, StringComparer.Ordinal),
            rows.Select(row => row.CollectorId).OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(
            rows.Count,
            rows.Select(row => row.CollectorId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryRowNamesASourceAndAReason()
    {
        foreach (var row in LinuxCollectorParity.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.CollectorId));
            Assert.False(string.IsNullOrWhiteSpace(row.LinuxSource));
            Assert.False(string.IsNullOrWhiteSpace(row.Notes));
            Assert.True(Enum.IsDefined(row.Feasibility));
        }
    }

    [Fact]
    public void SpotMappingsNameTheRealSource()
    {
        Assert.Contains("power_supply", LinuxCollectorParity.Get("battery")!.LinuxSource, StringComparison.Ordinal);
        Assert.Contains("smartctl", LinuxCollectorParity.Get("storage")!.LinuxSource, StringComparison.Ordinal);
        Assert.Contains("cpuinfo", LinuxCollectorParity.Get("cpu")!.LinuxSource, StringComparison.Ordinal);
        Assert.Contains("dmi", LinuxCollectorParity.Get("system")!.LinuxSource, StringComparison.Ordinal);
        Assert.Contains("dpkg", LinuxCollectorParity.Get("software")!.LinuxSource, StringComparison.Ordinal);
        Assert.Contains("journalctl", LinuxCollectorParity.Get("crash")!.LinuxSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SecurityHasNoLinuxEquivalent()
    {
        Assert.Equal(LinuxParity.NotFeasible, LinuxCollectorParity.Get("security")!.Feasibility);
    }

    [Fact]
    public void NoCollectorPortsAsIs()
    {
        // This pins the study's headline finding: no row reaches Full.
        // A future port that legitimately marks a collector Full must
        // update this test deliberately, with the evidence for the claim.
        Assert.DoesNotContain(LinuxCollectorParity.All, row => row.Feasibility == LinuxParity.Full);
        Assert.Contains("Windows stays the shipping target", LinuxCollectorParity.StudyVerdict, StringComparison.Ordinal);
    }

    [Fact]
    public void GetReturnsNullForUnknownCollector()
    {
        Assert.Null(LinuxCollectorParity.Get("not-a-collector"));
    }

    [Fact]
    public void GetThrowsOnNullId()
    {
        Assert.Throws<ArgumentNullException>(() => LinuxCollectorParity.Get(null!));
    }
}
