namespace TrueSpec.Core.Reports;

using TrueSpec.Core.Models;

public enum ReportFormat
{
    Json,
    Html,
    Pdf,
}

/// <summary>
/// One report data model feeds every output format. The JSON and HTML exporters
/// use the same real snapshot, so both always agree.
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
        new("1.0", DateTimeOffset.Now, snapshot);
}

public interface IReportExporter
{
    ReportFormat Format { get; }

    Task ExportAsync(ReportModel report, Stream destination, CancellationToken cancellationToken = default);
}
