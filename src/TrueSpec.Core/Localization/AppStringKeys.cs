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

    // History page (v2.1.12): the chrome of the History page — the
    // trend cards, the saved-scans table, the two-machine comparison
    // and the value comparison, plus the CPU temperature log. The page
    // is long-lived and cached by the shell, so the strings resolve
    // from the stored language code and re-render through
    // ApplyLanguage, the same in-place pattern the Overview page
    // uses. The data the page shows stays English in this version:
    // machine names, the trend summary and the battery / storage /
    // driver-age forecast texts are measured or Core-owned wordings
    // produced by the trend analyzer with no language parameter yet;
    // the value verdict sentence and its no-verdict reason are
    // Core-owned wordings produced by ValueComparer; and the table's
    // cell fallbacks ("Not measurable" / "Not reported") are
    // measured-data fallbacks that the CSV export agrees with row for
    // row, so the CSV contract stays English — translating them is a
    // later slice. Templates carry the dynamic values ({0} = a machine
    // name, a count, a date, a file path or an exception message),
    // formatted with the invariant culture by
    // StringLocalizer.GetFormatted, so a number or date inside a German
    // sentence keeps the app's existing formatting. The removed-scan
    // message uses two keys (RemovedOne / RemovedMany) instead of the
    // old inline plural hack. The range labels reuse the table
    // header keys (Score, Battery health, Storage wear) instead of a
    // second copy of the same words.
    public const string HistoryEyebrow = "HistoryEyebrow";
    public const string HistoryTitle = "HistoryTitle";
    public const string HistoryIntro = "HistoryIntro";
    public const string HistoryMachineTitle = "HistoryMachineTitle";
    public const string HistoryPickMachine = "HistoryPickMachine";
    public const string HistoryMachineNote = "HistoryMachineNote";
    public const string HistoryBatteryAlertEyebrow = "HistoryBatteryAlertEyebrow";
    public const string HistoryScoreTitle = "HistoryScoreTitle";
    public const string HistoryBatteryTitle = "HistoryBatteryTitle";
    public const string HistoryWearTitle = "HistoryWearTitle";
    public const string HistoryForecastEyebrow = "HistoryForecastEyebrow";
    public const string HistoryCpuLogTitle = "HistoryCpuLogTitle";
    public const string HistoryRefreshChart = "HistoryRefreshChart";
    public const string HistoryChartCaption = "HistoryChartCaption";
    public const string HistoryDriverAgeEyebrow = "HistoryDriverAgeEyebrow";
    public const string HistorySavedScansTitle = "HistorySavedScansTitle";
    public const string HistoryExportCsv = "HistoryExportCsv";
    public const string HistoryTableNote = "HistoryTableNote";
    public const string HistoryHeaderScanned = "HistoryHeaderScanned";
    public const string HistoryHeaderScore = "HistoryHeaderScore";
    public const string HistoryHeaderCycles = "HistoryHeaderCycles";
    public const string HistoryHeaderPowerOnHours = "HistoryHeaderPowerOnHours";
    public const string HistoryRemoveButton = "HistoryRemoveButton";
    public const string HistoryConfirmRemoveButton = "HistoryConfirmRemoveButton";
    public const string HistoryCompareTitle = "HistoryCompareTitle";
    public const string HistoryCompareNote = "HistoryCompareNote";
    public const string HistoryMachineA = "HistoryMachineA";
    public const string HistoryMachineB = "HistoryMachineB";
    public const string HistoryCompareButton = "HistoryCompareButton";
    public const string HistoryValueTitle = "HistoryValueTitle";
    public const string HistoryValueIntro = "HistoryValueIntro";
    public const string HistoryAskingPriceLabel = "HistoryAskingPriceLabel";
    public const string HistoryPriceExample = "HistoryPriceExample";
    public const string HistoryCompareValueButton = "HistoryCompareValueButton";
    public const string HistoryWinnersTitle = "HistoryWinnersTitle";
    public const string HistorySubtitleTemplate = "HistorySubtitleTemplate";
    public const string HistoryLoading = "HistoryLoading";
    public const string HistoryUnavailable = "HistoryUnavailable";
    public const string HistoryNoScans = "HistoryNoScans";
    public const string HistoryOneScan = "HistoryOneScan";
    public const string HistoryScansCount = "HistoryScansCount";
    public const string HistoryReadFailed = "HistoryReadFailed";
    public const string HistoryRemoveUnavailable = "HistoryRemoveUnavailable";
    public const string HistoryRemoveConfirm = "HistoryRemoveConfirm";
    public const string HistoryRemoveGone = "HistoryRemoveGone";
    public const string HistoryRemovedNone = "HistoryRemovedNone";
    public const string HistoryRemovedOne = "HistoryRemovedOne";
    public const string HistoryRemovedMany = "HistoryRemovedMany";
    public const string HistoryRemoveFailed = "HistoryRemoveFailed";
    public const string HistoryCompareUnavailable = "HistoryCompareUnavailable";
    public const string HistoryOnlyOneMachine = "HistoryOnlyOneMachine";
    public const string HistoryPickDifferent = "HistoryPickDifferent";
    public const string HistoryPickTwoCompare = "HistoryPickTwoCompare";
    public const string HistoryPickTwo = "HistoryPickTwo";
    public const string HistoryComparing = "HistoryComparing";
    public const string HistoryCompared = "HistoryCompared";
    public const string HistoryCompareFailed = "HistoryCompareFailed";
    public const string HistoryValuePrompt = "HistoryValuePrompt";
    public const string HistoryValueCompareFirst = "HistoryValueCompareFirst";
    public const string HistoryValueBadPrice = "HistoryValueBadPrice";
    public const string HistoryValueHonestyNote = "HistoryValueHonestyNote";
    public const string HistoryRowScans = "HistoryRowScans";
    public const string HistoryRowFirstScan = "HistoryRowFirstScan";
    public const string HistoryRowLastScan = "HistoryRowLastScan";
    public const string HistoryRowLatestScore = "HistoryRowLatestScore";
    public const string HistoryRowLatestBattery = "HistoryRowLatestBattery";
    public const string HistoryRowLatestWear = "HistoryRowLatestWear";
    public const string HistoryRowLatestDriver = "HistoryRowLatestDriver";
    public const string HistoryRowLastUpdate = "HistoryRowLastUpdate";
    public const string HistoryValueScorePerDollar = "HistoryValueScorePerDollar";
    public const string HistoryWinnerA = "HistoryWinnerA";
    public const string HistoryWinnerB = "HistoryWinnerB";
    public const string HistoryWinnerTie = "HistoryWinnerTie";
    public const string HistoryWinnerNotMeasurable = "HistoryWinnerNotMeasurable";
    public const string HistoryRangeNone = "HistoryRangeNone";
    public const string HistoryRangeOne = "HistoryRangeOne";
    public const string HistoryRangeMany = "HistoryRangeMany";
    public const string HistoryLogLoading = "HistoryLogLoading";
    public const string HistoryLogOff = "HistoryLogOff";
    public const string HistoryLogNoSamples = "HistoryLogNoSamples";
    public const string HistoryLogNoCpuSensor = "HistoryLogNoCpuSensor";
    public const string HistoryLogNoReadings = "HistoryLogNoReadings";
    public const string HistoryLogOneReading = "HistoryLogOneReading";
    public const string HistoryLogTooFarApart = "HistoryLogTooFarApart";
    public const string HistoryLogSummary = "HistoryLogSummary";
    public const string HistoryLogSummaryNewest = "HistoryLogSummaryNewest";
    public const string HistoryLogSummaryAll = "HistoryLogSummaryAll";
    public const string HistoryLogGapNone = "HistoryLogGapNone";
    public const string HistoryLogGapOne = "HistoryLogGapOne";
    public const string HistoryLogGapMany = "HistoryLogGapMany";
    public const string HistoryLogReadFailed = "HistoryLogReadFailed";

    // Localisation, phase 7 (v2.1.13): the History page's remaining
    // Core-owned wordings — the trend summary, the battery-health alert,
    // the oldest-driver summary and the degradation forecasts (all
    // produced by Core with no language parameter until now), plus the
    // value comparison's no-verdict reasons. The U9 verdict sentence
    // itself is NOT here: it stays English in every language, pending
    // Jawad's veto decision (see ValueComparer). The table's cell
    // fallbacks ("Not measurable" / "Not reported") are NOT here
    // either: the CSV export agrees with the table row for row, so
    // the CSV contract stays English. Plurals follow the phase 6
    // pattern (one/many keys instead of an inline plural hack), and
    // direction words that change German word order are full-sentence
    // templates per direction, not bare words.
    public const string HistoryTrendEmptySummary = "HistoryTrendEmptySummary";
    public const string HistoryTrendScansOne = "HistoryTrendScansOne";
    public const string HistoryTrendScansMany = "HistoryTrendScansMany";
    public const string HistoryTrendDeltaNotReported = "HistoryTrendDeltaNotReported";
    public const string HistoryTrendDeltaReportedOnce = "HistoryTrendDeltaReportedOnce";
    public const string HistoryTrendDeltaUp = "HistoryTrendDeltaUp";
    public const string HistoryTrendDeltaDown = "HistoryTrendDeltaDown";
    public const string HistoryTrendDeltaUnchanged = "HistoryTrendDeltaUnchanged";
    public const string HistoryTrendPointsUnit = "HistoryTrendPointsUnit";
    public const string HistoryBatteryAlertBandDrop = "HistoryBatteryAlertBandDrop";
    public const string HistoryBatteryAlertFastFall = "HistoryBatteryAlertFastFall";
    public const string HistoryBatteryBandNormal = "HistoryBatteryBandNormal";
    public const string HistoryBatteryBandWorn = "HistoryBatteryBandWorn";
    public const string HistoryBatteryBandReplace = "HistoryBatteryBandReplace";
    public const string HistoryDriverAgeOnlyOne = "HistoryDriverAgeOnlyOne";
    public const string HistoryDriverAgeFirstLatest = "HistoryDriverAgeFirstLatest";
    public const string HistoryDriverAgeCountOnThatScan = "HistoryDriverAgeCountOnThatScan";
    public const string HistoryDriverAgeNewer = "HistoryDriverAgeNewer";
    public const string HistoryDriverAgeOlder = "HistoryDriverAgeOlder";
    public const string HistoryDriverAgeUnchanged = "HistoryDriverAgeUnchanged";
    public const string HistoryDriverAgeCountStayed = "HistoryDriverAgeCountStayed";
    public const string HistoryDriverAgeCountWent = "HistoryDriverAgeCountWent";
    public const string HistoryDriverAgeCountFirst = "HistoryDriverAgeCountFirst";
    public const string HistoryDriverAgeCountLatest = "HistoryDriverAgeCountLatest";
    public const string HistoryDriverAgeAndJoin = "HistoryDriverAgeAndJoin";
    public const string HistoryForecastMetricSsdWear = "HistoryForecastMetricSsdWear";
    public const string HistoryForecastNotEnoughUsedOne = "HistoryForecastNotEnoughUsedOne";
    public const string HistoryForecastNotEnoughUsedManyNeededOne = "HistoryForecastNotEnoughUsedManyNeededOne";
    public const string HistoryForecastNotEnoughUsedManyNeededMany = "HistoryForecastNotEnoughUsedManyNeededMany";
    public const string HistoryForecastNotEnoughDaysOne = "HistoryForecastNotEnoughDaysOne";
    public const string HistoryForecastNotEnoughDaysMany = "HistoryForecastNotEnoughDaysMany";
    public const string HistoryForecastNoDecline = "HistoryForecastNoDecline";
    public const string HistoryForecastAlreadyReached = "HistoryForecastAlreadyReached";
    public const string HistoryForecastThresholdBattery = "HistoryForecastThresholdBattery";
    public const string HistoryForecastThresholdStorage = "HistoryForecastThresholdStorage";
    public const string HistoryForecastReadyFirst = "HistoryForecastReadyFirst";
    public const string HistoryForecastFell = "HistoryForecastFell";
    public const string HistoryForecastRose = "HistoryForecastRose";
    public const string HistoryForecastReadyRange = "HistoryForecastReadyRange";
    public const string HistoryForecastReadyOpenOne = "HistoryForecastReadyOpenOne";
    public const string HistoryForecastReadyOpenMany = "HistoryForecastReadyOpenMany";
    public const string HistoryForecastStepChange = "HistoryForecastStepChange";
    public const string HistoryForecastJumped = "HistoryForecastJumped";
    public const string HistoryForecastFellSharply = "HistoryForecastFellSharply";
    public const string HistoryForecastDropped = "HistoryForecastDropped";
    public const string HistoryForecastCauseReplaced = "HistoryForecastCauseReplaced";
    public const string HistoryForecastCauseChanged = "HistoryForecastCauseChanged";
    public const string HistoryForecastPartBattery = "HistoryForecastPartBattery";
    public const string HistoryForecastPartStorage = "HistoryForecastPartStorage";
    public const string HistoryValueNoVerdictNoScore = "HistoryValueNoVerdictNoScore";
    public const string HistoryValueNoVerdictNoPrice = "HistoryValueNoVerdictNoPrice";
    public const string HistoryValueNoVerdictMissing = "HistoryValueNoVerdictMissing";

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
        HistoryEyebrow,
        HistoryTitle,
        HistoryIntro,
        HistoryMachineTitle,
        HistoryPickMachine,
        HistoryMachineNote,
        HistoryBatteryAlertEyebrow,
        HistoryScoreTitle,
        HistoryBatteryTitle,
        HistoryWearTitle,
        HistoryForecastEyebrow,
        HistoryCpuLogTitle,
        HistoryRefreshChart,
        HistoryChartCaption,
        HistoryDriverAgeEyebrow,
        HistorySavedScansTitle,
        HistoryExportCsv,
        HistoryTableNote,
        HistoryHeaderScanned,
        HistoryHeaderScore,
        HistoryHeaderCycles,
        HistoryHeaderPowerOnHours,
        HistoryRemoveButton,
        HistoryConfirmRemoveButton,
        HistoryCompareTitle,
        HistoryCompareNote,
        HistoryMachineA,
        HistoryMachineB,
        HistoryCompareButton,
        HistoryValueTitle,
        HistoryValueIntro,
        HistoryAskingPriceLabel,
        HistoryPriceExample,
        HistoryCompareValueButton,
        HistoryWinnersTitle,
        HistorySubtitleTemplate,
        HistoryLoading,
        HistoryUnavailable,
        HistoryNoScans,
        HistoryOneScan,
        HistoryScansCount,
        HistoryReadFailed,
        HistoryRemoveUnavailable,
        HistoryRemoveConfirm,
        HistoryRemoveGone,
        HistoryRemovedNone,
        HistoryRemovedOne,
        HistoryRemovedMany,
        HistoryRemoveFailed,
        HistoryCompareUnavailable,
        HistoryOnlyOneMachine,
        HistoryPickDifferent,
        HistoryPickTwoCompare,
        HistoryPickTwo,
        HistoryComparing,
        HistoryCompared,
        HistoryCompareFailed,
        HistoryValuePrompt,
        HistoryValueCompareFirst,
        HistoryValueBadPrice,
        HistoryValueHonestyNote,
        HistoryRowScans,
        HistoryRowFirstScan,
        HistoryRowLastScan,
        HistoryRowLatestScore,
        HistoryRowLatestBattery,
        HistoryRowLatestWear,
        HistoryRowLatestDriver,
        HistoryRowLastUpdate,
        HistoryValueScorePerDollar,
        HistoryWinnerA,
        HistoryWinnerB,
        HistoryWinnerTie,
        HistoryWinnerNotMeasurable,
        HistoryRangeNone,
        HistoryRangeOne,
        HistoryRangeMany,
        HistoryLogLoading,
        HistoryLogOff,
        HistoryLogNoSamples,
        HistoryLogNoCpuSensor,
        HistoryLogNoReadings,
        HistoryLogOneReading,
        HistoryLogTooFarApart,
        HistoryLogSummary,
        HistoryLogSummaryNewest,
        HistoryLogSummaryAll,
        HistoryLogGapNone,
        HistoryLogGapOne,
        HistoryLogGapMany,
        HistoryLogReadFailed,
        HistoryTrendEmptySummary,
        HistoryTrendScansOne,
        HistoryTrendScansMany,
        HistoryTrendDeltaNotReported,
        HistoryTrendDeltaReportedOnce,
        HistoryTrendDeltaUp,
        HistoryTrendDeltaDown,
        HistoryTrendDeltaUnchanged,
        HistoryTrendPointsUnit,
        HistoryBatteryAlertBandDrop,
        HistoryBatteryAlertFastFall,
        HistoryBatteryBandNormal,
        HistoryBatteryBandWorn,
        HistoryBatteryBandReplace,
        HistoryDriverAgeOnlyOne,
        HistoryDriverAgeFirstLatest,
        HistoryDriverAgeCountOnThatScan,
        HistoryDriverAgeNewer,
        HistoryDriverAgeOlder,
        HistoryDriverAgeUnchanged,
        HistoryDriverAgeCountStayed,
        HistoryDriverAgeCountWent,
        HistoryDriverAgeCountFirst,
        HistoryDriverAgeCountLatest,
        HistoryDriverAgeAndJoin,
        HistoryForecastMetricSsdWear,
        HistoryForecastNotEnoughUsedOne,
        HistoryForecastNotEnoughUsedManyNeededOne,
        HistoryForecastNotEnoughUsedManyNeededMany,
        HistoryForecastNotEnoughDaysOne,
        HistoryForecastNotEnoughDaysMany,
        HistoryForecastNoDecline,
        HistoryForecastAlreadyReached,
        HistoryForecastThresholdBattery,
        HistoryForecastThresholdStorage,
        HistoryForecastReadyFirst,
        HistoryForecastFell,
        HistoryForecastRose,
        HistoryForecastReadyRange,
        HistoryForecastReadyOpenOne,
        HistoryForecastReadyOpenMany,
        HistoryForecastStepChange,
        HistoryForecastJumped,
        HistoryForecastFellSharply,
        HistoryForecastDropped,
        HistoryForecastCauseReplaced,
        HistoryForecastCauseChanged,
        HistoryForecastPartBattery,
        HistoryForecastPartStorage,
        HistoryValueNoVerdictNoScore,
        HistoryValueNoVerdictNoPrice,
        HistoryValueNoVerdictMissing,
    };
}
