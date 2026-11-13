namespace TrueSpec.Core.Reports;

using System.Globalization;
using System.Text;
using TrueSpec.Core.History;
using TrueSpec.Core.Scoring;

/// <summary>
/// Writes one machine's latest scan as its own CSV (plan: Beyond v2.0,
/// item 1, third element — "a CSV export per machine"): a header block
/// with the fleet table's values for that machine, then one row per
/// scored component, lowest health first, so the component that needs
/// attention is at the top. Every cell goes through
/// <see cref="CsvReportExporter.Escape"/> (RFC 4180 quoting plus
/// formula-injection neutralization), and the file is UTF-8 with BOM,
/// like the Fleet and History CSV exports. Verdict and score use the
/// table's own display text, so the file agrees with the table exactly.
/// </summary>
public sealed class MachineCsvExporter
{
    public async Task ExportAsync(
        FleetMachineRow row,
        ScoreResult score,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(destination);

        var csv = Build(row, score);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        // GetBytes alone does not emit the byte order mark: the preamble
        // only comes out via GetPreamble (or a StreamWriter). Write it
        // first so the file really is UTF-8 with BOM, like the exporter
        // intends.
        await destination.WriteAsync(encoding.GetPreamble().AsMemory(), cancellationToken);
        await destination.WriteAsync(encoding.GetBytes(csv).AsMemory(), cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    public static string Build(FleetMachineRow row, ScoreResult score)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(score);

        var builder = new StringBuilder();
        // Header block: the fleet table's values for this machine.
        builder.AppendLine(Row("Machine", row.MachineName));
        builder.AppendLine(Row("Last scan", row.LastScan.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
        builder.AppendLine(Row("Scans", row.ScanCount.ToString(CultureInfo.InvariantCulture)));
        builder.AppendLine(Row("Score", CsvDisplayText.ScoreText(row.LatestScore)));
        builder.AppendLine(Row("Verdict", row.Verdict));
        builder.AppendLine(Row("Worst component", row.WorstComponent ?? "Not reported"));
        builder.AppendLine(Row("Worst component health (%)", CsvDisplayText.PercentText(row.WorstComponentHealth)));
        builder.AppendLine();

        // One row per scored component, lowest health first (triage
        // order, same rule as the fleet table's lowest-score-first
        // order). Non-finite health values are not measurements (the
        // same guard the scoring engine uses) and are skipped.
        builder.AppendLine(Row("Component", "Health (%)"));
        var components = score.Components
            .Where(c => double.IsFinite(c.HealthPercent))
            .OrderBy(c => c.HealthPercent)
            .ThenBy(c => c.Component, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (components.Length == 0)
        {
            builder.AppendLine(Row("No measurable components"));
        }
        else
        {
            foreach (var component in components)
            {
                builder.AppendLine(Row(
                    component.Component,
                    CsvDisplayText.PercentText(component.HealthPercent)));
            }
        }

        return builder.ToString();
    }

    private static string Row(params string[] cells) =>
        string.Join(",", cells.Select(CsvReportExporter.Escape));
}
