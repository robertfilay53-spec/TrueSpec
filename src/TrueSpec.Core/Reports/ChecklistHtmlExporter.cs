namespace TrueSpec.Core.Reports;

using System.Net;
using System.Text;
using TrueSpec.Core.Models;

/// <summary>
/// A standalone, printable buyer checklist: the PASS / WARNING / FAIL banner
/// plus one row per check with a real checkbox the buyer can tick on paper.
/// Print CSS hides everything except the checklist and forces a clean
/// black-on-white page. Like every TrueSpec output it is generated locally
/// and HTML-encoded.
/// </summary>
public sealed class ChecklistHtmlExporter
{
    public async Task ExportAsync(
        SystemSnapshot snapshot,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(destination);

        var html = Build(snapshot);
        await using var writer = new StreamWriter(destination, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        await writer.WriteAsync(html.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }

    internal static string Build(SystemSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine($"<title>TrueSpec buyer checklist - {Encode(snapshot.MachineName)}</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("""
            body { font-family: 'Segoe UI', Arial, sans-serif; margin: 0; background: #fff; color: #16181d; }
            main { max-width: 800px; margin: 0 auto; padding: 32px 20px 60px; }
            h1 { font-size: 24px; margin: 0 0 4px; }
            h2 { font-size: 17px; margin: 26px 0 8px; }
            p, td, th { font-size: 13px; line-height: 1.5; }
            .muted { color: #5b6470; }
            .banner { border-radius: 10px; padding: 14px 16px; font-weight: 700; font-size: 16px; margin: 16px 0; }
            .banner.green { background: #dcfce7; color: #166534; }
            .banner.amber { background: #fef3c7; color: #92400e; }
            .banner.red { background: #fee2e2; color: #991b1b; }
            .banner.grey { background: #eef0f2; color: #374151; }
            table { width: 100%; border-collapse: collapse; }
            th, td { text-align: left; padding: 10px; border-bottom: 1px solid #e2e5e9; vertical-align: top; }
            .box { width: 18px; height: 18px; border: 2px solid #16181d; border-radius: 3px; display: inline-block; }
            .tag { display: inline-block; border-radius: 999px; padding: 2px 9px; font-size: 11px; font-weight: 700; }
            .tag.green { background: #dcfce7; } .tag.amber { background: #fef3c7; } .tag.red { background: #fee2e2; } .tag.grey { background: #eef0f2; }
            footer { margin-top: 30px; font-size: 11px; color: #5b6470; }
            @media print {
              body { background: #fff; }
              .no-print { display: none; }
              main { padding: 0; max-width: none; }
              .banner.green, .banner.amber, .banner.red, .banner.grey { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
            }
            """);
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body><main>");

        builder.AppendLine("<p class=\"muted\">TRUESPEC BUYER CHECKLIST</p>");
        builder.AppendLine($"<h1>{Encode(snapshot.MachineName)}</h1>");
        builder.AppendLine($"<p class=\"muted\">{Encode(snapshot.OperatingSystem)} - snapshot captured {Encode(snapshot.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss"))}</p>");

        var bannerClass = snapshot.BuyerChecks.OverallState switch
        {
            BuyerCheckState.Pass => "green",
            BuyerCheckState.Warning => "amber",
            BuyerCheckState.Fail => "red",
            _ => "grey",
        };
        builder.AppendLine($"<div class=\"banner {bannerClass}\">{Encode(snapshot.BuyerChecks.BannerText)}</div>");

        builder.AppendLine($"<p><strong>{Encode(snapshot.BuyerChecks.SummaryText)}.</strong> Tick each row as you verify it with the seller.</p>");

        builder.AppendLine("<table><thead><tr><th></th><th>Check</th><th>Result</th><th>Detail</th><th>What to ask the seller</th></tr></thead><tbody>");
        foreach (var check in snapshot.BuyerChecks.Checks)
        {
            var tagClass = check.State switch
            {
                BuyerCheckState.Pass => "green",
                BuyerCheckState.Warning => "amber",
                BuyerCheckState.Fail => "red",
                _ => "grey",
            };
            builder.AppendLine("<tr>");
            builder.AppendLine("<td><span class=\"box\"></span></td>");
            builder.AppendLine($"<td><strong>{Encode(check.Title)}</strong></td>");
            builder.AppendLine($"<td><span class=\"tag {tagClass}\">{Encode(check.StateText)}</span></td>");
            builder.AppendLine($"<td>{Encode(check.Detail)}</td>");
            builder.AppendLine($"<td>{Encode(check.WhatToAsk)}</td>");
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<h2>Before you pay</h2>");
        builder.AppendLine($"<p>Ask the seller to run TrueSpec on the machine in front of you and compare the live score with this checklist. If any row says FAIL, walk away or renegotiate. TrueSpec never invents a number: anything it could not measure says \"{SpecLabels.NotReportedByHardware}\".</p>");

        builder.AppendLine("<footer>Generated locally by TrueSpec. This checklist was not uploaded anywhere.</footer>");
        builder.AppendLine("</main></body></html>");
        return builder.ToString();
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
