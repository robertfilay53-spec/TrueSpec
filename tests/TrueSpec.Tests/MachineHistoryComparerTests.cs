using TrueSpec.Core.History;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// Multi-machine history (v1.2): one summary per machine, facts only, no
/// verdict and no "which is better". Each call reads one machine's list;
/// mixed machines are refused, an empty list is a zero-scan summary.
/// </summary>
public class MachineHistoryComparerTests
{
    private static readonly DateTimeOffset FirstScan = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondScan = new(2026, 1, 31, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Summarize_TwoScans_ReportsLatestValues()
    {
        var before = MachineSnapshot("Alpha Laptop", FirstScan, 90d, "90.0%", "2%", "2020-01-15", "2026-01-05 09:00");
        var after = MachineSnapshot("Alpha Laptop", SecondScan, 85d, "85.0%", "5%", "2019-06-01", "2026-01-20 09:30");

        var summary = MachineHistoryComparer.Summarize(new[] { before, after });

        Assert.Equal("Alpha Laptop", summary.MachineName);
        Assert.Equal(2, summary.ScanCount);
        Assert.Equal(FirstScan, summary.FirstScan);
        Assert.Equal(SecondScan, summary.LastScan);
        Assert.Equal(85d, summary.LatestScore);
        Assert.Equal(85d, summary.LatestBatteryHealth);
        Assert.Equal(5d, summary.LatestStorageWear);
        Assert.Equal(new DateTimeOffset(2019, 6, 1, 0, 0, 0, TimeSpan.Zero), summary.LatestOldestDriverDate);
        Assert.Equal(new DateTimeOffset(2026, 1, 20, 9, 30, 0, TimeSpan.Zero), summary.LastUpdateInstall);
    }

    [Fact]
    public void Summarize_LatestIsNewestScanEvenWhenInputIsUnordered()
    {
        var before = MachineSnapshot("Alpha Laptop", FirstScan, 90d, "90.0%", "2%", "2020-01-15", "2026-01-05 09:00");
        var after = MachineSnapshot("Alpha Laptop", SecondScan, 85d, "85.0%", "5%", "2019-06-01", "2026-01-20 09:30");

        var summary = MachineHistoryComparer.Summarize(new[] { after, before });

        Assert.Equal(SecondScan, summary.LastScan);
        Assert.Equal(85d, summary.LatestScore);
        Assert.Equal(new DateTimeOffset(2019, 6, 1, 0, 0, 0, TimeSpan.Zero), summary.LatestOldestDriverDate);
    }

    [Fact]
    public void Summarize_EmptyList_ReturnsZeroScansAndNulls()
    {
        var summary = MachineHistoryComparer.Summarize(Array.Empty<SystemSnapshot>());

        Assert.Equal(0, summary.ScanCount);
        Assert.Null(summary.FirstScan);
        Assert.Null(summary.LastScan);
        Assert.Null(summary.LatestScore);
        Assert.Null(summary.LatestBatteryHealth);
        Assert.Null(summary.LatestStorageWear);
        Assert.Null(summary.LatestOldestDriverDate);
        Assert.Null(summary.LastUpdateInstall);
    }

    [Fact]
    public void Summarize_MixedMachines_Throws()
    {
        var first = MachineSnapshot("Alpha Laptop", FirstScan, 90d, "90.0%", "2%", "2020-01-15", "2026-01-05 09:00");
        var other = MachineSnapshot("Beta Laptop", SecondScan, 80d, "80.0%", "1%", "2021-01-15", "2026-01-06 09:00");

        Assert.Throws<ArgumentException>(() => MachineHistoryComparer.Summarize(new[] { first, other }));
    }

    [Fact]
    public void Summarize_GapsInLatestScan_BecomeNullInsteadOfOlderValues()
    {
        // The latest scan reports nothing measurable: the summary must not
        // fall back to the older scan's values, because "latest" means the
        // latest scan, gaps included.
        var before = MachineSnapshot("Alpha Laptop", FirstScan, 90d, "90.0%", "2%", "2020-01-15", "2026-01-05 09:00");
        var after = TestSnapshots.HistorySnapshot(
            0d, SpecLabels.NotReportedByHardware, "11", "5%", "150", SecondScan, "Alpha Laptop",
            measurableScore: false);

        var summary = MachineHistoryComparer.Summarize(new[] { before, after });

        Assert.Equal(2, summary.ScanCount);
        Assert.Null(summary.LatestScore);
        Assert.Null(summary.LatestBatteryHealth);
        Assert.Equal(5d, summary.LatestStorageWear);
        Assert.Null(summary.LatestOldestDriverDate);
        Assert.Null(summary.LastUpdateInstall);
    }

    [Theory]
    [InlineData("not-a-date", "2026-01-20 09:30")]
    [InlineData("2020-01-15", "yesterday")]
    [InlineData("15/01/2020", "20/01/2026 09:30")]
    public void Summarize_UnparseableDates_AreNull(string driverDate, string updateInstall)
    {
        var snapshot = MachineSnapshot("Alpha Laptop", FirstScan, 90d, "90.0%", "2%", driverDate, updateInstall);

        var summary = MachineHistoryComparer.Summarize(new[] { snapshot });

        if (driverDate == "2020-01-15")
        {
            Assert.Equal(new DateTimeOffset(2020, 1, 15, 0, 0, 0, TimeSpan.Zero), summary.LatestOldestDriverDate);
            Assert.Null(summary.LastUpdateInstall);
        }
        else if (updateInstall == "2026-01-20 09:30")
        {
            Assert.Null(summary.LatestOldestDriverDate);
            Assert.Equal(new DateTimeOffset(2026, 1, 20, 9, 30, 0, TimeSpan.Zero), summary.LastUpdateInstall);
        }
        else
        {
            Assert.Null(summary.LatestOldestDriverDate);
            Assert.Null(summary.LastUpdateInstall);
        }
    }

    [Fact]
    public void Compare_PairsTwoMachineSummaries()
    {
        var alpha = new[] { MachineSnapshot("Alpha Laptop", FirstScan, 90d, "90.0%", "2%", "2020-01-15", "2026-01-05 09:00") };
        var beta = new[]
        {
            MachineSnapshot("Beta Laptop", FirstScan, 70d, "70.0%", "8%", "2018-03-01", "2025-12-01 08:00"),
            MachineSnapshot("Beta Laptop", SecondScan, 72d, "71.0%", "9%", "2018-03-01", "2026-01-10 08:00"),
        };

        var comparison = MachineHistoryComparer.Compare(alpha, beta);

        Assert.Equal("Alpha Laptop", comparison.MachineA.MachineName);
        Assert.Equal(1, comparison.MachineA.ScanCount);
        Assert.Equal(90d, comparison.MachineA.LatestScore);
        Assert.Equal("Beta Laptop", comparison.MachineB.MachineName);
        Assert.Equal(2, comparison.MachineB.ScanCount);
        Assert.Equal(72d, comparison.MachineB.LatestScore);
        Assert.Equal(71d, comparison.MachineB.LatestBatteryHealth);
    }

    [Fact]
    public void Compare_MixedListOnOneSide_Throws()
    {
        var mixed = new[]
        {
            MachineSnapshot("Alpha Laptop", FirstScan, 90d, "90.0%", "2%", "2020-01-15", "2026-01-05 09:00"),
            MachineSnapshot("Beta Laptop", SecondScan, 80d, "80.0%", "1%", "2021-01-15", "2026-01-06 09:00"),
        };
        var beta = new[] { MachineSnapshot("Beta Laptop", FirstScan, 70d, "70.0%", "8%", "2018-03-01", "2025-12-01 08:00") };

        Assert.Throws<ArgumentException>(() => MachineHistoryComparer.Compare(mixed, beta));
    }

    private static SystemSnapshot MachineSnapshot(
        string machineName,
        DateTimeOffset capturedAt,
        double score,
        string batteryHealth,
        string wear,
        string oldestDriverDate,
        string lastUpdateInstall) =>
        TestSnapshots.HistorySnapshot(
            score, batteryHealth, "10", wear, "100",
            capturedAt, machineName,
            oldestDriverDate: oldestDriverDate,
            lastUpdateInstall: lastUpdateInstall);
}
