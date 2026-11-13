using System.Reflection;
using TrueSpec.Core.Health;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// The collector-label contract (hardening 6a): every label a collector
/// emits is either a <see cref="SpecLabels"/> constant or a documented
/// dynamic label built from one ("Drive 2 - Power-on hours",
/// "Volume C: - Free space"). A collector renaming "Power-on hours" used
/// to silently disable rule R1 (AuthenticityAnalyzer) and the storage
/// trend, because the consumers matched the old string. The fixtures in
/// <see cref="TestSnapshots"/> mirror real collector output, so a label
/// that drifts in either place fails here instead of silently disabling
/// a rule on a real machine.
/// </summary>
public class CollectorLabelContractTests
{
    private static readonly HashSet<string> KnownLabels = typeof(SpecLabels)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetValue(null)!)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Dynamic labels: a fixed prefix plus a base label that is itself a
    /// SpecLabels constant. The base label after the prefix must be known.
    /// </summary>
    private static readonly string[] DynamicPrefixes =
    {
        "Drive ",
        "Volume ",
        "Stick ",
        "Adapter ",
        "Old driver - ",
    };

    private static bool IsKnownLabel(string label)
    {
        if (KnownLabels.Contains(label))
        {
            return true;
        }

        foreach (var prefix in DynamicPrefixes)
        {
            if (!label.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            // "Drive 2 - Power-on hours": the base label after " - ".
            var separator = label.IndexOf(" - ", StringComparison.Ordinal);
            if (separator < 0)
            {
                // "Old driver - <device name>": a device name, not a label.
                return prefix == "Old driver - ";
            }

            return KnownLabels.Contains(label[(separator + 3)..]);
        }

        return false;
    }

    private static IEnumerable<SpecValue> AllFixtureSpecs()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshots = new[]
        {
            TestSnapshots.Healthy(),
            TestSnapshots.HistorySnapshot(82, "90%", "120", "4", "12400", now),
            TestSnapshots.HistorySnapshot(70, "75%", "300", "12", "45000", now, oldestDriverDate: "2019-06-06", driversOlderThan5: "3", lastUpdateInstall: "2026-10-01"),
        };

        return snapshots
            .SelectMany(s => s.Components)
            .SelectMany(c => c.Specs);
    }

    [Fact]
    public void EveryFixtureLabel_IsASpecLabelsConstantOrADocumentedDynamicLabel()
    {
        Assert.NotEmpty(KnownLabels);

        var unknown = AllFixtureSpecs()
            .Select(s => s.Label)
            .Distinct(StringComparer.Ordinal)
            .Where(label => !IsKnownLabel(label))
            .ToList();

        Assert.True(
            unknown.Count == 0,
            "Labels that are neither a SpecLabels constant nor a documented dynamic label: " +
            string.Join(", ", unknown));
    }

    [Fact]
    public void RuleConsumedLabels_ResolveOnARealSnapshot()
    {
        // The labels the health rules, the authenticity analyzer and the
        // trends read must be present on a snapshot shaped like a real
        // scan, or the rules silently skip. "Power-on hours" is the R1
        // case: a rename used to disable the rule with no error.
        var snapshot = TestSnapshots.Healthy();
        var storage = snapshot.Find("storage")!;

        Assert.Equal(0d, SpecText.Number(storage.Specs, SpecLabels.PowerOnHours));
        Assert.Equal(0d, SpecText.Number(storage.Specs, SpecLabels.WearLevel));
        Assert.Equal(0d, SpecText.Number(storage.Specs, SpecLabels.UncorrectedErrors));

        var battery = snapshot.Find("battery")!;
        Assert.Equal(50000d, SpecText.Number(battery.Specs, SpecLabels.DesignCapacity));
        Assert.Equal(45000d, SpecText.Number(battery.Specs, SpecLabels.FullChargeCapacity));

        var cpu = snapshot.Find("cpu")!;
        Assert.Equal(55d, SpecText.Number(cpu.Specs, SpecLabels.PackageTemperature));

        var gpu = snapshot.Find("gpu")!;
        Assert.Equal(60d, SpecText.Number(gpu.Specs, SpecLabels.GPUTemperature));

        // A history-shaped snapshot carries a real power-on-hours reading.
        var history = TestSnapshots.HistorySnapshot(82, "90%", "120", "4", "12400", DateTimeOffset.UtcNow);
        var historyStorage = history.Find("storage")!;
        Assert.Equal(12400d, SpecText.Number(historyStorage.Specs, SpecLabels.PowerOnHours));
        Assert.Equal(4d, SpecText.Number(historyStorage.Specs, SpecLabels.WearLevel));
    }

    [Fact]
    public void RuleConsumedLabels_TypedValueBeatsARenamedDisplayString()
    {
        // Even if a collector rewords the display string, the typed
        // channel keeps the rule working: the rule reads NumericValue,
        // not the text.
        var storage = TestSnapshots.Storage("0", "12400") with
        {
            Specs = new[]
            {
                new SpecValue(SpecLabels.PowerOnHours, "about twelve thousand hours", "SMART", NumericValue: 12400d, NumericUnit: "hours"),
            },
        };

        Assert.Equal(12400d, SpecText.Number(storage.Specs, SpecLabels.PowerOnHours));
    }

    [Fact]
    public void OsVisibleMemoryLabel_NeverClaimsToBeATotal()
    {
        // AppOnFly v2.0.0: Win32_ComputerSystem.TotalPhysicalMemory moved
        // 2.0 GB -> 1.8 GB between scans of the same virtual machine, and
        // the Compare page showed a "total" changing. The value is the
        // memory Windows currently sees (dynamic memory on a VM moves
        // it); only the Win32_PhysicalMemory stick sum is the installed
        // total. The label must never say "total" again.
        Assert.Equal("Memory visible to Windows", SpecLabels.TotalMemoryReportedByWindows);
        Assert.DoesNotContain("Total", SpecLabels.TotalMemoryReportedByWindows, StringComparison.OrdinalIgnoreCase);
    }
}
