namespace TrueSpec.Core.Security;

/// <summary>
/// Validates file system paths that come from settings or user choices before
/// the app reads or writes them. TrueSpec never follows a path that escapes the
/// folder the user picked, and never writes outside a real user folder.
/// </summary>
public static class PathSafety
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Validates a folder the user picked for report exports. Returns the
    /// resolved absolute path, or null when the folder is not safe to use.
    /// A safe folder is absolute, exists or can be created, is not a drive
    /// root or a Windows system folder, and contains no traversal that
    /// escapes it.
    /// </summary>
    public static string? ValidateExportFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(folder.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (!Path.IsPathRooted(fullPath))
        {
            return null;
        }

        // Never write to a drive root or a Windows system folder.
        var root = Path.GetPathRoot(fullPath);
        if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), root?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(windowsFolder) &&
            fullPath.StartsWith(windowsFolder, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!string.IsNullOrEmpty(systemFolder) &&
            fullPath.StartsWith(systemFolder, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles) &&
            fullPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return fullPath;
    }

    /// <summary>
    /// Validates a database file path. The file must live inside the given
    /// base folder (the app data folder) after resolving every ".." segment.
    /// </summary>
    public static string? ValidateDatabasePath(string? databasePath, string baseFolder)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(baseFolder))
        {
            return null;
        }

        string fullPath;
        string fullBase;
        try
        {
            fullPath = Path.GetFullPath(databasePath.Trim());
            fullBase = Path.GetFullPath(baseFolder.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (!fullPath.StartsWith(fullBase.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".db", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return fullPath;
    }

    /// <summary>
    /// Builds a safe file name for a report export. Strips directory
    /// separators and invalid characters, and never lets a caller inject a
    /// path through the file name.
    /// </summary>
    public static string SafeReportFileName(string prefix, string extension, DateTime timestamp)
    {
        var cleanPrefix = new string(prefix
            .Where(c => !InvalidFileNameChars.Contains(c) && c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar)
            .ToArray());
        if (string.IsNullOrWhiteSpace(cleanPrefix))
        {
            cleanPrefix = "TrueSpec-report";
        }

        var cleanExtension = extension.TrimStart('.');
        cleanExtension = new string(cleanExtension
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray());
        if (string.IsNullOrWhiteSpace(cleanExtension))
        {
            cleanExtension = "txt";
        }

        return $"{cleanPrefix}-{timestamp:yyyyMMdd-HHmmss}.{cleanExtension}";
    }

    /// <summary>
    /// The default folder for reports and local data: Documents/TrueSpec for
    /// reports, LocalApplicationData/TrueSpec for the snapshot database and
    /// settings. Falls back to the user profile when a special folder is
    /// unavailable.
    /// </summary>
    public static string DefaultExportFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents))
        {
            return Path.Combine(documents, "TrueSpec");
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profile, "TrueSpec");
    }

    public static string DefaultAppDataFolder()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            return Path.Combine(local, "TrueSpec");
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profile, "TrueSpec");
    }
}
