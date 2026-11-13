using System;
using System.IO;
using TrueSpec.Core.Security;

namespace TrueSpec.App.Services;

/// <summary>
/// Appends timestamped startup milestones to the data root's startup.log
/// (%LocalAppData%\TrueSpec by default, next to the exe in portable mode)
/// so a headless launch still leaves evidence of how far startup got.
/// Logging is best-effort: it never throws and never blocks startup.
/// </summary>
public static class StartupLog
{
    // One lock for every writer: scan threads append concurrently, and
    // File.AppendAllText without a lock can interleave or lose lines.
    private static readonly object Gate = new();

    // The log is a diagnostic aid, not an archive: past this size it is
    // restarted with a rotation note instead of growing without bound.
    private const long MaxBytes = 256 * 1024;

    public static string LogPath => AppDataRoot.Resolve().StartupLogPath;

    public static void Append(string message)
    {
        try
        {
            lock (Gate)
            {
                var root = AppDataRoot.Resolve();
                Directory.CreateDirectory(root.Folder);
                var path = root.StartupLogPath;
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    File.WriteAllText(
                        path,
                        $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} Startup log rotated (over 256 KB).{Environment.NewLine}");
                }

                File.AppendAllText(
                    path,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never break startup.
        }
    }
}
