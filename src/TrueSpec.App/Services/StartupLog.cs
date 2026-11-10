using System;
using System.IO;
using TrueSpec.Core.Security;

namespace TrueSpec.App.Services;

/// <summary>
/// Appends timestamped startup milestones to %LocalAppData%\TrueSpec\startup.log
/// so a headless launch still leaves evidence of how far startup got.
/// Logging is best-effort: it never throws and never blocks startup.
/// </summary>
public static class StartupLog
{
    public static string LogPath => Path.Combine(PathSafety.DefaultAppDataFolder(), "startup.log");

    public static void Append(string message)
    {
        try
        {
            var folder = PathSafety.DefaultAppDataFolder();
            Directory.CreateDirectory(folder);
            File.AppendAllText(
                Path.Combine(folder, "startup.log"),
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never break startup.
        }
    }
}
