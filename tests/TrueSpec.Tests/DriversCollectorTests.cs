using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// H2 regression tests: the WMI 1000-row cap silently truncated the
/// driver list, so "Installed drivers" and the age counts were computed
/// over a partial list with no indication. The collector now surfaces
/// truncation honestly: a "1000+" headline plus a truncation spec row.
/// The tests drive the pure <see cref="DriversCollector.BuildSnapshot"/>
/// helper with fake rows, so no live WMI is needed.
/// </summary>
public class DriversCollectorTests
{
    // Plain ISO dates, not DMTF ("20100101000000.000000+000"): WmiRow.Date
    // parses DMTF via ManagementDateTimeConverter, which throws
    // PlatformNotSupportedException off Windows, and TryParse cannot read
    // DMTF — so DMTF fakes would date every row null on the Linux leg.
    // ISO parses on both legs (ToDateTime throws, TryParse succeeds).
    // DMTF parsing itself is WmiRow's job, covered by live-WMI tests.
    private const string RecentDate = "2025-06-01";
    private const string OldDate = "2010-01-01";

    private static WmiRow Driver(string name, string date, bool signed) =>
        new(new Dictionary<string, object?>
        {
            ["DeviceName"] = name,
            ["DriverDate"] = date,
            ["DriverVersion"] = "1.0.0",
            ["IsSigned"] = signed,
        });

    private static List<WmiRow> Drivers(int count, string date = RecentDate, bool signed = true)
    {
        var rows = new List<WmiRow>(count);
        for (var i = 0; i < count; i++)
        {
            rows.Add(Driver($"Test driver {i}", date, signed));
        }

        return rows;
    }

    private static string? SpecValue(ComponentSnapshot snapshot, string label) =>
        snapshot.Specs.FirstOrDefault(s => s.Label == label)?.Value;

    [Fact]
    public void TruncatedList_HeadlineSays1000Plus_AndTruncationNotePresent()
    {
        var snapshot = DriversCollector.BuildSnapshot(Drivers(1000), Wmi.TruncatedError);

        Assert.StartsWith("1000+ drivers", snapshot.Headline, StringComparison.Ordinal);
        Assert.DoesNotContain("1000 drivers -", snapshot.Headline, StringComparison.Ordinal);
        Assert.Equal("1000", SpecValue(snapshot, SpecLabels.InstalledDrivers));
        var note = snapshot.Specs.FirstOrDefault(s => s.Label == "Driver list truncated");
        Assert.NotNull(note);
        Assert.Contains("truncated at 1000 rows", note!.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("first 1000", note.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TruncatedList_CountsCoverFirst1000Only()
    {
        // 998 recent signed drivers, one very old signed driver and one
        // recent unsigned driver: the counts are over the rows actually
        // read, and the truncation row says so.
        var rows = Drivers(998);
        rows.Add(Driver("Old test driver", OldDate, signed: true));
        rows.Add(Driver("Unsigned test driver", RecentDate, signed: false));

        var snapshot = DriversCollector.BuildSnapshot(rows, Wmi.TruncatedError);

        Assert.StartsWith("1000+ drivers - 1 unsigned", snapshot.Headline, StringComparison.Ordinal);
        Assert.Equal("1", SpecValue(snapshot, SpecLabels.DriversOlderThan5Years));
        Assert.Equal("2010-01-01", SpecValue(snapshot, SpecLabels.OldestDriverDate));
        Assert.Contains(snapshot.Specs, s => s.Label == "Driver list truncated");
    }

    [Fact]
    public void FullList_HeadlineIsExact_AndNoTruncationNote()
    {
        var rows = new List<WmiRow>
        {
            Driver("Recent signed driver", RecentDate, signed: true),
            Driver("Recent unsigned driver", RecentDate, signed: false),
            Driver("Old signed driver", OldDate, signed: true),
        };

        var snapshot = DriversCollector.BuildSnapshot(rows, queryError: null);

        Assert.Equal("3 drivers - 1 unsigned", snapshot.Headline);
        Assert.Equal("3", SpecValue(snapshot, SpecLabels.InstalledDrivers));
        Assert.Equal("1", SpecValue(snapshot, SpecLabels.DriversOlderThan5Years));
        Assert.Equal("2010-01-01", SpecValue(snapshot, SpecLabels.OldestDriverDate));
        Assert.DoesNotContain(snapshot.Specs, s => s.Label == "Driver list truncated");
    }
}
