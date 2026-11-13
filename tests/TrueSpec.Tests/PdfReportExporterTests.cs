using System.Diagnostics;
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
        Assert.Contains(SpecLabels.NotReportedByHardware, text, StringComparison.Ordinal);
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
    [InlineData(SpecLabels.NotReportedByHardware)]
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

    private static SystemSnapshot SnapshotWithDna()
    {
        var dna = TrueSpec.Core.Comparison.MachineDna.Build(new[]
        {
            (TrueSpec.Core.Comparison.MachineDnaPartKind.Board, "Base board", (string?)"ABCD1234EFGH5678"),
            (TrueSpec.Core.Comparison.MachineDnaPartKind.Bios, "BIOS", (string?)"WXYZ9876QRST5432"),
            (TrueSpec.Core.Comparison.MachineDnaPartKind.Disk, "Disk 0", (string?)"DISK000111222333"),
        });
        return TestSnapshots.Healthy() with
        {
            MachineDnaHash = dna.Hash,
            MachineDnaParts = dna.Parts,
        };
    }

    [Fact]
    public async Task CertificatePdf_IsOnePageWithBannerHashAndFiveNumbers()
    {
        var snapshot = SnapshotWithDna();
        var exporter = new PdfReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportCertificateAsync(ReportModel.FromSnapshot(snapshot), stream);

        var bytes = stream.ToArray();
        var text = Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.Contains("%%EOF", text, StringComparison.Ordinal);
        Assert.All(bytes, b => Assert.True(b < 128, "PDF body must stay plain ASCII"));
        Assert.Equal(1, CountOccurrences(text, "/Type /Page "));
        Assert.Contains("page 1 of 1", text, StringComparison.Ordinal);
        Assert.Contains("TrueSpec buyer certificate", text, StringComparison.Ordinal);
        Assert.Contains(snapshot.BuyerChecks.BannerText, text, StringComparison.Ordinal);
        Assert.Contains("SSD wear: 0%", text, StringComparison.Ordinal);
        Assert.Contains("Battery health: 90%", text, StringComparison.Ordinal);
        Assert.Contains("Battery cycle count: 2", text, StringComparison.Ordinal);
        Assert.Contains("Power-on hours: 0", text, StringComparison.Ordinal);
        Assert.Contains("Throttle result: Not tested - no load test has run", text, StringComparison.Ordinal);
        Assert.Contains(snapshot.MachineDnaHash!, text, StringComparison.Ordinal);
        Assert.Contains(snapshot.CapturedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
        AssertXrefOffsetsAreValid(bytes, text);
    }

    [Fact]
    public async Task CertificatePdf_HonestStatesWhenNothingWasMeasured()
    {
        var healthy = TestSnapshots.Healthy();
        var snapshot = healthy with
        {
            Components = healthy.Components
                .Where(c => c.Id != "storage" && c.Id != "battery")
                .ToList(),
        };
        var exporter = new PdfReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportCertificateAsync(ReportModel.FromSnapshot(snapshot), stream);

        var text = Encoding.ASCII.GetString(stream.ToArray());
        Assert.Equal(1, CountOccurrences(text, "/Type /Page "));
        Assert.Contains(SpecLabels.NotReportedByHardware, text, StringComparison.Ordinal);
        Assert.Contains("DNA hash: DNA not recorded", text, StringComparison.Ordinal);
        Assert.Contains("Throttle result: Not tested - no load test has run", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CertificatePdf_NeverContainsAFullSerial()
    {
        var snapshot = SnapshotWithDna();
        var exporter = new PdfReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportCertificateAsync(ReportModel.FromSnapshot(snapshot), stream);

        var text = Encoding.ASCII.GetString(stream.ToArray());
        Assert.DoesNotContain("ABCD1234EFGH5678", text, StringComparison.Ordinal);
        Assert.DoesNotContain("WXYZ9876QRST5432", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DISK000111222333", text, StringComparison.Ordinal);
        // The full hash belongs in the file; the serials do not.
        Assert.Contains(snapshot.MachineDnaHash!, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CertificatePdf_RealReaderCheck_WithPdftotext_WhenAvailable()
    {
        // A real-reader check (poppler pdftotext) on top of the raw-byte
        // checks above: when poppler is not installed the test ends here
        // cleanly instead of failing the suite for a missing tool.
        var snapshot = SnapshotWithDna();
        var path = Path.Combine(Path.GetTempPath(), $"truespec-cert-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var file = File.Create(path))
            {
                await new PdfReportExporter().ExportCertificateAsync(ReportModel.FromSnapshot(snapshot), file);
            }

            string extracted;
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("pdftotext", $"\"{path}\" -")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                };
                using var process = System.Diagnostics.Process.Start(start);
                if (process is null)
                {
                    return;
                }

                extracted = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
            {
                return;
            }

            Assert.Contains("TrueSpec buyer certificate", extracted, StringComparison.Ordinal);
            Assert.Contains(snapshot.BuyerChecks.BannerText, extracted, StringComparison.Ordinal);
            Assert.Contains(snapshot.MachineDnaHash!, extracted, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
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

    [Fact]
    public async Task PdfExporter_WrapKeepsEveryCharacterOfASpacelessLongString()
    {
        // Polish: Wrap() dropped one character on spaceless long strings
        // (URLs/paths) at every wrap. The URL below is 150 characters with
        // no space, so it wraps at least once; concatenating the text the
        // PDF actually shows must reproduce it exactly. The URL can span
        // a page break, so ShownText drops the page-footer shows that
        // would otherwise sit between its two halves.
        var url = "https://example.com/" + new string('a', 120) + "/report";
        Assert.True(url.Length > 95, "Fixture must be long enough to wrap.");
        var healthy = TestSnapshots.Healthy();
        var spec = new SpecValue("Model", url, "Test source");
        var component = new ComponentSnapshot(
            "storage", "Storage", "Storage", "storage", "Test drive", 100d,
            new[] { spec }, "Drive health.", DateTimeOffset.Now);
        var snapshot = healthy with { Components = healthy.Components.Append(component).ToList() };

        var exporter = new PdfReportExporter();
        await using var stream = new MemoryStream();
        await exporter.ExportAsync(ReportModel.FromSnapshot(snapshot), stream);

        var shown = ShownText(Encoding.ASCII.GetString(stream.ToArray()));
        Assert.Contains(url, shown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PdfExporter_RealReaderCheck_PdftotextReadsTheReport()
    {
        // Test-infra (item 7): poppler pdftotext was manual only. When it
        // is installed (GitHub Ubuntu runners have it; Windows runners do
        // not), a real reader must extract the report text, including a
        // long spaceless URL in full. Otherwise the test skips visibly
        // instead of passing vacuously.
        var pdftotext = FindOnPath(OperatingSystem.IsWindows() ? "pdftotext.exe" : "pdftotext");
        if (pdftotext is null)
        {
            // xUnit 2.9.3 has no Assert.Skip and SkipException is not
            // publicly constructible, so the check simply returns on
            // runners without poppler (Windows). The reason is written to
            // the test output so the skip is visible, not silent.
            Console.WriteLine("SKIPPED: pdftotext is not installed, so the real-reader PDF check did not run.");
            return;
        }

        var url = "https://example.com/" + new string('b', 120) + "/report";
        var healthy = TestSnapshots.Healthy();
        var component = new ComponentSnapshot(
            "storage", "Storage", "Storage", "storage", "Test drive", 100d,
            new[] { new SpecValue("Model", url, "Test source") },
            "Drive health.", DateTimeOffset.Now);
        var snapshot = healthy with { Components = healthy.Components.Append(component).ToList() };

        var exporter = new PdfReportExporter();
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var pdfPath = Path.Combine(directory, "report.pdf");
            var txtPath = Path.Combine(directory, "report.txt");
            await using (var stream = File.Create(pdfPath))
            {
                await exporter.ExportAsync(ReportModel.FromSnapshot(snapshot), stream);
            }

            var start = new ProcessStartInfo
            {
                FileName = pdftotext,
                Arguments = $"\"{pdfPath}\" \"{txtPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
            };
            using var process = Process.Start(start);
            Assert.NotNull(process);
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.True(process.ExitCode == 0, $"pdftotext failed: {error}");

            var extracted = await File.ReadAllTextAsync(txtPath);
            Assert.Contains("Test Laptop", extracted, StringComparison.Ordinal);
            // A reader breaks long lines and pages: the URL can span a
            // line wrap and a page break, with the "TrueSpec - page N of
            // M" footer and a form feed between its halves. Join the
            // lines back (dropping footers) before asserting the URL
            // survived in full: the check is that every character is
            // present, not that it fits on one line.
            var unwrapped = string.Concat(extracted
                .Split(new[] { '\n', '\f', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("TrueSpec - page ", StringComparison.Ordinal)));
            Assert.Contains(url, unwrapped, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Finds a tool on PATH, or null when it is not installed.</summary>
    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var candidate = Path.Combine(directory.Trim(), fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Concatenates every PDF literal string the content streams show, in
    /// order, unescaping \(, \) and \\: what a reader sees on the page.
    /// Page-footer shows ("TrueSpec - page N of M") are dropped: a string
    /// that spans a page break would otherwise read with the footer in
    /// the middle, and the footer is not part of the content.
    /// </summary>
    private static string ShownText(string pdf)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < pdf.Length; i++)
        {
            if (pdf[i] != '(')
            {
                continue;
            }

            var text = new StringBuilder();
            var j = i + 1;
            var closed = false;
            while (j < pdf.Length)
            {
                if (pdf[j] == '\\' && j + 1 < pdf.Length)
                {
                    text.Append(pdf[j + 1]);
                    j += 2;
                    continue;
                }

                if (pdf[j] == ')')
                {
                    closed = true;
                    break;
                }

                text.Append(pdf[j]);
                j++;
            }

            if (closed)
            {
                var shown = text.ToString();
                if (!shown.StartsWith("TrueSpec - page ", StringComparison.Ordinal))
                {
                    builder.Append(shown);
                }

                i = j;
            }
        }

        return builder.ToString();
    }
}
