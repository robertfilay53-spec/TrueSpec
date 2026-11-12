using TrueSpec.App.Services;
using TrueSpec.App.ViewModels;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Sensors;
using TrueSpec.Core.Settings;

namespace TrueSpec.Tests;

/// <summary>
/// Tests for the v1.4.1 export-UX fix (F10). The v1.4.0 laptop test
/// reported export as broken when the files were in fact written: the
/// save was silent (a status-bar line only) and the user expected the
/// Downloads folder. Every export now raises an unmissable confirmation
/// with the exact saved path plus "Open folder" / "Open file" actions,
/// and the default export folder is Downloads when it exists.
/// </summary>
public class ExportConfirmationTests
{
    private sealed class RecordingFileOpener : IExportFileOpener
    {
        public List<string> OpenedFolders { get; } = new();

        public List<string> OpenedFiles { get; } = new();

        public bool OpenFolder(string folderPath)
        {
            OpenedFolders.Add(folderPath);
            return true;
        }

        public bool OpenFile(string filePath)
        {
            OpenedFiles.Add(filePath);
            return true;
        }
    }

    private static (MainViewModel Shell, RecordingFileOpener Opener, string Root) CreateShell(string? exportFolder)
    {
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
        if (exportFolder is not null)
        {
            Assert.True(store.Save(new AppSettings { ExportFolder = exportFolder }));
        }

        var opener = new RecordingFileOpener();
        var shell = new MainViewModel(
            Array.Empty<ICollector>(),
            new UnavailableSensorMonitor(),
            new BuyerCheckEvaluator(),
            store,
            opener);
        return (shell, opener, root);
    }

