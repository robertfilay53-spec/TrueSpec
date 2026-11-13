namespace TrueSpec.Core.Reports;

using System.Text.Json;
using System.Text.Json.Serialization;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

/// <summary>
/// The result of importing one JSON report file: the scan it carried, or
/// a plain-English reason it could not be used. A bad file is a failed
/// import, never an exception out of the import path and never a partial
/// scan in the store.
/// </summary>
public sealed record SnapshotImportResult(
    bool Success,
    SystemSnapshot? Snapshot,
    string? Error)
{
    public static SnapshotImportResult Ok(SystemSnapshot snapshot) => new(true, snapshot, null);

    public static SnapshotImportResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Technician mode (v2.1.0, plan Beyond v2.0 item 1): reads a JSON report
/// file written by <see cref="JsonReportExporter"/> and returns the scan
/// inside it, so the Fleet page can show machines that were scanned with
/// the installed app instead of the portable stick. The file is validated
/// before anything is trusted: it must parse, carry schema version "1.0",
/// hold a scan with a machine name, and hold a finite score. A finite
/// score outside 0-100 is clamped into range (the same rule the component
/// snapshot applies to a health value) instead of rejecting a file over
/// one out-of-range number. Files over 10 MB are refused without being
/// read, so a hostile file cannot eat memory. No network, no elevation,
/// nothing leaves the machine.
/// </summary>
public sealed class SnapshotReportImporter
{
    /// <summary>
    /// The largest report file accepted. A real TrueSpec JSON report is
    /// tens of kilobytes; 10 MB leaves ample headroom for a large machine
    /// while bounding what a hand-edited file can make the app read.
    /// </summary>
    public const int MaxReportBytes = 10 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<SnapshotImportResult> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            var bytes = await ReadCappedAsync(source, cancellationToken);
            if (bytes is null)
            {
                return SnapshotImportResult.Fail(
                    "The file is too large to be a TrueSpec report (over 10 MB) and was skipped.");
            }

            var report = JsonSerializer.Deserialize<ReportModel>(bytes, Options);
            if (report is null)
            {
                return SnapshotImportResult.Fail("The file is empty and is not a TrueSpec JSON report.");
            }

            if (!string.Equals(report.SchemaVersion, "1.0", StringComparison.Ordinal))
            {
                return SnapshotImportResult.Fail(
                    $"The file uses report schema version '{report.SchemaVersion}', which this version of TrueSpec cannot import. Only version 1.0 reports can be imported.");
            }

            if (report.Snapshot is null)
            {
                return SnapshotImportResult.Fail("The file holds no scan, so there is nothing to import.");
            }

            var snapshot = report.Snapshot;
            if (string.IsNullOrWhiteSpace(snapshot.MachineName))
            {
                return SnapshotImportResult.Fail("The file holds a scan with no machine name, so it cannot be listed in the fleet.");
            }

            if (snapshot.Components is null || snapshot.Score is null || snapshot.BuyerChecks is null)
            {
                return SnapshotImportResult.Fail("The file is missing part of the scan (components, score or buyer checks), so it cannot be imported.");
            }

            if (!double.IsFinite(snapshot.Score.OverallScore))
            {
                return SnapshotImportResult.Fail("The file holds a score that is not a number, so it cannot be imported.");
            }

            if (snapshot.Score.OverallScore < 0d || snapshot.Score.OverallScore > 100d)
            {
                var clamped = new ScoreResult(
                    Math.Clamp(snapshot.Score.OverallScore, 0d, 100d),
                    snapshot.Score.Verdict,
                    snapshot.Score.Components,
                    snapshot.Score.MeasuredWeight);
                snapshot = snapshot with { Score = clamped };
            }

            return SnapshotImportResult.Ok(snapshot);
        }
        catch (JsonException)
        {
            return SnapshotImportResult.Fail("The file is not a TrueSpec JSON report, so it was skipped.");
        }
        catch (NotSupportedException)
        {
            return SnapshotImportResult.Fail("The file is not a TrueSpec JSON report, so it was skipped.");
        }
    }

    private static async Task<byte[]?> ReadCappedAsync(Stream source, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxReportBytes)
            {
                return null;
            }
        }

        return buffer.ToArray();
    }
}
