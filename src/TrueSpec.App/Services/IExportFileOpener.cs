namespace TrueSpec.App.Services;

/// <summary>
/// Opens an exported file or its folder in the operating system's shell
/// (Explorer on Windows). Kept behind an interface so the shell view model
/// can be tested without launching Explorer, and so the "Open folder" /
/// "Open file" buttons on the export confirmation never depend on the
/// windowing system directly.
/// </summary>
public interface IExportFileOpener
{
    /// <summary>Opens the folder in the shell. Returns false when the folder does not exist or could not be opened.</summary>
    bool OpenFolder(string folderPath);

    /// <summary>Opens the file with its default application. Returns false when the file does not exist or could not be opened.</summary>
    bool OpenFile(string filePath);
}
