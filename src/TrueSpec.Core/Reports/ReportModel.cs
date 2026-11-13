namespace TrueSpec.Core.Reports;

using TrueSpec.Core.Models;

public enum ReportFormat
{
    Json,
    Html,
    Csv,
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

    /// <summary>
    /// The buyer certificate for this snapshot (Blueprint 4, U7): banner,
    /// score, the five key numbers, the DNA hash and the scan date. The
    /// JSON export carries it so a re-scan can be verified
    /// programmatically; the other exporters ignore it.
    /// </summary>
    public BuyerCertificate Certificate => BuyerCertificate.FromSnapshot(Snapshot);

    public static ReportModel FromSnapshot(SystemSnapshot snapshot) =>
        new("1.0", DateTimeOffset.Now, snapshot);
}

public interface IReportExporter
{
    ReportFormat Format { get; }

    Task ExportAsync(ReportModel report, Stream destination, CancellationToken cancellationToken = default);
}
