using System.Text;
using TrueSpec.App.Services;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

/// <summary>
/// Technician mode batch export (v2.1.4): "Export all machines" writes
/// one CSV per machine into the export folder, each file exactly like
/// the per-row export. Windows leg only (it drives the App's
/// ExportOrchestrator), like FleetViewModelTests: excluded from the
/// net10.0 leg in the csproj.
/// </summary>
public class ExportAllMachinesTests
{
    private sealed class RecordingFileOpener : IExportFileOpener
    {
        public bool OpenFolder(string folderPath) => true;

        public bool OpenFile(string filePath) => true;
    }

    private static ExportOrchestrator Orchestrator(string folder) => new(
        () => TestSnapshots.Healthy(),
        () => folder,
        new RecordingFileOpener(),
        (_, _) => { },
        _ => { });

    private static FleetMachineRow Row(
        string machine,
        int scans,
        double? score,
        string verdict = "Used - healthy",
        string? worst = "storage",
        double? worstHealth = 62d) =>
        new(
            machine,
            scans,
            new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero),
            score,
            verdict,
            worst,
            worstHealth);

    private static ScoreResult Score(params ComponentScore[] components) =>
        new(82.4d, Verdict.UsedHealthy, components, 100d);

    private static SystemSnapshot Snapshot(string machine) =>
        TestSnapshots.Healthy() with { MachineName = machine, Score = Score(new ComponentScore("cpu", 95d, 40d, 0.4d, 38d)) };

    private static string Root()
    {
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public async Task ExportsOneCsvPerMachine_ContentsAgreeWithPerMachineExport()
    {
        var root = Root();
        var folder = Path.Combine(root, "exports");

        try
        {
            var rows = new[]
            {
                Row("SHOP-PC-GOOD", 2, 92d),
                Row("SHOP-PC-BAD", 5, 55d, "Used - worn", "battery", 47d),
            };
            var snapshots = new Dictionary<string, SystemSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["SHOP-PC-GOOD"] = Snapshot("SHOP-PC-GOOD"),
                ["SHOP-PC-BAD"] = Snapshot("SHOP-PC-BAD"),
            };

            var result = await Orchestrator(folder).ExportAllMachinesCsvAsync(rows, snapshots);

            Assert.Equal(folder, result.Folder);
            Assert.Equal(2, result.ExportedPaths.Count);
            Assert.Empty(result.Skipped);
            var files = Directory.GetFiles(folder);
            Assert.Equal(2, files.Length);
            foreach (var row in rows)
            {
                var snapshot = snapshots[row.MachineName];
                var expected = MachineCsvExporter.Build(row, snapshot.Score!);
                var path = Assert.Single(files, f => Path.GetFileName(f).StartsWith($"TrueSpec-machine-{row.MachineName}-", StringComparison.Ordinal));
                var bytes = await File.ReadAllBytesAsync(path);
                var preamble = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble();
                Assert.Equal(preamble, bytes[..preamble.Length]);
                Assert.Equal(expected, Encoding.UTF8.GetString(bytes[preamble.Length..]));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SkipsMachineWithNoScore_OthersStillExport()
    {
        var root = Root();
        var folder = Path.Combine(root, "exports");

        try
        {
            var rows = new[]
            {
                Row("SHOP-PC-GOOD", 2, 92d),
                Row("SHOP-PC-NOSCORE", 1, null),
            };
            // The record type is non-nullable, but a snapshot read from an
            // older or hand-edited store row can carry no score; the
            // null-forgiving operator builds that shape for the test.
            var snapshots = new Dictionary<string, SystemSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["SHOP-PC-GOOD"] = Snapshot("SHOP-PC-GOOD"),
                ["SHOP-PC-NOSCORE"] = TestSnapshots.Healthy() with { MachineName = "SHOP-PC-NOSCORE", Score = null! },
            };

            var result = await Orchestrator(folder).ExportAllMachinesCsvAsync(rows, snapshots);

            Assert.Single(result.ExportedPaths);
            Assert.Contains("SHOP-PC-GOOD", File.ReadAllText(result.ExportedPaths[0]), StringComparison.Ordinal);
            var skipped = Assert.Single(result.Skipped);
            Assert.Equal("SHOP-PC-NOSCORE", skipped.MachineName);
            Assert.Equal(ExportAllSkipReasonKind.NoScore, skipped.Kind);
            Assert.Null(skipped.WriteError);
            Assert.Contains("no score", ExportAllSkipReason.Text(skipped.Kind, skipped.WriteError, "en"), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SkipsMachineWithNoSnapshot_OthersStillExport()
    {
        var root = Root();
        var folder = Path.Combine(root, "exports");

        try
        {
            var rows = new[]
            {
                Row("SHOP-PC-GOOD", 2, 92d),
                Row("SHOP-PC-GONE", 1, 55d),
            };
            var snapshots = new Dictionary<string, SystemSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["SHOP-PC-GOOD"] = Snapshot("SHOP-PC-GOOD"),
            };

            var result = await Orchestrator(folder).ExportAllMachinesCsvAsync(rows, snapshots);

            Assert.Single(result.ExportedPaths);
            var skipped = Assert.Single(result.Skipped);
            Assert.Equal("SHOP-PC-GONE", skipped.MachineName);
            Assert.Equal(ExportAllSkipReasonKind.NoSavedScan, skipped.Kind);
            Assert.Null(skipped.WriteError);
            Assert.Contains("no saved scan", ExportAllSkipReason.Text(skipped.Kind, skipped.WriteError, "en"), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SameSecondNameCollision_KeepsBothFiles()
    {
        var root = Root();
        var folder = Path.Combine(root, "exports");

        try
        {
            // Both names sanitise to the same file stem ("AB"): a slash is
            // a separator on Windows and a backslash is stripped by
            // SafeReportFileName. Both files must survive, each with its
            // own machine's CSV inside.
            var rows = new[]
            {
                Row("A/B", 1, 92d),
                Row("A\\B", 1, 55d),
            };
            var snapshots = new Dictionary<string, SystemSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["A/B"] = Snapshot("A/B"),
                ["A\\B"] = Snapshot("A\\B"),
            };

            var result = await Orchestrator(folder).ExportAllMachinesCsvAsync(rows, snapshots);

            Assert.Equal(2, result.ExportedPaths.Count);
            Assert.Empty(result.Skipped);
            Assert.Equal(2, result.ExportedPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Contains("A/B", File.ReadAllText(result.ExportedPaths[0]), StringComparison.Ordinal);
            Assert.Contains("A\\B", File.ReadAllText(result.ExportedPaths[1]), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunTwice_DoesNotOverwriteFirstRun()
    {
        var root = Root();
        var folder = Path.Combine(root, "exports");

        try
        {
            var rows = new[] { Row("SHOP-PC-GOOD", 2, 92d) };
            var snapshots = new Dictionary<string, SystemSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["SHOP-PC-GOOD"] = Snapshot("SHOP-PC-GOOD"),
            };
            var orchestrator = Orchestrator(folder);

            var first = await orchestrator.ExportAllMachinesCsvAsync(rows, snapshots);
            var second = await orchestrator.ExportAllMachinesCsvAsync(rows, snapshots);

            var files = Directory.GetFiles(folder);
            Assert.Equal(2, files.Length);
            Assert.Equal(2, files.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(files, f => Assert.Contains("SHOP-PC-GOOD", File.ReadAllText(f), StringComparison.Ordinal));
            Assert.Single(first.ExportedPaths);
            Assert.Single(second.ExportedPaths);
            Assert.NotEqual(first.ExportedPaths[0], second.ExportedPaths[0], StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EmptyRows_ReturnsZeroAndCreatesNoFolder()
    {
        var root = Root();
        var folder = Path.Combine(root, "exports");

        try
        {
            var result = await Orchestrator(folder).ExportAllMachinesCsvAsync(
                Array.Empty<FleetMachineRow>(),
                new Dictionary<string, SystemSnapshot>());

            Assert.Empty(result.ExportedPaths);
            Assert.Empty(result.Skipped);
            Assert.False(Directory.Exists(folder));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExportFolderIsAFile_ThrowsInsteadOfWritingElsewhere()
    {
        var root = Root();

        try
        {
            var fileAsFolder = Path.Combine(root, "not-a-folder");
            await File.WriteAllTextAsync(fileAsFolder, "x");
            var rows = new[] { Row("SHOP-PC-GOOD", 2, 92d) };
            var snapshots = new Dictionary<string, SystemSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["SHOP-PC-GOOD"] = Snapshot("SHOP-PC-GOOD"),
            };

            await Assert.ThrowsAsync<IOException>(() =>
                Orchestrator(fileAsFolder).ExportAllMachinesCsvAsync(rows, snapshots));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
