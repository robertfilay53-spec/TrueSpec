namespace TrueSpec.Core.Reports;

using System.Globalization;
using System.Text;
using TrueSpec.Core.History;

/// <summary>
/// Writes the History page's table as CSV, built from the same
/// <see cref="HistoryTrend"/> the page already has, so the file agrees with
/// the table exactly. Every cell uses the History table's own text
/// (HistoryRowViewModel): score "Not measurable" when null else "0.#",
/// battery health / storage wear "Not reported" when null else "0.#%",
/// cycles / power-on hours "Not reported" when null else "0", scanned as
/// "yyyy-MM-dd HH:mm". Every cell goes through
/// <see cref="CsvReportExporter.Escape"/> (RFC 4180 quoting plus
/// formula-injection neutralization), and the file is UTF-8 with BOM.
/// </summary>
public sealed class HistoryCsvExporter
{
    public async Task ExportAsync(
        HistoryTrend trend,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trend);
        ArgumentNullException.ThrowIfNull(destination);

        var csv = Build(trend);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        // GetBytes alone does not emit the byte order mark: the preamble
        // only comes out via GetPreamble (or a StreamWriter). Write it
        // first so the file really is UTF-8 with BOM, like the exporter
        // intends.
        await destination.WriteAsync(encoding.GetPreamble().AsMemory(), cancellationToken);
        await destination.WriteAsync(encoding.GetBytes(csv).AsMemory(), cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    public static string Build(HistoryTrend trend)
    {
        ArgumentNullException.ThrowIfNull(trend);

        var builder = new StringBuilder();
        builder.AppendLine(Row(
            "Scanned",
            "Score",
            "Battery health (%)",
            "Battery cycles",
            "Storage wear (%)",
            "Power-on hours"));

        // Trend points are oldest first, and the History page builds its
        // rows the same way (one row per score point, same indexes), so the
        // CSV rows line up with the table row for row.
        for (var i = 0; i < trend.ScorePoints.Count; i++)
        {
            builder.AppendLine(Row(
                trend.ScorePoints[i].CapturedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                ScoreText(trend.ScorePoints[i].Value),
                PercentText(trend.BatteryHealthPoints[i].Value),
                CountText(trend.BatteryCyclePoints[i].Value),
                PercentText(trend.StorageWearPoints[i].Value),
                CountText(trend.StoragePowerOnHoursPoints[i].Value)));
        }

        return builder.ToString();
    }

    private static string Row(params string[] cells) =>
        string.Join(",", cells.Select(CsvReportExporter.Escape));

    private static string ScoreText(double? score) =>
        score is null ? "Not measurable" : score.Value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string PercentText(double? value) =>
        value is null
            ? "Not reported"
            : string.Create(CultureInfo.InvariantCulture, $"{value.Value:0.#}%");

    private static string CountText(double? value) =>
        value is null ? "Not reported" : value.Value.ToString("0", CultureInfo.InvariantCulture);
}
