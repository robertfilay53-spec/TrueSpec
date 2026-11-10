namespace TrueSpec.Core.Reports;

using TrueSpec.Core.Models;

public enum ReportFormat
{
    Json,
    Html,
    Pdf,
}

/// <summary>
/// One report data model feeds every output format. P0 ships the JSON exporter;
/// HTML and PDF arrive in P7 without changing this model.
/// </summary>
public sealed record ReportModel(
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    SystemSnapshot Snapshot)
{
    public string MachineName => Snapshot.MachineName;

    public string OperatingSystem => Snapshot.OperatingSystem;

    public double OverallScore => Snapshot.Score.OverallScore;

    public Verdict Verdict => Snapshot.Score.Verdict;

    public IReadOnlyList<ComponentSnapshot> Components => Snapshot.Components;

    public BuyerCheckResult BuyerChecks => Snapshot.BuyerChecks;

    public static ReportModel FromSnapshot(SystemSnapshot snapshot) =>
        new("0.1", DateTimeOffset.Now, snapshot);
}

public interface IReportExporter
{
    ReportFormat Format { get; }

    Task ExportAsync(ReportModel report, Stream destination, CancellationToken cancellationToken = default);
}