    private static void DeleteTree(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExportJson_WritesFileAndRaisesConfirmationWithPath()
    {
        var exportFolder = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"), "exports");
        var (shell, _, root) = CreateShell(exportFolder);
        try
        {
            await shell.ExportJsonCommand.ExecuteAsync(null);

            Assert.True(shell.HasExportConfirmation);
            Assert.NotNull(shell.ExportConfirmationPath);
            Assert.StartsWith(exportFolder, shell.ExportConfirmationPath, StringComparison.Ordinal);
            Assert.EndsWith(".json", shell.ExportConfirmationPath, StringComparison.Ordinal);
            Assert.True(File.Exists(shell.ExportConfirmationPath));
            Assert.True(new FileInfo(shell.ExportConfirmationPath).Length > 0);
            Assert.Contains(shell.ExportConfirmationPath, shell.ExportConfirmationText, StringComparison.Ordinal);
            Assert.StartsWith($"Report exported to {shell.ExportConfirmationPath}", shell.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task ExportPdf_WritesFileAndRaisesConfirmationWithPath()
    {
        var exportFolder = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"), "exports");
        var (shell, _, root) = CreateShell(exportFolder);
        try
        {
            await shell.ExportPdfCommand.ExecuteAsync(null);

            Assert.True(shell.HasExportConfirmation);
            Assert.NotNull(shell.ExportConfirmationPath);
            Assert.EndsWith(".pdf", shell.ExportConfirmationPath, StringComparison.Ordinal);
            Assert.True(File.Exists(shell.ExportConfirmationPath));
            Assert.Contains(shell.ExportConfirmationPath, shell.ExportConfirmationText, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task ExportChecklist_WritesFileAndRaisesConfirmationWithPath()
    {
        var exportFolder = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"), "exports");
        var (shell, _, root) = CreateShell(exportFolder);
        try
        {
            await shell.ExportChecklistCommand.ExecuteAsync(null);

            Assert.True(shell.HasExportConfirmation);
            Assert.NotNull(shell.ExportConfirmationPath);
            Assert.EndsWith(".html", shell.ExportConfirmationPath, StringComparison.Ordinal);
            Assert.True(File.Exists(shell.ExportConfirmationPath));
            Assert.StartsWith("Buyer checklist exported to ", shell.ExportConfirmationText, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task ExportHistoryCsv_WithNoHistory_RaisesNoConfirmation()
    {
        var exportFolder = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"), "exports");
        var (shell, _, root) = CreateShell(exportFolder);
        try
        {
            await shell.ExportHistoryCsvCommand.ExecuteAsync(null);

            Assert.False(shell.HasExportConfirmation);
            Assert.Null(shell.ExportConfirmationPath);
            Assert.Contains("no history to export", shell.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task ExportFailure_RaisesNoConfirmation()
    {
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var blocker = Path.Combine(root, "blocker.txt");
        File.WriteAllText(blocker, "a file, not a folder");
        var (shell, _, shellRoot) = CreateShell(blocker);
        try
        {
            await shell.ExportJsonCommand.ExecuteAsync(null);

            Assert.False(shell.HasExportConfirmation);
            Assert.Null(shell.ExportConfirmationPath);
            Assert.StartsWith("Export failed:", shell.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTree(shellRoot);
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task SettingsChosenFolder_IsRespectedOverDefault()
    {
        // A user who already picked a folder in Settings must keep it: the
        // export lands in their folder, not in Downloads.
        var customFolder = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"), "my-exports");
        var (shell, _, root) = CreateShell(customFolder);
        try
        {
            Assert.Equal(customFolder, shell.ExportFolder);

            await shell.ExportJsonCommand.ExecuteAsync(null);

            Assert.NotNull(shell.ExportConfirmationPath);
            Assert.StartsWith(customFolder, shell.ExportConfirmationPath, StringComparison.Ordinal);
            Assert.True(File.Exists(shell.ExportConfirmationPath));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task OpenFolderAndOpenFile_UseConfirmationPath()
    {
        var exportFolder = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"), "exports");
        var (shell, opener, root) = CreateShell(exportFolder);
        try
        {
            await shell.ExportJsonCommand.ExecuteAsync(null);
            Assert.NotNull(shell.ExportConfirmationPath);

            shell.OpenExportFolderCommand.Execute(null);
            Assert.Single(opener.OpenedFolders);
            Assert.Equal(exportFolder, opener.OpenedFolders[0]);

            shell.OpenExportedFileCommand.Execute(null);
            Assert.Single(opener.OpenedFiles);
            Assert.Equal(shell.ExportConfirmationPath, opener.OpenedFiles[0]);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task DismissExportConfirmation_ClearsBanner()
    {
        var exportFolder = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"), "exports");
        var (shell, _, root) = CreateShell(exportFolder);
        try
        {
            await shell.ExportJsonCommand.ExecuteAsync(null);
            Assert.True(shell.HasExportConfirmation);

            shell.DismissExportConfirmationCommand.Execute(null);

            Assert.False(shell.HasExportConfirmation);
            Assert.Null(shell.ExportConfirmationPath);
            Assert.Equal(string.Empty, shell.ExportConfirmationText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void OpenExportedFile_DoesNothingWithNoConfirmation()
    {
        var (shell, opener, root) = CreateShell(null);
        try
        {
            shell.OpenExportedFileCommand.Execute(null);
            shell.OpenExportFolderCommand.Execute(null);

            Assert.Empty(opener.OpenedFiles);
            Assert.Empty(opener.OpenedFolders);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task ExportConfirmation_NotesThatTheFileContainsSensitiveIdentifiers()
    {
        // Hardening 6g: an export contains hardware serial numbers, MAC
        // addresses, IP addresses and Wi-Fi network names. The
        // confirmation says so, so the buyer knows before sharing it.
        var exportFolder = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"), "exports");
        var (shell, _, root) = CreateShell(exportFolder);
        try
        {
            await shell.ExportJsonCommand.ExecuteAsync(null);

            Assert.Contains("serial numbers", shell.ExportConfirmationText, StringComparison.Ordinal);
            Assert.Contains("MAC addresses", shell.ExportConfirmationText, StringComparison.Ordinal);
            Assert.Contains("IP addresses", shell.ExportConfirmationText, StringComparison.Ordinal);
            Assert.Contains("Wi-Fi network names", shell.ExportConfirmationText, StringComparison.Ordinal);
            Assert.Contains("Share it only with someone you trust", shell.ExportConfirmationText, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTree(root);
        }
    }
}
