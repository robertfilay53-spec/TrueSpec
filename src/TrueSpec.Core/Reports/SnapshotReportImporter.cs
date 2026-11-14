namespace TrueSpec.Core.Reports;

using System.Text.Json;
using System.Text.Json.Serialization;
using TrueSpec.Core.Localization;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

/// <summary>
/// The result of importing one JSON report file: the scan it carried, or
/// a plain-English reason it could not be used. A bad file is a failed
/// import, never an exception out of the import path and never a partial
/// scan in the store. The reason is also carried as a structured
/// <see cref="SnapshotImportFailureKind"/> so a caller can render it in
/// the user's language; <see cref="Error"/> stays the English sentence
/// for the existing callers and tests.
/// </summary>
public sealed record SnapshotImportResult(
    bool Success,
    SystemSnapshot? Snapshot,
    string? Error,
    SnapshotImportFailureKind? FailureKind = null,
    string? SchemaVersion = null)
{
    public static SnapshotImportResult Ok(SystemSnapshot snapshot) => new(true, snapshot, null);

    public static SnapshotImportResult Fail(SnapshotImportFailureKind kind, string? schemaVersion = null) =>
        new(false, null, SnapshotImportFailureReason.Text(kind, schemaVersion, LanguagePacks.EnglishCode), kind, schemaVersion);
}

/// <summary>
/// Why a JSON report file could not be imported, as a structured kind
/// instead of a sentence. The Fleet page renders the reason in the page
/// language from the kind, so a German user sees German; the English
/// sentence in <see cref="SnapshotImportFailureReason"/> stays the
/// importer's public <see cref="SnapshotImportResult.Error"/> text.
/// </summary>
public enum SnapshotImportFailureKind
{
    TooLarge,
    Empty,
    WrongSchemaVersion,
    NoSnapshot,
    NoMachineName,
    MissingPart,
    ScoreNotFinite,
    NotJsonReport,
}

/// <summary>
/// Renders a <see cref="SnapshotImportFailureKind"/> as the reason
/// sentence in the requested language, from the catalogue keys. The
/// schema version is data (a value from the file), never translated.
/// </summary>
public static class SnapshotImportFailureReason
{
    public static string Text(SnapshotImportFailureKind kind, string? schemaVersion, string? languageCode) =>
        kind switch
        {
            SnapshotImportFailureKind.TooLarge => StringLocalizer.Get(AppStringKeys.SnapshotImportTooLarge, languageCode),
            SnapshotImportFailureKind.Empty => StringLocalizer.Get(AppStringKeys.SnapshotImportEmpty, languageCode),
            SnapshotImportFailureKind.WrongSchemaVersion => StringLocalizer.GetFormatted(
                AppStringKeys.SnapshotImportWrongSchemaVersion,
                languageCode,
                schemaVersion ?? string.Empty),
            SnapshotImportFailureKind.NoSnapshot => StringLocalizer.Get(AppStringKeys.SnapshotImportNoSnapshot, languageCode),
            SnapshotImportFailureKind.NoMachineName => StringLocalizer.Get(AppStringKeys.SnapshotImportNoMachineName, languageCode),
            SnapshotImportFailureKind.MissingPart => StringLocalizer.Get(AppStringKeys.SnapshotImportMissingPart, languageCode),
            SnapshotImportFailureKind.ScoreNotFinite => StringLocalizer.Get(AppStringKeys.SnapshotImportScoreNotFinite, languageCode),
            SnapshotImportFailureKind.NotJsonReport => StringLocalizer.Get(AppStringKeys.SnapshotImportNotJsonReport, languageCode),
            _ => StringLocalizer.Get(AppStringKeys.SnapshotImportNotJsonReport, languageCode),
        };
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
                return SnapshotImportResult.Fail(SnapshotImportFailureKind.TooLarge);
            }

            var report = JsonSerializer.Deserialize<ReportModel>(bytes, Options);
            if (report is null)
            {
                return SnapshotImportResult.Fail(SnapshotImportFailureKind.Empty);
            }

            if (!string.Equals(report.SchemaVersion, "1.0", StringComparison.Ordinal))
            {
                return SnapshotImportResult.Fail(SnapshotImportFailureKind.WrongSchemaVersion, report.SchemaVersion);
            }

            if (report.Snapshot is null)
            {
                return SnapshotImportResult.Fail(SnapshotImportFailureKind.NoSnapshot);
            }

            var snapshot = report.Snapshot;
            if (string.IsNullOrWhiteSpace(snapshot.MachineName))
            {
                return SnapshotImportResult.Fail(SnapshotImportFailureKind.NoMachineName);
            }

            if (snapshot.Components is null || snapshot.Score is null || snapshot.BuyerChecks is null)
            {
                return SnapshotImportResult.Fail(SnapshotImportFailureKind.MissingPart);
            }

            if (!double.IsFinite(snapshot.Score.OverallScore))
            {
                return SnapshotImportResult.Fail(SnapshotImportFailureKind.ScoreNotFinite);
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
            return SnapshotImportResult.Fail(SnapshotImportFailureKind.NotJsonReport);
        }
        catch (NotSupportedException)
        {
            return SnapshotImportResult.Fail(SnapshotImportFailureKind.NotJsonReport);
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
