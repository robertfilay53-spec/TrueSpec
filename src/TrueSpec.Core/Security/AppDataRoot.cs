namespace TrueSpec.Core.Security;

/// <summary>
/// The data root: the one folder all local state files live under
/// (settings.json, history.db, sensor-log.csv, startup.log,
/// window-state.json). The default is %LocalAppData%\TrueSpec, unchanged
/// from before. Portable mode puts the data next to the executable
/// instead: a single folder, no install, runs from a USB stick (the
/// "buyer kit" mode). Portable mode is selected by either of:
/// <list type="bullet">
/// <item>a marker file named <c>TrueSpec.portable</c> next to the exe, or</item>
/// <item>the environment variable <c>TRUESPEC_PORTABLE</c> set to 1 / true / yes / on.</item>
/// </list>
/// Either signal alone is enough; when both are absent the app behaves
/// exactly as before. When portable mode is requested but the exe folder
/// fails validation (a system folder, Program Files), the app falls back
/// to the default folder instead of writing somewhere unsafe: portable
/// mode must never be a way to smuggle state into a protected location.
/// The marker file is looked for next to the exe (AppContext.BaseDirectory),
/// never in the working directory: a stray file in the CWD must not flip
/// the mode. Validators that take a base folder (ValidateDatabasePath,
/// ValidateSensorLogPath) accept the portable root by passing
/// <see cref="Folder"/> as the base; <see cref="PathSafety.ValidateAppDataRoot"/>
/// validates the root itself.
/// </summary>
public sealed record AppDataRoot(string Folder, bool IsPortable)
{
    /// <summary>Marker file next to the exe that selects portable mode.</summary>
    public const string PortableMarkerFileName = "TrueSpec.portable";

    /// <summary>Environment variable that selects portable mode ("1", "true", "yes", "on").</summary>
    public const string PortableEnvironmentVariable = "TRUESPEC_PORTABLE";

    public string SettingsPath => Path.Combine(Folder, "settings.json");

    public string DatabasePath => Path.Combine(Folder, "history.db");

    public string SensorLogPath => Path.Combine(Folder, "sensor-log.csv");

    public string StartupLogPath => Path.Combine(Folder, "startup.log");

    public string WindowStatePath => Path.Combine(Folder, "window-state.json");

    /// <summary>The default root: %LocalAppData%\TrueSpec, as before.</summary>
    public static AppDataRoot Default() => new(PathSafety.DefaultAppDataFolder(), false);

    /// <summary>The portable root for an exe folder: the data lives next to the exe.</summary>
    public static AppDataRoot Portable(string exeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exeDirectory);

        var folder = Path.GetFullPath(exeDirectory);
        return new AppDataRoot(folder, true);
    }

    /// <summary>
    /// Resolves the root for this run: portable when the marker file sits
    /// next to the exe or the environment variable asks for it, default
    /// otherwise. The parameters are seams for tests: pass an explicit
    /// exe directory and env value instead of the real ones. A requested
    /// portable root that fails validation falls back to the default.
    /// </summary>
    public static AppDataRoot Resolve(string? exeDirectory = null, string? environmentValue = null)
    {
        var exeFolder = exeDirectory ?? AppContext.BaseDirectory;
        string fullExeFolder;
        try
        {
            fullExeFolder = Path.GetFullPath(exeFolder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Default();
        }

        var envValue = environmentValue ?? Environment.GetEnvironmentVariable(PortableEnvironmentVariable);
        var portableRequested = IsTruthy(envValue) ||
            File.Exists(Path.Combine(fullExeFolder, PortableMarkerFileName));

        if (!portableRequested)
        {
            return Default();
        }

        return PathSafety.ValidateAppDataRoot(fullExeFolder) is { } validated
            ? new AppDataRoot(validated, true)
            : Default();
    }

    private static bool IsTruthy(string? value) =>
        value is not null &&
        value.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
}
