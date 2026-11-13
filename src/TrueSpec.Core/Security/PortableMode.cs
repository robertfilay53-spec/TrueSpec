namespace TrueSpec.Core.Security;

using TrueSpec.Core.Localization;

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

/// <summary>
/// The outcome of flipping the portable-mode marker. The message is
/// carried as a string key plus its arguments, not as a formatted
/// string, so the caller can re-resolve it in another language: the
/// Settings page shows the message in the language that was current
/// when the toggle ran, and re-resolves it when the language changes
/// (a <c>with { LanguageCode = ... }</c> copy). <see cref="Message"/>
/// resolves the key for <see cref="LanguageCode"/> (English when null),
/// formatting numbers with the invariant culture like every other
/// localized template. No message is ever produced in two languages
/// at once: the key is the single source of truth.
/// </summary>
public sealed record PortableModeToggleResult(
    /// <summary>True when the marker operation did what was asked.</summary>
    bool Success,
    /// <summary>The effective portable state after the operation.</summary>
    bool IsPortable,
    /// <summary>The catalogue key of the outcome message.</summary>
    string MessageKey,
    /// <summary>Values for the message template's placeholders.</summary>
    object?[] MessageArgs,
    /// <summary>The language the message resolves in; null reads English.</summary>
    string? LanguageCode = null)
{
    /// <summary>Plain-English outcome, including what to do next, in the result's language.</summary>
    public string Message => StringLocalizer.GetFormatted(MessageKey, LanguageCode, MessageArgs);
}

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
    /// after the marker is gone. The outcome message comes back in the
    /// requested language (<paramref name="languageCode"/>, English
    /// when null or unknown).
    /// </summary>
    public static PortableModeToggleResult SetEnabled(string exeDirectory, bool enable, string? environmentValue = null, string? languageCode = null)
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
                AppStringKeys.PortableToggleFolderUnusable, Array.Empty<object?>(), languageCode);
        }

        var markerPath = Path.Combine(fullExeFolder, AppDataRoot.PortableMarkerFileName);

        if (enable)
        {
            if (PathSafety.ValidateAppDataRoot(fullExeFolder) is null)
            {
                return new PortableModeToggleResult(false, false,
                    AppStringKeys.PortableToggleProtectedFolder, Array.Empty<object?>(), languageCode);
            }

            if (File.Exists(markerPath))
            {
                return new PortableModeToggleResult(true, true,
                    AppStringKeys.PortableToggleAlreadyOn, Array.Empty<object?>(), languageCode);
            }

            try
            {
                File.WriteAllText(markerPath, string.Empty);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new PortableModeToggleResult(false, false,
                    AppStringKeys.PortableToggleEnableFailed, Array.Empty<object?>(), languageCode);
            }

            return new PortableModeToggleResult(true, true,
                AppStringKeys.PortableToggleEnabled, Array.Empty<object?>(), languageCode);
        }

        if (!File.Exists(markerPath))
        {
            if (AppDataRoot.EnvRequestsPortable(environmentValue))
            {
                return new PortableModeToggleResult(true, true,
                    AppStringKeys.PortableToggleNoMarkerEnv, Array.Empty<object?>(), languageCode);
            }

            return new PortableModeToggleResult(true, false, AppStringKeys.PortableToggleAlreadyOff, Array.Empty<object?>(), languageCode);
        }

        try
        {
            File.Delete(markerPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new PortableModeToggleResult(false, true,
                AppStringKeys.PortableToggleDisableFailed, Array.Empty<object?>(), languageCode);
        }

        if (AppDataRoot.EnvRequestsPortable(environmentValue))
        {
            return new PortableModeToggleResult(true, true,
                AppStringKeys.PortableToggleMarkerRemovedEnv, Array.Empty<object?>(), languageCode);
        }

        return new PortableModeToggleResult(true, false,
            AppStringKeys.PortableToggleDisabled, Array.Empty<object?>(), languageCode);
    }
}
