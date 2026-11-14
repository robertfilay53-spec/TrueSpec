namespace TrueSpec.Core.Localization;

/// <summary>
/// The built-in language packs. English is the fallback for every key:
/// a language that misses a key shows the English text, never a blank.
/// German is the first additional language (v2.1.7): the navigation
/// rail and the Settings language card; v2.1.8 adds the whole Settings
/// page; v2.1.9 adds the Core diagnostic strings (plug-in status and
/// reasons, manifest errors, portable-toggle messages); v2.1.10 adds
/// the shared component detail view; v2.1.11 adds the Overview page.
/// The remaining app pages stay English until later versions add them,
/// and the language card says so.
/// The translations use the standard terms Windows itself uses in
/// German (Speicher for Storage, Anzeige for Display, Autostart for
/// Startup, Einstellungen for Settings), so a German-speaking buyer
/// meets familiar words, not invented ones. Brand names (TrueSpec) and
/// component abbreviations (CPU, RAM, GPU) are never translated.
/// Templates with placeholders ({0}, {1}) are formatted with the
/// invariant culture by StringLocalizer.GetFormatted: the numbers in
/// them keep the app's existing formatting, and the placeholder order
/// is part of the translation (German word order differs).
/// </summary>
public static class LanguagePacks
{
    public const string EnglishCode = "en";
    public const string GermanCode = "de";

