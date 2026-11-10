using System.Text;
using System.Text.Json;
using TrueSpec.Core;
using TrueSpec.Core.Reports;

namespace TrueSpec.Tests;

public class ReportExporterTests
{
    [Fact]
    public async Task JsonExporter_WritesTheSameReportModelWithSchemaVersion()
    {
        var snapshot = TestSnapshots.Healthy();
        var report = ReportModel.FromSnapshot(snapshot);
        var exporter = new JsonReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportAsync(report, stream);

        stream.Position = 0;
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(snapshot.MachineName, root.GetProperty("machineName").GetString());
        Assert.Equal(ReportFormat.Json, exporter.Format);
        Assert.Equal(
            snapshot.Components.Count,
            root.GetProperty("components").GetArrayLength());
        Assert.Equal(
            snapshot.BuyerChecks.Checks.Count,
            root.GetProperty("buyerChecks").GetProperty("checks").GetArrayLength());
    }

    [Fact]
    public async Task HtmlExporter_WritesAStandalonePageWithSources()
    {
        var snapshot = TestSnapshots.Healthy();
        var exporter = new HtmlReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportAsync(ReportModel.FromSnapshot(snapshot), stream);

        var html = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(ReportFormat.Html, exporter.Format);
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("Test Laptop", html, StringComparison.Ordinal);
        Assert.Contains("CIM Win32_OperatingSystem", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnavailableSnapshot_KeepsNotAvailableWordingInHtml()
    {
        var snapshot = await MachineScanService.CreateDefault().ScanAsync();
        var exporter = new HtmlReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportAsync(ReportModel.FromSnapshot(snapshot), stream);

        var html = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("Not available", html, StringComparison.Ordinal);
        Assert.DoesNotContain("sample", html, StringComparison.OrdinalIgnoreCase);
    }
}
