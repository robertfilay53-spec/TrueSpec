namespace TrueSpec.Tests;

using TrueSpec.Core.Localization;

/// <summary>
/// Localisation foundation (v2.1.7): the string-key catalogue, the
/// built-in packs, and the fallback rules. Every test here runs on
/// both legs: the localizer is pure Core, and a broken pack must fail
/// CI, not a German user's navigation rail.
/// </summary>
public sealed class LanguagePackTests
{
    [Fact]
    public void Catalogue_HasNoDuplicateKeys()
    {
        Assert.Equal(AppStringKeys.All.Count, AppStringKeys.All.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Catalogue_CoversAllNavItems()
    {
        // The rail has 22 items; a key per item, no more, no fewer.
        var navKeys = AppStringKeys.All.Where(k => k.StartsWith("Nav", StringComparison.Ordinal)).ToList();
        Assert.Equal(22, navKeys.Count);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    public void BuiltInPack_IsValidAgainstCatalogue(string code)
    {
        var pack = LanguagePacks.Get(code);
        Assert.Empty(pack.Validate(AppStringKeys.All));
    }

    [Fact]
    public void BuiltInPacks_CoverEveryKey()
    {
        foreach (var pack in LanguagePacks.All)
        {
            foreach (var key in AppStringKeys.All)
            {
                Assert.True(pack.Strings.ContainsKey(key), $"Pack '{pack.Code}' misses key '{key}'.");
                Assert.False(string.IsNullOrWhiteSpace(pack.Strings[key]), $"Pack '{pack.Code}' has an empty value for '{key}'.");
            }
        }
    }

    [Fact]
    public void GermanPack_UsesStandardGermanUiTerms()
    {
        // Pinned deliberately: these are the terms Windows itself uses
        // in German, so a German-speaking buyer meets familiar words.
        // Changing one is a deliberate translation decision, not a typo fix.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("Übersicht", german[AppStringKeys.NavOverview]);
        Assert.Equal("Speicher", german[AppStringKeys.NavStorage]);
        Assert.Equal("Akku", german[AppStringKeys.NavBattery]);
        Assert.Equal("Anzeige", german[AppStringKeys.NavDisplay]);
        Assert.Equal("Mainboard", german[AppStringKeys.NavMotherboard]);
        Assert.Equal("Treiber", german[AppStringKeys.NavDrivers]);
        Assert.Equal("Autostart", german[AppStringKeys.NavStartup]);
        Assert.Equal("Absturzverlauf", german[AppStringKeys.NavCrash]);
        Assert.Equal("Verlauf", german[AppStringKeys.NavHistory]);
        Assert.Equal("Bericht", german[AppStringKeys.NavReport]);
        Assert.Equal("Vergleich", german[AppStringKeys.NavCompare]);
        Assert.Equal("Gerätepark", german[AppStringKeys.NavFleet]);
        Assert.Equal("Einstellungen", german[AppStringKeys.NavSettings]);
        Assert.Equal("Sprache", german[AppStringKeys.LanguageTitle]);
    }

    [Fact]
    public void GermanPack_SettingsPageTerms()
    {
        // Localisation, phase 2 (v2.1.8): the whole Settings page.
        // Same pinning rule as the rail terms above: changing one is a
        // deliberate translation decision, not a typo fix.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("EINSTELLUNGEN", german[AppStringKeys.SettingsEyebrow]);
        Assert.Equal("Einstellungen", german[AppStringKeys.SettingsTitle]);
        Assert.Equal("Darstellung", german[AppStringKeys.AppearanceTitle]);
        Assert.Equal("Dunkel (Standard)", german[AppStringKeys.ThemeDarkDefault]);
        Assert.Equal("Hell", german[AppStringKeys.ThemeLight]);
        Assert.Equal("Aktualisierungsrate der Live-Sensoren", german[AppStringKeys.RefreshTitle]);
        Assert.Equal("1 Sekunde", german[AppStringKeys.SecondsOne]);
        Assert.Equal("{0} Sekunden", german[AppStringKeys.SecondsMany]);
        Assert.Equal("Sensorprotokollierung und Warnungen", german[AppStringKeys.LoggingTitle]);
        Assert.Equal("Ein", german[AppStringKeys.ToggleOn]);
        Assert.Equal("Aus", german[AppStringKeys.ToggleOff]);
        Assert.Equal("Warnung bei CPU-Temperatur", german[AppStringKeys.AlertTitle]);
        Assert.Equal("Keine Temperaturwarnung eingestellt.", german[AppStringKeys.AlertSummaryOff]);
        Assert.Equal("Ordner für Berichtsexporte", german[AppStringKeys.ExportTitle]);
        Assert.Equal("(Standard)", german[AppStringKeys.ExportDefaultSuffix]);
        Assert.Equal("Datenspeicherort (tragbarer Modus)", german[AppStringKeys.DataLocationTitle]);
        Assert.Equal("Der tragbare Modus ist aus. Daten werden im Standardordner gespeichert.", german[AppStringKeys.PortableSummaryOff]);
        Assert.Equal("Tragbaren Modus einschalten", german[AppStringKeys.PortableActionOn]);
        Assert.Equal("Plug-ins", german[AppStringKeys.PluginsTitle]);
        Assert.Equal("Keine Plug-ins gefunden.", german[AppStringKeys.PluginsSummaryNone]);
        Assert.Equal("1 Plug-in gefunden.", german[AppStringKeys.PluginsSummaryOne]);
        Assert.Equal("{0} Plug-ins gefunden.", german[AppStringKeys.PluginsSummaryMany]);
        Assert.Equal("Datenschutz", german[AppStringKeys.PrivacyTitle]);
        Assert.Equal(
            "Navigation und Einstellungen sind übersetzt. Andere Detailseiten sind noch auf Englisch und folgen in späteren Versionen.",
            german[AppStringKeys.LanguageNote]);
    }

    [Fact]
    public void GetFormatted_FillsPlaceholdersWithInvariantCulture()
    {
        // The number keeps the app's formatting in both languages:
        // a German sentence never turns 90.5 into "90,5".
        Assert.Equal(
            "Alert when the CPU temperature reaches 90 C for 30 seconds.",
            StringLocalizer.GetFormatted(AppStringKeys.AlertSummaryOn, "en", "90", 30));
        Assert.Equal(
            "Warnung, wenn die CPU-Temperatur 90 °C für 30 Sekunden erreicht.",
            StringLocalizer.GetFormatted(AppStringKeys.AlertSummaryOn, "de", "90", 30));
        Assert.Equal("5 Plug-ins gefunden.", StringLocalizer.GetFormatted(AppStringKeys.PluginsSummaryMany, "de", 5));
    }

    [Fact]
    public void GetFormatted_FallsBackToEnglishTemplateForMissingKey()
    {
        var partial = new LanguagePack("de", "Deutsch", new Dictionary<string, string>
        {
            [AppStringKeys.NavOverview] = "Übersicht",
        });

        Assert.Equal(
            "Alert when the CPU temperature reaches 90 C for 30 seconds.",
            StringLocalizer.GetFormatted(AppStringKeys.AlertSummaryOn, partial, "90", 30));
    }

    [Fact]
    public void GetFormatted_NeverThrowsOnBadTemplateOrMissingArgs()
    {
        // A broken translation returns its template instead of
        // crashing the page: a placeholder past the arguments makes
        // string.Format throw, and the localizer hands the template
        // back untouched.
        var broken = new LanguagePack("de", "Deutsch", new Dictionary<string, string>
        {
            [AppStringKeys.AlertSummaryOn] = "Warnung {0} {1} {2}.",
        });

        Assert.Equal("Warnung {0} {1} {2}.", StringLocalizer.GetFormatted(AppStringKeys.AlertSummaryOn, broken, "90", 30));
        Assert.Equal("NoSuchKey", StringLocalizer.GetFormatted("NoSuchKey", "de", 1));
        Assert.Equal(string.Empty, StringLocalizer.GetFormatted(null, "de", 1));
        Assert.Equal("1 Plug-in gefunden.", StringLocalizer.GetFormatted(AppStringKeys.PluginsSummaryOne, "de"));
    }

    [Fact]
    public void Validate_CatchesMissingKey()
    {
        var partial = new LanguagePack("de", "Deutsch", new Dictionary<string, string>
        {
            [AppStringKeys.NavOverview] = "Übersicht",
        });

        var errors = partial.Validate(AppStringKeys.All);

        Assert.Contains(errors, e => e.Contains(AppStringKeys.NavHistory, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_CatchesUnknownKey()
    {
        var pack = new LanguagePack("de", "Deutsch", new Dictionary<string, string>
        {
            ["NotAKey"] = "x",
        });

        var errors = pack.Validate(AppStringKeys.All);

        Assert.Contains(errors, e => e.Contains("NotAKey", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_CatchesEmptyTranslationAndBadCode()
    {
        var pack = new LanguagePack("xx!", string.Empty, new Dictionary<string, string>
        {
            [AppStringKeys.NavOverview] = "   ",
        });

        var errors = pack.Validate(AppStringKeys.All);

        Assert.Contains(errors, e => e.Contains("code", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("display name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains(AppStringKeys.NavOverview, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("de", true)]
    [InlineData("de-DE", true)]
    [InlineData("e", false)]
    [InlineData("eng", false)]
    [InlineData("EN", false)]
    [InlineData("de-de", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidCode_FollowsTheDocumentedShape(string? code, bool expected)
    {
        Assert.Equal(expected, LanguagePack.IsValidCode(code));
    }

    [Theory]
    [InlineData("de", "de")]
    [InlineData("DE", "de")]
    [InlineData(" de ", "de")]
    [InlineData("de-DE", "de-de")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalize_LowercasesAndTrims(string? input, string? expected)
    {
        Assert.Equal(expected, LanguagePacks.Normalize(input));
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("de", true)]
    [InlineData("de-DE", true)]
    [InlineData("DE", true)]
    [InlineData("xx", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupported_ResolvesRegionVariants(string? code, bool expected)
    {
        Assert.Equal(expected, LanguagePacks.IsSupported(code));
    }

    [Fact]
    public void Get_ReturnsGermanPackForRegionVariant()
    {
        Assert.Same(LanguagePacks.German, LanguagePacks.Get("de-DE"));
        Assert.Same(LanguagePacks.German, LanguagePacks.Get("DE"));
    }

    [Fact]
    public void Get_FallsBackToEnglishForUnknownCode()
    {
        Assert.Same(LanguagePacks.English, LanguagePacks.Get("xx"));
        Assert.Same(LanguagePacks.English, LanguagePacks.Get(null));
        Assert.Same(LanguagePacks.English, LanguagePacks.Get("  "));
    }

    [Fact]
    public void Localizer_ReturnsGermanTextForGerman()
    {
        Assert.Equal("Übersicht", StringLocalizer.Get(AppStringKeys.NavOverview, "de"));
        Assert.Equal("Einstellungen", StringLocalizer.Get(AppStringKeys.NavSettings, "de"));
    }

    [Fact]
    public void Localizer_FallsBackToEnglishForMissingKeyInPack()
    {
        var partial = new LanguagePack("de", "Deutsch", new Dictionary<string, string>
        {
            [AppStringKeys.NavOverview] = "Übersicht",
        });

        Assert.Equal("History", StringLocalizer.Get(AppStringKeys.NavHistory, partial));
        Assert.Equal("Übersicht", StringLocalizer.Get(AppStringKeys.NavOverview, partial));
    }

    [Fact]
    public void Localizer_ReturnsKeyForUnknownKey()
    {
        Assert.Equal("NoSuchKey", StringLocalizer.Get("NoSuchKey", "de"));
        Assert.Equal("NoSuchKey", StringLocalizer.Get("NoSuchKey", "en"));
    }

    [Fact]
    public void Localizer_NeverThrowsOnGarbageInput()
    {
        Assert.Equal(string.Empty, StringLocalizer.Get(null, "de"));
        Assert.Equal(string.Empty, StringLocalizer.Get(string.Empty, "de"));
        Assert.Equal("Overview", StringLocalizer.Get(AppStringKeys.NavOverview, (LanguagePack?)null));
        Assert.Equal("Overview", StringLocalizer.Get(AppStringKeys.NavOverview, "xx"));
    }
}
