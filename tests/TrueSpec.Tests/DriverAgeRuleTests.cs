using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// Hardening item 6f regression tests: one driver-age rule. The Wi-Fi,
/// Ethernet and GPU pages (<see cref="CollectorSupport.FormatDriverAge"/>)
/// and the Drivers page count
/// (<see cref="DriversCollector.BuildSnapshot"/>) must agree exactly:
/// a driver is flagged stale on a component page exactly when it is
/// counted old on the Drivers page. They used to disagree at the
/// boundary (the count used <c>TotalDays &gt; 365 * 5</c>, the display
/// used calendar years), so a driver one day short of its 5th anniversary
/// was counted old but shown as 4 years old. Both now use
/// <see cref="CollectorSupport.DriverAgeYears"/>. Windows leg only (it
/// drives the Windows collectors), like DriversCollectorTests.
/// </summary>
public class DriverAgeRuleTests
{
    private static WmiRow Driver(string name, string date) =>
        new(new Dictionary<string, object?>
        {
            ["DeviceName"] = name,
            ["DriverDate"] = date,
            ["DriverVersion"] = "1.0.0",
            ["IsSigned"] = true,
        });

    private static string? SpecValue(ComponentSnapshot snapshot, string label) =>
        snapshot.Specs.FirstOrDefault(s => s.Label == label)?.Value;

    private static string Iso(DateTimeOffset date) =>
        date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void SharedRule_IsFiveYears()
    {
        Assert.Equal(5, CollectorSupport.DriverStaleAgeYears);
        Assert.Equal("Drivers older than 5 years", SpecLabels.DriversOlderThan5Years);
    }

    [Fact]
    public void FiveYearOldDriver_IsNotStale_InEitherPlace()
    {
        // Exactly on the 5th anniversary: 5 whole years old, not older
        // than 5. Under the old 365-day count this driver was 1,826 days
        // old and counted stale while the display said "5 years old".
        var asOf = DateTimeOffset.UtcNow;
        var anniversary = Iso(asOf.AddYears(-5));

        Assert.Equal("Driver from " + asOf.AddYears(-5).Year + " (5 years old)",
            CollectorSupport.FormatDriverAge(anniversary, asOf));

        var snapshot = DriversCollector.BuildSnapshot(
            new[] { Driver("Anniversary driver", anniversary) }, null);
        Assert.Equal("0", SpecValue(snapshot, SpecLabels.DriversOlderThan5Years));
    }

    [Fact]
    public void SixYearOldDriver_IsStale_InBothPlaces()
    {
        var asOf = DateTimeOffset.UtcNow;
        var date = Iso(asOf.AddYears(-6));

        Assert.Contains("older than 5 years", CollectorSupport.FormatDriverAge(date, asOf), StringComparison.Ordinal);

        var snapshot = DriversCollector.BuildSnapshot(
            new[] { Driver("Old driver", date) }, null);
        Assert.Equal("1", SpecValue(snapshot, SpecLabels.DriversOlderThan5Years));
    }

    [Fact]
    public void FourYearOldDriver_IsNotStale_InEitherPlace()
    {
        var asOf = DateTimeOffset.UtcNow;
        var date = Iso(asOf.AddYears(-4));

        Assert.DoesNotContain("older than", CollectorSupport.FormatDriverAge(date, asOf), StringComparison.Ordinal);

        var snapshot = DriversCollector.BuildSnapshot(
            new[] { Driver("Recent driver", date) }, null);
        Assert.Equal("0", SpecValue(snapshot, SpecLabels.DriversOlderThan5Years));
    }

    [Fact]
    public void MissingOrFutureDriverDate_IsNotReported_NeverNegative()
    {
        var asOf = DateTimeOffset.UtcNow;

        Assert.Equal(SpecLabels.NotReportedByHardware, CollectorSupport.FormatDriverAge(null, asOf));
        Assert.Equal(SpecLabels.NotReportedByHardware, CollectorSupport.FormatDriverAge("not-a-date", asOf));

        var future = CollectorSupport.FormatDriverAge(Iso(asOf.AddYears(1)), asOf);
        Assert.Contains("0 years old", future, StringComparison.Ordinal);
        Assert.DoesNotContain("older than", future, StringComparison.Ordinal);
    }
}
