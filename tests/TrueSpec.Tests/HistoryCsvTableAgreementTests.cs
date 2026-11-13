using TrueSpec.App.ViewModels;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;

namespace TrueSpec.Tests;

/// <summary>
/// History CSV agreement against the real History table (Windows leg only:
/// the ViewModels live in the App, which the net10.0 leg cannot reference).
/// Every CSV cell must equal the <see cref="HistoryRowViewModel"/> text for
/// the same trend point, including the gap text.
/// </summary>
public class HistoryCsvTableAgreementTests
{
    [Fact]
    public void Build_CellTextEqualsHistoryRowViewModelText()
    {
        var first = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var second = new DateTimeOffset(2026, 1, 31, 10, 30, 0, TimeSpan.Zero);
        var before = TestSnapshots.HistorySnapshot(90d, "90.0%", "10", "2%", "100", first);
        var after = TestSnapshots.HistorySnapshot(85.5d, SpecLabels.NotReportedByHardware, null, "5%", "250", second);

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after });
        var csv = HistoryCsvExporter.Build(trend);
        var lines = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Scanned,Score,Battery health (%),Battery cycles,Storage wear (%),Power-on hours", lines[0]);
        for (var i = 0; i < trend.ScorePoints.Count; i++)
        {
            var row = new HistoryRowViewModel(
                trend.SnapshotIds[i],
                trend.ScorePoints[i].CapturedAt,
                trend.ScorePoints[i].Value,
                trend.BatteryHealthPoints[i].Value,
                trend.BatteryCyclePoints[i].Value,
                trend.StorageWearPoints[i].Value,
                trend.StoragePowerOnHoursPoints[i].Value);
            // The row names the scan it shows: the id at the same index
            // as the point is the scan that point came from.
            Assert.Equal(trend.SnapshotIds[i], row.Id);
            var expected = string.Join(",", new[]
            {
                row.CapturedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                row.ScoreText,
                row.BatteryHealthText,
                row.BatteryCyclesText,
                row.StorageWearText,
                row.StorageHoursText,
            });
            Assert.Equal(expected, lines[i + 1]);
        }
    }
}
