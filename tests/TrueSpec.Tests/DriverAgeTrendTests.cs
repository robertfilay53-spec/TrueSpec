using System.Globalization;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// Driver-age trend (v1.2): the age in days of the oldest driver at scan
/// time, from the "drivers" component's "Oldest driver date" spec. A
/// missing, unparseable or future date is a gap, never a zero; the delta is
/// latest minus first measured age, so a negative delta means the oldest
/// driver got newer.
/// </summary>
public class DriverAgeTrendTests
{
    private static readonly DateTimeOffset FirstScan = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondScan = new(2026, 1, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SameOldestDriver_AgeGrowsByDaysBetweenScans()
    {
        var before = DriverSnapshot(FirstScan, "2020-01-15", "3");
        var after = DriverSnapshot(SecondScan, "2020-01-15", "5");

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after });

        Assert.Equal(2, trend.DriverAgePoints.Count);
        Assert.Equal(AgeDays(FirstScan, "2020-01-15"), trend.DriverAgePoints[0].Value);
        Assert.Equal(AgeDays(SecondScan, "2020-01-15"), trend.DriverAgePoints[1].Value);
        Assert.Equal(30d, trend.DriverAgeDelta);
        Assert.Contains("2020-01-15", trend.DriverAgeSummary, StringComparison.Ordinal);
        Assert.Contains("got 30 days older", trend.DriverAgeSummary, StringComparison.Ordinal);
        Assert.Contains("went from 3 to 5", trend.DriverAgeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void NewerOldestDriver_AgeFallsAndDeltaIsNegative()
    {
        var before = DriverSnapshot(FirstScan, "2019-01-01", "4");
        var after = DriverSnapshot(SecondScan, "2024-06-01", "1");

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after });

        var expectedDelta = AgeDays(SecondScan, "2024-06-01") - AgeDays(FirstScan, "2019-01-01");
        Assert.Equal(expectedDelta, trend.DriverAgeDelta);
        Assert.True(trend.DriverAgeDelta < 0);
        Assert.Contains("got 1948 days newer", trend.DriverAgeSummary, StringComparison.Ordinal);
        Assert.Contains("went from 4 to 1", trend.DriverAgeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void OlderOldestDriver_AgeRisesFasterThanTimePassing()
    {
        var before = DriverSnapshot(FirstScan, "2024-01-01", "0");
        var after = DriverSnapshot(SecondScan, "2018-06-01", "2");

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after });

        Assert.True(trend.DriverAgeDelta > 30d);
        Assert.Contains("got", trend.DriverAgeSummary, StringComparison.Ordinal);
        Assert.Contains("days older", trend.DriverAgeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void OldDriverCountUnchanged_SummarySaysSo()
    {
        var before = DriverSnapshot(FirstScan, "2020-01-15", "3");
        var after = DriverSnapshot(SecondScan, "2020-01-15", "3");

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after });

        Assert.Contains("stayed at 3", trend.DriverAgeSummary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("15/01/2020")]
    [InlineData("2027-06-01")]
    public void BadOldestDriverDate_IsAGapNeverAZero(string badDate)
    {
        // The second scan reports a date that cannot be used: unparseable,
        // wrong format, or in the future relative to the scan. Only the
        // first scan measured an age, so there is no delta and the summary
        // says one more scan is needed.
        var before = DriverSnapshot(FirstScan, "2020-01-15", "3");
        var after = DriverSnapshot(SecondScan, badDate, "3");

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after });

        Assert.Null(trend.DriverAgePoints[1].Value);
        Assert.Equal(AgeDays(FirstScan, "2020-01-15"), trend.DriverAgePoints[0].Value);
        Assert.Null(trend.DriverAgeDelta);
        Assert.Contains("one more scan is needed", trend.DriverAgeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingDriversComponentInMiddleScan_IsAGapAndDeltaStillComputed()
    {
        var first = DriverSnapshot(FirstScan, "2020-01-15", "3");
        var middle = TestSnapshots.HistorySnapshot(88d, "88.0%", "11", "3%", "200", new DateTimeOffset(2026, 1, 16, 10, 0, 0, TimeSpan.Zero));
        var last = DriverSnapshot(SecondScan, "2020-01-15", "5");

        var trend = HistoryTrendAnalyzer.Analyze(new[] { first, middle, last });

        Assert.Equal(3, trend.DriverAgePoints.Count);
        Assert.Null(trend.DriverAgePoints[1].Value);
        Assert.Equal(30d, trend.DriverAgeDelta);
        Assert.Contains("got 30 days older", trend.DriverAgeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleScan_HasAPointButNoDelta()
    {
        var trend = HistoryTrendAnalyzer.Analyze(new[] { DriverSnapshot(FirstScan, "2020-01-15", "3") });

        Assert.Single(trend.DriverAgePoints);
        Assert.Equal(AgeDays(FirstScan, "2020-01-15"), trend.DriverAgePoints[0].Value);
        Assert.Null(trend.DriverAgeDelta);
        Assert.Contains("2020-01-15", trend.DriverAgeSummary, StringComparison.Ordinal);
        Assert.Contains("one more scan is needed", trend.DriverAgeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void NoDriversComponent_SummaryIsNullAndPointsAreGaps()
    {
        var before = TestSnapshots.HistorySnapshot(90d, "90.0%", "10", "2%", "100", FirstScan);
        var after = TestSnapshots.HistorySnapshot(85d, "85.0%", "11", "4%", "150", SecondScan);

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after });

        Assert.All(trend.DriverAgePoints, p => Assert.Null(p.Value));
        Assert.Null(trend.DriverAgeDelta);
        Assert.Null(trend.DriverAgeSummary);
    }

    [Fact]
    public void EmptyHistory_DriverFieldsAreEmpty()
    {
        var trend = HistoryTrendAnalyzer.Analyze(Array.Empty<SystemSnapshot>());

        Assert.Empty(trend.DriverAgePoints);
        Assert.Null(trend.DriverAgeDelta);
        Assert.Null(trend.DriverAgeSummary);
    }

    [Fact]
    public void MixedMachines_Throws()
    {
        var first = DriverSnapshot(FirstScan, "2020-01-15", "3");
        var other = DriverSnapshot(SecondScan, "2020-01-15", "3", machineName: "Other Laptop");

        Assert.Throws<ArgumentException>(() => HistoryTrendAnalyzer.Analyze(new[] { first, other }));
    }

    private static SystemSnapshot DriverSnapshot(
        DateTimeOffset capturedAt,
        string oldestDriverDate,
        string driversOlderThan5,
        string machineName = "Test Laptop") =>
        TestSnapshots.HistorySnapshot(
            90d, "90.0%", "10", "2%", "100",
            capturedAt, machineName,
            oldestDriverDate: oldestDriverDate,
            driversOlderThan5: driversOlderThan5);

    /// <summary>
    /// Whole days between a day-precision driver date and the scan date,
    /// computed independently of the implementation (DateOnly day numbers).
    /// </summary>
    private static double AgeDays(DateTimeOffset scan, string driverDate) =>
        new DateOnly(scan.Year, scan.Month, scan.Day).DayNumber
        - DateOnly.ParseExact(driverDate, "yyyy-MM-dd", CultureInfo.InvariantCulture).DayNumber;
}
