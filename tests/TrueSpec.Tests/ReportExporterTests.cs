using System.Text.Json;
using TrueSpec.Core;
using TrueSpec.Core.Reports;

namespace TrueSpec.Tests;

public class ReportExporterTests
{
    [Fact]
    public async Task JsonExporter_WritesTheReportModelToAStream()
    {
        var snapshot = await MachineScanService.CreateDefaultStub().ScanAsync();
        var report = ReportModel.FromSnapshot(snapshot);
        var exporter = new JsonReportExporter();

        await using var stream = new MemoryStream();
        await exporter.ExportAsync(report, stream);

        stream.Position = 0;
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.Equal("0.1", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(snapshot.MachineName, root.GetProperty("snapshot").GetProperty("machineName").GetString());
        Assert.Equal(ReportFormat.Json, exporter.Format);
    }
}
