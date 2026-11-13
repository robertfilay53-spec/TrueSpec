namespace TrueSpec.Core.Reports;

using System.Globalization;
using System.Text;
using TrueSpec.Core.History;

/// <summary>
/// Writes the Fleet page's table as CSV, built from the same
/// <see cref="FleetMachineRow"/> rows the page shows, so the file agrees
/// with the table exactly. Every cell goes through
/// <see cref="CsvReportExporter.Escape"/> (RFC 4180 quoting plus
/// formula-injection neutralization), and the file is UTF-8 with BOM,
/// like the History CSV export. Scores and worst-component health use
/// the table's own text ("Not measurable" / "Not reported" when null).
/// </summary>
public sealed class FleetCsvExporter
{
    public async Task ExportAsync(
        IReadOnlyList<FleetMachineRow> rows,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(destination);

        var csv = Build(rows);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        // GetBytes alone does not emit the byte order mark: the preamble
        // only comes out via GetPreamble (or a StreamWriter). Write it
        // first so the file really is UTF-8 with BOM, like the exporter
        // intends.
        await destination.WriteAsync(encoding.GetPreamble().AsMemory(), cancellationToken);
        await destination.WriteAsync(encoding.GetBytes(csv).AsMemory(), cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    public static string Build(IReadOnlyList<FleetMachineRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        builder.AppendLine(Row(
            "Machine",
            "Scans",
            "Last scan",
            "Score",
            "Verdict",
            "Worst component",
            "Worst component health (%)"));

        // Rows are already in the page's order (lowest score first); the
        // CSV keeps that order so the file lines up with the table.
        foreach (var row in rows)
        {
            builder.AppendLine(Row(
                row.MachineName,
                row.ScanCount.ToString(CultureInfo.InvariantCulture),
                row.LastScan.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                CsvDisplayText.ScoreText(row.LatestScore),
                row.Verdict,
                row.WorstComponent ?? "Not reported",
                CsvDisplayText.PercentText(row.WorstComponentHealth)));
        }

        return builder.ToString();
    }

    private static string Row(params string[] cells) =>
        string.Join(",", cells.Select(CsvReportExporter.Escape));
}
