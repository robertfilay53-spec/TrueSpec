namespace TrueSpec.Core.Reports;

using System.Globalization;
using System.Net;
using System.Text;
using TrueSpec.Core.Models;

/// <summary>
/// Writes the same report data model as a self-contained HTML page. Every value
/// keeps its source label and unmeasurable values stay unmeasurable.
/// </summary>
public sealed class HtmlReportExporter : IReportExporter
{
    public ReportFormat Format => ReportFormat.Html;

    public async Task ExportAsync(
        ReportModel report,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        var html = Build(report);
        await using var writer = new StreamWriter(destination, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        await writer.WriteAsync(html.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }

    private static string Build(ReportModel report)
    {
        var snapshot = report.Snapshot;
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine($"<title>TrueSpec report - {Encode(report.MachineName)}</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("""
            :root { color-scheme: light dark; }
            body { font-family: 'Segoe UI', Arial, sans-serif; margin: 0; background: #f4f5f7; color: #16181d; }
            main { max-width: 1100px; margin: 0 auto; padding: 32px 20px 60px; }
            .card { background: #fff; border: 1px solid #e2e5e9; border-radius: 12px; padding: 20px; margin-bottom: 18px; }
            h1, h2, h3 { margin: 0 0 8px; }
            h1 { font-size: 28px; }
            h2 { font-size: 20px; margin-top: 26px; }
            h3 { font-size: 15px; }
            p, td, th { font-size: 13px; line-height: 1.5; }
            .muted { color: #5b6470; }
            .score { font-size: 42px; font-weight: 700; }
            .green { color: #16a34a; } .amber { color: #b45309; } .red { color: #dc2626; } .grey { color: #6b7280; }
            table { width: 100%; border-collapse: collapse; }
            th, td { text-align: left; padding: 8px 10px; border-bottom: 1px solid #e2e5e9; vertical-align: top; }
            th { color: #5b6470; font-weight: 600; }
            .tag { display: inline-block; border-radius: 999px; padding: 2px 9px; font-size: 11px; font-weight: 700; }
            .tag.green { background: #dcfce7; } .tag.amber { background: #fef3c7; } .tag.red { background: #fee2e2; } .tag.grey { background: #eef0f2; }
            footer { margin-top: 30px; font-size: 11px; color: #5b6470; }
            @media (prefers-color-scheme: dark) {
              body { background: #121417; color: #edeff2; }
              .card { background: #1b1e22; border-color: #2a2e33; }
              .muted, th, footer { color: #9aa3ad; }
              th, td { border-color: #2a2e33; }
              .tag.grey { background: #2a2e33; }
            }
            @media print {
              body { background: #fff; }
              .card { border-color: #ccc; }
              main { padding: 0; max-width: none; }
            }
            """);
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body><main>");

        builder.AppendLine("<section class=\"card\">");
        builder.AppendLine("<p class=\"muted\">TRUESPEC REPORT</p>");
        builder.AppendLine($"<h1>{Encode(report.MachineName)}</h1>");
        builder.AppendLine($"<p class=\"muted\">{Encode(report.OperatingSystem)}</p>");
        var scoreClass = StatusClass(snapshot.Score.Verdict.ToHealthStatus());
        builder.AppendLine($"<p><span class=\"score {scoreClass}\">{ScoreText(snapshot)}</span> <span class=\"muted\">/ 100</span></p>");
        builder.AppendLine($"<h2 class=\"{scoreClass}\">{Encode(snapshot.Score.Verdict.ToDisplayText())}</h2>");
        builder.AppendLine($"<p class=\"muted\">{Encode(snapshot.Score.Explanation)}</p>");
        builder.AppendLine($"<p class=\"muted\">Generated {Encode(report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))} from a snapshot captured {Encode(snapshot.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss"))}.</p>");
        builder.AppendLine("</section>");

        builder.AppendLine("<section class=\"card\">");
        builder.AppendLine($"<h2>New Laptop Check</h2>");
        builder.AppendLine($"<p><strong>{Encode(snapshot.BuyerChecks.BannerText)}</strong></p>");
        builder.AppendLine($"<p>{Encode(snapshot.BuyerChecks.SummaryText)}.</p>");
        builder.AppendLine("<table><thead><tr><th>Check</th><th>Result</th><th>Detail</th><th>What to ask</th></tr></thead><tbody>");
        foreach (var check in snapshot.BuyerChecks.Checks)
        {
            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>{Encode(check.Title)}</td>");
            builder.AppendLine($"<td><span class=\"tag {CheckClass(check.State)}\">{Encode(check.StateText)}</span></td>");
            builder.AppendLine($"<td>{Encode(check.Detail)}</td>");
            builder.AppendLine($"<td>{Encode(check.WhatToAsk)}</td>");
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table>");
        builder.AppendLine("</section>");

        builder.AppendLine("<h2>Components</h2>");
        foreach (var component in snapshot.Components)
        {
            builder.AppendLine("<section class=\"card\">");
            builder.AppendLine($"<h3>{Encode(component.Name)} - {Encode(component.Headline)}</h3>");
            builder.AppendLine($"<p class=\"muted\">Health: {Encode(component.HealthText)} ({Encode(component.Status.ToDisplayText())})</p>");
            builder.AppendLine($"<p>{Encode(component.WhatThisMeans)}</p>");
            builder.AppendLine("<table><thead><tr><th>Specification</th><th>Value</th><th>Source</th></tr></thead><tbody>");
            foreach (var spec in component.Specs)
            {
                builder.AppendLine("<tr>");
                builder.AppendLine($"<td>{Encode(spec.Label)}</td>");
                builder.AppendLine($"<td>{Encode(spec.DisplayValue)}</td>");
                builder.AppendLine($"<td class=\"muted\">{Encode(spec.Source)}</td>");
                builder.AppendLine("</tr>");
            }

            builder.AppendLine("</tbody></table>");
            builder.AppendLine("</section>");
        }

        builder.AppendLine("<footer>TrueSpec is offline. This report was generated on this machine and was not uploaded anywhere. Unmeasurable values say \"Not reported by hardware\" or \"Not available\" instead of showing a fake number.</footer>");
        builder.AppendLine("</main></body></html>");
        return builder.ToString();
    }

    private static string ScoreText(SystemSnapshot snapshot) =>
        snapshot.Score.HasMeasurableComponents
            ? snapshot.Score.OverallScore.ToString("0.#", CultureInfo.InvariantCulture)
            : "Not available";

    private static string StatusClass(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "green",
        HealthStatus.Watch => "amber",
        HealthStatus.Problem => "red",
        _ => "grey",
    };

    private static string CheckClass(BuyerCheckState state) => state switch
    {
        BuyerCheckState.Pass => "green",
        BuyerCheckState.Warning => "amber",
        BuyerCheckState.Fail => "red",
        _ => "grey",
    };

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
