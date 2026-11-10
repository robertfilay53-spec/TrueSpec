using System.Text;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;

namespace TrueSpec.Tests;

/// <summary>
/// Tests for the P4 report exports: CSV output and the printable buyer
/// checklist. The CSV tests include the formula-injection guard: a value
/// that looks like a spreadsheet formula must never reach Excel as one.
/// </summary>
public class ExportTests
{
    [Fact]
    public async Task CsvExporter_WritesHeaderAndOneRowPerSpec()
    {
        var snapshot = TestSnapshots.Healthy();
        var report = ReportModel.FromSnapshot(snapshot);

        using var stream = new MemoryStream();
        await new CsvReportExporter().ExportAsync(report, stream);

        var text = Encoding.UTF8.GetString(stream.ToArray());
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Component,Spec,Value,Source,Health", lines[0]);
        Assert.Contains("CPU,Package temperature,55 C,LibreHardwareMonitor,", text);
        Assert.Contains("RAM,Total memory,16 GB,CIM Win32_PhysicalMemory,", text);
        Assert.Equal(snapshot.Components.SelectMany(c => c.Specs).Count() + 1, lines.Length);
    }

    [Fact]
    public async Task CsvExporter_NeutralisesFormulaInjection()
    {
        var snapshot = TestSnapshots.Healthy();
        var poisoned = snapshot with
        {
            Components = snapshot.Components.Select(c =>
                c.Id == "cpu"
                    ? c with { Specs = [new SpecValue("Package temperature", "=2+5", "LibreHardwareMonitor")] }
                    : c).ToList(),
        };
        var report = ReportModel.FromSnapshot(poisoned);

        using var stream = new MemoryStream();
        await new CsvReportExporter().ExportAsync(report, stream);

        var text = Encoding.UTF8.GetString(stream.ToArray());

        // The value must not start a cell with '='. The exporter prefixes a
        // tab, which Excel renders as harmless text.
        Assert.DoesNotContain(",=2+5,", text);
        Assert.Contains("CPU,Package temperature,\t=2+5,LibreHardwareMonitor,", text);
    }

    [Fact]
    public async Task CsvExporter_QuotesValuesWithCommasAndQuotes()
    {
        var snapshot = TestSnapshots.Healthy();
        var tricky = snapshot with
        {
            Components = snapshot.Components.Select(c =>
                c.Id == "cpu"
                    ? c with { Specs = [new SpecValue("Note", "say \"hi\", then wait", "Manual")] }
                    : c).ToList(),
        };

        using var stream = new MemoryStream();
        await new CsvReportExporter().ExportAsync(ReportModel.FromSnapshot(tricky), stream);

        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("\"say \"\"hi\"\", then wait\"", text);
    }

    [Fact]
    public async Task ChecklistExporter_WritesPrintableHtmlWithEveryCheck()
    {
        var snapshot = TestSnapshots.Healthy();

        using var stream = new MemoryStream();
        await new ChecklistHtmlExporter().ExportAsync(snapshot, stream);

        var html = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("@media print", html);
        Assert.Contains("Test Laptop", html);
        Assert.Contains("Buyer checklist", html);
        foreach (var check in snapshot.BuyerChecks.Checks)
        {
            Assert.Contains(check.Title, html);
        }
    }

    [Fact]
    public async Task ChecklistExporter_EncodesHtmlInMachineName()
    {
        var snapshot = TestSnapshots.Healthy() with { MachineName = "<script>alert(1)</script>" };

        using var stream = new MemoryStream();
        await new ChecklistHtmlExporter().ExportAsync(snapshot, stream);

        var html = Encoding.UTF8.GetString(stream.ToArray());

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
