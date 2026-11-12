using TrueSpec.App.Services;

namespace TrueSpec.Tests;

/// <summary>
/// Shell export opener validation (hardening 6g): the opener itself
/// refuses a non-report extension and anything outside the export folder.
/// Only the refusal paths are tested here: an accepted open would launch
/// Explorer / the registered app on the CI machine.
/// Windows leg only (it drives TrueSpec.App's Services), like
/// ExportConfirmationTests: excluded from the net10.0 leg in the csproj.
/// </summary>
public class ShellExportFileOpenerTests
{
    private static string TempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact]
    public void OpenFile_RejectsAnExecutableExtension()
    {
        var directory = TempDirectory();
        try
        {
            var exe = Path.Combine(directory, "report.exe");
            File.WriteAllText(exe, "not an executable, but the extension is what matters");

            Assert.False(new ShellExportFileOpener().OpenFile(exe));
            Assert.False(new ShellExportFileOpener(() => directory).OpenFile(exe));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void OpenFile_RejectsAFileOutsideTheExportFolder()
    {
        var exportFolder = TempDirectory();
        var elsewhere = TempDirectory();
        try
        {
            var pdf = Path.Combine(elsewhere, "report.pdf");
            File.WriteAllText(pdf, "a report, but not in the export folder");

            Assert.False(new ShellExportFileOpener(() => exportFolder).OpenFile(pdf));
        }
        finally
        {
            Directory.Delete(exportFolder, recursive: true);
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    public void OpenFile_RejectsAMissingFile()
    {
        var directory = TempDirectory();
        try
        {
            Assert.False(new ShellExportFileOpener(() => directory)
                .OpenFile(Path.Combine(directory, "missing.pdf")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void OpenFolder_RejectsAFolderOtherThanTheExportFolder()
    {
        var exportFolder = TempDirectory();
        var elsewhere = TempDirectory();
        try
        {
            Assert.False(new ShellExportFileOpener(() => exportFolder).OpenFolder(elsewhere));
            Assert.False(new ShellExportFileOpener(() => exportFolder).OpenFolder(Path.Combine(exportFolder, "missing")));
        }
        finally
        {
            Directory.Delete(exportFolder, recursive: true);
            Directory.Delete(elsewhere, recursive: true);
        }
    }
}