    public static LanguagePack English { get; } = new(
        EnglishCode,
        "English",
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AppStringKeys.NavOverview] = "Overview",
            [AppStringKeys.NavSystem] = "System",
            [AppStringKeys.NavCpu] = "CPU",
            [AppStringKeys.NavRam] = "RAM",
            [AppStringKeys.NavStorage] = "Storage",
            [AppStringKeys.NavBattery] = "Battery",
            [AppStringKeys.NavGpu] = "GPU",
            [AppStringKeys.NavDisplay] = "Display",
            [AppStringKeys.NavNetwork] = "Network",
            [AppStringKeys.NavMotherboard] = "Motherboard",
            [AppStringKeys.NavSecurity] = "Security",
            [AppStringKeys.NavDrivers] = "Drivers",
            [AppStringKeys.NavUpdates] = "Updates",
            [AppStringKeys.NavStartup] = "Startup",
            [AppStringKeys.NavCrash] = "Crash history",
            [AppStringKeys.NavHistory] = "History",
            [AppStringKeys.NavSoftware] = "Software",
            [AppStringKeys.NavTests] = "Tests",
            [AppStringKeys.NavReport] = "Report",
            [AppStringKeys.NavCompare] = "Compare",
            [AppStringKeys.NavFleet] = "Fleet",
            [AppStringKeys.NavSettings] = "Settings",
            [AppStringKeys.LanguageTitle] = "Language",
            [AppStringKeys.LanguageDescription] = "Choose the language for the navigation rail. The language applies right away.",
            [AppStringKeys.LanguageNote] = "Navigation, Settings, component detail pages and the Overview page are translated. Other pages are still in English and follow in later versions.",
            [AppStringKeys.SettingsEyebrow] = "SETTINGS",
            [AppStringKeys.SettingsTitle] = "Settings",
            [AppStringKeys.SettingsIntro] = "Small, honest controls. Every change saves immediately and applies right away. Nothing here phones home.",
            [AppStringKeys.AppearanceTitle] = "Appearance",
            [AppStringKeys.AppearanceNote] = "Dark is the default theme. Light is available for bright rooms.",
            [AppStringKeys.ThemeDark] = "Dark",
            [AppStringKeys.ThemeLight] = "Light",
            [AppStringKeys.ThemeDarkDefault] = "Dark (default)",
            [AppStringKeys.RefreshTitle] = "Live sensor refresh rate",
            [AppStringKeys.RefreshNote] = "How often the live sensor strip re-reads. Faster is livelier; slower is calmer on the CPU.",
            [AppStringKeys.SecondsOne] = "1 second",
            [AppStringKeys.SecondsMany] = "{0} seconds",
            [AppStringKeys.LoggingTitle] = "Sensor logging and alerts",
            [AppStringKeys.LoggingIntro] = "Live sensor readings are recorded to a local CSV log while TrueSpec runs. The log never leaves this machine.",
            [AppStringKeys.RecordTitle] = "Record sensor readings",
            [AppStringKeys.RecordNote] = "One row per sample, at the refresh rate above. A sensor that stops reporting leaves its cell empty, never a zero.",
            [AppStringKeys.ToggleOn] = "On",
            [AppStringKeys.ToggleOff] = "Off",
            [AppStringKeys.AlertTitle] = "CPU temperature alert",
            [AppStringKeys.AlertSummaryOn] = "Alert when the CPU temperature reaches {0} C for {1} seconds.",
            [AppStringKeys.AlertSummaryOff] = "No temperature alert set.",
            [AppStringKeys.AlertNote] = "TrueSpec ships with no alert set. The alert fires only after the temperature stays above your threshold for the full duration.",
            [AppStringKeys.AlertAboveLabel] = "Alert above (C)",
            [AppStringKeys.AlertDurationLabel] = "Must stay above for",
            [AppStringKeys.ThresholdMessage] = "Enter a temperature between 30 and 120 C.",
            [AppStringKeys.ExportTitle] = "Report export folder",
            [AppStringKeys.ExportDefaultSuffix] = "(default)",
            [AppStringKeys.ExportChoose] = "Choose folder...",
            [AppStringKeys.ExportReset] = "Reset to default",
            [AppStringKeys.ExportFolderRejected] = "That folder can't be used for exports (drive root or system folder). Pick another folder.",
            [AppStringKeys.ExportNote] = "JSON, HTML, CSV reports and the printable buyer checklist are saved here. Nothing is uploaded.",
            [AppStringKeys.DataLocationTitle] = "Data location (portable mode)",
            [AppStringKeys.PortableSummaryOnMarkerEnv] = "Portable mode is on (marker file and environment variable). Data is kept next to the app.",
            [AppStringKeys.PortableSummaryOnMarker] = "Portable mode is on. Data is kept next to the app.",
            [AppStringKeys.PortableSummaryOnEnv] = "Portable mode is on through the TRUESPEC_PORTABLE environment variable. Data is kept next to the app.",
            [AppStringKeys.PortableSummaryMarkerBlocked] = "The portable marker file is present, but this app folder is a system or protected folder, so portable mode is not active and data stays in the default folder.",
            [AppStringKeys.PortableSummaryOff] = "Portable mode is off. Data is kept in the default folder.",
            [AppStringKeys.PortableActionOn] = "Turn portable mode on",
            [AppStringKeys.PortableActionOff] = "Turn portable mode off",
            [AppStringKeys.DataLocationNote] = "Takes effect after a restart. Scans already saved stay in the folder they were saved in; only new scans go to the new location.",
            [AppStringKeys.PluginsTitle] = "Plug-ins",
            [AppStringKeys.PluginsIntro] = "A plug-in adds a collector — a new hardware source — without changing TrueSpec. Put each plug-in in its own folder under the folder below. Every folder needs a plugin.json manifest that names the plug-in, the collector contract version it was written for, and the collectors it provides.",
            [AppStringKeys.PluginsSummaryNone] = "No plug-ins found.",
            [AppStringKeys.PluginsSummaryOne] = "1 plug-in found.",
            [AppStringKeys.PluginsSummaryMany] = "{0} plug-ins found.",
            [AppStringKeys.PluginsRefresh] = "Refresh plug-ins",
            [AppStringKeys.PluginsNotLoadedNote] = "Plug-ins are discovered here but never loaded in this version: only signed plug-ins load, and plug-in signing is not set up yet.",
            [AppStringKeys.PrivacyTitle] = "Privacy",
            [AppStringKeys.PrivacyText] = "TrueSpec has no telemetry, no account, and no network calls. Scans stay on this machine. There is no update check and no startup registration: an offline app does not promise network features.",
            [AppStringKeys.PluginStatusReady] = "Ready — not loaded yet",
            [AppStringKeys.PluginStatusNeedsNewer] = "Needs a newer TrueSpec",
            [AppStringKeys.PluginStatusOlderContract] = "Written for an older contract",
            [AppStringKeys.PluginStatusManifestProblem] = "Manifest problem",
            [AppStringKeys.PluginStatusAssemblyMissing] = "Assembly missing",
            [AppStringKeys.PluginStatusUnknown] = "Unknown",
            [AppStringKeys.PluginReasonNoManifest] = "No plugin.json manifest in this folder.",
            [AppStringKeys.PluginReasonManifestUnreadable] = "The manifest could not be read.",
            [AppStringKeys.PluginReasonContractVersion] = "This plug-in was written for collector contract version {0}; this TrueSpec speaks contract version {1}.",
            [AppStringKeys.PluginReasonAssemblyMissing] = "The plug-in file '{0}' is missing from this folder.",
            [AppStringKeys.PluginReasonReady] = "Manifest valid. Not loaded yet: only signed plug-ins load, and plug-in signing is not set up yet.",
            [AppStringKeys.PluginErrorFolderUnreadable] = "The plug-ins folder could not be read, so no plug-ins are listed.",
            [AppStringKeys.PluginErrorTooMany] = "More than {0} plug-in folders were found; only the first {1} (alphabetically) are listed.",
            [AppStringKeys.ManifestTooLarge] = "The manifest is too large (over 64 KB) and was skipped.",
            [AppStringKeys.ManifestNotObject] = "The manifest is not a JSON object, so it was skipped.",
            [AppStringKeys.ManifestUnknownField] = "The manifest has an unknown field '{0}', so it was skipped.",
            [AppStringKeys.ManifestMissingId] = "The manifest is missing its 'id', so it was skipped.",
            [AppStringKeys.ManifestMissingName] = "The manifest is missing its 'name', so it was skipped.",
            [AppStringKeys.ManifestMissingVersion] = "The manifest is missing its 'version', so it was skipped.",
            [AppStringKeys.ManifestMissingContractVersion] = "The manifest is missing its 'contractVersion' (a whole number), so it was skipped.",
            [AppStringKeys.ManifestMissingAssembly] = "The manifest is missing its 'assembly' file name, so it was skipped.",
            [AppStringKeys.ManifestAssemblyPath] = "The manifest's 'assembly' must be a plain file name inside the plug-in's own folder, not a path, so it was skipped.",
            [AppStringKeys.ManifestAssemblyNotFile] = "The manifest's 'assembly' must name a file, so it was skipped.",
            [AppStringKeys.ManifestAssemblyInvalidName] = "The manifest's 'assembly' is not a valid file name, so it was skipped.",
            [AppStringKeys.ManifestAssemblyNotDll] = "The manifest's 'assembly' must be a .dll file, so it was skipped.",
            [AppStringKeys.ManifestCollectorNotObject] = "A collector in the manifest is not an object, so the manifest was skipped.",
            [AppStringKeys.ManifestCollectorUnknownField] = "The collector has an unknown field '{0}', so the manifest was skipped.",
            [AppStringKeys.ManifestCollectorMissingFields] = "A collector in the manifest is missing its 'id', 'name' or 'category', so the manifest was skipped.",
            [AppStringKeys.ManifestMissingCollectors] = "The manifest is missing its 'collectors' list, so it was skipped.",
            [AppStringKeys.ManifestNoCollectors] = "The manifest lists no collectors, so it was skipped.",
            [AppStringKeys.ManifestTooManyCollectors] = "The manifest lists {0} collectors; at most {1} are allowed, so it was skipped.",
            [AppStringKeys.ManifestCollectorBadId] = "The collector id '{0}' is not a valid id (lowercase letters, digits and dashes, starting with a letter), so the manifest was skipped.",
            [AppStringKeys.ManifestCollectorDuplicateId] = "The manifest lists the collector id '{0}' twice, so the manifest was skipped.",
            [AppStringKeys.ManifestInvalidJson] = "The manifest is not valid JSON, so it was skipped.",
            [AppStringKeys.ManifestUnreadable] = "The manifest could not be read, so it was skipped.",
            [AppStringKeys.PortableToggleFolderUnusable] = "Portable mode could not be changed: that app folder could not be used.",
            [AppStringKeys.PortableToggleProtectedFolder] = "Portable mode cannot be enabled here: this app folder is a system or protected folder, so the app would keep its data in the default folder anyway.",
            [AppStringKeys.PortableToggleAlreadyOn] = "Portable mode is already on. Restart TrueSpec for it to take effect.",
            [AppStringKeys.PortableToggleEnableFailed] = "Portable mode could not be enabled: the app folder could not be written to.",
            [AppStringKeys.PortableToggleEnabled] = "Portable mode is on. Restart TrueSpec: from then on, settings, history and the sensor log are kept next to the app instead of in the default folder. Scans already saved stay in the old folder.",
            [AppStringKeys.PortableToggleNoMarkerEnv] = "There is no marker file to remove: portable mode is on through the TRUESPEC_PORTABLE environment variable. Unset the variable to turn it off.",
            [AppStringKeys.PortableToggleAlreadyOff] = "Portable mode is already off.",
            [AppStringKeys.PortableToggleDisableFailed] = "Portable mode could not be disabled: the marker file could not be removed.",
            [AppStringKeys.PortableToggleMarkerRemovedEnv] = "The marker file was removed, but the TRUESPEC_PORTABLE environment variable still requests portable mode, so it stays on until that is unset.",
            [AppStringKeys.PortableToggleDisabled] = "Portable mode is off. Restart TrueSpec: from then on, data is kept in the default folder. Scans already saved next to the app stay there.",
            [AppStringKeys.DetailSpecificationsTitle] = "Specifications",
            [AppStringKeys.DetailSearchPlaceholder] = "Search specifications",
            [AppStringKeys.DetailCopyHint] = "Click any value to copy it. Hover a value or source name to see where the reading came from.",
            [AppStringKeys.DetailAgeSignalsTitle] = "Age signals",
            [AppStringKeys.DetailAgeSignalsIntro] = "The raw numbers behind the authenticity verdict. Each signal names where it was read from.",
            [AppStringKeys.DetailWhatThisMeansTitle] = "What this means",
            [AppStringKeys.DetailNotCollectedHeadline] = "Not collected",
            [AppStringKeys.DetailNotCollectedText] = "This component has not been collected yet.",
            [AppStringKeys.DetailCaptured] = "Reading captured {0}",
            [AppStringKeys.DetailNoSpecs] = "No specifications were collected for this component.",
            [AppStringKeys.DetailNoSpecsMatch] = "No specifications match \"{0}\". Clear the search to see them all.",
            [AppStringKeys.DetailAgeSignalsSkipped] = "{0} authenticity check(s) could not run because a signal was not reported.",
            [AppStringKeys.DetailSignalUntrusted] = "Not trusted: another check proved this date impossible.",
            [AppStringKeys.DetailEstimateSuffix] = " (ESTIMATE)",
            [AppStringKeys.OverviewAdminEyebrow] = "RUN AS ADMINISTRATOR FOR FULL RESULTS",
            [AppStringKeys.OverviewOutOf100] = "out of 100",
            [AppStringKeys.OverviewOverallHealth] = "Overall health",
            [AppStringKeys.OverviewScoreNote] = "Score = weighted average of measurable components. A component without a real health number does not count.",
            [AppStringKeys.OverviewNewLaptopCheck] = "New Laptop Check",
            [AppStringKeys.OverviewExportReport] = "Export Report",
            [AppStringKeys.OverviewComponentHealth] = "Component health",
            [AppStringKeys.OverviewHealthLabel] = "Health",
            [AppStringKeys.OverviewDetails] = "Details",
            [AppStringKeys.OverviewNoComponents] = "No components were collected in this scan. Refresh the scan from the status bar; if the section stays empty, the collectors could not read this machine.",
            [AppStringKeys.OverviewLiveSensors] = "Live sensors",
            [AppStringKeys.OverviewNoSensorReading] = "No sensor reading",
            [AppStringKeys.OverviewAlertEyebrow] = "TEMPERATURE ALERT",
            [AppStringKeys.OverviewAlertNote] = "Alerts fire only after the temperature stays above your threshold for the full duration you set. Change the threshold in Settings.",
            [AppStringKeys.OverviewFooter] = "Every reading keeps its source. No telemetry, no network calls.",
            [AppStringKeys.OverviewScanning] = "Scanning…",
            [AppStringKeys.OverviewScanningNote] = "The hardware scan is still running. The score appears when the scan finishes.",
            [AppStringKeys.OverviewSensorLogUnavailable] = "Sensor logging is unavailable on this machine.",
            [AppStringKeys.OverviewSensorLogOn] = "Sensor readings are recorded to {0}. The chart is on the History page.",
            [AppStringKeys.OverviewSensorLogOff] = "Sensor logging is off. Turn it on in Settings to record readings.",
            [AppStringKeys.OverviewAlertSet] = "Alert when the CPU temperature reaches {0} C for {1} seconds. Set it in Settings.",
            [AppStringKeys.OverviewAlertNotSet] = "No temperature alert set. TrueSpec ships with no threshold; set one in Settings if you want an alert.",
            [AppStringKeys.OverviewRefreshText] = "Live sensors refresh every {0}. Values appear only when LibreHardwareMonitor reports them.",
            [AppStringKeys.OverviewLiveUpdated] = "Live sensors updated {0}",
            [AppStringKeys.OverviewLiveReadFailed] = "Live sensor read failed: {0}",
            [AppStringKeys.OverviewWeight] = "Weight {0}",
            [AppStringKeys.OverviewContribution] = "+{0} pts",
            [AppStringKeys.OverviewKeyCpuTemp] = "CPU temperature",
            [AppStringKeys.OverviewKeyMemory] = "Memory",
            [AppStringKeys.OverviewKeyDiskHealth] = "Disk health",
            [AppStringKeys.OverviewKeyBatteryHealth] = "Battery health",
            [AppStringKeys.OverviewMetricCpuTemp] = "CPU temp",
            [AppStringKeys.OverviewMetricGpuTemp] = "GPU temp",
            [AppStringKeys.OverviewMetricCpuUsage] = "CPU usage",
            [AppStringKeys.OverviewMetricBatteryPower] = "Battery power",
            [AppStringKeys.OverviewMetricNotAvailable] = "Not available",
        });

    public static LanguagePack German { get; } = new(
        GermanCode,
        "Deutsch",
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AppStringKeys.NavOverview] = "Übersicht",
            [AppStringKeys.NavSystem] = "System",
            [AppStringKeys.NavCpu] = "CPU",
            [AppStringKeys.NavRam] = "RAM",
            [AppStringKeys.NavStorage] = "Speicher",
            [AppStringKeys.NavBattery] = "Akku",
            [AppStringKeys.NavGpu] = "GPU",
            [AppStringKeys.NavDisplay] = "Anzeige",
            [AppStringKeys.NavNetwork] = "Netzwerk",
            [AppStringKeys.NavMotherboard] = "Mainboard",
            [AppStringKeys.NavSecurity] = "Sicherheit",
            [AppStringKeys.NavDrivers] = "Treiber",
            [AppStringKeys.NavUpdates] = "Updates",
            [AppStringKeys.NavStartup] = "Autostart",
            [AppStringKeys.NavCrash] = "Absturzverlauf",
            [AppStringKeys.NavHistory] = "Verlauf",
            [AppStringKeys.NavSoftware] = "Software",
            [AppStringKeys.NavTests] = "Tests",
            [AppStringKeys.NavReport] = "Bericht",
            [AppStringKeys.NavCompare] = "Vergleich",
            [AppStringKeys.NavFleet] = "Gerätepark",
            [AppStringKeys.NavSettings] = "Einstellungen",
            [AppStringKeys.LanguageTitle] = "Sprache",
            [AppStringKeys.LanguageDescription] = "Wählen Sie die Sprache für die Navigationsleiste. Die Sprache wird sofort angewendet.",
            [AppStringKeys.LanguageNote] = "Navigation, Einstellungen, Komponentendetailseiten und die Übersichtsseite sind übersetzt. Andere Seiten sind noch auf Englisch und folgen in späteren Versionen.",
            [AppStringKeys.SettingsEyebrow] = "EINSTELLUNGEN",
            [AppStringKeys.SettingsTitle] = "Einstellungen",
            [AppStringKeys.SettingsIntro] = "Kleine, ehrliche Einstellungen. Jede Änderung wird sofort gespeichert und angewendet. Nichts hier telefoniert nach Hause.",
            [AppStringKeys.AppearanceTitle] = "Darstellung",
            [AppStringKeys.AppearanceNote] = "Dunkel ist das Standarddesign. Hell ist für helle Räume verfügbar.",
            [AppStringKeys.ThemeDark] = "Dunkel",
            [AppStringKeys.ThemeLight] = "Hell",
            [AppStringKeys.ThemeDarkDefault] = "Dunkel (Standard)",
            [AppStringKeys.RefreshTitle] = "Aktualisierungsrate der Live-Sensoren",
            [AppStringKeys.RefreshNote] = "Wie oft der Live-Sensorstreifen neu liest. Schneller ist lebhafter, langsamer schont die CPU.",
            [AppStringKeys.SecondsOne] = "1 Sekunde",
            [AppStringKeys.SecondsMany] = "{0} Sekunden",
            [AppStringKeys.LoggingTitle] = "Sensorprotokollierung und Warnungen",
            [AppStringKeys.LoggingIntro] = "Live-Sensormesswerte werden während TrueSpec läuft in einer lokalen CSV-Datei protokolliert. Das Protokoll verlässt diesen Rechner nicht.",
            [AppStringKeys.RecordTitle] = "Sensormesswerte aufzeichnen",
            [AppStringKeys.RecordNote] = "Eine Zeile pro Messwert, mit der Aktualisierungsrate oben. Ein Sensor, der nichts mehr meldet, hinterlässt eine leere Zelle, niemals eine Null.",
            [AppStringKeys.ToggleOn] = "Ein",
            [AppStringKeys.ToggleOff] = "Aus",
            [AppStringKeys.AlertTitle] = "Warnung bei CPU-Temperatur",
            [AppStringKeys.AlertSummaryOn] = "Warnung, wenn die CPU-Temperatur {0} °C für {1} Sekunden erreicht.",
            [AppStringKeys.AlertSummaryOff] = "Keine Temperaturwarnung eingestellt.",
            [AppStringKeys.AlertNote] = "TrueSpec wird ohne eingestellte Warnung ausgeliefert. Die Warnung löst erst aus, wenn die Temperatur die volle Dauer über dem Schwellenwert bleibt.",
            [AppStringKeys.AlertAboveLabel] = "Warnung oberhalb (°C)",
            [AppStringKeys.AlertDurationLabel] = "Muss darüber bleiben für",
            [AppStringKeys.ThresholdMessage] = "Geben Sie eine Temperatur zwischen 30 und 120 °C ein.",
            [AppStringKeys.ExportTitle] = "Ordner für Berichtsexporte",
            [AppStringKeys.ExportDefaultSuffix] = "(Standard)",
            [AppStringKeys.ExportChoose] = "Ordner wählen...",
            [AppStringKeys.ExportReset] = "Auf Standard zurücksetzen",
            [AppStringKeys.ExportFolderRejected] = "Dieser Ordner kann nicht für Exporte verwendet werden (Stammverzeichnis oder Systemordner). Wählen Sie einen anderen Ordner.",
            [AppStringKeys.ExportNote] = "JSON-, HTML- und CSV-Berichte sowie die ausdruckbare Käufer-Checkliste werden hier gespeichert. Nichts wird hochgeladen.",
            [AppStringKeys.DataLocationTitle] = "Datenspeicherort (tragbarer Modus)",
            [AppStringKeys.PortableSummaryOnMarkerEnv] = "Der tragbare Modus ist an (Marker-Datei und Umgebungsvariable). Daten werden neben der App gespeichert.",
            [AppStringKeys.PortableSummaryOnMarker] = "Der tragbare Modus ist an. Daten werden neben der App gespeichert.",
            [AppStringKeys.PortableSummaryOnEnv] = "Der tragbare Modus ist über die Umgebungsvariable TRUESPEC_PORTABLE an. Daten werden neben der App gespeichert.",
            [AppStringKeys.PortableSummaryMarkerBlocked] = "Die Marker-Datei ist vorhanden, aber dieser App-Ordner ist ein System- oder geschützter Ordner, daher ist der tragbare Modus nicht aktiv und die Daten bleiben im Standardordner.",
            [AppStringKeys.PortableSummaryOff] = "Der tragbare Modus ist aus. Daten werden im Standardordner gespeichert.",
            [AppStringKeys.PortableActionOn] = "Tragbaren Modus einschalten",
            [AppStringKeys.PortableActionOff] = "Tragbaren Modus ausschalten",
            [AppStringKeys.DataLocationNote] = "Wird nach einem Neustart wirksam. Bereits gespeicherte Scans bleiben in dem Ordner, in dem sie gespeichert wurden; nur neue Scans gehen an den neuen Ort.",
            [AppStringKeys.PluginsTitle] = "Plug-ins",
            [AppStringKeys.PluginsIntro] = "Ein Plug-in fügt einen Collector hinzu — eine neue Hardwarequelle — ohne TrueSpec zu verändern. Legen Sie jedes Plug-in in einen eigenen Ordner unter dem unten genannten Ordner. Jeder Ordner braucht ein plugin.json-Manifest, das das Plug-in, die Collector-Vertragsversion, für die es geschrieben wurde, und die Collector nennt, die es bereitstellt.",
            [AppStringKeys.PluginsSummaryNone] = "Keine Plug-ins gefunden.",
            [AppStringKeys.PluginsSummaryOne] = "1 Plug-in gefunden.",
            [AppStringKeys.PluginsSummaryMany] = "{0} Plug-ins gefunden.",
            [AppStringKeys.PluginsRefresh] = "Plug-ins aktualisieren",
            [AppStringKeys.PluginsNotLoadedNote] = "Plug-ins werden hier erkannt, aber in dieser Version nie geladen: Nur signierte Plug-ins werden geladen, und die Plug-in-Signierung ist noch nicht eingerichtet.",
            [AppStringKeys.PrivacyTitle] = "Datenschutz",
            [AppStringKeys.PrivacyText] = "TrueSpec hat keine Telemetrie, kein Konto und keine Netzwerkaufrufe. Scans bleiben auf diesem Rechner. Es gibt keine Update-Prüfung und keine Autostart-Registrierung: Eine Offline-App verspricht keine Netzwerkfunktionen.",
            [AppStringKeys.PluginStatusReady] = "Bereit — noch nicht geladen",
            [AppStringKeys.PluginStatusNeedsNewer] = "Braucht ein neueres TrueSpec",
            [AppStringKeys.PluginStatusOlderContract] = "Für einen älteren Vertrag geschrieben",
            [AppStringKeys.PluginStatusManifestProblem] = "Manifestproblem",
            [AppStringKeys.PluginStatusAssemblyMissing] = "Assembly fehlt",
            [AppStringKeys.PluginStatusUnknown] = "Unbekannt",
            [AppStringKeys.PluginReasonNoManifest] = "Kein plugin.json-Manifest in diesem Ordner.",
            [AppStringKeys.PluginReasonManifestUnreadable] = "Das Manifest konnte nicht gelesen werden.",
            [AppStringKeys.PluginReasonContractVersion] = "Dieses Plug-in wurde für die Collector-Vertragsversion {0} geschrieben; dieses TrueSpec spricht Vertragsversion {1}.",
            [AppStringKeys.PluginReasonAssemblyMissing] = "Die Plug-in-Datei '{0}' fehlt in diesem Ordner.",
            [AppStringKeys.PluginReasonReady] = "Manifest gültig. Noch nicht geladen: Nur signierte Plug-ins werden geladen, und die Plug-in-Signierung ist noch nicht eingerichtet.",
            [AppStringKeys.PluginErrorFolderUnreadable] = "Der Plug-ins-Ordner konnte nicht gelesen werden, daher werden keine Plug-ins aufgelistet.",
            [AppStringKeys.PluginErrorTooMany] = "Mehr als {0} Plug-in-Ordner wurden gefunden; nur die ersten {1} (alphabetisch) werden aufgelistet.",
            [AppStringKeys.ManifestTooLarge] = "Das Manifest ist zu groß (über 64 KB) und wurde übersprungen.",
            [AppStringKeys.ManifestNotObject] = "Das Manifest ist kein JSON-Objekt und wurde daher übersprungen.",
            [AppStringKeys.ManifestUnknownField] = "Das Manifest hat ein unbekanntes Feld '{0}' und wurde daher übersprungen.",
            [AppStringKeys.ManifestMissingId] = "Dem Manifest fehlt die 'id', daher wurde es übersprungen.",
            [AppStringKeys.ManifestMissingName] = "Dem Manifest fehlt der 'name', daher wurde es übersprungen.",
            [AppStringKeys.ManifestMissingVersion] = "Dem Manifest fehlt die 'version', daher wurde es übersprungen.",
            [AppStringKeys.ManifestMissingContractVersion] = "Dem Manifest fehlt die 'contractVersion' (eine ganze Zahl), daher wurde es übersprungen.",
            [AppStringKeys.ManifestMissingAssembly] = "Dem Manifest fehlt der Dateiname der 'assembly', daher wurde es übersprungen.",
            [AppStringKeys.ManifestAssemblyPath] = "Die 'assembly' des Manifests muss ein einfacher Dateiname im eigenen Ordner des Plug-ins sein, kein Pfad, daher wurde es übersprungen.",
            [AppStringKeys.ManifestAssemblyNotFile] = "Die 'assembly' des Manifests muss eine Datei benennen, daher wurde es übersprungen.",
            [AppStringKeys.ManifestAssemblyInvalidName] = "Die 'assembly' des Manifests ist kein gültiger Dateiname, daher wurde es übersprungen.",
            [AppStringKeys.ManifestAssemblyNotDll] = "Die 'assembly' des Manifests muss eine .dll-Datei sein, daher wurde es übersprungen.",
            [AppStringKeys.ManifestCollectorNotObject] = "Ein Collector im Manifest ist kein Objekt, daher wurde das Manifest übersprungen.",
            [AppStringKeys.ManifestCollectorUnknownField] = "Der Collector hat ein unbekanntes Feld '{0}', daher wurde das Manifest übersprungen.",
            [AppStringKeys.ManifestCollectorMissingFields] = "Einem Collector im Manifest fehlen 'id', 'name' oder 'category', daher wurde das Manifest übersprungen.",
            [AppStringKeys.ManifestMissingCollectors] = "Dem Manifest fehlt die 'collectors'-Liste, daher wurde es übersprungen.",
            [AppStringKeys.ManifestNoCollectors] = "Das Manifest listet keine Collector auf, daher wurde es übersprungen.",
            [AppStringKeys.ManifestTooManyCollectors] = "Das Manifest listet {0} Collector auf; höchstens {1} sind erlaubt, daher wurde es übersprungen.",
            [AppStringKeys.ManifestCollectorBadId] = "Die Collector-ID '{0}' ist keine gültige ID (Kleinbuchstaben, Ziffern und Bindestriche, beginnend mit einem Buchstaben), daher wurde das Manifest übersprungen.",
            [AppStringKeys.ManifestCollectorDuplicateId] = "Das Manifest listet die Collector-ID '{0}' zweimal auf, daher wurde das Manifest übersprungen.",
            [AppStringKeys.ManifestInvalidJson] = "Das Manifest ist kein gültiges JSON, daher wurde es übersprungen.",
            [AppStringKeys.ManifestUnreadable] = "Das Manifest konnte nicht gelesen werden, daher wurde es übersprungen.",
            [AppStringKeys.PortableToggleFolderUnusable] = "Der tragbare Modus konnte nicht geändert werden: Dieser App-Ordner konnte nicht verwendet werden.",
            [AppStringKeys.PortableToggleProtectedFolder] = "Der tragbare Modus kann hier nicht aktiviert werden: Dieser App-Ordner ist ein System- oder geschützter Ordner, daher würde die App ihre Daten ohnehin im Standardordner speichern.",
            [AppStringKeys.PortableToggleAlreadyOn] = "Der tragbare Modus ist bereits an. Starten Sie TrueSpec neu, damit er wirksam wird.",
            [AppStringKeys.PortableToggleEnableFailed] = "Der tragbare Modus konnte nicht aktiviert werden: Der App-Ordner konnte nicht beschrieben werden.",
            [AppStringKeys.PortableToggleEnabled] = "Der tragbare Modus ist an. Starten Sie TrueSpec neu: Von da an werden Einstellungen, Verlauf und Sensorprotokoll neben der App gespeichert statt im Standardordner. Bereits gespeicherte Scans bleiben im alten Ordner.",
            [AppStringKeys.PortableToggleNoMarkerEnv] = "Es gibt keine Marker-Datei zu entfernen: Der tragbare Modus ist über die Umgebungsvariable TRUESPEC_PORTABLE an. Heben Sie die Variable auf, um ihn auszuschalten.",
            [AppStringKeys.PortableToggleAlreadyOff] = "Der tragbare Modus ist bereits aus.",
            [AppStringKeys.PortableToggleDisableFailed] = "Der tragbare Modus konnte nicht deaktiviert werden: Die Marker-Datei konnte nicht entfernt werden.",
            [AppStringKeys.PortableToggleMarkerRemovedEnv] = "Die Marker-Datei wurde entfernt, aber die Umgebungsvariable TRUESPEC_PORTABLE fordert den tragbaren Modus weiterhin an, daher bleibt er an, bis sie aufgehoben wird.",
            [AppStringKeys.PortableToggleDisabled] = "Der tragbare Modus ist aus. Starten Sie TrueSpec neu: Von da an werden Daten im Standardordner gespeichert. Bereits neben der App gespeicherte Scans bleiben dort.",
            [AppStringKeys.DetailSpecificationsTitle] = "Spezifikationen",
            [AppStringKeys.DetailSearchPlaceholder] = "Spezifikationen durchsuchen",
            [AppStringKeys.DetailCopyHint] = "Klicken Sie auf einen Wert, um ihn zu kopieren. Bewegen Sie die Maus über einen Wert oder Quellennamen, um zu sehen, woher der Messwert stammt.",
            [AppStringKeys.DetailAgeSignalsTitle] = "Alterssignale",
            [AppStringKeys.DetailAgeSignalsIntro] = "Die Rohwerte hinter dem Echtheitsurteil. Jedes Signal nennt seine Quelle.",
            [AppStringKeys.DetailWhatThisMeansTitle] = "Was das bedeutet",
            [AppStringKeys.DetailNotCollectedHeadline] = "Nicht erfasst",
            [AppStringKeys.DetailNotCollectedText] = "Diese Komponente wurde noch nicht erfasst.",
            [AppStringKeys.DetailCaptured] = "Messwert erfasst {0}",
            [AppStringKeys.DetailNoSpecs] = "Für diese Komponente wurden keine Spezifikationen erfasst.",
            [AppStringKeys.DetailNoSpecsMatch] = "Keine Spezifikationen passen zu „{0}\". Leeren Sie die Suche, um alle zu sehen.",
            [AppStringKeys.DetailAgeSignalsSkipped] = "{0} Echtheitsprüfung(en) konnte(n) nicht ausgeführt werden, weil ein Signal nicht gemeldet wurde.",
            [AppStringKeys.DetailSignalUntrusted] = "Nicht vertrauenswürdig: Eine andere Prüfung hat bewiesen, dass dieses Datum unmöglich ist.",
            [AppStringKeys.DetailEstimateSuffix] = " (geschätzt)",
            [AppStringKeys.OverviewAdminEyebrow] = "FÜR VOLLSTÄNDIGE ERGEBNISSE ALS ADMINISTRATOR AUSFÜHREN",
            [AppStringKeys.OverviewOutOf100] = "von 100",
            [AppStringKeys.OverviewOverallHealth] = "Gesamtzustand",
            [AppStringKeys.OverviewScoreNote] = "Die Punktzahl ist der gewichtete Durchschnitt der messbaren Komponenten. Eine Komponente ohne echten Gesundheitswert zählt nicht.",
            [AppStringKeys.OverviewNewLaptopCheck] = "Neuen Laptop prüfen",
            [AppStringKeys.OverviewExportReport] = "Bericht exportieren",
            [AppStringKeys.OverviewComponentHealth] = "Komponentenzustand",
            [AppStringKeys.OverviewHealthLabel] = "Zustand",
            [AppStringKeys.OverviewDetails] = "Details",
            [AppStringKeys.OverviewNoComponents] = "Bei diesem Scan wurden keine Komponenten erfasst. Aktualisieren Sie den Scan über die Statusleiste; wenn der Abschnitt leer bleibt, konnten die Collector diesen Rechner nicht lesen.",
            [AppStringKeys.OverviewLiveSensors] = "Live-Sensoren",
            [AppStringKeys.OverviewNoSensorReading] = "Kein Sensormesswert",
            [AppStringKeys.OverviewAlertEyebrow] = "TEMPERATURWARNUNG",
            [AppStringKeys.OverviewAlertNote] = "Warnungen lösen erst aus, wenn die Temperatur die volle von Ihnen eingestellte Dauer über dem Schwellenwert bleibt. Ändern Sie den Schwellenwert in den Einstellungen.",
            [AppStringKeys.OverviewFooter] = "Jeder Messwert behält seine Quelle. Keine Telemetrie, keine Netzwerkaufrufe.",
            [AppStringKeys.OverviewScanning] = "Scan läuft…",
            [AppStringKeys.OverviewScanningNote] = "Der Hardware-Scan läuft noch. Die Punktzahl erscheint, wenn der Scan fertig ist.",
            [AppStringKeys.OverviewSensorLogUnavailable] = "Sensorprotokollierung ist auf diesem Rechner nicht verfügbar.",
            [AppStringKeys.OverviewSensorLogOn] = "Sensormesswerte werden in {0} aufgezeichnet. Das Diagramm ist auf der Verlaufsseite.",
            [AppStringKeys.OverviewSensorLogOff] = "Sensorprotokollierung ist aus. Schalten Sie sie in den Einstellungen ein, um Messwerte aufzuzeichnen.",
            [AppStringKeys.OverviewAlertSet] = "Warnung, wenn die CPU-Temperatur {0} °C für {1} Sekunden erreicht. Stellen Sie sie in den Einstellungen ein.",
            [AppStringKeys.OverviewAlertNotSet] = "Keine Temperaturwarnung eingestellt. TrueSpec wird ohne Schwellenwert ausgeliefert; stellen Sie einen in den Einstellungen ein, wenn Sie eine Warnung möchten.",
            [AppStringKeys.OverviewRefreshText] = "Live-Sensoren werden alle {0} neu gelesen. Werte erscheinen nur, wenn LibreHardwareMonitor sie meldet.",
            [AppStringKeys.OverviewLiveUpdated] = "Live-Sensoren aktualisiert um {0}",
            [AppStringKeys.OverviewLiveReadFailed] = "Lesen der Live-Sensoren fehlgeschlagen: {0}",
            [AppStringKeys.OverviewWeight] = "Gewicht {0}",
            [AppStringKeys.OverviewContribution] = "+{0} Pkt.",
            [AppStringKeys.OverviewKeyCpuTemp] = "CPU-Temperatur",
            [AppStringKeys.OverviewKeyMemory] = "Arbeitsspeicher",
            [AppStringKeys.OverviewKeyDiskHealth] = "Datenträgerzustand",
            [AppStringKeys.OverviewKeyBatteryHealth] = "Akkuzustand",
            [AppStringKeys.OverviewMetricCpuTemp] = "CPU-Temperatur",
            [AppStringKeys.OverviewMetricGpuTemp] = "GPU-Temperatur",
            [AppStringKeys.OverviewMetricCpuUsage] = "CPU-Auslastung",
            [AppStringKeys.OverviewMetricBatteryPower] = "Akkuleistung",
            [AppStringKeys.OverviewMetricNotAvailable] = "Nicht verfügbar",
        });

    /// <summary>Every built-in pack, English first.</summary>
    public static IReadOnlyList<LanguagePack> All { get; } = new[] { English, German };

    /// <summary>
    /// The pack for a code, or English when the code is blank, unknown
    /// or malformed. A region variant ("de-DE") falls back to its base
    /// language ("de") before falling back to English. Never returns
    /// null and never throws.
    /// </summary>
    public static LanguagePack Get(string? code)
    {
        var normalized = Normalize(code);
        if (normalized is null)
        {
            return English;
        }

        foreach (var pack in All)
        {
            if (string.Equals(pack.Code, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return pack;
            }
        }

        var dash = normalized.IndexOf('-');
        if (dash > 0)
        {
            var baseCode = normalized[..dash];
            foreach (var pack in All)
            {
                if (string.Equals(pack.Code, baseCode, StringComparison.OrdinalIgnoreCase))
                {
                    return pack;
                }
            }
        }

        return English;
    }

    /// <summary>
    /// True when a built-in pack carries this code (after normalization),
    /// including a region variant whose base language has a pack
    /// ("de-DE" is supported because "de" is). Blank or unknown codes
    /// are not supported.
    /// </summary>
    public static bool IsSupported(string? code)
    {
        var normalized = Normalize(code);
        if (normalized is null)
        {
            return false;
        }

        foreach (var pack in All)
        {
            if (string.Equals(pack.Code, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var dash = normalized.IndexOf('-');
            if (dash > 0 && string.Equals(pack.Code, normalized[..dash], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Normalizes a code for comparison: trimmed, lowercased, or null
    /// when blank. The stored code is always the normalized form, so a
    /// hand-edited settings file that says "DE" or " de " still works.
    /// </summary>
    public static string? Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return code.Trim().ToLowerInvariant();
    }
}
