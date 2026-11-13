using System.Text;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

/// <summary>
/// Per-machine CSV export (v2.0.4): one machine's latest scan as its own
/// file — a header block with the fleet table's values for that machine,
/// then one row per scored component, lowest health first. Runs on both
/// legs (Core only, no App dependency).
/// </summary>
public class MachineCsvExporterTests
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

    private static ScoreResult Score(params ComponentScore[] components) =>
        new(82.4d, Verdict.UsedHealthy, components, 100d);

    [Fact]
    public void Build_HeaderBlockPlusComponentRowsLowestFirst()
    {
        var csv = MachineCsvExporter.Build(
            Row("SHOP-PC-01", 3, 82.4d),
            Score(
                new ComponentScore("cpu", 95d, 40d, 0.4d, 38d),
                new ComponentScore("memory", 78d, 20d, 0.2d, 15.6d),
                new ComponentScore("storage", 62d, 10d, 0.1d, 6.2d)));

        var lines = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Machine,SHOP-PC-01", lines[0]);
        Assert.Equal("Last scan,2026-11-10 09:30", lines[1]);
        Assert.Equal("Scans,3", lines[2]);
        Assert.Equal("Score,82.4", lines[3]);
        Assert.Equal("Verdict,Used - healthy", lines[4]);
        Assert.Equal("Worst component,storage", lines[5]);
        Assert.Equal("Worst component health (%),62%", lines[6]);
        Assert.Equal("Component,Health (%)", lines[7]);
        // Lowest health first: the component that needs attention is at
        // the top, whatever order the scoring engine produced.
        Assert.Equal("storage,62%", lines[8]);
        Assert.Equal("memory,78%", lines[9]);
        Assert.Equal("cpu,95%", lines[10]);
        Assert.Equal(11, lines.Length);
    }

    [Fact]
    public void Build_NullScoreAndNoComponentsUsesTableText()
    {
        var csv = MachineCsvExporter.Build(
            Row("NO-SCORE-PC", 1, null, "Not measurable", null, null),
            ScoreResult.Empty);

        Assert.Contains("Machine,NO-SCORE-PC", csv, StringComparison.Ordinal);
        Assert.Contains("Score,Not measurable", csv, StringComparison.Ordinal);
        Assert.Contains("Verdict,Not measurable", csv, StringComparison.Ordinal);
        Assert.Contains("Worst component,Not reported", csv, StringComparison.Ordinal);
        Assert.Contains("Worst component health (%),Not reported", csv, StringComparison.Ordinal);
        Assert.Contains("No measurable components", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NonFiniteHealthIsSkipped()
    {
        var csv = MachineCsvExporter.Build(
            Row("SHOP-PC-01", 1, 82.4d),
            Score(
                new ComponentScore("storage", 62d, 10d, 0.1d, 6.2d),
                new ComponentScore("mystery", double.NaN, 10d, 0.1d, 0d)));

        Assert.Contains("storage,62%", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("mystery", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_MachineNameWithCommaIsQuoted()
    {
        var csv = MachineCsvExporter.Build(
            Row("SHOP, PC-01", 1, 80d),
            Score(new ComponentScore("storage", 80d, 10d, 0.1d, 8d)));

        Assert.Contains("Machine,\"SHOP, PC-01\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_WritesUtf8BomThenContent()
    {
        await using var stream = new MemoryStream();
        await new MachineCsvExporter().ExportAsync(
            Row("SHOP-PC-01", 2, 75d),
            Score(new ComponentScore("storage", 75d, 10d, 0.1d, 7.5d)),
            stream);

        var bytes = stream.ToArray();
        // UTF-8 BOM, then the header block.
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.StartsWith("Machine,SHOP-PC-01", text, StringComparison.Ordinal);
        Assert.Contains("storage,75%", text, StringComparison.Ordinal);
    }
}
