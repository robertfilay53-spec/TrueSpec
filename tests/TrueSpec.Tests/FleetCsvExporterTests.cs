using System.Text;
using TrueSpec.Core.History;
using TrueSpec.Core.Reports;

namespace TrueSpec.Tests;

/// <summary>
/// Fleet CSV export (v2.0.3): the file agrees with the Fleet table
/// exactly. Runs on both legs.
/// </summary>
public class FleetCsvExporterTests
{
    private static FleetMachineRow Row(
        string machine,
        int scans,
        double? score,
        string verdict = "Used - healthy",
        string? worst = "storage",
        double? worstHealth = 62d) =>
        new(
            machine,
            scans,
            new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero),
            score,
            verdict,
            worst,
            worstHealth);

    [Fact]
    public void Build_HeaderPlusOneRow()
    {
        var csv = FleetCsvExporter.Build(new[]
        {
            Row("SHOP-PC-01", 3, 82.4d),
        });

        var lines = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Machine,Scans,Last scan,Score,Verdict,Worst component,Worst component health (%)", lines[0]);
        Assert.Equal("SHOP-PC-01,3,2026-11-10 09:30,82.4,Used - healthy,storage,62%", lines[1]);
    }

    [Fact]
    public void Build_EmptyRowsGivesHeaderOnly()
    {
        var csv = FleetCsvExporter.Build(Array.Empty<FleetMachineRow>());

        var lines = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.StartsWith("Machine,Scans,", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NullScoreAndWorstUseTableText()
    {
        var csv = FleetCsvExporter.Build(new[]
        {
            Row("NO-SCORE-PC", 1, null, "Not measurable", null, null),
        });

        Assert.Contains("NO-SCORE-PC,1,2026-11-10 09:30,Not measurable,Not measurable,Not reported,Not reported", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_MachineNameWithCommaIsQuoted()
    {
        var csv = FleetCsvExporter.Build(new[]
        {
            Row("SHOP, PC-01", 1, 80d),
        });

        Assert.Contains("\"SHOP, PC-01\",1,", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_WritesUtf8BomThenContent()
    {
        await using var stream = new MemoryStream();
        await new FleetCsvExporter().ExportAsync(
            new[] { Row("SHOP-PC-01", 2, 75d) },
            stream);

        var bytes = stream.ToArray();
        // UTF-8 BOM, then the header.
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.StartsWith("Machine,Scans,Last scan,", text, StringComparison.Ordinal);
        Assert.Contains("SHOP-PC-01,2,", text, StringComparison.Ordinal);
    }
}
