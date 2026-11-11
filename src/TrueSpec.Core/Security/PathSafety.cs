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
    /// A safe folder is absolute (checked on the raw input, before any
    /// resolution), contains no ".." segment at all, is not a drive root
    /// or a Windows system folder. Export folders come from a folder
    /// picker, which never produces a relative path or a traversal segment,
    /// so rejecting them outright is safe and a ".." in a stored setting
    /// is a poisoning attempt.
    /// </summary>
    public static string? ValidateExportFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var trimmed = folder.Trim();

        // A relative path must never be resolved against the working
        // directory: Path.GetFullPath would silently turn "relative/folder"
        // into an absolute path under the CWD, which is exactly the escape
        // this validator exists to prevent.
        if (!Path.IsPathRooted(trimmed))
        {
            return null;
        }

        // Any ".." segment is rejected, even one that stays inside the
        // folder: a stored setting must never smuggle a traversal past the
        // rooted check, and no legitimate export folder contains one.
        if (trimmed.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            return null;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(trimmed);
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

        // The prefix check needs a separator boundary: "C:\WindowsUpdate"
        // is not inside "C:\Windows", and "C:\Program FilesXYZ" is not
        // inside "C:\Program Files" - a bare StartsWith would reject both
        // safe folders.
        if (IsWithin(Environment.GetFolderPath(Environment.SpecialFolder.Windows), fullPath) ||
            IsWithin(Environment.GetFolderPath(Environment.SpecialFolder.System), fullPath) ||
            IsWithin(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), fullPath) ||
            IsWithin(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), fullPath))
        {
            return null;
        }

        return fullPath;
    }

    /// <summary>
    /// True when <paramref name="fullPath"/> is the folder itself or a
    /// descendant of it. A sibling whose name merely starts with the folder
    /// name ("C:\WindowsUpdate" vs "C:\Windows") is not within it.
    /// </summary>
    private static bool IsWithin(string? folder, string fullPath)
    {
        if (string.IsNullOrEmpty(folder))
        {
            return false;
        }

        var trimmed = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, trimmed, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(trimmed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(trimmed + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
    /// path through the file name. Only the report formats the app actually
    /// produces (csv, json, html, pdf) are allowed as extensions: a report file
    /// must never end in an executable extension.
    /// </summary>
    public static string SafeReportFileName(string prefix, string extension, DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(extension);

        // Strip both separators on every platform: a backslash is a valid
        // file name character on Linux but a path separator on Windows, and
        // this file name is used on Windows. Also strip every character
        // Windows forbids in a file name (< > : " | ? * and controls):
        // Path.GetInvalidFileNameChars on Linux does not include them, so
        // a machine-name prefix containing ':' would otherwise produce a
        // file name Windows cannot create.
        var cleanPrefix = new string(prefix
            .Where(c => !InvalidFileNameChars.Contains(c) && !WindowsInvalidFileNameChars.Contains(c) && c != '/' && c != '\\' && !char.IsControl(c))
            .ToArray());
        // A ".." in the prefix is a traversal attempt, never a label.
        while (cleanPrefix.Contains("..", StringComparison.Ordinal))
        {
            cleanPrefix = cleanPrefix.Replace("..", string.Empty, StringComparison.Ordinal);
        }
        if (string.IsNullOrWhiteSpace(cleanPrefix))
        {
            cleanPrefix = "TrueSpec-report";
        }

        var cleanExtension = extension.Trim().TrimStart('.').ToLowerInvariant();
        if (!AllowedReportExtensions.Contains(cleanExtension, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported report extension '{extension}'.", nameof(extension));
        }

        return $"{cleanPrefix}-{timestamp:yyyyMMdd-HHmmss}.{cleanExtension}";
    }

    private static readonly string[] AllowedReportExtensions = { "csv", "json", "html", "pdf" };

    private static readonly char[] WindowsInvalidFileNameChars = { '<', '>', ':', '"', '|', '?', '*' };

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
