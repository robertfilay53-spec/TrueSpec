namespace TrueSpec.Core.Settings;

using System.Text.Json;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON under the app data
/// folder. A missing, corrupt or hand-edited file falls back to defaults
/// instead of crashing, and every loaded value is sanitized first.
/// </summary>
public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public AppSettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFilePath();
    }

    public static string DefaultFilePath() =>
        Path.Combine(Security.PathSafety.DefaultAppDataFolder(), "settings.json");

    public string FilePath => _filePath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return AppSettings.Default;
            }

            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings?.Sanitized() ?? AppSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return AppSettings.Default;
        }
    }

    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings.Sanitized(), JsonOptions);

            // Write to a temp file in the same folder, then move over the old
            // file, so a crash mid-write never leaves a half-written settings
            // file behind. The temp name is random per save: a fixed
            // "settings.json.tmp" is a predictable path a local process
            // could pre-plant (for example as a symlink) to redirect the
            // write, and a stale temp from a crashed run would be reused.
            var tempDirectory = string.IsNullOrEmpty(directory) ? "." : directory;
            var tempPath = Path.Combine(tempDirectory, $".truespec-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch
            {
                // A failed save must not leave a temp file behind.
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // Best effort only: the save already failed.
                }

                throw;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
