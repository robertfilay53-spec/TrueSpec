namespace TrueSpec.Core.Reports;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes the report data model as machine readable JSON. No network access,
/// no telemetry, everything stays on the machine.
/// </summary>
public sealed class JsonReportExporter : IReportExporter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public ReportFormat Format => ReportFormat.Json;

    public async Task ExportAsync(
        ReportModel report,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        await JsonSerializer.SerializeAsync(destination, report, Options, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }
}
