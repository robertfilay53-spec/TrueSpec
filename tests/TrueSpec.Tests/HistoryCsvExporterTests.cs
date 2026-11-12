using System.Globalization;
using System.Text;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;

namespace TrueSpec.Tests;

/// <summary>
/// History CSV export (v1.2): the CSV is built from the same
/// <see cref="HistoryTrend"/> the History page already has, so every cell
/// must read exactly like the History table for the same data. The expected
/// cell text below mirrors <c>HistoryRowViewModel</c> (score "Not
/// measurable" else "0.#"; battery health / storage wear "Not reported"
/// else "0.#%"; cycles / power-on hours "Not reported" else "0") with the
/// same format strings, because Core cannot reference the App ViewModels.
/// A Windows-leg test (HistoryCsvTableAgreementTests) checks the agreement
/// against the real ViewModel directly.
/// </summary>
public class HistoryCsvExporterTests
{
    private static readonly DateTimeOffset FirstScan = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondScan = new(2026, 1, 31, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Build_WritesHeaderAndRowsOldestFirst()
    {
        var before = TestSnapshots.HistorySnapshot(90d, "90.0%", "10", "2%", "100", FirstScan);
        var after = TestSnapshots.HistorySnapshot(85.5d, "85.5%", "12", "5%", "250", SecondScan);

        // Input newest first on purpose: the CSV must still be oldest first.
        var trend = HistoryTrendAnalyzer.Analyze(new[] { after, before });
        var csv = HistoryCsvExporter.Build(trend);

        var expected = string.Join(Environment.NewLine, new[]
        {
            "Scanned,Score,Battery health (%),Battery cycles,Storage wear (%),Power-on hours",
            "2026-01-01 10:00,90,90%,10,2%,100",
            "2026-01-31 10:30,85.5,85.5%,12,5%,250",
            string.Empty,
        });
        Assert.Equal(expected, csv);
    }

    [Fact]
    public void Build_GapsRenderAsTheHistoryTableText()
    {
        var before = TestSnapshots.HistorySnapshot(90d, "90.0%", "10", "2%", "100", FirstScan);
        // Second scan: the battery stopped reporting health, and the cycle
        // count and power-on hours specs are missing entirely.
        var after = TestSnapshots.HistorySnapshot(85d, "Not reported by hardware", null, "5%", null, SecondScan);

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after });
        var csv = HistoryCsvExporter.Build(trend);
        var lines = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.Equal("2026-01-31 10:30,85,Not reported,Not reported,5%,Not reported", lines[2]);
    }

    [Fact]
    public void Build_CellTextEqualsHistoryTableCellText()
    {
        // Same trend, same data: every CSV cell must equal the text the
        // History table shows, formatted with the table's own format
        // strings (see HistoryRowViewModel).
        var before = TestSnapshots.HistorySnapshot(90d, "90.0%", "10", "2%", "100", FirstScan);
        var after = TestSnapshots.HistorySnapshot(85.5d, "Not reported by hardware", null, "5%", "250", SecondScan);

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after });
        var csv = HistoryCsvExporter.Build(trend);
        var lines = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < trend.ScorePoints.Count; i++)
        {
            var expected = string.Join(",", new[]
            {
                trend.ScorePoints[i].CapturedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                ScoreCell(trend.ScorePoints[i].Value),
                PercentCell(trend.BatteryHealthPoints[i].Value),
                CountCell(trend.BatteryCyclePoints[i].Value),
                PercentCell(trend.StorageWearPoints[i].Value),
                CountCell(trend.StoragePowerOnHoursPoints[i].Value),
            });
            Assert.Equal(expected, lines[i + 1]);
        }
    }

    [Fact]
    public void Build_EmptyTrend_WritesHeaderOnly()
    {
        var csv = HistoryCsvExporter.Build(HistoryTrend.Empty);

        Assert.Equal(
            "Scanned,Score,Battery health (%),Battery cycles,Storage wear (%),Power-on hours" + Environment.NewLine,
            csv);
    }

    [Fact]
    public async Task ExportAsync_WritesUtf8BomAndSameTextAsBuild()
    {
        var before = TestSnapshots.HistorySnapshot(90d, "90.0%", "10", "2%", "100", FirstScan);
        var after = TestSnapshots.HistorySnapshot(85d, "85.0%", "11", "4%", "150", SecondScan);
        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after });

        using var stream = new MemoryStream();
        await new HistoryCsvExporter().ExportAsync(trend, stream);

        var bytes = stream.ToArray();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal(HistoryCsvExporter.Build(trend), Encoding.UTF8.GetString(bytes[3..]));
    }

    [Fact]
    public async Task ExportAsync_NullArgumentsThrow()
    {
        var exporter = new HistoryCsvExporter();
        var trend = HistoryTrendAnalyzer.Analyze(Array.Empty<SystemSnapshot>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => exporter.ExportAsync(null!, new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => exporter.ExportAsync(trend, null!));
    }

    [Fact]
    public void Build_NullTrendThrows()
    {
        Assert.Throws<ArgumentNullException>(() => HistoryCsvExporter.Build(null!));
    }

    // The History table's cell formats, copied from HistoryRowViewModel.
    private static string ScoreCell(double? score) =>
        score is null ? "Not measurable" : score.Value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string PercentCell(double? value) =>
        value is null ? "Not reported" : string.Create(CultureInfo.InvariantCulture, $"{value.Value:0.#}%");

    private static string CountCell(double? value) =>
        value is null ? "Not reported" : value.Value.ToString("0", CultureInfo.InvariantCulture);
}
