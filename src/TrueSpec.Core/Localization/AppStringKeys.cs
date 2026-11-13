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
    };
}
