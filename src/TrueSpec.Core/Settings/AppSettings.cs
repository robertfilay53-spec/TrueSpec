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

    /// <summary>CPU temperature alert thresholds are user-set only; 30 C is the lowest sensible value.</summary>
    public const double MinCpuTemperatureThresholdC = 30;

    public const double MaxCpuTemperatureThresholdC = 120;

    /// <summary>A crossing must persist this long before it alerts, so spikes never alert.</summary>
    public const int MinCpuTemperatureThresholdSeconds = 5;

    public const int MaxCpuTemperatureThresholdSeconds = 300;

    public const int DefaultCpuTemperatureThresholdSeconds = 30;

    [JsonPropertyName("isDarkTheme")]
    public bool IsDarkTheme { get; init; } = true;

    [JsonPropertyName("refreshIntervalSeconds")]
    public int RefreshIntervalSeconds { get; init; } = DefaultRefreshSeconds;

    /// <summary>Empty means the default Documents/TrueSpec folder.</summary>
    [JsonPropertyName("exportFolder")]
    public string ExportFolder { get; init; } = string.Empty;

    /// <summary>Whether live sensor readings are recorded to the local CSV log while the app runs.</summary>
    [JsonPropertyName("sensorLoggingEnabled")]
    public bool SensorLoggingEnabled { get; init; } = true;

    /// <summary>
    /// CPU temperature alert threshold in C, or null when alerting is off.
    /// Null is the default: TrueSpec ships with no threshold that implies a
    /// diagnosis; the user sets one.
    /// </summary>
    [JsonPropertyName("cpuTemperatureThresholdC")]
    public double? CpuTemperatureThresholdC { get; init; }

    /// <summary>How long the CPU temperature must stay at or above the threshold before alerting.</summary>
    [JsonPropertyName("cpuTemperatureThresholdSeconds")]
    public int CpuTemperatureThresholdSeconds { get; init; } = DefaultCpuTemperatureThresholdSeconds;

    /// <summary>
    /// Schema version of this file. Version 1 is the first versioned
    /// schema; older files have no field and load as version 1. A future
    /// version can migrate on load instead of guessing what unknown fields
    /// mean. Unknown future versions are normalized to the current version
    /// by <see cref="Sanitized"/>: only known fields are ever read, so an
    /// unknown version number never changes behaviour.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

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
        var threshold = CpuTemperatureThresholdC is null || double.IsNaN(CpuTemperatureThresholdC.Value) || double.IsInfinity(CpuTemperatureThresholdC.Value)
            ? (double?)null
            : Math.Clamp(CpuTemperatureThresholdC.Value, MinCpuTemperatureThresholdC, MaxCpuTemperatureThresholdC);
        var duration = Math.Clamp(CpuTemperatureThresholdSeconds, MinCpuTemperatureThresholdSeconds, MaxCpuTemperatureThresholdSeconds);
        return this with
        {
            RefreshIntervalSeconds = refresh,
            ExportFolder = folder,
            CpuTemperatureThresholdC = threshold,
            CpuTemperatureThresholdSeconds = duration,
            SchemaVersion = CurrentSchemaVersion,
        };
    }
}
