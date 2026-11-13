namespace TrueSpec.Core.Localization;

/// <summary>
/// The string-key catalogue for localisation. Every user-facing string
/// that TrueSpec translates is named here exactly once; a language pack
/// that misses a key, or carries a key that is not in this catalogue,
/// fails validation (<see cref="LanguagePack.Validate"/>). Keys are
/// stable identifiers: the English text is the fallback value, never
/// the key, so renaming a key is a deliberate, test-visible change.
/// Measured values, units, numbers, dates and thresholds are never
/// keys: localisation translates chrome only, never a measurement.
/// </summary>
public static class AppStringKeys
{
    // Navigation rail (22 items, in rail order). These are pure chrome:
    // no measured value, no unit, no threshold appears in any label.
    public const string NavOverview = "NavOverview";
    public const string NavSystem = "NavSystem";
    public const string NavCpu = "NavCpu";
    public const string NavRam = "NavRam";
    public const string NavStorage = "NavStorage";
    public const string NavBattery = "NavBattery";
    public const string NavGpu = "NavGpu";
    public const string NavDisplay = "NavDisplay";
    public const string NavNetwork = "NavNetwork";
    public const string NavMotherboard = "NavMotherboard";
    public const string NavSecurity = "NavSecurity";
    public const string NavDrivers = "NavDrivers";
    public const string NavUpdates = "NavUpdates";
    public const string NavStartup = "NavStartup";
    public const string NavCrash = "NavCrash";
    public const string NavHistory = "NavHistory";
    public const string NavSoftware = "NavSoftware";
    public const string NavTests = "NavTests";
    public const string NavReport = "NavReport";
    public const string NavCompare = "NavCompare";
    public const string NavFleet = "NavFleet";
    public const string NavSettings = "NavSettings";

    // Settings > Language card. The card is the picker's own chrome, so
    // its strings are keys too: picking a language switches the card
    // itself, which is the only way the user can see the choice worked.
    public const string LanguageTitle = "LanguageTitle";
    public const string LanguageDescription = "LanguageDescription";
    public const string LanguageNote = "LanguageNote";

    // Settings page (v2.1.8): every string the page owns, in page order.
    // The page is pure chrome — no measured value, no unit, no threshold
    // appears as a literal; numbers the page shows (threshold, durations,
    // counts, folder paths) are values formatted into a template, never
    // part of the translated text itself. Two strings on the page are
    // deliberately NOT keys: the plug-in list's per-item status/reason
    // and the portable-toggle result message are diagnostic strings
    // produced by Core (PluginDiscovery, PortableMode), which take no
    // language parameter; they stay English until a later "Core strings"
    // slice threads a language code through those APIs.
    public const string SettingsEyebrow = "SettingsEyebrow";
    public const string SettingsTitle = "SettingsTitle";
    public const string SettingsIntro = "SettingsIntro";
    public const string AppearanceTitle = "AppearanceTitle";
    public const string AppearanceNote = "AppearanceNote";
    public const string ThemeDark = "ThemeDark";
    public const string ThemeLight = "ThemeLight";
    public const string ThemeDarkDefault = "ThemeDarkDefault";
    public const string RefreshTitle = "RefreshTitle";
    public const string RefreshNote = "RefreshNote";
    public const string SecondsOne = "SecondsOne";
    public const string SecondsMany = "SecondsMany";
    public const string LoggingTitle = "LoggingTitle";
    public const string LoggingIntro = "LoggingIntro";
    public const string RecordTitle = "RecordTitle";
    public const string RecordNote = "RecordNote";
    public const string ToggleOn = "ToggleOn";
    public const string ToggleOff = "ToggleOff";
    public const string AlertTitle = "AlertTitle";
    public const string AlertSummaryOn = "AlertSummaryOn";
    public const string AlertSummaryOff = "AlertSummaryOff";
    public const string AlertNote = "AlertNote";
    public const string AlertAboveLabel = "AlertAboveLabel";
    public const string AlertDurationLabel = "AlertDurationLabel";
    public const string ThresholdMessage = "ThresholdMessage";
    public const string ExportTitle = "ExportTitle";
    public const string ExportDefaultSuffix = "ExportDefaultSuffix";
    public const string ExportChoose = "ExportChoose";
    public const string ExportReset = "ExportReset";
    public const string ExportFolderRejected = "ExportFolderRejected";
    public const string ExportNote = "ExportNote";
    public const string DataLocationTitle = "DataLocationTitle";
    public const string PortableSummaryOnMarkerEnv = "PortableSummaryOnMarkerEnv";
    public const string PortableSummaryOnMarker = "PortableSummaryOnMarker";
    public const string PortableSummaryOnEnv = "PortableSummaryOnEnv";
    public const string PortableSummaryMarkerBlocked = "PortableSummaryMarkerBlocked";
    public const string PortableSummaryOff = "PortableSummaryOff";
    public const string PortableActionOn = "PortableActionOn";
    public const string PortableActionOff = "PortableActionOff";
    public const string DataLocationNote = "DataLocationNote";
    public const string PluginsTitle = "PluginsTitle";
    public const string PluginsIntro = "PluginsIntro";
    public const string PluginsSummaryNone = "PluginsSummaryNone";
    public const string PluginsSummaryOne = "PluginsSummaryOne";
    public const string PluginsSummaryMany = "PluginsSummaryMany";
    public const string PluginsRefresh = "PluginsRefresh";
    public const string PluginsNotLoadedNote = "PluginsNotLoadedNote";
    public const string PrivacyTitle = "PrivacyTitle";
    public const string PrivacyText = "PrivacyText";

    /// <summary>Every key in the catalogue, in the order above.</summary>
    public static IReadOnlyList<string> All { get; } = new[]
    {
        NavOverview,
        NavSystem,
        NavCpu,
        NavRam,
        NavStorage,
        NavBattery,
        NavGpu,
        NavDisplay,
        NavNetwork,
        NavMotherboard,
        NavSecurity,
        NavDrivers,
        NavUpdates,
        NavStartup,
        NavCrash,
        NavHistory,
        NavSoftware,
        NavTests,
        NavReport,
        NavCompare,
        NavFleet,
        NavSettings,
        LanguageTitle,
        LanguageDescription,
        LanguageNote,
        SettingsEyebrow,
        SettingsTitle,
        SettingsIntro,
        AppearanceTitle,
        AppearanceNote,
        ThemeDark,
        ThemeLight,
        ThemeDarkDefault,
        RefreshTitle,
        RefreshNote,
        SecondsOne,
        SecondsMany,
        LoggingTitle,
        LoggingIntro,
        RecordTitle,
        RecordNote,
        ToggleOn,
        ToggleOff,
        AlertTitle,
        AlertSummaryOn,
        AlertSummaryOff,
        AlertNote,
        AlertAboveLabel,
        AlertDurationLabel,
        ThresholdMessage,
        ExportTitle,
        ExportDefaultSuffix,
        ExportChoose,
        ExportReset,
        ExportFolderRejected,
        ExportNote,
        DataLocationTitle,
        PortableSummaryOnMarkerEnv,
        PortableSummaryOnMarker,
        PortableSummaryOnEnv,
        PortableSummaryMarkerBlocked,
        PortableSummaryOff,
        PortableActionOn,
        PortableActionOff,
        DataLocationNote,
        PluginsTitle,
        PluginsIntro,
        PluginsSummaryNone,
        PluginsSummaryOne,
        PluginsSummaryMany,
        PluginsRefresh,
        PluginsNotLoadedNote,
        PrivacyTitle,
        PrivacyText,
    };
}
