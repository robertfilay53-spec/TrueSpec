namespace TrueSpec.Core.Localization;

/// <summary>
/// The built-in language packs. English is the fallback for every key:
/// a language that misses a key shows the English text, never a blank.
/// German is the first additional language (v2.1.7): the navigation
/// rail and the Settings language card. Detail pages stay English until
/// later versions add them, and the language card says so.
/// The translations use the standard terms Windows itself uses in
/// German (Speicher for Storage, Anzeige for Display, Autostart for
/// Startup, Einstellungen for Settings), so a German-speaking buyer
/// meets familiar words, not invented ones. Brand names (TrueSpec) and
/// component abbreviations (CPU, RAM, GPU) are never translated.
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
            [AppStringKeys.LanguageNote] = "Navigation is translated. Detail pages are still in English and follow in later versions.",
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
            [AppStringKeys.LanguageNote] = "Die Navigation ist übersetzt. Detailseiten sind noch auf Englisch und folgen in späteren Versionen.",
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
