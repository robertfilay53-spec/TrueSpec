using System.Text;
using System.Text.Json;
using TrueSpec.Core.Comparison;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

/// <summary>
/// Technician mode: importing JSON reports into the fleet store (v2.1.0,
/// plan Beyond v2.0 item 1). The Report page already exports a machine's
/// scan as JSON; a technician who scanned machines with the installed app
/// (not the portable stick) can now bring those files into one store, and
/// the Fleet page shows them all. Runs on both legs: pure Core code over
/// streams and a real SQLite store.
/// </summary>
public class SnapshotReportImporterTests
{
    private static async Task<MemoryStream> ExportAsync(SystemSnapshot snapshot)
    {
        var stream = new MemoryStream();
        await new JsonReportExporter().ExportAsync(ReportModel.FromSnapshot(snapshot), stream);
        stream.Position = 0;
        return stream;
    }

    private static SystemSnapshot SnapshotWithDna()
    {
        var snapshot = TestSnapshots.Healthy();
        var parts = new[]
        {
            new MachineDnaPart(MachineDnaPartKind.Board, "Board", "ABCD1234"),
            new MachineDnaPart(MachineDnaPartKind.Cpu, "CPU", "WXYZ9876"),
        };
        return snapshot with
        {
            MachineDnaHash = "deadbeef",
            MachineDnaParts = parts,
        };
    }

    [Fact]
    public async Task ExportedReport_ImportsBackWithTheSameValues()
    {
        var snapshot = SnapshotWithDna();

        await using var exported = await ExportAsync(snapshot);
        var result = await new SnapshotReportImporter().ImportAsync(exported);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(snapshot.Id, result.Snapshot!.Id);
        Assert.Equal(snapshot.MachineName, result.Snapshot.MachineName);
        Assert.Equal(snapshot.CapturedAt, result.Snapshot.CapturedAt);
        Assert.Equal(snapshot.Score.OverallScore, result.Snapshot.Score.OverallScore);
        Assert.Equal(snapshot.Components.Count, result.Snapshot.Components.Count);
        Assert.Equal(snapshot.Components[0].Headline, result.Snapshot.Components[0].Headline);
        Assert.Equal("deadbeef", result.Snapshot.MachineDnaHash);
        Assert.Equal(2, result.Snapshot.MachineDnaParts!.Count);
        Assert.Equal("ABCD1234", result.Snapshot.MachineDnaParts[0].SerialTruncated);
    }

    [Fact]
    public async Task NotJson_IsRejectedWithAPlainReason()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("this is not json at all"));

        var result = await new SnapshotReportImporter().ImportAsync(stream);

        Assert.False(result.Success);
        Assert.Null(result.Snapshot);
        Assert.Contains("not a TrueSpec JSON report", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongSchemaVersion_IsRejected()
    {
        var json = """
            {"schemaVersion":"9.9","generatedAt":"2026-11-13T00:00:00+00:00","snapshot":null}
            """;
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await new SnapshotReportImporter().ImportAsync(stream);

        Assert.False(result.Success);
        Assert.Contains("schema version", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingSnapshot_IsRejected()
    {
        var json = """
            {"schemaVersion":"1.0","generatedAt":"2026-11-13T00:00:00+00:00","snapshot":null}
            """;
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await new SnapshotReportImporter().ImportAsync(stream);

        Assert.False(result.Success);
        Assert.Contains("no scan", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyMachineName_IsRejected()
    {
        var snapshot = TestSnapshots.Healthy() with { MachineName = "   " };

        await using var exported = await ExportAsync(snapshot);
        var result = await new SnapshotReportImporter().ImportAsync(exported);

        Assert.False(result.Success);
        Assert.Contains("machine name", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScoreAbove100_IsClampedInsteadOfRejected()
    {
        var snapshot = TestSnapshots.Healthy();
        var score = new ScoreResult(150d, Verdict.UsedHealthy, snapshot.Score.Components, 10d);
        var overScored = snapshot with { Score = score };

        await using var exported = await ExportAsync(overScored);
        var result = await new SnapshotReportImporter().ImportAsync(exported);

        Assert.True(result.Success, result.Error);
        Assert.Equal(100d, result.Snapshot!.Score.OverallScore);
    }

    [Fact]
    public async Task ScoreBelowZero_IsClampedInsteadOfRejected()
    {
        var snapshot = TestSnapshots.Healthy();
        var score = new ScoreResult(-5d, Verdict.UsedHealthy, snapshot.Score.Components, 10d);
        var underScored = snapshot with { Score = score };

        await using var exported = await ExportAsync(underScored);
        var result = await new SnapshotReportImporter().ImportAsync(exported);

        Assert.True(result.Success, result.Error);
        Assert.Equal(0d, result.Snapshot!.Score.OverallScore);
    }

    [Fact]
    public async Task OversizedFile_IsRejectedWithoutReadingItAll()
    {
        var padding = new string(' ', SnapshotReportImporter.MaxReportBytes + 1);
        var json = "{\"schemaVersion\":\"1.0\",\"generatedAt\":\"2026-11-13T00:00:00+00:00\",\"snapshot\":null,\"pad\":\"" + padding + "\"}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await new SnapshotReportImporter().ImportAsync(stream);

        Assert.False(result.Success);
        Assert.Contains("too large", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SameFileImportedTwice_StoresOneScan()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SqliteSnapshotStore(Path.Combine(directory, "snapshots.db"));
            await store.InitializeAsync();
            var importer = new SnapshotReportImporter();
            var snapshot = TestSnapshots.Healthy();

            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var exported = await ExportAsync(snapshot);
                var result = await importer.ImportAsync(exported);
                Assert.True(result.Success, result.Error);
                await store.SaveAsync(result.Snapshot!);
            }

            var history = await store.GetHistoryAsync();
            Assert.Single(history);
            Assert.Equal(snapshot.Id, history[0].Id);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
