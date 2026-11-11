namespace TrueSpec.Core.Reports;

/// <summary>
/// The single place that maps a report format to its file extension and
/// exporter. MainViewModel's export path resolves through here, so a format
/// the UI offers can never silently disagree with the exporter or with
/// PathSafety's allowed-extension list. That disagreement is exactly what
/// broke PDF export in v1.0.1: the dispatcher produced "pdf" and the PDF
/// writer itself worked, but the allowlist rejected "pdf", so Export PDF
/// failed with "Unsupported report extension 'pdf'." and no file was
/// produced. The regression test resolves every format here and builds its
/// file name, which is the path that failure took.
/// </summary>
public static class ReportExporterFactory
{
    public static (string Extension, IReportExporter Exporter) Resolve(ReportFormat format) =>
        format switch
        {
            ReportFormat.Html => ("html", new HtmlReportExporter()),
            ReportFormat.Csv => ("csv", new CsvReportExporter()),
            ReportFormat.Pdf => ("pdf", new PdfReportExporter()),
            _ => ("json", new JsonReportExporter()),
        };
}
