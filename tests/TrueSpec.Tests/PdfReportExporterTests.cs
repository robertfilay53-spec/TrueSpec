using System.Globalization;
using System.Text;
using TrueSpec.Core;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

public class PdfReportExporterTests
{
    [Fact]
    public async Task PdfExporter_WritesAValidPdf14FileWithTheReportData()
    {
        var snapshot = TestSnapshots.Healthy();
        var exporter = new PdfReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportAsync(ReportModel.FromSnapshot(snapshot), stream);

        var bytes = stream.ToArray();
        var text = Encoding.ASCII.GetString(bytes);

        Assert.Equal(ReportFormat.Pdf, exporter.Format);
        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.Contains("%%EOF", text, StringComparison.Ordinal);
        Assert.Contains("Test Laptop", text, StringComparison.Ordinal);
        Assert.Contains("TrueSpec report", text, StringComparison.Ordinal);
        Assert.Contains("/BaseFont /Helvetica", text, StringComparison.Ordinal);
        Assert.All(bytes, b => Assert.True(b < 128, "PDF body must stay plain ASCII"));
        AssertXrefOffsetsAreValid(bytes, text);
    }

    [Fact]
    public async Task PdfExporter_KeepsNotReportedWordingForUnmeasurableValues()
    {
        var snapshot = await MachineScanService.CreateDefault().ScanAsync();
        var exporter = new PdfReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportAsync(ReportModel.FromSnapshot(snapshot), stream);

        var text = Encoding.ASCII.GetString(stream.ToArray());
        Assert.Contains("Not reported by hardware", text, StringComparison.Ordinal);
        Assert.Contains("Not available", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PdfExporter_SanitizesParenthesesAndNonAsciiInsteadOfBreaking()
    {
        var snapshot = TestSnapshots.Healthy() with { MachineName = "Test (Laptop) — ünïcode" };
        var exporter = new PdfReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportAsync(ReportModel.FromSnapshot(snapshot), stream);

        var bytes = stream.ToArray();
        var text = Encoding.ASCII.GetString(bytes);
        Assert.All(bytes, b => Assert.True(b < 128, "Non-ASCII must be replaced, never emitted raw"));
        Assert.Contains("Test \\(Laptop\\)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("—", text, StringComparison.Ordinal);
        AssertXrefOffsetsAreValid(bytes, text);
    }

    [Fact]
    public async Task PdfExporter_PaginatesALongReport()
    {
        var snapshot = LongSnapshot();
        var exporter = new PdfReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportAsync(ReportModel.FromSnapshot(snapshot), stream);

        var text = Encoding.ASCII.GetString(stream.ToArray());
        // "/Type /Page " (trailing space) matches page objects only, not
        // the "/Type /Pages" parent object.
        var pageObjects = CountOccurrences(text, "/Type /Page ");
        Assert.True(pageObjects >= 2, $"Expected at least 2 pages, found {pageObjects}");
        Assert.Contains("page 2 of", text, StringComparison.Ordinal);
        AssertXrefOffsetsAreValid(stream.ToArray(), text);
    }

    [Fact]
    public async Task PdfExporter_NewLaptopCheckSection_GivesEachCheckItsOwnRowAndShowsTheSplit()
    {
        var snapshot = TestSnapshots.Healthy();
        var exporter = new PdfReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportAsync(ReportModel.FromSnapshot(snapshot), stream);

        var text = Encoding.ASCII.GetString(stream.ToArray());

        // The honest split, not "0 of 10" / "10 of 10 checks passed".
        Assert.Contains("(10 passed)", text, StringComparison.Ordinal);
        // Each check is its own row: a bold title line with the result,
        // then the detail as a separate line, never glued into one
        // run-together "Title: State - Detail" line.
        Assert.Contains("(SSD wear is 0% [Pass]) Tj", text, StringComparison.Ordinal);
        Assert.Contains("(The drive reports 0% wear and no SMART warnings.) Tj", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SSD wear is 0%: Pass -", text, StringComparison.Ordinal);
        AssertXrefOffsetsAreValid(stream.ToArray(), text);
    }

    [Theory]
    [InlineData("Not reported")]
    [InlineData("Not reported by hardware")]
    public async Task PdfExporter_OmitsOperatingSystemLineWhenItWasNotReported(string operatingSystem)
    {
        // v1.4.0 laptop PDF header: "RANAJAWADLAPTOP / Not reported" — a
        // header field with no value. The line is omitted instead.
        var snapshot = TestSnapshots.Healthy() with { OperatingSystem = operatingSystem };
        var exporter = new PdfReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportAsync(ReportModel.FromSnapshot(snapshot), stream);

        var text = Encoding.ASCII.GetString(stream.ToArray());
        Assert.DoesNotContain($"({operatingSystem})", text, StringComparison.Ordinal);
        Assert.Contains("Test Laptop", text, StringComparison.Ordinal);
        AssertXrefOffsetsAreValid(stream.ToArray(), text);
    }

    [Fact]
    public async Task PdfExporter_ShowsOperatingSystemLineWhenItWasReported()
    {
        var snapshot = TestSnapshots.Healthy();
        var exporter = new PdfReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportAsync(ReportModel.FromSnapshot(snapshot), stream);

        var text = Encoding.ASCII.GetString(stream.ToArray());
        Assert.Contains("(Windows 11 Pro)", text, StringComparison.Ordinal);
        AssertXrefOffsetsAreValid(stream.ToArray(), text);
    }

    [Fact]
    public void Escape_HandlesParenthesesBackslashAndControlCharacters()
    {
        Assert.Equal("a \\(b\\) c", PdfReportExporter.Escape("a (b) c"));
        Assert.Equal("a \\\\ b", PdfReportExporter.Escape("a \\ b"));
        Assert.Equal("a?b", PdfReportExporter.Escape("a\tb"));
        Assert.Equal("plain", PdfReportExporter.Escape("plain"));
    }

    /// <summary>
    /// Reads the xref table this exporter wrote and checks every live entry
    /// really points at the object it claims to: a wrong offset is the most
    /// common hand-rolled PDF bug, and a reader would refuse the file.
    /// </summary>
    private static void AssertXrefOffsetsAreValid(byte[] bytes, string text)
    {
        var startxref = text.LastIndexOf("startxref", StringComparison.Ordinal);
        Assert.True(startxref >= 0, "startxref missing");
        var xrefPosition = int.Parse(
            text[(startxref + "startxref".Length)..].Trim().Split('\n')[0].Trim(),
            CultureInfo.InvariantCulture);
        Assert.Equal("xref", Encoding.ASCII.GetString(bytes, xrefPosition, 4));

        var lines = text[xrefPosition..].Split('\n');
        Assert.Equal("xref", lines[0].Trim());
        var header = lines[1].Trim().Split(' ');
        var count = int.Parse(header[1], CultureInfo.InvariantCulture);
        Assert.True(count >= 5, "A report PDF has at least the catalog, pages, fonts and one page");

        for (var i = 0; i < count; i++)
        {
            var entry = lines[2 + i];
            var offset = int.Parse(entry[..10], CultureInfo.InvariantCulture);
            var kind = entry[17];
            if (i == 0)
            {
                Assert.Equal('f', kind);
                continue;
            }

            Assert.Equal('n', kind);
            var atObject = Encoding.ASCII.GetString(bytes, offset, $"{i} 0 obj".Length);
            Assert.Equal($"{i} 0 obj", atObject);
        }
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static SystemSnapshot LongSnapshot()
    {
        var healthy = TestSnapshots.Healthy();
        var specs = Enumerable.Range(0, 200)
            .Select(i => new SpecValue(
                $"Spec {i}",
                $"A long value that keeps going so the line wraps {i} " + new string('x', 80),
                "Test source"))
            .ToList();
        var big = new ComponentSnapshot(
            "storage", "Storage", "Storage", "storage", "Test drive", 100d, specs,
            "Drive health comes from the manufacturer wear counter.", DateTimeOffset.Now);
        return healthy with { Components = healthy.Components.Append(big).ToList() };
    }
}
