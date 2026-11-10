namespace TrueSpec.Core.Settings;

using System.Text.Json.Serialization;

/// <summary>
/// User settings. Everything here is local: theme, live refresh interval and
/// the report export folder. There is no account, no update check and no
/// network setting, because the app makes no network calls.
/// </summary>
public sealed record AppSettings
{
    public const int MinRefreshSeconds = 1;
    public const int MaxRefreshSeconds = 30;
    public const int DefaultRefreshSeconds = 2;

    [JsonPropertyName("isDarkTheme")]
    public bool IsDarkTheme { get; init; } = true;

    [JsonPropertyName("refreshIntervalSeconds")]
    public int RefreshIntervalSeconds { get; init; } = DefaultRefreshSeconds;

    /// <summary>Empty means the default Documents/TrueSpec folder.</summary>
    [JsonPropertyName("exportFolder")]
    public string ExportFolder { get; init; } = string.Empty;

    public static AppSettings Default { get; } = new();

    /// <summary>
    /// Returns a copy with every value clamped or validated into range.
    /// Called on load so a hand-edited or corrupt settings file can never
    /// produce a broken interval or an unsafe folder.
    /// </summary>
    public AppSettings Sanitized()
    {
        var refresh = Math.Clamp(RefreshIntervalSeconds, MinRefreshSeconds, MaxRefreshSeconds);
        var folder = Security.PathSafety.ValidateExportFolder(ExportFolder) ?? string.Empty;
        return this with { RefreshIntervalSeconds = refresh, ExportFolder = folder };
    }
}
