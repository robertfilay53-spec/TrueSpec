using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Security;

namespace TrueSpec.App.Services;

/// <summary>
/// Everything the shell does with an export file, in one place: pick the
/// folder, write the file, raise the confirmation. MainViewModel used to
/// own all of this inline; the shell now only owns the confirmation state
/// (path + text) and the commands that call here. No dialog, no cloud:
/// exports are local files in the export folder.
/// </summary>
public sealed class ExportOrchestrator
{
    private readonly Func<SystemSnapshot> _snapshot;
    private readonly Func<string> _exportFolder;
    private readonly IExportFileOpener _fileOpener;
    private readonly Action<string, string> _onExported;
    private readonly Action<string> _onFailed;

    /// <param name="snapshot">The current scan: exports always describe the snapshot on screen.</param>
    /// <param name="exportFolder">The effective export folder (user-picked, else the default).</param>
    /// <param name="fileOpener">Opens the folder or file after a successful export.</param>
    /// <param name="onExported">Raised with (path, label) after a successful export.</param>
    /// <param name="onFailed">Raised with the user-facing failure line.</param>
    public ExportOrchestrator(
        Func<SystemSnapshot> snapshot,
        Func<string> exportFolder,
        IExportFileOpener fileOpener,
        Action<string, string> onExported,
        Action<string> onFailed)
    {
        _snapshot = snapshot;
        _exportFolder = exportFolder;
        _fileOpener = fileOpener;
        _onExported = onExported;
        _onFailed = onFailed;
    }

    /// <summary>Exports the current snapshot as a report (JSON, HTML, CSV or PDF).</summary>
    public async Task ExportReportAsync(ReportFormat format)
    {
        try
        {
            var folder = PrepareFolder();
            var (extension, exporter) = ReportExporterFactory.Resolve(format);
            var path = Path.Combine(folder, PathSafety.SafeReportFileName("TrueSpec-report", extension, DateTime.Now));
            await using var stream = File.Create(path);
            await exporter.ExportAsync(ReportModel.FromSnapshot(_snapshot()), stream);
            _onExported(path, "Report");
        }
        catch (Exception exception)
        {
            _onFailed($"Export failed: {exception.Message}");
        }
    }

    /// <summary>
    /// The buyer certificate (v2.0 U7, Blueprint 4): a one-page PDF from
    /// <see cref="PdfReportExporter"/> with the banner, score, five key
    /// numbers, DNA hash and scan date. It is a separate file from the
    /// full report, never a relabelled copy of it.
    /// </summary>
    public async Task ExportBuyerCertificateAsync()
    {
        try
        {
            var folder = PrepareFolder();
            var path = Path.Combine(folder, PathSafety.SafeReportFileName("TrueSpec-certificate", "pdf", DateTime.Now));
            await using var stream = File.Create(path);
            await new PdfReportExporter().ExportCertificateAsync(ReportModel.FromSnapshot(_snapshot()), stream);
            _onExported(path, "Buyer certificate");
        }
        catch (Exception exception)
        {
            _onFailed($"Buyer certificate export failed: {exception.Message}");
        }
    }

    /// <summary>
    /// Exports the trend currently shown on the History page as CSV. With
    /// no history there is nothing to export, and the failure says so
    /// instead of writing an empty file.
    /// </summary>
    public async Task ExportHistoryCsvAsync(HistoryTrend? trend)
    {
        if (trend is null || trend.SnapshotCount == 0)
        {
            _onFailed("History export failed: there is no history to export yet.");
            return;
        }

        try
        {
            var folder = PrepareFolder();
            var path = Path.Combine(folder, PathSafety.SafeReportFileName("TrueSpec-history", "csv", DateTime.Now));
            await using var stream = File.Create(path);
            await new HistoryCsvExporter().ExportAsync(trend, stream);
            _onExported(path, "History");
        }
        catch (Exception exception)
        {
            _onFailed($"History export failed: {exception.Message}");
        }
    }

    /// <summary>
    /// Exports the fleet table currently shown on the Fleet page as CSV.
    /// With no fleet there is nothing to export, and the failure says so
    /// instead of writing an empty file.
    /// </summary>
    public async Task ExportFleetCsvAsync(IReadOnlyList<FleetMachineRow>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            _onFailed("Fleet export failed: there is no fleet to export yet.");
            return;
        }

