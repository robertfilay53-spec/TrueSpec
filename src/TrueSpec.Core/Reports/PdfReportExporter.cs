namespace TrueSpec.Core.Reports;

using System.Globalization;
using System.Text;
using TrueSpec.Core.Models;

/// <summary>
/// Writes the same report data model as a PDF file, with no third-party
/// library: a minimal PDF 1.4 writer using only the standard Helvetica fonts.
/// Every value keeps the wording of the HTML/JSON exports, and unmeasurable
/// values stay unmeasurable. The file is plain ASCII so it opens in any PDF
/// reader, and the cross-reference table is built from measured byte offsets.
/// </summary>
public sealed class PdfReportExporter : IReportExporter
{
    public ReportFormat Format => ReportFormat.Pdf;

    // A4, points.
    private const double PageWidth = 595d;
    private const double PageHeight = 842d;
    private const double Margin = 48d;
    private const double TopY = PageHeight - 56d;
    private const double BottomY = 48d;

    // Helvetica at 10pt averages about 5pt per character; 95 columns fit
    // inside the margins with room to spare.
    private const int WrapColumns = 95;

    public async Task ExportAsync(
        ReportModel report,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        var bytes = Build(report);
        await destination.WriteAsync(bytes, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    private static byte[] Build(ReportModel report)
    {
        var lines = CollectLines(report);
        var pages = Paginate(lines);
        return WritePdf(pages);
    }

    private static List<PdfLine> CollectLines(ReportModel report)
    {
        var snapshot = report.Snapshot;
        var lines = new List<PdfLine>
        {
            new("TrueSpec report", Bold: true, Size: 16),
            new(snapshot.MachineName, Bold: true, Size: 12),
        };

        // The operating-system line is only useful when the scan actually
        // read one. A timed-out system collector leaves the literal
        // "Not reported" here, which reads as a stray value after the
        // machine name, so the line is omitted instead of shown empty.
        if (HasReportedValue(report.OperatingSystem))
        {
            lines.Add(new(report.OperatingSystem, Bold: false, Size: 10));
        }

        lines.Add(new(string.Create(
            CultureInfo.InvariantCulture,
            $"Generated {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} from a snapshot captured {snapshot.CapturedAt:yyyy-MM-dd HH:mm:ss}."),
            Bold: false, Size: 10));
        lines.Add(PdfLine.Blank);
        lines.Add(new(string.Create(
            CultureInfo.InvariantCulture,
            $"Score: {(snapshot.Score.HasMeasurableComponents ? snapshot.Score.OverallScore.ToString("0.#", CultureInfo.InvariantCulture) : "Not available")} / 100"),
            Bold: true, Size: 12));
        lines.Add(new($"Verdict: {snapshot.Score.Verdict.ToDisplayText()}", Bold: true, Size: 10));
        lines.Add(new(snapshot.Score.Explanation, Bold: false, Size: 10));
        lines.Add(PdfLine.Blank);
        lines.Add(new("New Laptop Check", Bold: true, Size: 12));
        lines.Add(new(snapshot.BuyerChecks.BannerText, Bold: false, Size: 10));
        lines.Add(new(snapshot.BuyerChecks.SummaryText, Bold: false, Size: 10));
        lines.Add(PdfLine.Blank);

        // One row per check: a bold title line with the result, then the
        // detail on its own line, then a blank separator. The old
        // single-line form wrapped into a run-together wall with no
        // visible boundary between checks.
        foreach (var check in snapshot.BuyerChecks.Checks)
        {
            lines.Add(new(
                string.Create(CultureInfo.InvariantCulture, $"{check.Title} [{check.StateText}]"),
                Bold: true, Size: 10));
            lines.Add(new(check.Detail, Bold: false, Size: 10));
            lines.Add(PdfLine.Blank);
        }

        lines.Add(new("Components", Bold: true, Size: 12));
        foreach (var component in snapshot.Components)
        {
            lines.Add(new(
                string.Create(CultureInfo.InvariantCulture, $"{component.Name} - {component.Headline}"),
                Bold: true, Size: 10));
            lines.Add(new(
                string.Create(CultureInfo.InvariantCulture, $"Health: {component.HealthText} ({component.Status.ToDisplayText()})"),
                Bold: false, Size: 10));
            lines.Add(new(component.WhatThisMeans, Bold: false, Size: 10));
            foreach (var spec in component.Specs)
            {
                lines.Add(new(
                    string.Create(CultureInfo.InvariantCulture, $"{spec.Label}: {spec.DisplayValue} [{spec.Source}]"),
                    Bold: false, Size: 10));
            }

            lines.Add(PdfLine.Blank);
        }

        lines.Add(new(
            "TrueSpec is offline. This report was generated on this machine and was not uploaded anywhere. " +
            "Unmeasurable values say \"Not reported by hardware\" or \"Not available\" instead of showing a fake number.",
            Bold: false, Size: 10));
        return lines;
    }

    /// <summary>
    /// True when a header value is a real reading: the literal "Not
    /// reported" fallbacks are missing data, not a value to print.
    /// </summary>
    private static bool HasReportedValue(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value.Trim(), "Not reported", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(value.Trim(), "Not reported by hardware", StringComparison.OrdinalIgnoreCase);

    private static List<List<PdfLine>> Paginate(List<PdfLine> lines)
    {
        var pages = new List<List<PdfLine>>();
        var current = new List<PdfLine>();
        var used = 0d;
        var usableHeight = TopY - BottomY;

        foreach (var line in lines)
        {
            // Wrap first: a long spec line becomes several short lines, each
            // of which takes its own row on the page.
            var wrapped = Wrap(line);
            foreach (var row in wrapped)
            {
                if (used + row.Height > usableHeight && current.Count > 0)
                {
                    pages.Add(current);
                    current = new List<PdfLine>();
                    used = 0d;
                }

                current.Add(row);
                used += row.Height;
            }
        }

        if (current.Count > 0)
        {
            pages.Add(current);
        }

        return pages.Count == 0 ? new List<List<PdfLine>> { new() } : pages;
    }

    private static List<PdfLine> Wrap(PdfLine line)
    {
        if (line.Text.Length <= WrapColumns)
        {
            return new List<PdfLine> { line };
        }

        var rows = new List<PdfLine>();
        var remaining = line.Text;
        while (remaining.Length > WrapColumns)
        {
            var cut = remaining.LastIndexOf(' ', WrapColumns);
            if (cut <= 0)
            {
                cut = WrapColumns;
            }

            rows.Add(new PdfLine(remaining[..cut], line.Bold, line.Size));
            remaining = remaining[(cut + 1)..].TrimStart();
            if (remaining.Length == 0)
            {
                break;
            }
        }

        if (remaining.Length > 0)
        {
            rows.Add(new PdfLine(remaining, line.Bold, line.Size));
        }

        return rows;
    }

    private static byte[] WritePdf(List<List<PdfLine>> pages)
    {
        // Object numbers: 1 catalog, 2 pages, 3 Helvetica, 4 Helvetica-Bold,
        // then 2 per page (page object + content stream).
        var pageCount = pages.Count;
        var stream = new MemoryStream();
        var offsets = new List<long>();

        void WriteAscii(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        WriteAscii("%PDF-1.4\n");

        void BeginObject(int number)
        {
            offsets.Add(stream.Position);
            WriteAscii(string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n"));
        }

        BeginObject(1);
        WriteAscii("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        BeginObject(2);
        var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{5 + (i * 2)} 0 R"));
        WriteAscii(string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>\nendobj\n"));

        BeginObject(3);
        WriteAscii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        BeginObject(4);
        WriteAscii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n");

        for (var p = 0; p < pageCount; p++)
        {
            var content = BuildPageContent(pages[p], p + 1, pageCount);
            var contentBytes = Encoding.ASCII.GetBytes(content);

            BeginObject(5 + (p * 2));
            WriteAscii(string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth:0} {PageHeight:0}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {6 + (p * 2)} 0 R >>\nendobj\n"));

            BeginObject(6 + (p * 2));
            WriteAscii(string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Length {contentBytes.Length} >>\nstream\n"));
            stream.Write(contentBytes, 0, contentBytes.Length);
            WriteAscii("\nendstream\nendobj\n");
        }

        var xrefPosition = stream.Position;
        var objectCount = 4 + (pageCount * 2) + 1;
        WriteAscii(string.Create(CultureInfo.InvariantCulture, $"xref\n0 {objectCount}\n"));
        WriteAscii("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            WriteAscii(string.Create(CultureInfo.InvariantCulture, $"{offset:0000000000} 00000 n \n"));
        }

        WriteAscii(string.Create(
            CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {objectCount} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF\n"));

        return stream.ToArray();
    }

    private static string BuildPageContent(List<PdfLine> lines, int pageNumber, int pageCount)
    {
        var builder = new StringBuilder();
        var y = TopY;
        foreach (var line in lines)
        {
            if (line.Text.Length > 0)
            {
                builder.Append(string.Create(
                    CultureInfo.InvariantCulture,
                    $"BT /{(line.Bold ? "F2" : "F1")} {line.Size} Tf 1 0 0 1 {Margin:0} {y:0.##} Tm ({Escape(line.Text)}) Tj ET\n"));
            }

            y -= line.Height;
        }

        builder.Append(string.Create(
            CultureInfo.InvariantCulture,
            $"BT /F1 9 Tf 1 0 0 1 {Margin:0} {BottomY - 14:0.##} Tm (TrueSpec - page {pageNumber} of {pageCount}) Tj ET\n"));
        return builder.ToString();
    }

    /// <summary>
    /// PDF literal strings are ASCII with ( ) and \ escaped. Anything
    /// outside printable ASCII becomes '?': a machine name with unusual
    /// characters still exports instead of breaking the file.
    /// </summary>
    public static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '(':
                case ')':
                case '\\':
                    builder.Append('\\');
                    builder.Append(ch);
                    break;
                default:
                    builder.Append(ch is >= ' ' and <= '~' ? ch : '?');
                    break;
            }
        }

        return builder.ToString();
    }

    private sealed record PdfLine(string Text, bool Bold, int Size)
    {
        public static PdfLine Blank { get; } = new(string.Empty, Bold: false, Size: 10);

        public double Height => Size switch
        {
            >= 16 => 24d,
            >= 12 => 18d,
            _ => 14d,
        };
    }
}
