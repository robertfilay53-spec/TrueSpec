using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using TrueSpec.Core.Security;

namespace TrueSpec.App.Services;

/// <summary>
/// Opens files and folders through the OS shell (<see cref="ProcessStartInfo.UseShellExecute"/>):
/// Explorer for a folder, the registered application for a file. The
/// caller (MainViewModel) only ever passes paths it just wrote inside the
/// effective export folder, and this class never executes anything: a
/// report file must never end in an executable extension, and the shell
/// opens it with the application the user already registered for it.
/// <para>
/// Defence in depth, inside the opener itself: when an export-folder
/// provider is supplied, <see cref="OpenFile"/> only opens a file directly
/// inside that folder whose extension is a report extension (csv, json,
/// html, pdf), and <see cref="OpenFolder"/> only opens that folder. A
/// caller that passes anything else gets false, never a shell launch.
/// Without a provider (tests that construct the opener bare) only the
/// existence checks apply.
/// </para>
/// </summary>
public sealed class ShellExportFileOpener : IExportFileOpener
{
    private readonly Func<string>? _exportFolderProvider;

    /// <param name="exportFolderProvider">The effective export folder, when the opener should enforce it.</param>
    public ShellExportFileOpener(Func<string>? exportFolderProvider = null)
    {
        _exportFolderProvider = exportFolderProvider;
    }

    public bool OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return false;
        }

        if (_exportFolderProvider is not null &&
            !string.Equals(
                Path.GetFullPath(folderPath),
                Path.GetFullPath(_exportFolderProvider()),
                StringComparison.OrdinalIgnoreCase))
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

        if (!PathSafety.IsAllowedReportExtension(filePath))
        {
            return false;
        }

        if (_exportFolderProvider is not null)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.Equals(folder, Path.GetFullPath(_exportFolderProvider()), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
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
