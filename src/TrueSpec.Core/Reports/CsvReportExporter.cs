namespace TrueSpec.Core.Reports;

using System.Text;
using TrueSpec.Core.Models;

/// <summary>
/// Writes the report as CSV: one row per specification, with the score,
/// verdict and buyer checks first. Values are quoted per RFC 4180 and any
/// value that could be read as a spreadsheet formula (=, +, -, @, |, % at
/// the start) is neutralized with a leading tab, so opening the file in
/// Excel can never run a formula from a hardware string.
/// </summary>
public sealed class CsvReportExporter : IReportExporter
{
    public ReportFormat Format => ReportFormat.Csv;

    public async Task ExportAsync(
        ReportModel report,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        var csv = Build(report);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv);
        await destination.WriteAsync(bytes.AsMemory(), cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    internal static string Build(ReportModel report)
    {
        var snapshot = report.Snapshot;
        var builder = new StringBuilder();
        builder.AppendLine("Section,Item,Value,Source");

        builder.AppendLine(Row("Score", "Overall score", ScoreText(snapshot), "TrueSpec scoring engine"));
        builder.AppendLine(Row("Score", "Verdict", snapshot.Score.Verdict.ToDisplayText(), "TrueSpec scoring engine"));
        builder.AppendLine(Row("Score", "Measured weight", $"{snapshot.Score.MeasuredWeight:0.#} of 100", "TrueSpec scoring engine"));
        builder.AppendLine(Row("Machine", "Machine name", report.MachineName, "CIM Win32_ComputerSystem"));
        builder.AppendLine(Row("Machine", "Operating system", report.OperatingSystem, "CIM Win32_OperatingSystem"));
        builder.AppendLine(Row("Machine", "Snapshot captured", snapshot.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss"), "TrueSpec"));
        builder.AppendLine(Row("Machine", "Report generated", report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"), "TrueSpec"));

        foreach (var check in snapshot.BuyerChecks.Checks)
        {
            builder.AppendLine(Row("Buyer check", check.Title, $"{check.StateText} - {check.Detail}", "TrueSpec buyer check"));
        }

        foreach (var component in snapshot.Components)
        {
            foreach (var spec in component.Specs)
            {
                builder.AppendLine(Row(component.Name, spec.Label, spec.DisplayValue, spec.Source));
            }
        }

        return builder.ToString();
    }

    private static string ScoreText(SystemSnapshot snapshot) =>
        snapshot.Score.HasMeasurableComponents
            ? snapshot.Score.OverallScore.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
            : "Not available";

    private static string Row(string section, string item, string? value, string? source) =>
        string.Join(",", new[]
        {
            Escape(section),
            Escape(item),
            Escape(value ?? string.Empty),
            Escape(source ?? string.Empty),
        });

    /// <summary>
    /// RFC 4180 quoting plus formula-injection neutralization. A value that
    /// starts with =, +, -, @, | or % (after trimming) gets a leading tab so
    /// a spreadsheet treats it as text, never as a formula.
    /// </summary>
    internal static string Escape(string value)
    {
        var text = value.Trim();
        if (text.Length > 0 && "=+-@|%".Contains(text[0]))
        {
            text = "\t" + text;
        }

        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r') || text.StartsWith('\t'))
        {
            return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return text;
    }
}
