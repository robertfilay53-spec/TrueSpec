namespace TrueSpec.Core.Localization;

/// <summary>
/// The built-in language packs. English is the fallback for every key:
/// a language that misses a key shows the English text, never a blank.
/// German is the first additional language (v2.1.7): the navigation
/// rail and the Settings language card; v2.1.8 adds the whole Settings
/// page. Other detail pages stay English until later versions add them,
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
            [AppStringKeys.LanguageNote] = "Navigation and Settings are translated. Other detail pages are still in English and follow in later versions.",
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
            [AppStringKeys.LanguageNote] = "Navigation und Einstellungen sind übersetzt. Andere Detailseiten sind noch auf Englisch und folgen in späteren Versionen.",
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
