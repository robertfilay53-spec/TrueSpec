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
    // part of the translated text itself. The two strings v2.1.8 left
    // English are keys since v2.1.9 (see "Core diagnostic strings"
    // below): the plug-in list's per-item status/reason and the
    // portable-toggle result message now take a language code too.
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

    // Core diagnostic strings (v2.1.9): the strings the plug-in and
    // portable-mode code in Core produces, threaded with a language code
    // so the Settings page's plug-in list and portable-toggle message
    // render in the current language. They are diagnostic text about
    // files, folders and manifests the user placed or the app wrote —
    // never a measured value, a unit or a threshold. File names,
    // manifest field names ('id', 'contractVersion', 'assembly'),
    // collector ids and the TRUESPEC_PORTABLE variable name stay
    // untranslated inside the sentences; only the surrounding words
    // change. Templates carry the dynamic values ({0} = a version, a
    // count or a name), formatted with the invariant culture by
    // StringLocalizer.GetFormatted, so a number inside a German
    // sentence keeps the app's existing formatting.
    public const string PluginStatusReady = "PluginStatusReady";
    public const string PluginStatusNeedsNewer = "PluginStatusNeedsNewer";
    public const string PluginStatusOlderContract = "PluginStatusOlderContract";
    public const string PluginStatusManifestProblem = "PluginStatusManifestProblem";
    public const string PluginStatusAssemblyMissing = "PluginStatusAssemblyMissing";
    public const string PluginStatusUnknown = "PluginStatusUnknown";
    public const string PluginReasonNoManifest = "PluginReasonNoManifest";
    public const string PluginReasonManifestUnreadable = "PluginReasonManifestUnreadable";
    public const string PluginReasonContractVersion = "PluginReasonContractVersion";
    public const string PluginReasonAssemblyMissing = "PluginReasonAssemblyMissing";
    public const string PluginReasonReady = "PluginReasonReady";
    public const string PluginErrorFolderUnreadable = "PluginErrorFolderUnreadable";
    public const string PluginErrorTooMany = "PluginErrorTooMany";
    public const string ManifestTooLarge = "ManifestTooLarge";
    public const string ManifestNotObject = "ManifestNotObject";
    public const string ManifestUnknownField = "ManifestUnknownField";
    public const string ManifestMissingId = "ManifestMissingId";
    public const string ManifestMissingName = "ManifestMissingName";
    public const string ManifestMissingVersion = "ManifestMissingVersion";
    public const string ManifestMissingContractVersion = "ManifestMissingContractVersion";
    public const string ManifestMissingAssembly = "ManifestMissingAssembly";
    public const string ManifestAssemblyPath = "ManifestAssemblyPath";
    public const string ManifestAssemblyNotFile = "ManifestAssemblyNotFile";
    public const string ManifestAssemblyInvalidName = "ManifestAssemblyInvalidName";
    public const string ManifestAssemblyNotDll = "ManifestAssemblyNotDll";
    public const string ManifestCollectorNotObject = "ManifestCollectorNotObject";
    public const string ManifestCollectorUnknownField = "ManifestCollectorUnknownField";
    public const string ManifestCollectorMissingFields = "ManifestCollectorMissingFields";
    public const string ManifestMissingCollectors = "ManifestMissingCollectors";
    public const string ManifestNoCollectors = "ManifestNoCollectors";
    public const string ManifestTooManyCollectors = "ManifestTooManyCollectors";
    public const string ManifestCollectorBadId = "ManifestCollectorBadId";
    public const string ManifestCollectorDuplicateId = "ManifestCollectorDuplicateId";
    public const string ManifestInvalidJson = "ManifestInvalidJson";
    public const string ManifestUnreadable = "ManifestUnreadable";
    public const string PortableToggleFolderUnusable = "PortableToggleFolderUnusable";
    public const string PortableToggleProtectedFolder = "PortableToggleProtectedFolder";
    public const string PortableToggleAlreadyOn = "PortableToggleAlreadyOn";
    public const string PortableToggleEnableFailed = "PortableToggleEnableFailed";
    public const string PortableToggleEnabled = "PortableToggleEnabled";
    public const string PortableToggleNoMarkerEnv = "PortableToggleNoMarkerEnv";
    public const string PortableToggleAlreadyOff = "PortableToggleAlreadyOff";
    public const string PortableToggleDisableFailed = "PortableToggleDisableFailed";
    public const string PortableToggleMarkerRemovedEnv = "PortableToggleMarkerRemovedEnv";
    public const string PortableToggleDisabled = "PortableToggleDisabled";

    // Component detail pages (v2.1.10): the chrome of the shared
    // detail view (ComponentDetailView), which every one of the 15
    // component pages uses — one surface, all detail pages at once.
    // The data the page shows stays English in this version: component
    // names, headlines, health and status texts, spec labels, values
    // and sources, and the "What this means" text are measured data
    // produced by the collectors, not page chrome; translating them is
    // a later slice. The "(ESTIMATE)" marker on a value is page chrome
    // (an annotation the view adds), so it is a key: the value itself
    // never changes, only the annotation. Templates carry the dynamic
    // values ({0} = a captured date, a search string, or a count),
    // formatted with the invariant culture by
    // StringLocalizer.GetFormatted, so a number or date inside a German
    // sentence keeps the app's existing formatting.
    public const string DetailSpecificationsTitle = "DetailSpecificationsTitle";
    public const string DetailSearchPlaceholder = "DetailSearchPlaceholder";
    public const string DetailCopyHint = "DetailCopyHint";
    public const string DetailAgeSignalsTitle = "DetailAgeSignalsTitle";
    public const string DetailAgeSignalsIntro = "DetailAgeSignalsIntro";
    public const string DetailWhatThisMeansTitle = "DetailWhatThisMeansTitle";
    public const string DetailNotCollectedHeadline = "DetailNotCollectedHeadline";
    public const string DetailNotCollectedText = "DetailNotCollectedText";
    public const string DetailCaptured = "DetailCaptured";
    public const string DetailNoSpecs = "DetailNoSpecs";
    public const string DetailNoSpecsMatch = "DetailNoSpecsMatch";
    public const string DetailAgeSignalsSkipped = "DetailAgeSignalsSkipped";
    public const string DetailSignalUntrusted = "DetailSignalUntrusted";
    public const string DetailEstimateSuffix = "DetailEstimateSuffix";

    // Overview page (v2.1.11): the chrome of the landing page — the
    // first screen a user sees. The page is long-lived and updated in
    // place after every scan, so the strings resolve from the stored
    // language code and re-render through ApplyLanguage, the same
    // in-place pattern the detail pages use. The data the page shows
    // stays English in this version: component names, headlines,
    // health and status texts, spec labels, values and sources, the
    // score explanation and the verdict text are measured data
    // produced by the collectors and the scoring engine, not page
    // chrome; translating them is a later slice. The admin notice text
    // and the threshold-alert texts are diagnostic strings produced by
    // Core (AdminElevationNotice, ThresholdAlertEvaluator) with no
    // language parameter yet; they stay English here and follow in a
    // later "Core strings" slice, the same pattern v2.1.8 left and
    // v2.1.9 closed for the Settings page. Templates carry the dynamic
    // values ({0} = a file path, a threshold, a duration, a time, a
    // weight or a points value), formatted with the invariant culture
    // by StringLocalizer.GetFormatted, so a number inside a German
    // sentence keeps the app's existing formatting. The refresh
    // interval reuses the Settings seconds keys (SecondsOne /
    // SecondsMany) instead of a second copy of the same words.
    public const string OverviewAdminEyebrow = "OverviewAdminEyebrow";
    public const string OverviewOutOf100 = "OverviewOutOf100";
    public const string OverviewOverallHealth = "OverviewOverallHealth";
    public const string OverviewScoreNote = "OverviewScoreNote";
    public const string OverviewNewLaptopCheck = "OverviewNewLaptopCheck";
    public const string OverviewExportReport = "OverviewExportReport";
    public const string OverviewComponentHealth = "OverviewComponentHealth";
    public const string OverviewHealthLabel = "OverviewHealthLabel";
    public const string OverviewDetails = "OverviewDetails";
    public const string OverviewNoComponents = "OverviewNoComponents";
    public const string OverviewLiveSensors = "OverviewLiveSensors";
    public const string OverviewNoSensorReading = "OverviewNoSensorReading";
    public const string OverviewAlertEyebrow = "OverviewAlertEyebrow";
    public const string OverviewAlertNote = "OverviewAlertNote";
    public const string OverviewFooter = "OverviewFooter";
    public const string OverviewScanning = "OverviewScanning";
    public const string OverviewScanningNote = "OverviewScanningNote";
    public const string OverviewSensorLogUnavailable = "OverviewSensorLogUnavailable";
    public const string OverviewSensorLogOn = "OverviewSensorLogOn";
    public const string OverviewSensorLogOff = "OverviewSensorLogOff";
    public const string OverviewAlertSet = "OverviewAlertSet";
    public const string OverviewAlertNotSet = "OverviewAlertNotSet";
    public const string OverviewRefreshText = "OverviewRefreshText";
    public const string OverviewLiveUpdated = "OverviewLiveUpdated";
    public const string OverviewLiveReadFailed = "OverviewLiveReadFailed";
    public const string OverviewWeight = "OverviewWeight";
    public const string OverviewContribution = "OverviewContribution";
    public const string OverviewKeyCpuTemp = "OverviewKeyCpuTemp";
    public const string OverviewKeyMemory = "OverviewKeyMemory";
    public const string OverviewKeyDiskHealth = "OverviewKeyDiskHealth";
    public const string OverviewKeyBatteryHealth = "OverviewKeyBatteryHealth";
    public const string OverviewMetricCpuTemp = "OverviewMetricCpuTemp";
    public const string OverviewMetricGpuTemp = "OverviewMetricGpuTemp";
    public const string OverviewMetricCpuUsage = "OverviewMetricCpuUsage";
    public const string OverviewMetricBatteryPower = "OverviewMetricBatteryPower";
    public const string OverviewMetricNotAvailable = "OverviewMetricNotAvailable";

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
        PluginStatusReady,
        PluginStatusNeedsNewer,
        PluginStatusOlderContract,
        PluginStatusManifestProblem,
        PluginStatusAssemblyMissing,
        PluginStatusUnknown,
        PluginReasonNoManifest,
        PluginReasonManifestUnreadable,
        PluginReasonContractVersion,
        PluginReasonAssemblyMissing,
        PluginReasonReady,
        PluginErrorFolderUnreadable,
        PluginErrorTooMany,
        ManifestTooLarge,
        ManifestNotObject,
        ManifestUnknownField,
        ManifestMissingId,
        ManifestMissingName,
        ManifestMissingVersion,
        ManifestMissingContractVersion,
        ManifestMissingAssembly,
        ManifestAssemblyPath,
        ManifestAssemblyNotFile,
        ManifestAssemblyInvalidName,
        ManifestAssemblyNotDll,
        ManifestCollectorNotObject,
        ManifestCollectorUnknownField,
        ManifestCollectorMissingFields,
        ManifestMissingCollectors,
        ManifestNoCollectors,
        ManifestTooManyCollectors,
        ManifestCollectorBadId,
        ManifestCollectorDuplicateId,
        ManifestInvalidJson,
        ManifestUnreadable,
        PortableToggleFolderUnusable,
        PortableToggleProtectedFolder,
        PortableToggleAlreadyOn,
        PortableToggleEnableFailed,
        PortableToggleEnabled,
        PortableToggleNoMarkerEnv,
        PortableToggleAlreadyOff,
        PortableToggleDisableFailed,
        PortableToggleMarkerRemovedEnv,
        PortableToggleDisabled,
        DetailSpecificationsTitle,
        DetailSearchPlaceholder,
        DetailCopyHint,
        DetailAgeSignalsTitle,
        DetailAgeSignalsIntro,
        DetailWhatThisMeansTitle,
        DetailNotCollectedHeadline,
        DetailNotCollectedText,
        DetailCaptured,
        DetailNoSpecs,
        DetailNoSpecsMatch,
        DetailAgeSignalsSkipped,
        DetailSignalUntrusted,
        DetailEstimateSuffix,
        OverviewAdminEyebrow,
        OverviewOutOf100,
        OverviewOverallHealth,
        OverviewScoreNote,
        OverviewNewLaptopCheck,
        OverviewExportReport,
        OverviewComponentHealth,
        OverviewHealthLabel,
        OverviewDetails,
        OverviewNoComponents,
        OverviewLiveSensors,
        OverviewNoSensorReading,
        OverviewAlertEyebrow,
        OverviewAlertNote,
        OverviewFooter,
        OverviewScanning,
        OverviewScanningNote,
        OverviewSensorLogUnavailable,
        OverviewSensorLogOn,
        OverviewSensorLogOff,
        OverviewAlertSet,
        OverviewAlertNotSet,
        OverviewRefreshText,
        OverviewLiveUpdated,
        OverviewLiveReadFailed,
        OverviewWeight,
        OverviewContribution,
        OverviewKeyCpuTemp,
        OverviewKeyMemory,
        OverviewKeyDiskHealth,
        OverviewKeyBatteryHealth,
        OverviewMetricCpuTemp,
        OverviewMetricGpuTemp,
        OverviewMetricCpuUsage,
        OverviewMetricBatteryPower,
        OverviewMetricNotAvailable,
    };
}
