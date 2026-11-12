using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace TrueSpec.App.Services;

/// <summary>
/// Opens files and folders through the OS shell (<see cref="ProcessStartInfo.UseShellExecute"/>):
/// Explorer for a folder, the registered application for a file. The
/// caller (MainViewModel) only ever passes paths it just wrote inside the
/// effective export folder, and this class never executes anything: a
/// report file must never end in an executable extension, and the shell
/// opens it with the application the user already registered for it.
/// </summary>
public sealed class ShellExportFileOpener : IExportFileOpener
{
    public bool OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return false;
        }

        return TryShellOpen(folderPath);
    }

    public bool OpenFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        return TryShellOpen(filePath);
    }

    private static bool TryShellOpen(string path)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException or NotSupportedException)
        {
            return false;
        }
    }
}