        try
        {
            var folder = PrepareFolder();
            var path = Path.Combine(folder, PathSafety.SafeReportFileName("TrueSpec-fleet", "csv", DateTime.Now));
            await using var stream = File.Create(path);
            await new FleetCsvExporter().ExportAsync(rows, stream);
            _onExported(path, "Fleet");
        }
        catch (Exception exception)
        {
            _onFailed($"Fleet export failed: {exception.Message}");
        }
    }

    /// <summary>
    /// Exports one machine's latest scan as its own CSV. The machine name
    /// goes through SafeReportFileName, so a hostile machine name cannot
    /// inject a path through the file name. With no row or no score there
    /// is nothing to export, and the failure says so instead of writing
    /// an empty file.
    /// </summary>
    public async Task ExportMachineCsvAsync(FleetMachineRow? row, ScoreResult? score)
    {
        if (row is null || score is null)
        {
            _onFailed("Machine export failed: there is no machine to export yet.");
            return;
        }

        try
        {
            var folder = PrepareFolder();
            var path = Path.Combine(folder, PathSafety.SafeReportFileName($"TrueSpec-machine-{row.MachineName}", "csv", DateTime.Now));
            await using var stream = File.Create(path);
            await new MachineCsvExporter().ExportAsync(row, score, stream);
            _onExported(path, "Machine");
        }
        catch (Exception exception)
        {
            _onFailed($"Machine export failed: {exception.Message}");
        }
    }

    /// <summary>
    /// Exports every machine in the fleet as its own CSV (v2.1.4,
    /// Technician mode: "Export all machines"): one file per machine in
    /// the export folder, each written exactly like the per-row export,
    /// so the files agree with the table row for row. A machine whose
    /// latest scan cannot be read, or has no score, is skipped with a
    /// plain-English reason, and a write failure for one machine never
    /// stops the rest. File names go through SafeReportFileName, and a
    /// name that already exists in the folder (two machines whose names
    /// sanitise identically, exported in the same second) gets a -2, -3
    /// suffix instead of overwriting the first file. Returns the outcome;
    /// the caller renders the status line.
    /// </summary>
    public async Task<ExportAllMachinesResult> ExportAllMachinesCsvAsync(
        IReadOnlyList<FleetMachineRow>? rows,
        IReadOnlyDictionary<string, SystemSnapshot>? snapshotsByMachine)
    {
        var folder = _exportFolder();
        var exported = new List<string>();
        var skipped = new List<(string MachineName, string Reason)>();
        if (rows is null || rows.Count == 0)
        {
            return new ExportAllMachinesResult(folder, exported, skipped);
        }

        Directory.CreateDirectory(folder);
        foreach (var row in rows)
        {
            if (snapshotsByMachine is null ||
                !snapshotsByMachine.TryGetValue(row.MachineName, out var snapshot) ||
                snapshot is null)
            {
                skipped.Add((row.MachineName, "no saved scan was found"));
                continue;
            }

            if (snapshot.Score is null)
            {
                skipped.Add((row.MachineName, "the latest scan has no score to export"));
                continue;
            }

            try
            {
                var fileName = PathSafety.SafeReportFileName($"TrueSpec-machine-{row.MachineName}", "csv", DateTime.Now);
                var path = UniquePath(folder, fileName);
                await using var stream = File.Create(path);
                await new MachineCsvExporter().ExportAsync(row, snapshot.Score, stream);
                exported.Add(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                skipped.Add((row.MachineName, $"could not be written ({exception.Message})"));
            }
        }

        return new ExportAllMachinesResult(folder, exported, skipped);
    }

    /// <summary>
    /// A file name that already exists in the folder gets a -2, -3 suffix
    /// before the extension instead of overwriting the file already there:
    /// two machines whose names sanitise to the same file name, exported
    /// in the same second, must each keep their own CSV.
    /// </summary>
    private static string UniquePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path))
        {
            return path;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var suffix = 2;
        while (File.Exists(path = Path.Combine(folder, $"{stem}-{suffix}{extension}")))
        {
            suffix++;
        }

        return path;
    }

    /// <summary>Exports the buyer checklist for the current snapshot as HTML.</summary>
    public async Task ExportChecklistAsync()
    {
        try
        {
            var folder = PrepareFolder();
            var path = Path.Combine(folder, PathSafety.SafeReportFileName("TrueSpec-checklist", "html", DateTime.Now));
            await using var stream = File.Create(path);
            await new ChecklistHtmlExporter().ExportAsync(_snapshot(), stream);
            _onExported(path, "Buyer checklist");
        }
        catch (Exception exception)
        {
            _onFailed($"Checklist export failed: {exception.Message}");
        }
    }

    /// <summary>
    /// Opens the folder of the last export. Only ever opens a folder that
    /// still exists; a missing folder is reported, never opened.
    /// </summary>
    public bool OpenExportFolder(string? confirmationPath)
    {
        if (confirmationPath is null)
        {
            return false;
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(confirmationPath));
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            _onFailed("Export folder could not be opened: the folder no longer exists.");
            return false;
        }

        if (!_fileOpener.OpenFolder(folder))
        {
            _onFailed($"Could not open {folder}. The file is still saved there.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Opens the file of the last export. Only ever opens a file this app
    /// just wrote into the export folder: a confirmation path pointing
    /// anywhere else is not opened, even if it exists.
    /// </summary>
    public bool OpenExportedFile(string? confirmationPath)
    {
        if (confirmationPath is null)
        {
            return false;
        }

        if (!File.Exists(confirmationPath) || !IsDirectlyInExportFolder(confirmationPath))
        {
            _onFailed("Exported file could not be opened: the file is no longer in the export folder.");
            return false;
        }

        if (!_fileOpener.OpenFile(confirmationPath))
        {
            _onFailed($"Could not open {confirmationPath}. The file is still saved in the export folder.");
            return false;
        }

        return true;
    }

    private string PrepareFolder()
    {
        var folder = _exportFolder();
        Directory.CreateDirectory(folder);
        return folder;
    }

    private bool IsDirectlyInExportFolder(string path)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        return string.Equals(folder, Path.GetFullPath(_exportFolder()), StringComparison.OrdinalIgnoreCase);
    }
}
