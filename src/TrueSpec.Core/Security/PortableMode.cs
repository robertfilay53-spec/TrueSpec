namespace TrueSpec.Core.Security;

/// <summary>The effective portable-mode state for one exe folder.</summary>
public sealed record PortableModeStatus(
    /// <summary>True when this run keeps its data next to the exe.</summary>
    bool IsPortable,
    /// <summary>True when a <c>TrueSpec.portable</c> marker file sits next to the exe.</summary>
    bool MarkerPresent,
    /// <summary>True when the <c>TRUESPEC_PORTABLE</c> environment variable asks for portable mode.</summary>
    bool EnvRequested,
    /// <summary>The folder this run actually keeps its data in.</summary>
    string DataFolder,
    /// <summary>The exe folder the status was computed for.</summary>
    string ExeFolder);

/// <summary>The outcome of flipping the portable-mode marker.</summary>
public sealed record PortableModeToggleResult(
    /// <summary>True when the marker operation did what was asked.</summary>
    bool Success,
    /// <summary>The effective portable state after the operation.</summary>
    bool IsPortable,
    /// <summary>Plain-English outcome, including what to do next.</summary>
    string Message);

/// <summary>
/// Portable mode, switched from inside the app (v2.1.1). The released
/// portable zip ships only the exe, so a buyer who extracts it to a USB
/// stick runs with installed-app behaviour until something creates the
/// marker: this is that something. The Settings page calls it; the marker
/// file is the only thing it touches, next to the exe, never in the
/// working directory. Enabling is refused for an exe folder portable mode
/// would fall back from (a system or protected folder): creating a marker
/// that does nothing would be a lie. The mode only takes effect after a
/// restart, because the data root is resolved once at startup; scans
/// already saved stay in the folder they were saved in.
/// </summary>
public static class PortableMode
{
    /// <summary>
    /// The portable state for an exe folder. The parameters are seams
    /// for tests: pass an explicit exe directory and env value instead
    /// of the real ones. An unusable exe folder reports the default root.
    /// </summary>
    public static PortableModeStatus GetStatus(string? exeDirectory = null, string? environmentValue = null)
    {
        var exeFolder = exeDirectory ?? AppContext.BaseDirectory;
        string fullExeFolder;
        try
        {
            fullExeFolder = Path.GetFullPath(exeFolder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            var fallback = AppDataRoot.Default();
            return new PortableModeStatus(false, false, false, fallback.Folder, exeFolder);
        }

        var markerPresent = File.Exists(Path.Combine(fullExeFolder, AppDataRoot.PortableMarkerFileName));
        var envRequested = AppDataRoot.EnvRequestsPortable(environmentValue);
        var root = AppDataRoot.Resolve(exeDirectory: fullExeFolder, environmentValue: environmentValue);
        return new PortableModeStatus(root.IsPortable, markerPresent, envRequested, root.Folder, fullExeFolder);
    }

    /// <summary>
    /// Creates the marker (enable) or removes it (disable) next to the
    /// exe. <paramref name="environmentValue"/> is a seam for tests:
    /// pass an explicit value instead of reading the real environment.
    /// The returned <see cref="PortableModeToggleResult.IsPortable"/> is
    /// the effective state after the operation, re-resolved, so it stays
    /// honest when the environment variable still requests portable mode
    /// after the marker is gone.
    /// </summary>
    public static PortableModeToggleResult SetEnabled(string exeDirectory, bool enable, string? environmentValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exeDirectory);

        string fullExeFolder;
        try
        {
            fullExeFolder = Path.GetFullPath(exeDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new PortableModeToggleResult(false, false,
                "Portable mode could not be changed: that app folder could not be used.");
        }

        var markerPath = Path.Combine(fullExeFolder, AppDataRoot.PortableMarkerFileName);

        if (enable)
        {
            if (PathSafety.ValidateAppDataRoot(fullExeFolder) is null)
            {
                return new PortableModeToggleResult(false, false,
                    "Portable mode cannot be enabled here: this app folder is a system or protected folder, so the app would keep its data in the default folder anyway.");
            }

            if (File.Exists(markerPath))
            {
                return new PortableModeToggleResult(true, true,
                    "Portable mode is already on. Restart TrueSpec for it to take effect.");
            }

            try
            {
                File.WriteAllText(markerPath, string.Empty);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new PortableModeToggleResult(false, false,
                    "Portable mode could not be enabled: the app folder could not be written to.");
            }

            return new PortableModeToggleResult(true, true,
                "Portable mode is on. Restart TrueSpec: from then on, settings, history and the sensor log are kept next to the app instead of in the default folder. Scans already saved stay in the old folder.");
        }

        if (!File.Exists(markerPath))
        {
            if (AppDataRoot.EnvRequestsPortable(environmentValue))
            {
                return new PortableModeToggleResult(true, true,
                    "There is no marker file to remove: portable mode is on through the TRUESPEC_PORTABLE environment variable. Unset the variable to turn it off.");
            }

            return new PortableModeToggleResult(true, false, "Portable mode is already off.");
        }

        try
        {
            File.Delete(markerPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new PortableModeToggleResult(false, true,
                "Portable mode could not be disabled: the marker file could not be removed.");
        }

        if (AppDataRoot.EnvRequestsPortable(environmentValue))
        {
            return new PortableModeToggleResult(true, true,
                "The marker file was removed, but the TRUESPEC_PORTABLE environment variable still requests portable mode, so it stays on until that is unset.");
        }

        return new PortableModeToggleResult(true, false,
            "Portable mode is off. Restart TrueSpec: from then on, data is kept in the default folder. Scans already saved next to the app stay there.");
    }
}
