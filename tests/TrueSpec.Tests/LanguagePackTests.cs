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
    [InlineData("fr")]
    [InlineData("es")]
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
    public void FrenchPack_UsesStandardFrenchUiTerms()
    {
        // Localisation, phase 1 (v2.1.20): the French rail, the same
        // first surface German phase 1 translated. Pinned deliberately:
        // these are the terms Windows itself uses in French, so a
        // French-speaking buyer meets familiar words. Changing one is a
        // deliberate translation decision, not a typo fix. The French
        // wording is the worker's own translation, not reviewed by a
        // native speaker (the same honest limitation as the German
        // wording). The language card says the rail, the Settings page,
        // the component detail pages, the Overview page, the History
        // page, the Report page, the Tests page, the New Laptop Check
        // page, the Crash History page, the Comparison page and the
        // Fleet page are translated and every page is now translated
        // (phase 7, v2.1.26).
        var french = LanguagePacks.French.Strings;
        Assert.Equal("Vue d'ensemble", french[AppStringKeys.NavOverview]);
        Assert.Equal("Stockage", french[AppStringKeys.NavStorage]);
        Assert.Equal("Batterie", french[AppStringKeys.NavBattery]);
        Assert.Equal("Affichage", french[AppStringKeys.NavDisplay]);
        Assert.Equal("Carte mère", french[AppStringKeys.NavMotherboard]);
        Assert.Equal("Pilotes", french[AppStringKeys.NavDrivers]);
        Assert.Equal("Démarrage", french[AppStringKeys.NavStartup]);
        Assert.Equal("Historique des incidents", french[AppStringKeys.NavCrash]);
        Assert.Equal("Historique", french[AppStringKeys.NavHistory]);
        Assert.Equal("Rapport", french[AppStringKeys.NavReport]);
        Assert.Equal("Comparer", french[AppStringKeys.NavCompare]);
        Assert.Equal("Parc", french[AppStringKeys.NavFleet]);
        Assert.Equal("Paramètres", french[AppStringKeys.NavSettings]);
        Assert.Equal("Langue", french[AppStringKeys.LanguageTitle]);
        Assert.Equal(
            "La barre de navigation, la page Paramètres, les pages de détail des composants, la page Vue d'ensemble, la page Historique, la page Rapport, la page Tests, la page Vérification d'ordinateur portable neuf, la page Historique des incidents, la page Comparer et la page Parc sont traduites. Toutes les pages sont désormais traduites.",
            french[AppStringKeys.LanguageNote]);
    }

    [Fact]
    public void SpanishPack_UsesStandardSpanishUiTerms()
    {
        // Localisation, phase 1, Spanish (v2.1.27): the Spanish rail, the
        // same first surface German phase 1 and French phase 1 translated.
        // Pinned deliberately: these are the terms Windows itself uses in
        // Spanish, so a Spanish-speaking buyer meets familiar words.
        // Changing one is a deliberate translation decision, not a typo
        // fix. The Spanish wording is the worker's own translation, not
        // reviewed by a native speaker (the same honest limitation as the
        // German and French wording). The language card says the rail and
        // the card are translated and the other pages follow in later
        // versions.
        var spanish = LanguagePacks.Spanish.Strings;
        Assert.Equal("Vista general", spanish[AppStringKeys.NavOverview]);
        Assert.Equal("Almacenamiento", spanish[AppStringKeys.NavStorage]);
        Assert.Equal("Batería", spanish[AppStringKeys.NavBattery]);
        Assert.Equal("Pantalla", spanish[AppStringKeys.NavDisplay]);
        Assert.Equal("Placa base", spanish[AppStringKeys.NavMotherboard]);
        Assert.Equal("Controladores", spanish[AppStringKeys.NavDrivers]);
        Assert.Equal("Inicio", spanish[AppStringKeys.NavStartup]);
        Assert.Equal("Historial de bloqueos", spanish[AppStringKeys.NavCrash]);
        Assert.Equal("Historial", spanish[AppStringKeys.NavHistory]);
        Assert.Equal("Informe", spanish[AppStringKeys.NavReport]);
        Assert.Equal("Comparar", spanish[AppStringKeys.NavCompare]);
        Assert.Equal("Flota", spanish[AppStringKeys.NavFleet]);
        Assert.Equal("Configuración", spanish[AppStringKeys.NavSettings]);
        Assert.Equal("Idioma", spanish[AppStringKeys.LanguageTitle]);
        Assert.Equal(
            "La barra de navegación y la tarjeta de idioma están traducidas. Las demás páginas seguirán en versiones posteriores.",
            spanish[AppStringKeys.LanguageNote]);
    }

    [Fact]
    public void SpanishPack_TemplateFormattingKeepsInvariantNumbers()
    {
        // Localisation, phase 1, Spanish (v2.1.27): templates keep their
        // placeholders and format numbers with the invariant culture, so
        // a Spanish user sees the same numbers as an English one.
        var spanish = LanguagePacks.Spanish.Strings;
        Assert.Equal("{0} segundos", spanish[AppStringKeys.SecondsMany]);
        Assert.Equal(
            "Avisar cuando la temperatura de la CPU llegue a {0} °C durante {1} segundos.",
            spanish[AppStringKeys.AlertSummaryOn]);
        Assert.Equal(
            "Conjunto de datos v{0}: {1} CPU, {2} GPU, actualizado el {3}.",
            spanish[AppStringKeys.HardwareDatabaseVersion]);
        Assert.Equal("Lectura registrada el {0}", spanish[AppStringKeys.DetailCaptured]);
        Assert.Equal("+{0} pts", spanish[AppStringKeys.OverviewContribution]);
        Assert.Equal("Temperatura: {0}", spanish[AppStringKeys.LoadTestTempMeasured]);
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
        // Hardware database card (v2.1.14): the Settings page stays
        // fully translated, so the new card's strings are keys too.
        Assert.Equal("Hardware-Datenbank", german[AppStringKeys.HardwareDatabaseTitle]);
        Assert.Equal(
            "Zeigt den Nenn-Boost-Takt aus einer kleinen mitgelieferten Datenbank mit CPU- und GPU-Daten, wenn Ihr Prozessor oder Ihre Grafikkarte enthalten ist. Die Datenbank ist optional und ersetzt keinen gemessenen Wert. Wird nach einem Neustart wirksam.",
            german[AppStringKeys.HardwareDatabaseNote]);
        Assert.Equal("Datensatz v{0}: {1} CPUs, {2} GPUs, aktualisiert {3}.", german[AppStringKeys.HardwareDatabaseVersion]);
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
            "Navigation, Einstellungen, Komponentendetailseiten, die Übersichtsseite, die Verlaufsseite, die Berichtsseite, die Testseite, die Seite „Neuer Laptop Check\", die Absturzverlaufsseite, die Vergleichsseite und die Geräteparkseite sind übersetzt. Alle Seiten sind jetzt übersetzt.",
            german[AppStringKeys.LanguageNote]);
    }

    [Fact]
    public void GermanPack_DetailPageTerms()
    {
        // Localisation, phase 4 (v2.1.10): the shared component detail
        // view — one surface, all 15 component pages. Same pinning rule
        // as the rail, Settings and Core terms: changing one is a
        // deliberate translation decision, not a typo fix. The data the
        // page shows (names, headlines, spec labels and values) is not
        // in this catalogue; only the page chrome is.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("Spezifikationen", german[AppStringKeys.DetailSpecificationsTitle]);
        Assert.Equal("Spezifikationen durchsuchen", german[AppStringKeys.DetailSearchPlaceholder]);
        Assert.Equal(
            "Klicken Sie auf einen Wert, um ihn zu kopieren. Bewegen Sie die Maus über einen Wert oder Quellennamen, um zu sehen, woher der Messwert stammt.",
            german[AppStringKeys.DetailCopyHint]);
        Assert.Equal("Alterssignale", german[AppStringKeys.DetailAgeSignalsTitle]);
        Assert.Equal(
            "Die Rohwerte hinter dem Echtheitsurteil. Jedes Signal nennt seine Quelle.",
            german[AppStringKeys.DetailAgeSignalsIntro]);
        Assert.Equal("Was das bedeutet", german[AppStringKeys.DetailWhatThisMeansTitle]);
        Assert.Equal("Nicht erfasst", german[AppStringKeys.DetailNotCollectedHeadline]);
        Assert.Equal("Diese Komponente wurde noch nicht erfasst.", german[AppStringKeys.DetailNotCollectedText]);
        Assert.Equal("Messwert erfasst {0}", german[AppStringKeys.DetailCaptured]);
        Assert.Equal("Für diese Komponente wurden keine Spezifikationen erfasst.", german[AppStringKeys.DetailNoSpecs]);
        Assert.Equal(
            "Keine Spezifikationen passen zu „{0}\". Leeren Sie die Suche, um alle zu sehen.",
            german[AppStringKeys.DetailNoSpecsMatch]);
        Assert.Equal(
            "{0} Echtheitsprüfung(en) konnte(n) nicht ausgeführt werden, weil ein Signal nicht gemeldet wurde.",
            german[AppStringKeys.DetailAgeSignalsSkipped]);
        Assert.Equal(
            "Nicht vertrauenswürdig: Eine andere Prüfung hat bewiesen, dass dieses Datum unmöglich ist.",
            german[AppStringKeys.DetailSignalUntrusted]);
        Assert.Equal(" (geschätzt)", german[AppStringKeys.DetailEstimateSuffix]);
    }

    [Fact]
    public void FrenchPack_DetailPageTerms()
    {
        // Localisation, phase 4, French (v2.1.23): the shared component
        // detail view — one surface, all 15 component pages. Same
        // pinning rule as the German phase 4 terms: changing one is a
        // deliberate translation decision, not a typo fix. The data the
        // page shows (names, headlines, spec labels and values) is not
        // in this catalogue; only the page chrome is. No new keys — the
        // French pack already covers the whole 523-key catalogue.
        var french = LanguagePacks.French.Strings;
        Assert.Equal("Caractéristiques", french[AppStringKeys.DetailSpecificationsTitle]);
        Assert.Equal("Rechercher dans les caractéristiques", french[AppStringKeys.DetailSearchPlaceholder]);
        Assert.Equal(
            "Cliquez sur une valeur pour la copier. Survolez une valeur ou un nom de source pour voir d'où provient la mesure.",
            french[AppStringKeys.DetailCopyHint]);
        Assert.Equal("Signaux d'âge", french[AppStringKeys.DetailAgeSignalsTitle]);
        Assert.Equal(
            "Les chiffres bruts derrière le verdict d'authenticité. Chaque signal indique où il a été lu.",
            french[AppStringKeys.DetailAgeSignalsIntro]);
        Assert.Equal("Ce que cela signifie", french[AppStringKeys.DetailWhatThisMeansTitle]);
        Assert.Equal("Non collecté", french[AppStringKeys.DetailNotCollectedHeadline]);
        Assert.Equal("Ce composant n'a pas encore été collecté.", french[AppStringKeys.DetailNotCollectedText]);
        Assert.Equal("Mesure enregistrée le {0}", french[AppStringKeys.DetailCaptured]);
        Assert.Equal("Aucune caractéristique n'a été collectée pour ce composant.", french[AppStringKeys.DetailNoSpecs]);
        Assert.Equal(
            "Aucune caractéristique ne correspond à « {0} ». Effacez la recherche pour toutes les voir.",
            french[AppStringKeys.DetailNoSpecsMatch]);
        Assert.Equal(
            "{0} contrôle(s) d'authenticité n'ont pas pu être exécutés car un signal n'a pas été signalé.",
            french[AppStringKeys.DetailAgeSignalsSkipped]);
        Assert.Equal(
            "Non fiable : un autre contrôle a prouvé que cette date est impossible.",
            french[AppStringKeys.DetailSignalUntrusted]);
        Assert.Equal(" (ESTIMATION)", french[AppStringKeys.DetailEstimateSuffix]);
    }

    [Fact]
    public void GermanPack_OverviewPageTerms()
    {
        // Localisation, phase 5 (v2.1.11): the Overview page — the
        // landing page, the first screen a user sees. Same pinning rule
        // as the rail, Settings, Core and detail terms: changing one is
        // a deliberate translation decision, not a typo fix. The data
        // the page shows (component names, headlines, health and status
        // texts, spec labels, values and sources, the score explanation
        // and the verdict text) is not in this catalogue; only the page
        // chrome is.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("FÜR VOLLSTÄNDIGE ERGEBNISSE ALS ADMINISTRATOR AUSFÜHREN", german[AppStringKeys.OverviewAdminEyebrow]);
        Assert.Equal("von 100", german[AppStringKeys.OverviewOutOf100]);
        Assert.Equal("Gesamtzustand", german[AppStringKeys.OverviewOverallHealth]);
        Assert.Equal(
            "Die Punktzahl ist der gewichtete Durchschnitt der messbaren Komponenten. Eine Komponente ohne echten Gesundheitswert zählt nicht.",
            german[AppStringKeys.OverviewScoreNote]);
        Assert.Equal("Neuen Laptop prüfen", german[AppStringKeys.OverviewNewLaptopCheck]);
        Assert.Equal("Bericht exportieren", german[AppStringKeys.OverviewExportReport]);
        Assert.Equal("Komponentenzustand", german[AppStringKeys.OverviewComponentHealth]);
        Assert.Equal("Zustand", german[AppStringKeys.OverviewHealthLabel]);
        Assert.Equal("Details", german[AppStringKeys.OverviewDetails]);
        Assert.Equal(
            "Bei diesem Scan wurden keine Komponenten erfasst. Aktualisieren Sie den Scan über die Statusleiste; wenn der Abschnitt leer bleibt, konnten die Collector diesen Rechner nicht lesen.",
            german[AppStringKeys.OverviewNoComponents]);
        Assert.Equal("Live-Sensoren", german[AppStringKeys.OverviewLiveSensors]);
        Assert.Equal("Kein Sensormesswert", german[AppStringKeys.OverviewNoSensorReading]);
        Assert.Equal("TEMPERATURWARNUNG", german[AppStringKeys.OverviewAlertEyebrow]);
        Assert.Equal(
            "Warnungen lösen erst aus, wenn die Temperatur die volle von Ihnen eingestellte Dauer über dem Schwellenwert bleibt. Ändern Sie den Schwellenwert in den Einstellungen.",
            german[AppStringKeys.OverviewAlertNote]);
        Assert.Equal("Jeder Messwert behält seine Quelle. Keine Telemetrie, keine Netzwerkaufrufe.", german[AppStringKeys.OverviewFooter]);
        Assert.Equal("Scan läuft…", german[AppStringKeys.OverviewScanning]);
        Assert.Equal("Der Hardware-Scan läuft noch. Die Punktzahl erscheint, wenn der Scan fertig ist.", german[AppStringKeys.OverviewScanningNote]);
        Assert.Equal("Sensorprotokollierung ist auf diesem Rechner nicht verfügbar.", german[AppStringKeys.OverviewSensorLogUnavailable]);
        Assert.Equal("Sensormesswerte werden in {0} aufgezeichnet. Das Diagramm ist auf der Verlaufsseite.", german[AppStringKeys.OverviewSensorLogOn]);
        Assert.Equal("Sensorprotokollierung ist aus. Schalten Sie sie in den Einstellungen ein, um Messwerte aufzuzeichnen.", german[AppStringKeys.OverviewSensorLogOff]);
        Assert.Equal("Warnung, wenn die CPU-Temperatur {0} °C für {1} Sekunden erreicht. Stellen Sie sie in den Einstellungen ein.", german[AppStringKeys.OverviewAlertSet]);
        Assert.Equal("Keine Temperaturwarnung eingestellt. TrueSpec wird ohne Schwellenwert ausgeliefert; stellen Sie einen in den Einstellungen ein, wenn Sie eine Warnung möchten.", german[AppStringKeys.OverviewAlertNotSet]);
        Assert.Equal("Live-Sensoren werden alle {0} neu gelesen. Werte erscheinen nur, wenn LibreHardwareMonitor sie meldet.", german[AppStringKeys.OverviewRefreshText]);
        Assert.Equal("Live-Sensoren aktualisiert um {0}", german[AppStringKeys.OverviewLiveUpdated]);
        Assert.Equal("Lesen der Live-Sensoren fehlgeschlagen: {0}", german[AppStringKeys.OverviewLiveReadFailed]);
        Assert.Equal("Gewicht {0}", german[AppStringKeys.OverviewWeight]);
        Assert.Equal("+{0} Pkt.", german[AppStringKeys.OverviewContribution]);
        Assert.Equal("CPU-Temperatur", german[AppStringKeys.OverviewKeyCpuTemp]);
        Assert.Equal("Arbeitsspeicher", german[AppStringKeys.OverviewKeyMemory]);
        Assert.Equal("Datenträgerzustand", german[AppStringKeys.OverviewKeyDiskHealth]);
        Assert.Equal("Akkuzustand", german[AppStringKeys.OverviewKeyBatteryHealth]);
        Assert.Equal("CPU-Temperatur", german[AppStringKeys.OverviewMetricCpuTemp]);
        Assert.Equal("GPU-Temperatur", german[AppStringKeys.OverviewMetricGpuTemp]);
        Assert.Equal("CPU-Auslastung", german[AppStringKeys.OverviewMetricCpuUsage]);
        Assert.Equal("Akkuleistung", german[AppStringKeys.OverviewMetricBatteryPower]);
        Assert.Equal("Nicht verfügbar", german[AppStringKeys.OverviewMetricNotAvailable]);

        // Templates keep the app's number formatting in German too.
        Assert.Equal(
            "Warnung, wenn die CPU-Temperatur 90 °C für 30 Sekunden erreicht. Stellen Sie sie in den Einstellungen ein.",
            StringLocalizer.GetFormatted(AppStringKeys.OverviewAlertSet, "de", "90", 30));
        Assert.Equal(
            "Sensormesswerte werden in C:\\TrueSpec\\sensor-log.csv aufgezeichnet. Das Diagramm ist auf der Verlaufsseite.",
            StringLocalizer.GetFormatted(AppStringKeys.OverviewSensorLogOn, "de", @"C:\TrueSpec\sensor-log.csv"));
        Assert.Equal("Gewicht 2.5", StringLocalizer.GetFormatted(AppStringKeys.OverviewWeight, "de", "2.5"));
        Assert.Equal("+5 Pkt.", StringLocalizer.GetFormatted(AppStringKeys.OverviewContribution, "de", "5"));
    }

    [Fact]
    public void FrenchPack_OverviewPageTerms()
    {
        // Localisation, phase 5, French (v2.1.24): the Overview page —
        // the landing page, the first screen a user sees. Same pinning
        // rule as the German phase 5 terms: changing one is a
        // deliberate translation decision, not a typo fix. The data
        // the page shows (component names, headlines, health and status
        // texts, spec labels, values and sources, the score explanation
        // and the verdict text) is not in this catalogue; only the page
        // chrome is. No new keys — the French pack already covers the
        // whole 523-key catalogue.
        var french = LanguagePacks.French.Strings;
        Assert.Equal("EXÉCUTER EN TANT QU'ADMINISTRATEUR POUR DES RÉSULTATS COMPLETS", french[AppStringKeys.OverviewAdminEyebrow]);
        Assert.Equal("sur 100", french[AppStringKeys.OverviewOutOf100]);
        Assert.Equal("Santé globale", french[AppStringKeys.OverviewOverallHealth]);
        Assert.Equal(
            "Score = moyenne pondérée des composants mesurables. Un composant sans véritable nombre de santé ne compte pas.",
            french[AppStringKeys.OverviewScoreNote]);
        Assert.Equal("Vérification d'ordinateur portable neuf", french[AppStringKeys.OverviewNewLaptopCheck]);
        Assert.Equal("Exporter le rapport", french[AppStringKeys.OverviewExportReport]);
        Assert.Equal("Santé des composants", french[AppStringKeys.OverviewComponentHealth]);
        Assert.Equal("Santé", french[AppStringKeys.OverviewHealthLabel]);
        Assert.Equal("Détails", french[AppStringKeys.OverviewDetails]);
        Assert.Equal(
            "Aucun composant n'a été collecté dans ce scan. Actualisez le scan depuis la barre d'état ; si la section reste vide, les collecteurs n'ont pas pu lire cette machine.",
            french[AppStringKeys.OverviewNoComponents]);
        Assert.Equal("Capteurs en direct", french[AppStringKeys.OverviewLiveSensors]);
        Assert.Equal("Aucune mesure de capteur", french[AppStringKeys.OverviewNoSensorReading]);
        Assert.Equal("ALERTE DE TEMPÉRATURE", french[AppStringKeys.OverviewAlertEyebrow]);
        Assert.Equal(
            "Les alertes ne se déclenchent que lorsque la température reste au-dessus de votre seuil pendant toute la durée définie. Modifiez le seuil dans les Paramètres.",
            french[AppStringKeys.OverviewAlertNote]);
        Assert.Equal("Chaque mesure conserve sa source. Aucune télémétrie, aucun appel réseau.", french[AppStringKeys.OverviewFooter]);
        Assert.Equal("Analyse en cours…", french[AppStringKeys.OverviewScanning]);
        Assert.Equal("L'analyse matérielle est toujours en cours. Le score apparaît lorsque l'analyse se termine.", french[AppStringKeys.OverviewScanningNote]);
        Assert.Equal("La journalisation des capteurs n'est pas disponible sur cette machine.", french[AppStringKeys.OverviewSensorLogUnavailable]);
        Assert.Equal("Les mesures des capteurs sont enregistrées dans {0}. Le graphique est sur la page Historique.", french[AppStringKeys.OverviewSensorLogOn]);
        Assert.Equal("La journalisation des capteurs est désactivée. Activez-la dans les Paramètres pour enregistrer les mesures.", french[AppStringKeys.OverviewSensorLogOff]);
        Assert.Equal("Alerte lorsque la température du processeur atteint {0} °C pendant {1} secondes. Définissez-la dans les Paramètres.", french[AppStringKeys.OverviewAlertSet]);
        Assert.Equal("Aucune alerte de température définie. TrueSpec est livré sans seuil ; définissez-en un dans les Paramètres si vous voulez une alerte.", french[AppStringKeys.OverviewAlertNotSet]);
        Assert.Equal("Les capteurs en direct s'actualisent toutes les {0}. Les valeurs n'apparaissent que lorsque LibreHardwareMonitor les signale.", french[AppStringKeys.OverviewRefreshText]);
        Assert.Equal("Capteurs en direct mis à jour {0}", french[AppStringKeys.OverviewLiveUpdated]);
        Assert.Equal("Échec de la lecture des capteurs en direct : {0}", french[AppStringKeys.OverviewLiveReadFailed]);
        Assert.Equal("Poids {0}", french[AppStringKeys.OverviewWeight]);
        Assert.Equal("+{0} pts", french[AppStringKeys.OverviewContribution]);
        Assert.Equal("Température du processeur", french[AppStringKeys.OverviewKeyCpuTemp]);
        Assert.Equal("Mémoire", french[AppStringKeys.OverviewKeyMemory]);
        Assert.Equal("Santé du disque", french[AppStringKeys.OverviewKeyDiskHealth]);
        Assert.Equal("Santé de la batterie", french[AppStringKeys.OverviewKeyBatteryHealth]);
        Assert.Equal("Temp. processeur", french[AppStringKeys.OverviewMetricCpuTemp]);
        Assert.Equal("Temp. GPU", french[AppStringKeys.OverviewMetricGpuTemp]);
        Assert.Equal("Utilisation du processeur", french[AppStringKeys.OverviewMetricCpuUsage]);
        Assert.Equal("Puissance de la batterie", french[AppStringKeys.OverviewMetricBatteryPower]);
        Assert.Equal("Non disponible", french[AppStringKeys.OverviewMetricNotAvailable]);

        // Templates keep the app's number formatting in French too.
        Assert.Equal(
            "Alerte lorsque la température du processeur atteint 90 °C pendant 30 secondes. Définissez-la dans les Paramètres.",
            StringLocalizer.GetFormatted(AppStringKeys.OverviewAlertSet, "fr", "90", 30));
        Assert.Equal(
            "Les mesures des capteurs sont enregistrées dans C:\\TrueSpec\\sensor-log.csv. Le graphique est sur la page Historique.",
            StringLocalizer.GetFormatted(AppStringKeys.OverviewSensorLogOn, "fr", @"C:\TrueSpec\sensor-log.csv"));
        Assert.Equal("Poids 2.5", StringLocalizer.GetFormatted(AppStringKeys.OverviewWeight, "fr", "2.5"));
        Assert.Equal("+5 pts", StringLocalizer.GetFormatted(AppStringKeys.OverviewContribution, "fr", "5"));
    }

    [Fact]
    public void GermanPack_HistoryPageTerms()
    {
        // Localisation, phase 6 (v2.1.12): the History page — the
        // trend cards, the saved-scans table, the two-machine
        // comparison and the value comparison, plus the CPU temperature
        // log. Same pinning rule as the rail, Settings, Core, detail
        // and Overview terms: changing one is a deliberate translation
        // decision, not a typo fix. The data the page shows (machine
        // names, trend summary and forecast texts, the value verdict
        // sentence and its no-verdict reason, the table's cell
        // fallbacks) is not in this catalogue; only the page chrome
        // is. The range labels reuse the table header keys instead of
        // a second copy of the same words.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("VERLAUF", german[AppStringKeys.HistoryEyebrow]);
        Assert.Equal("Wie sich dieses Gerät im Laufe der Zeit verändert", german[AppStringKeys.HistoryTitle]);
        Assert.Equal(
            "Jeder Scan wird in einer lokalen Datenbank auf diesem Gerät gespeichert. TrueSpec zeigt den Verlauf von Gesamtpunktzahl, Akkugesundheit und Speicherverschleiß über diese Scans. Ein Wert, den ein Scan nicht gemeldet hat, ist eine Lücke, niemals eine Null.",
            german[AppStringKeys.HistoryIntro]);
        Assert.Equal("Gerät", german[AppStringKeys.HistoryMachineTitle]);
        Assert.Equal("Gerät auswählen", german[AppStringKeys.HistoryPickMachine]);
        Assert.Equal(
            "Der Verlauf wird jeweils für ein Gerät angezeigt. Wählen Sie ein anderes Gerät, um dessen Verlauf zu sehen.",
            german[AppStringKeys.HistoryMachineNote]);
        Assert.Equal("WARNUNG ZUR AKKUGESUNDHEIT", german[AppStringKeys.HistoryBatteryAlertEyebrow]);
        Assert.Equal("Gesamtpunktzahl", german[AppStringKeys.HistoryScoreTitle]);
        Assert.Equal("Akkugesundheit", german[AppStringKeys.HistoryBatteryTitle]);
        Assert.Equal("Speicherverschleiß", german[AppStringKeys.HistoryWearTitle]);
        Assert.Equal("PROGNOSE", german[AppStringKeys.HistoryForecastEyebrow]);
        Assert.Equal("CPU-Temperaturprotokoll", german[AppStringKeys.HistoryCpuLogTitle]);
        Assert.Equal("Diagramm aktualisieren", german[AppStringKeys.HistoryRefreshChart]);
        Assert.Equal(
            "Die horizontale Achse ist die Zeit, nicht die Anzahl der Messwerte. Lücken in der Linie sind Messwerte, bei denen der CPU-Temperatursensor nichts gemeldet hat, oder Zeiträume, in denen die Protokollierung nicht lief (die App war geschlossen oder das Gerät im Ruhezustand). Die Linie verbindet niemals über eine Lücke hinweg. Die gestrichelte Linie ist Ihr Warnschwellenwert, sofern einer festgelegt ist. Das Protokoll ist eine lokale CSV-Datei unter %LocalAppData%\\TrueSpec; nichts wird hochgeladen.",
            german[AppStringKeys.HistoryChartCaption]);
        Assert.Equal("TREIBERALTER", german[AppStringKeys.HistoryDriverAgeEyebrow]);
        Assert.Equal("Gespeicherte Scans", german[AppStringKeys.HistorySavedScansTitle]);
        Assert.Equal("Verlauf als CSV exportieren", german[AppStringKeys.HistoryExportCsv]);
        Assert.Equal(
            "Älteste zuerst. Ein Strich bedeutet, dass dieser Scan den Wert nicht gemeldet hat. „Entfernen\" löscht genau diesen einen Scan aus diesem Datenordner — der erste Klick fragt nach, der zweite löscht.",
            german[AppStringKeys.HistoryTableNote]);
        Assert.Equal("Gescannt", german[AppStringKeys.HistoryHeaderScanned]);
        Assert.Equal("Punktzahl", german[AppStringKeys.HistoryHeaderScore]);
        Assert.Equal("Zyklen", german[AppStringKeys.HistoryHeaderCycles]);
        Assert.Equal("Betriebsstunden", german[AppStringKeys.HistoryHeaderPowerOnHours]);
        Assert.Equal("Entfernen", german[AppStringKeys.HistoryRemoveButton]);
        Assert.Equal("Entfernen bestätigen", german[AppStringKeys.HistoryConfirmRemoveButton]);
        Assert.Equal("Zwei Geräte vergleichen", german[AppStringKeys.HistoryCompareTitle]);
        Assert.Equal("Unterschiedliche Geräte — nebeneinander angezeigt, niemals in einem Verlauf vermischt.", german[AppStringKeys.HistoryCompareNote]);
        Assert.Equal("Gerät A", german[AppStringKeys.HistoryMachineA]);
        Assert.Equal("Gerät B", german[AppStringKeys.HistoryMachineB]);
        Assert.Equal("Vergleichen", german[AppStringKeys.HistoryCompareButton]);
        Assert.Equal("Welches ist der bessere Kauf?", german[AppStringKeys.HistoryValueTitle]);
        Assert.Equal(
            "Geben Sie den Angebotspreis jedes Verkäufers ein. Der Preis stammt von Ihnen, ist nicht gemessen und wird nur für diesen Vergleich verwendet. Er wird niemals mit dem Scan gespeichert.",
            german[AppStringKeys.HistoryValueIntro]);
        Assert.Equal("Angebotspreis (Sie geben ihn ein, nicht gemessen)", german[AppStringKeys.HistoryAskingPriceLabel]);
        Assert.Equal("Zum Beispiel 500", german[AppStringKeys.HistoryPriceExample]);
        Assert.Equal("Wert vergleichen", german[AppStringKeys.HistoryCompareValueButton]);
        Assert.Equal("Sieger pro Komponente (nur gemessene Gesundheit)", german[AppStringKeys.HistoryWinnersTitle]);
        Assert.Equal("Wie sich {0} im Laufe der Zeit verändert", german[AppStringKeys.HistorySubtitleTemplate]);
        Assert.Equal("Scanverlauf wird geladen …", german[AppStringKeys.HistoryLoading]);
        Assert.Equal("Der Scanverlauf ist auf diesem Gerät nicht verfügbar, es gibt also noch nichts zu zeigen.", german[AppStringKeys.HistoryUnavailable]);
        Assert.Equal("Noch keine gespeicherten Scans. Führen Sie einen Scan aus (aktualisieren) und kommen Sie zurück: Jeder Scan wird im lokalen Verlauf gespeichert.", german[AppStringKeys.HistoryNoScans]);
        Assert.Equal("Bisher ein Scan gespeichert. Führen Sie einen weiteren Scan aus (aktualisieren) und kommen Sie für den Verlauf zurück.", german[AppStringKeys.HistoryOneScan]);
        Assert.Equal("{0} Scans im Verlauf, älteste zuerst.", german[AppStringKeys.HistoryScansCount]);
        Assert.Equal("Der Verlauf konnte nicht gelesen werden: {0}", german[AppStringKeys.HistoryReadFailed]);
        Assert.Equal("Entfernen ist nicht verfügbar: Der Scanverlauf ist auf diesem Gerät nicht verfügbar.", german[AppStringKeys.HistoryRemoveUnavailable]);
        Assert.Equal("Den Scan vom {0} entfernen? Dadurch wird genau dieser eine gespeicherte Scan aus diesem Datenordner gelöscht. Klicken Sie auf „Entfernen bestätigen\", um zu löschen — bis dahin wird nichts gelöscht.", german[AppStringKeys.HistoryRemoveConfirm]);
        Assert.Equal("Nichts zu entfernen: Dieser Scan war bereits weg.", german[AppStringKeys.HistoryRemoveGone]);
        Assert.Equal("Den Scan vom {0} entfernt. Es sind keine gespeicherten Scans mehr vorhanden.", german[AppStringKeys.HistoryRemovedNone]);
        Assert.Equal("Den Scan vom {0} entfernt. Es verbleibt 1 Scan.", german[AppStringKeys.HistoryRemovedOne]);
        Assert.Equal("Den Scan vom {0} entfernt. Es verbleiben {1} Scans.", german[AppStringKeys.HistoryRemovedMany]);
        Assert.Equal("Entfernen fehlgeschlagen: {0}", german[AppStringKeys.HistoryRemoveFailed]);
        Assert.Equal("Der Scanverlauf ist auf diesem Gerät nicht verfügbar, es gibt also noch nichts zu vergleichen.", german[AppStringKeys.HistoryCompareUnavailable]);
        Assert.Equal("Bisher ist nur ein Gerät im Verlauf. Führen Sie einen Scan auf einem zweiten Gerät aus, dann erscheint dessen Verlauf hier zum Vergleich.", german[AppStringKeys.HistoryOnlyOneMachine]);
        Assert.Equal("Wählen Sie zwei unterschiedliche Geräte.", german[AppStringKeys.HistoryPickDifferent]);
        Assert.Equal("Wählen Sie zwei Geräte und vergleichen Sie sie. Unterschiedliche Geräte werden nebeneinander angezeigt, niemals in einem Verlauf vermischt.", german[AppStringKeys.HistoryPickTwoCompare]);
        Assert.Equal("Wählen Sie zwei Geräte zum Vergleichen.", german[AppStringKeys.HistoryPickTwo]);
        Assert.Equal("Geräte werden verglichen …", german[AppStringKeys.HistoryComparing]);
        Assert.Equal("{0} mit {1} verglichen. Unterschiedliche Geräte werden nebeneinander angezeigt, niemals in einem Verlauf vermischt.", german[AppStringKeys.HistoryCompared]);
        Assert.Equal("Vergleich fehlgeschlagen: {0}", german[AppStringKeys.HistoryCompareFailed]);
        Assert.Equal("Geben Sie den Angebotspreis jedes Verkäufers ein, um den Wert zu vergleichen.", german[AppStringKeys.HistoryValuePrompt]);
        Assert.Equal("Vergleichen Sie zuerst zwei Geräte und geben Sie dann die Angebotspreise ein.", german[AppStringKeys.HistoryValueCompareFirst]);
        Assert.Equal("Geben Sie für jedes Gerät einen Angebotspreis über null ein (nur Zahlen, zum Beispiel 500). Der Preis stammt von Ihnen, ist nicht gemessen.", german[AppStringKeys.HistoryValueBadPrice]);
        Assert.Equal("Die Preise stammen von Ihnen, sind nicht gemessen. Die Pro-Dollar-Zahl ist eine Rechnung aus der gemessenen Punktzahl und dem von Ihnen eingegebenen Preis, gekennzeichnet als „geschätzt\". Es werden keine Marktpreisdaten verwendet.", german[AppStringKeys.HistoryValueHonestyNote]);
        Assert.Equal("Scans im Verlauf", german[AppStringKeys.HistoryRowScans]);
        Assert.Equal("Erster Scan", german[AppStringKeys.HistoryRowFirstScan]);
        Assert.Equal("Letzter Scan", german[AppStringKeys.HistoryRowLastScan]);
        Assert.Equal("Neueste Punktzahl", german[AppStringKeys.HistoryRowLatestScore]);
        Assert.Equal("Neueste Akkugesundheit", german[AppStringKeys.HistoryRowLatestBattery]);
        Assert.Equal("Neuester Speicherverschleiß", german[AppStringKeys.HistoryRowLatestWear]);
        Assert.Equal("Neuestes ältestes Treiberdatum", german[AppStringKeys.HistoryRowLatestDriver]);
        Assert.Equal("Letzte Updateinstallation", german[AppStringKeys.HistoryRowLastUpdate]);
        Assert.Equal("Punktzahl pro 1 $ (geschätzt)", german[AppStringKeys.HistoryValueScorePerDollar]);
        Assert.Equal("Gerät A gewinnt ({0} vs {1})", german[AppStringKeys.HistoryWinnerA]);
        Assert.Equal("Gerät B gewinnt ({0} vs {1})", german[AppStringKeys.HistoryWinnerB]);
        Assert.Equal("Gleichstand ({0} vs {1})", german[AppStringKeys.HistoryWinnerTie]);
        Assert.Equal("Auf einem oder beiden Geräten nicht messbar", german[AppStringKeys.HistoryWinnerNotMeasurable]);
        Assert.Equal("{0}: in keinem gespeicherten Scan gemeldet.", german[AppStringKeys.HistoryRangeNone]);
        Assert.Equal("{0}: {1} (nur ein Scan).", german[AppStringKeys.HistoryRangeOne]);
        Assert.Equal("{0}: {1} bis {2} über {3} Scans.", german[AppStringKeys.HistoryRangeMany]);
        Assert.Equal("Sensorprotokoll wird geladen …", german[AppStringKeys.HistoryLogLoading]);
        Assert.Equal("Die Sensorprotokollierung ist aus. Schalten Sie sie in den Einstellungen ein, dann erscheint hier das CPU-Temperaturdiagramm.", german[AppStringKeys.HistoryLogOff]);
        Assert.Equal("Noch keine Sensormesswerte aufgezeichnet. Lassen Sie TrueSpec laufen, dann erscheint hier das CPU-Temperaturdiagramm.", german[AppStringKeys.HistoryLogNoSamples]);
        Assert.Equal("Das Protokoll dieses Geräts hat keinen CPU-Temperatursensor, es gibt also nichts zu zeichnen.", german[AppStringKeys.HistoryLogNoCpuSensor]);
        Assert.Equal("In {0} Messwerten wurden keine Temperaturwerte aufgezeichnet: Die Hardware dieses Geräts meldet keinen CPU-Temperatursensor. Das ist bei Geräten zu erwarten, deren Sensor nicht offengelegt wird, es gibt also kein CPU-Temperaturdiagramm zu zeigen.", german[AppStringKeys.HistoryLogNoReadings]);
        Assert.Equal("In {0} Messwerten wurde nur ein Temperaturwert aufgezeichnet, es gibt also nicht genug Daten für ein Diagramm.", german[AppStringKeys.HistoryLogOneReading]);
        Assert.Equal("Die Temperaturwerte liegen zeitlich zu weit auseinander, um eine Linie zu zeichnen: Jeder Wert steht allein, weil die Protokollierung dazwischen nicht lief.", german[AppStringKeys.HistoryLogTooFarApart]);
        Assert.Equal("CPU-Temperatur, {0} Messwerte von {1} bis {2}.", german[AppStringKeys.HistoryLogSummary]);
        Assert.Equal("Die neuesten {0} von {1} Messwerten werden gezeigt, {2}.", german[AppStringKeys.HistoryLogSummaryNewest]);
        Assert.Equal("{0} Messwerte, {1}.", german[AppStringKeys.HistoryLogSummaryAll]);
        Assert.Equal("keine Lücken", german[AppStringKeys.HistoryLogGapNone]);
        Assert.Equal("1 Lücke, in der der Sensor nichts gemeldet hat", german[AppStringKeys.HistoryLogGapOne]);
        Assert.Equal("{0} Lücken, in denen der Sensor nichts gemeldet hat", german[AppStringKeys.HistoryLogGapMany]);
        Assert.Equal("Das Sensorprotokoll konnte nicht gelesen werden: {0}", german[AppStringKeys.HistoryLogReadFailed]);

        // Templates keep the app's number formatting in German too.
        Assert.Equal(
            "Wie sich Test Laptop im Laufe der Zeit verändert",
            StringLocalizer.GetFormatted(AppStringKeys.HistorySubtitleTemplate, "de", "Test Laptop"));
        Assert.Equal(
            "2 Scans im Verlauf, älteste zuerst.",
            StringLocalizer.GetFormatted(AppStringKeys.HistoryScansCount, "de", 2));
        Assert.Equal(
            "Den Scan vom 2026-11-10 09:00 entfernt. Es verbleiben 2 Scans.",
            StringLocalizer.GetFormatted(AppStringKeys.HistoryRemovedMany, "de", "2026-11-10 09:00", 2));
        Assert.Equal(
            "Punktzahl: 90 bis 85 über 2 Scans.",
            StringLocalizer.GetFormatted(AppStringKeys.HistoryRangeMany, "de", "Punktzahl", "90", "85", 2));
        Assert.Equal(
            "In 3 Messwerten wurden keine Temperaturwerte aufgezeichnet: Die Hardware dieses Geräts meldet keinen CPU-Temperatursensor. Das ist bei Geräten zu erwarten, deren Sensor nicht offengelegt wird, es gibt also kein CPU-Temperaturdiagramm zu zeigen.",
            StringLocalizer.GetFormatted(AppStringKeys.HistoryLogNoReadings, "de", 3));
    }

    [Fact]
    public void FrenchPack_HistoryPageTerms()
    {
        // Localisation, phase 6, French (v2.1.25): the History page —
        // the trend cards, the saved-scans table, the two-machine
        // comparison and the value comparison, plus the CPU temperature
        // log. Same pinning rule as the German phase 6 terms: changing
        // one is a deliberate translation decision, not a typo fix. The
        // data the page shows (machine names, trend summary and
        // forecast texts, the value verdict sentence and its no-verdict
        // reason, the table's cell fallbacks) is not in this catalogue;
        // only the page chrome is. The range labels reuse the table
        // header keys instead of a second copy of the same words. No
        // new keys — the French pack already covers the whole 523-key
        // catalogue.
var french = LanguagePacks.French.Strings;
        Assert.Equal("HISTORIQUE", french[AppStringKeys.HistoryEyebrow]);
        Assert.Equal("Comment cette machine évolue au fil du temps", french[AppStringKeys.HistoryTitle]);
        Assert.Equal(
            "Chaque scan est enregistré dans une base de données locale sur cette machine. TrueSpec suit l'évolution du score global, de la santé de la batterie et de l'usure du stockage au fil de ces scans. Une valeur qu'un scan n'a pas signalée est un trou, jamais un zéro.",
            french[AppStringKeys.HistoryIntro]);
        Assert.Equal("Machine", french[AppStringKeys.HistoryMachineTitle]);
        Assert.Equal("Choisir une machine", french[AppStringKeys.HistoryPickMachine]);
        Assert.Equal(
            "L'historique est affiché pour une machine à la fois. Choisissez une autre machine pour voir sa propre tendance.",
            french[AppStringKeys.HistoryMachineNote]);
        Assert.Equal("ALERTE DE SANTÉ DE LA BATTERIE", french[AppStringKeys.HistoryBatteryAlertEyebrow]);
        Assert.Equal("Score global", french[AppStringKeys.HistoryScoreTitle]);
        Assert.Equal("Santé de la batterie", french[AppStringKeys.HistoryBatteryTitle]);
        Assert.Equal("Usure du stockage", french[AppStringKeys.HistoryWearTitle]);
        Assert.Equal("PRÉVISION", french[AppStringKeys.HistoryForecastEyebrow]);
        Assert.Equal("Journal de température du processeur", french[AppStringKeys.HistoryCpuLogTitle]);
        Assert.Equal("Actualiser le graphique", french[AppStringKeys.HistoryRefreshChart]);
        Assert.Equal(
            "L'axe horizontal est le temps, et non le nombre d'échantillons. Les trous dans la ligne sont des échantillons où le capteur de température du processeur n'a pas signalé, ou des périodes où la journalisation ne fonctionnait pas (l'application était fermée ou la machine en veille). La ligne ne relie jamais deux points séparés par un trou. La ligne pointillée est votre seuil d'alerte, lorsqu'un seuil est défini. Le journal est un fichier CSV local sous %LocalAppData%\\TrueSpec ; rien n'est envoyé.",
            french[AppStringKeys.HistoryChartCaption]);
        Assert.Equal("ÂGE DES PILOTES", french[AppStringKeys.HistoryDriverAgeEyebrow]);
        Assert.Equal("Scans enregistrés", french[AppStringKeys.HistorySavedScansTitle]);
        Assert.Equal("Exporter l'historique en CSV", french[AppStringKeys.HistoryExportCsv]);
        Assert.Equal(
            "Du plus ancien au plus récent. Un tiret signifie que ce scan n'a pas signalé la valeur. Supprimer efface ce scan de ce dossier de données — le premier clic demande confirmation, le second supprime.",
            french[AppStringKeys.HistoryTableNote]);
        Assert.Equal("Date du scan", french[AppStringKeys.HistoryHeaderScanned]);
        Assert.Equal("Score", french[AppStringKeys.HistoryHeaderScore]);
        Assert.Equal("Cycles", french[AppStringKeys.HistoryHeaderCycles]);
        Assert.Equal("Heures de fonctionnement", french[AppStringKeys.HistoryHeaderPowerOnHours]);
        Assert.Equal("Supprimer", french[AppStringKeys.HistoryRemoveButton]);
        Assert.Equal("Confirmer la suppression", french[AppStringKeys.HistoryConfirmRemoveButton]);
        Assert.Equal("Comparer deux machines", french[AppStringKeys.HistoryCompareTitle]);
        Assert.Equal("Des machines différentes — affichées côte à côte, jamais mélangées dans une même tendance.", french[AppStringKeys.HistoryCompareNote]);
        Assert.Equal("Machine A", french[AppStringKeys.HistoryMachineA]);
        Assert.Equal("Machine B", french[AppStringKeys.HistoryMachineB]);
        Assert.Equal("Comparer", french[AppStringKeys.HistoryCompareButton]);
        Assert.Equal("Laquelle est le meilleur achat ?", french[AppStringKeys.HistoryValueTitle]);
        Assert.Equal(
            "Saisissez le prix demandé par chaque vendeur. Le prix est le vôtre, il n'est pas mesuré, et il sert uniquement à cette comparaison. Il n'est jamais enregistré avec le scan.",
            french[AppStringKeys.HistoryValueIntro]);
        Assert.Equal("Prix demandé (c'est vous qui le saisissez, il n'est pas mesuré)", french[AppStringKeys.HistoryAskingPriceLabel]);
        Assert.Equal("Par exemple 500", french[AppStringKeys.HistoryPriceExample]);
        Assert.Equal("Comparer la valeur", french[AppStringKeys.HistoryCompareValueButton]);
        Assert.Equal("Gagnant par composant (santé mesurée uniquement)", french[AppStringKeys.HistoryWinnersTitle]);
        Assert.Equal("Comment {0} évolue au fil du temps", french[AppStringKeys.HistorySubtitleTemplate]);
        Assert.Equal("Chargement de l'historique des instantanés...", french[AppStringKeys.HistoryLoading]);
        Assert.Equal("L'historique des instantanés n'est pas disponible sur cette machine, il n'y a donc encore rien à suivre.", french[AppStringKeys.HistoryUnavailable]);
        Assert.Equal("Aucun scan enregistré pour l'instant. Lancez un scan (actualiser) et revenez : chaque scan est enregistré dans l'historique local.", french[AppStringKeys.HistoryNoScans]);
        Assert.Equal("Un seul scan enregistré pour l'instant. Lancez un autre scan (actualiser) et revenez pour voir la tendance.", french[AppStringKeys.HistoryOneScan]);
        Assert.Equal("{0} scans dans l'historique, du plus ancien au plus récent.", french[AppStringKeys.HistoryScansCount]);
        Assert.Equal("L'historique n'a pas pu être lu : {0}", french[AppStringKeys.HistoryReadFailed]);
        Assert.Equal("La suppression n'est pas disponible : l'historique des instantanés n'est pas disponible sur cette machine.", french[AppStringKeys.HistoryRemoveUnavailable]);
        Assert.Equal("Supprimer le scan du {0} ? Cela supprime ce scan enregistré de ce dossier de données. Cliquez sur « Confirmer la suppression » pour supprimer — rien n'est supprimé avant cela.", french[AppStringKeys.HistoryRemoveConfirm]);
        Assert.Equal("Rien à supprimer : ce scan avait déjà disparu.", french[AppStringKeys.HistoryRemoveGone]);
        Assert.Equal("Scan du {0} supprimé. Il ne reste aucun scan enregistré.", french[AppStringKeys.HistoryRemovedNone]);
        Assert.Equal("Scan du {0} supprimé. Il reste 1 scan.", french[AppStringKeys.HistoryRemovedOne]);
        Assert.Equal("Scan du {0} supprimé. Il reste {1} scans.", french[AppStringKeys.HistoryRemovedMany]);
        Assert.Equal("Échec de la suppression : {0}", french[AppStringKeys.HistoryRemoveFailed]);
        Assert.Equal("L'historique des instantanés n'est pas disponible sur cette machine, il n'y a donc encore rien à comparer.", french[AppStringKeys.HistoryCompareUnavailable]);
        Assert.Equal("Une seule machine figure dans l'historique pour l'instant. Lancez un scan sur une deuxième machine et son historique apparaîtra ici pour la comparaison.", french[AppStringKeys.HistoryOnlyOneMachine]);
        Assert.Equal("Choisissez deux machines différentes.", french[AppStringKeys.HistoryPickDifferent]);
        Assert.Equal("Choisissez deux machines et comparez. Des machines différentes sont affichées côte à côte, jamais mélangées dans une même tendance.", french[AppStringKeys.HistoryPickTwoCompare]);
        Assert.Equal("Choisissez deux machines à comparer.", french[AppStringKeys.HistoryPickTwo]);
        Assert.Equal("Comparaison des machines...", french[AppStringKeys.HistoryComparing]);
        Assert.Equal("{0} comparé à {1}. Des machines différentes sont affichées côte à côte, jamais mélangées dans une même tendance.", french[AppStringKeys.HistoryCompared]);
        Assert.Equal("Échec de la comparaison : {0}", french[AppStringKeys.HistoryCompareFailed]);
        Assert.Equal("Saisissez le prix demandé par chaque vendeur pour comparer la valeur.", french[AppStringKeys.HistoryValuePrompt]);
        Assert.Equal("Comparez d'abord deux machines, puis saisissez les prix demandés.", french[AppStringKeys.HistoryValueCompareFirst]);
        Assert.Equal("Saisissez un prix demandé supérieur à zéro pour chaque machine (chiffres uniquement, par exemple 500). Le prix est le vôtre, il n'est pas mesuré.", french[AppStringKeys.HistoryValueBadPrice]);
        Assert.Equal("Les prix sont les vôtres, ils ne sont pas mesurés. Le chiffre par dollar est un calcul arithmétique sur le score mesuré et le prix que vous avez saisi, étiqueté ESTIMATION. Aucune donnée de prix du marché n'est utilisée.", french[AppStringKeys.HistoryValueHonestyNote]);
        Assert.Equal("Scans dans l'historique", french[AppStringKeys.HistoryRowScans]);
        Assert.Equal("Premier scan", french[AppStringKeys.HistoryRowFirstScan]);
        Assert.Equal("Dernier scan", french[AppStringKeys.HistoryRowLastScan]);
        Assert.Equal("Dernier score", french[AppStringKeys.HistoryRowLatestScore]);
        Assert.Equal("Dernière santé de la batterie", french[AppStringKeys.HistoryRowLatestBattery]);
        Assert.Equal("Dernière usure du stockage", french[AppStringKeys.HistoryRowLatestWear]);
        Assert.Equal("Dernière date du pilote le plus ancien", french[AppStringKeys.HistoryRowLatestDriver]);
        Assert.Equal("Dernière installation de mise à jour", french[AppStringKeys.HistoryRowLastUpdate]);
        Assert.Equal("Score pour 1 $ (ESTIMATION)", french[AppStringKeys.HistoryValueScorePerDollar]);
        Assert.Equal("Machine A gagne ({0} contre {1})", french[AppStringKeys.HistoryWinnerA]);
        Assert.Equal("Machine B gagne ({0} contre {1})", french[AppStringKeys.HistoryWinnerB]);
        Assert.Equal("Égalité ({0} contre {1})", french[AppStringKeys.HistoryWinnerTie]);
        Assert.Equal("Non mesurable sur une machine ou sur les deux", french[AppStringKeys.HistoryWinnerNotMeasurable]);
        Assert.Equal("{0} : non signalé dans aucun scan enregistré.", french[AppStringKeys.HistoryRangeNone]);
        Assert.Equal("{0} : {1} (un seul scan).", french[AppStringKeys.HistoryRangeOne]);
        Assert.Equal("{0} : {1} à {2} sur {3} scans.", french[AppStringKeys.HistoryRangeMany]);
        Assert.Equal("Chargement du journal des capteurs...", french[AppStringKeys.HistoryLogLoading]);
        Assert.Equal("La journalisation des capteurs est désactivée. Activez-la dans les Paramètres et le graphique de température du processeur apparaîtra ici.", french[AppStringKeys.HistoryLogOff]);
        Assert.Equal("Aucun échantillon de capteur enregistré pour l'instant. Laissez TrueSpec en cours d'exécution et le graphique de température du processeur apparaîtra ici.", french[AppStringKeys.HistoryLogNoSamples]);
        Assert.Equal("Le journal de cette machine ne contient aucun capteur de température du processeur, il n'y a donc rien à représenter.", french[AppStringKeys.HistoryLogNoCpuSensor]);
        Assert.Equal("Aucune mesure de température n'a été enregistrée sur {0} échantillons : le matériel de cette machine ne signale pas de capteur de température du processeur. C'est normal sur les machines dont le capteur n'est pas exposé, il n'y a donc pas de graphique de température du processeur à afficher.", french[AppStringKeys.HistoryLogNoReadings]);
        Assert.Equal("Une seule mesure de température a été enregistrée sur {0} échantillons, il n'y a donc pas assez de données pour tracer un graphique.", french[AppStringKeys.HistoryLogOneReading]);
        Assert.Equal("Les mesures de température sont trop éloignées dans le temps pour tracer une ligne : chaque mesure est isolée car la journalisation ne fonctionnait pas entre elles.", french[AppStringKeys.HistoryLogTooFarApart]);
        Assert.Equal("Température du processeur, {0} échantillons du {1} au {2}.", french[AppStringKeys.HistoryLogSummary]);
        Assert.Equal("Affichage des {0} échantillons les plus récents sur {1}, {2}.", french[AppStringKeys.HistoryLogSummaryNewest]);
        Assert.Equal("{0} échantillons, {1}.", french[AppStringKeys.HistoryLogSummaryAll]);
        Assert.Equal("aucun trou", french[AppStringKeys.HistoryLogGapNone]);
        Assert.Equal("1 trou où le capteur n'a pas signalé", french[AppStringKeys.HistoryLogGapOne]);
        Assert.Equal("{0} trous où le capteur n'a pas signalé", french[AppStringKeys.HistoryLogGapMany]);
        Assert.Equal("Le journal des capteurs n'a pas pu être lu : {0}", french[AppStringKeys.HistoryLogReadFailed]);

        // Templates keep the app's number formatting in French too.
        Assert.Equal(
            "Comment Test Laptop évolue au fil du temps",
            StringLocalizer.GetFormatted(AppStringKeys.HistorySubtitleTemplate, "fr", "Test Laptop"));
        Assert.Equal(
            "2 scans dans l'historique, du plus ancien au plus récent.",
            StringLocalizer.GetFormatted(AppStringKeys.HistoryScansCount, "fr", 2));
        Assert.Equal(
            "Scan du 2026-11-10 09:00 supprimé. Il reste 2 scans.",
            StringLocalizer.GetFormatted(AppStringKeys.HistoryRemovedMany, "fr", "2026-11-10 09:00", 2));
        Assert.Equal(
            "Score : 90 à 85 sur 2 scans.",
            StringLocalizer.GetFormatted(AppStringKeys.HistoryRangeMany, "fr", "Score", "90", "85", 2));
        Assert.Equal(
            "Aucune mesure de température n'a été enregistrée sur 3 échantillons : le matériel de cette machine ne signale pas de capteur de température du processeur. C'est normal sur les machines dont le capteur n'est pas exposé, il n'y a donc pas de graphique de température du processeur à afficher.",
            StringLocalizer.GetFormatted(AppStringKeys.HistoryLogNoReadings, "fr", 3));
    }

    [Fact]
    public void GermanPack_ReportPageTerms()
    {
        // Localisation, phase 8 (v2.1.16): the Report page — the
        // headings, the export buttons, the DNA card notes, the format
        // descriptions, the preview heading, the footer note and the
        // export privacy note. Same pinning rule as the rail, Settings,
        // Core, detail, Overview and History terms: changing one is a
        // deliberate translation decision, not a typo fix. The data the
        // page shows (machine name, verdict text, score explanation,
        // buyer banner and summary, generated-text line, DNA
        // state/hash/verify result texts, export folder path) is not
        // in this catalogue; only the page chrome is. The format
        // headings (JSON, HTML, CSV, PDF) are format names and stay
        // untranslated, like CPU and GPU.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("BERICHT", german[AppStringKeys.ReportEyebrow]);
        Assert.Equal("Die Wahrheit exportieren", german[AppStringKeys.ReportTitle]);
        Assert.Equal(
            "Ein Datenmodell mit JSON-, HTML-, CSV- und PDF-Ausgaben. Alles wird auf diesem Rechner erzeugt.",
            german[AppStringKeys.ReportIntro]);
        Assert.Equal("Punktzahl", german[AppStringKeys.ReportScoreLabel]);
        Assert.Equal("JSON exportieren", german[AppStringKeys.ReportExportJson]);
        Assert.Equal("HTML exportieren", german[AppStringKeys.ReportExportHtml]);
        Assert.Equal("CSV exportieren", german[AppStringKeys.ReportExportCsv]);
        Assert.Equal("PDF exportieren", german[AppStringKeys.ReportExportPdf]);
        Assert.Equal("Käuferzertifikat (PDF)", german[AppStringKeys.ReportExportCertificate]);
        Assert.Equal("Dieses Gerät prüfen", german[AppStringKeys.ReportVerifyMachine]);
        Assert.Equal("Geräte-DNA", german[AppStringKeys.ReportDnaTitle]);
        Assert.Equal("Dieses Gerät wird geprüft, die Hardware wird erneut gescannt …", german[AppStringKeys.ReportVerifyingMachine]);
        Assert.Equal("In einem der Scans nicht identifiziert:", german[AppStringKeys.ReportDnaUnidentifiedTitle]);
        Assert.Equal(
            "Die DNA ist Identität, nicht Gesundheit: Sie ändert niemals die Punktzahl oder das Urteil oben. Sie bestätigt nur, zu welchem Gerät die Punktzahl gehört. Ein Scan als Standardbenutzer ohne erhöhte Rechte prüft trotzdem, aber Teile, die erhöhte Rechte zum Lesen brauchen, können als nicht identifiziert statt als übereinstimmend angezeigt werden.",
            german[AppStringKeys.ReportDnaNote]);
        Assert.Equal("Maschinenlesbare Daten für Skripte und Support.", german[AppStringKeys.ReportFormatJsonDescription]);
        Assert.Equal("Eine übersichtliche Seite, die Sie in jedem Browser öffnen können.", german[AppStringKeys.ReportFormatHtmlDescription]);
        Assert.Equal("Eine Tabelle mit allen Spezifikationen, sicher in Excel zu öffnen. Werte, die wie Formeln aussehen, werden neutralisiert.", german[AppStringKeys.ReportFormatCsvDescription]);
        Assert.Equal("Ein ausdruckbarer Bericht für den Verkäufer oder zum Aufbewahren beim Kaufbeleg. Auf diesem Rechner erstellt, keine zusätzliche Software.", german[AppStringKeys.ReportFormatPdfDescription]);
        Assert.Equal("Verfügbar", german[AppStringKeys.ReportFormatAvailable]);
        Assert.Equal("Berichtsvorschau", german[AppStringKeys.ReportPreviewTitle]);
        Assert.Equal("Berichtsexporte werden in den oben angezeigten Ordner geschrieben. Kein Hochladen, keine Cloud.", german[AppStringKeys.ReportFooterNote]);
        Assert.Equal(
            "Exportierte Berichte enthalten die Hardware-Seriennummern, MAC-Adressen, IP-Adressen und WLAN-Namen dieses Geräts. Teilen Sie sie nur mit Personen, denen Sie vertrauen.",
            german[AppStringKeys.ReportExportPrivacyNote]);
    }

    [Fact]
    public void GermanPack_TestsPageTerms()
    {
        // Localisation, phase 8 (v2.1.16): the Tests page — the
        // headings, the intro, the info note, the benchmark scope note,
        // the per-test status words, result lines and Run button
        // labels, and the load-test card (heading, intro, battery note,
        // duration label, Start/Cancel, fan-health heading and note,
        // temperature caption, the two warning templates, the fixed
        // status words and the fixed live-line states. Localisation
        // polish (v2.1.18): the measured live-line labels and the
        // elapsed line translate too, via templates. Same pinning
        // rule as the Report terms above: changing one is a deliberate
        // translation decision, not a typo fix. The data the page shows
        // (per-test names and workload descriptions, measured result
        // summaries and details, load-test measured results, verdict
        // text and live values) is not in this catalogue; only the
        // page chrome is. The load test reuses the test-item status
        // keys ("Not run yet.", "Running...", "Test failed", the
        // no-result template) instead of a second copy of the same
        // words.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("TESTS", german[AppStringKeys.TestsEyebrow]);
        Assert.Equal("Eingebaute Tests", german[AppStringKeys.TestsTitle]);
        Assert.Equal("Jeweils ein Klick. Keine versteckten Uploads, kein Konto, und nichts läuft, ohne dass Sie es starten.", german[AppStringKeys.TestsIntro]);
        Assert.Equal("CPU- und Datenträgertests führen echte lokale Messungen durch. Tests, die nicht umgesetzt sind, bleiben deaktiviert und zeigen kein Ergebnis.", german[AppStringKeys.TestsInfoNote]);
        Assert.Equal(
            "Diese Benchmarks messen nur dieses Gerät, mit der genauen Arbeitslast, die bei jedem Ergebnis angegeben ist. Sie ersetzen weder Geekbench (plattformübergreifender Vergleich) noch 3DMark (GPU-Rendering). Ein Ergebnis, das entstand, während ein anderes Programm die CPU nutzte, sagt dies auf dem Ergebnis selbst.",
            german[AppStringKeys.TestsBenchmarkScopeNote]);
        Assert.Equal("In dieser Version nicht verfügbar", german[AppStringKeys.TestsItemNotAvailable]);
        Assert.Equal("Bereit", german[AppStringKeys.TestsItemReady]);
        Assert.Equal("Läuft …", german[AppStringKeys.TestsItemRunning]);
        Assert.Equal("Test fehlgeschlagen", german[AppStringKeys.TestsItemFailed]);
        Assert.Equal("Noch nicht ausgeführt.", german[AppStringKeys.TestsItemNotRunYet]);
        Assert.Equal("Dieser Test ist noch nicht umgesetzt, daher wird kein Ergebnis angezeigt.", german[AppStringKeys.TestsItemNotImplementedResult]);
        Assert.Equal("Wird lokal gemessen. Nichts wird hochgeladen.", german[AppStringKeys.TestsItemMeasuring]);
        Assert.Equal("Es wird kein Ergebnis angezeigt, weil der Test nicht abgeschlossen wurde: {0}", german[AppStringKeys.TestsItemNoResult]);
        Assert.Equal("Test starten", german[AppStringKeys.TestsItemRun]);
        Assert.Equal("Läuft …", german[AppStringKeys.TestsItemRunningButton]);
        Assert.Equal("Nicht verfügbar", german[AppStringKeys.TestsItemNotAvailableButton]);
        Assert.Equal("Belastungstest", german[AppStringKeys.LoadTestTitle]);
        Assert.Equal("Belastet die CPU auf allen Kernen, beansprucht Arbeitsspeicher und Datenträger und beobachtet jede Sekunde Temperatur und Takt.", german[AppStringKeys.LoadTestIntro]);
        Assert.Equal("Dieses Gerät läuft auf Akku; die Ergebnisse können leistungsbegrenzt sein.", german[AppStringKeys.LoadTestBatteryNote]);
        Assert.Equal("Dauer (Sekunden):", german[AppStringKeys.LoadTestDurationLabel]);
        Assert.Equal("Belastungstest starten", german[AppStringKeys.LoadTestStart]);
        Assert.Equal("Abbrechen", german[AppStringKeys.LoadTestCancel]);
        Assert.Equal("Lüfterzustand", german[AppStringKeys.LoadTestFanHealthTitle]);
        Assert.Equal("TrueSpec meldet nur den Lüfterzustand; es steuert niemals Lüfter.", german[AppStringKeys.LoadTestFanHealthNote]);
        Assert.Equal("Temperatur während des Tests (°C). Eine Lücke in der Linie bedeutet, dass der Sensor in dieser Sekunde nichts gemeldet hat.", german[AppStringKeys.LoadTestTemperatureCaption]);
        Assert.Equal("Dadurch wird Ihre CPU {0} Sekunden lang belastet. Der Test stoppt automatisch, wenn die Temperatur {1} °C übersteigt.", german[AppStringKeys.LoadTestWarning]);
        Assert.Equal("Ein {0}-Sekunden-Test ist eine Funktionsprüfung, kein Dauerbelastungszertifikat.", german[AppStringKeys.LoadTestFunctionalCheck]);
        Assert.Equal("Automatisch gestoppt: Temperaturgrenze erreicht.", german[AppStringKeys.LoadTestStoppedTemperature]);
        Assert.Equal("Gestoppt: Die Sensoren melden nichts mehr.", german[AppStringKeys.LoadTestStoppedSensorLost]);
        Assert.Equal("Abgebrochen.", german[AppStringKeys.LoadTestCancelled]);
        Assert.Equal("Belastungstest abgeschlossen.", german[AppStringKeys.LoadTestComplete]);
        Assert.Equal("Temperatur: läuft nicht", german[AppStringKeys.LoadTestTempNotRunning]);
        Assert.Equal("Temperatur: wird gemessen …", german[AppStringKeys.LoadTestTempMeasuring]);
        Assert.Equal("Temperatur: Test beendet", german[AppStringKeys.LoadTestTempFinished]);
        Assert.Equal("Temperatur: von der Hardware nicht gemeldet", german[AppStringKeys.LoadTestTempNotReported]);
        Assert.Equal("Takt: läuft nicht", german[AppStringKeys.LoadTestClockNotRunning]);
        Assert.Equal("Takt: wird gemessen …", german[AppStringKeys.LoadTestClockMeasuring]);
        Assert.Equal("Takt: Test beendet", german[AppStringKeys.LoadTestClockFinished]);
        Assert.Equal("Takt: von der Hardware nicht gemeldet", german[AppStringKeys.LoadTestClockNotReported]);

        // Localisation polish (v2.1.18): the measured live lines and
        // the elapsed line translate their labels too, via templates;
        // only the measured value stays data.
        Assert.Equal("Temperatur: {0}", german[AppStringKeys.LoadTestTempMeasured]);
        Assert.Equal("Takt: {0}", german[AppStringKeys.LoadTestClockMeasured]);
        Assert.Equal("Abgeschlossen in {0} s.", german[AppStringKeys.LoadTestElapsedFinished]);

        // Templates keep the app's number formatting in German too.
        Assert.Equal(
            "Dadurch wird Ihre CPU 60 Sekunden lang belastet. Der Test stoppt automatisch, wenn die Temperatur 95 °C übersteigt.",
            StringLocalizer.GetFormatted(AppStringKeys.LoadTestWarning, "de", "60", "95"));
        Assert.Equal(
            "Ein 60-Sekunden-Test ist eine Funktionsprüfung, kein Dauerbelastungszertifikat.",
            StringLocalizer.GetFormatted(AppStringKeys.LoadTestFunctionalCheck, "de", "60"));
        Assert.Equal(
            "Es wird kein Ergebnis angezeigt, weil der Test nicht abgeschlossen wurde: disk vanished",
            StringLocalizer.GetFormatted(AppStringKeys.TestsItemNoResult, "de", "disk vanished"));
        Assert.Equal(
            "Temperatur: 72.3 C",
            StringLocalizer.GetFormatted(AppStringKeys.LoadTestTempMeasured, "de", "72.3 C"));
        Assert.Equal(
            "Takt: 4200 MHz",
            StringLocalizer.GetFormatted(AppStringKeys.LoadTestClockMeasured, "de", "4200 MHz"));
        Assert.Equal(
            "Abgeschlossen in 60 s.",
            StringLocalizer.GetFormatted(AppStringKeys.LoadTestElapsedFinished, "de", "60"));
    }

    [Fact]
    public void GermanPack_NewLaptopCheckPageTerms()
    {
        // Localisation, phase 8 (v2.1.16): the New Laptop Check page —
        // the eyebrow, the banner's fixed words, the Authenticity
        // heading, the no-prior-use line, the skipped-checks line, the
        // checklist heading and buttons, the advertised-RAM card, the
        // "What to ask the seller" label, the no-checks line, the
        // footer note, the row status words and the source labels.
        // Same pinning rule as the Report and Tests terms above:
        // changing one is a deliberate translation decision, not a
        // typo fix. The data the page shows (machine name, buyer
        // summary, authenticity verdict line, check titles, details
        // and what-to-ask lines, authenticity evidence lines, source
        // strings, the finding title inside the banner) is not in
        // this catalogue; only the page chrome is. The "Export
        // Report" button reuses the Overview key, so it is pinned
        // with the Overview terms, not here. The skipped-checks line
        // uses one/many keys instead of the old inline plural hack.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("NEUER LAPTOP CHECK", german[AppStringKeys.NewLaptopCheckEyebrow]);
        Assert.Equal("BESTANDEN - Dieses Gerät entspricht einem neuen Laptop", german[AppStringKeys.NewLaptopCheckBannerPass]);
        Assert.Equal("WARNUNG - Prüfen Sie die hervorgehobenen Punkte", german[AppStringKeys.NewLaptopCheckBannerWarning]);
        Assert.Equal("FEHLGESCHLAGEN - Dieses Gerät sieht nicht neu aus", german[AppStringKeys.NewLaptopCheckBannerFail]);
        Assert.Equal("Nicht genug Daten für ein Urteil", german[AppStringKeys.NewLaptopCheckBannerNotEnoughData]);
        Assert.Equal("BESTANDEN", german[AppStringKeys.NewLaptopCheckBannerStatePass]);
        Assert.Equal("WARNUNG", german[AppStringKeys.NewLaptopCheckBannerStateWarning]);
        Assert.Equal("FEHLGESCHLAGEN", german[AppStringKeys.NewLaptopCheckBannerStateFail]);
        Assert.Equal("Nicht genug Daten", german[AppStringKeys.NewLaptopCheckBannerStateNotEnoughData]);
        Assert.Equal("Authentizität", german[AppStringKeys.NewLaptopCheckAuthenticityTitle]);
        Assert.Equal("In den prübaren Alterssignalen wurden keine Anzeichen für eine frühere Nutzung gefunden.", german[AppStringKeys.NewLaptopCheckNoPriorUse]);
        Assert.Equal("1 Authentizitätsprüfung konnte nicht ausgeführt werden, weil ein Signal nicht gemeldet wurde: {0}", german[AppStringKeys.NewLaptopCheckSkippedOne]);
        Assert.Equal("{0} Authentizitätsprüfungen konnten nicht ausgeführt werden, weil ein Signal nicht gemeldet wurde: {1}", german[AppStringKeys.NewLaptopCheckSkippedMany]);
        Assert.Equal("Checkliste", german[AppStringKeys.NewLaptopCheckChecklistTitle]);
        Assert.Equal("Checkliste drucken", german[AppStringKeys.NewLaptopCheckPrintChecklist]);
        Assert.Equal("Angegebener RAM (optional)", german[AppStringKeys.NewLaptopCheckAdvertisedRamTitle]);
        Assert.Equal("Wird nur für die RAM-Prüfung verwendet. Es wird niemals als Hardwaremesswert behandelt.", german[AppStringKeys.NewLaptopCheckAdvertisedRamNote]);
        Assert.Equal("z. B. 16 GB", german[AppStringKeys.NewLaptopCheckAdvertisedRamPlaceholder]);
        Assert.Equal("Was Sie den Verkäufer fragen sollten", german[AppStringKeys.NewLaptopCheckWhatToAsk]);
        Assert.Equal("Für diesen Scan wurden keine Käuferprüfungen erstellt.", german[AppStringKeys.NewLaptopCheckNoChecks]);
        Assert.Equal("Für Käuferprüfungen gelten dieselben ehrlichen Regeln wie für den Rest der App: nur gemessene Werte, keine erfundenen Zahlen.", german[AppStringKeys.NewLaptopCheckFooterNote]);
        Assert.Equal("Quelle 1: {0}", german[AppStringKeys.NewLaptopCheckSourceOne]);
        Assert.Equal("Quelle 2: {0}", german[AppStringKeys.NewLaptopCheckSourceTwo]);
        Assert.Equal("Bestanden", german[AppStringKeys.BuyerCheckStatePass]);
        Assert.Equal("Warnung", german[AppStringKeys.BuyerCheckStateWarning]);
        Assert.Equal("Fehlgeschlagen", german[AppStringKeys.BuyerCheckStateFail]);
        Assert.Equal("Nicht zutreffend", german[AppStringKeys.BuyerCheckStateNotApplicable]);
        Assert.Equal("Stark", german[AppStringKeys.AuthenticitySeverityStrong]);
        Assert.Equal("Achtung", german[AppStringKeys.AuthenticitySeverityWatch]);
        Assert.Equal("Info", german[AppStringKeys.AuthenticitySeverityInfo]);

        // Templates keep the app's number formatting in German too.
        Assert.Equal(
            "1 Authentizitätsprüfung konnte nicht ausgeführt werden, weil ein Signal nicht gemeldet wurde: Windows install date",
            StringLocalizer.GetFormatted(AppStringKeys.NewLaptopCheckSkippedOne, "de", "Windows install date"));
        Assert.Equal(
            "3 Authentizitätsprüfungen konnten nicht ausgeführt werden, weil ein Signal nicht gemeldet wurde: BIOS date; Windows install date",
            StringLocalizer.GetFormatted(AppStringKeys.NewLaptopCheckSkippedMany, "de", 3, "BIOS date; Windows install date"));
        Assert.Equal(
            "Quelle 1: BIOS date",
            StringLocalizer.GetFormatted(AppStringKeys.NewLaptopCheckSourceOne, "de", "BIOS date"));
    }

    [Fact]
    public void FrenchPack_NewLaptopCheckPageTerms()
    {
        // Localisation, phase 6, French (v2.1.25): the New Laptop Check
        // page — the eyebrow, the banner's fixed words, the
        // Authenticity heading, the no-prior-use line, the
        // skipped-checks line, the checklist heading and buttons, the
        // advertised-RAM card, the "What to ask the seller" label, the
        // no-checks line, the footer note, the row status words and the
        // source labels. Same pinning rule as the German phase 8 terms:
        // changing one is a deliberate translation decision, not a typo
        // fix. The data the page shows (machine name, buyer summary,
        // authenticity verdict line, check titles, details and
        // what-to-ask lines, authenticity evidence lines, source
        // strings, the finding title inside the banner) is not in this
        // catalogue; only the page chrome is. The "Export Report"
        // button reuses the Overview key, so it is pinned with the
        // Overview terms, not here. The skipped-checks line uses
        // one/many keys instead of the old inline plural hack. No new
        // keys — the French pack already covers the whole 523-key
        // catalogue.
var french = LanguagePacks.French.Strings;
        Assert.Equal("VÉRIFICATION D'ORDINATEUR PORTABLE NEUF", french[AppStringKeys.NewLaptopCheckEyebrow]);
        Assert.Equal("RÉUSSI - cette machine correspond à un ordinateur portable neuf", french[AppStringKeys.NewLaptopCheckBannerPass]);
        Assert.Equal("ATTENTION - vérifiez les éléments mis en évidence", french[AppStringKeys.NewLaptopCheckBannerWarning]);
        Assert.Equal("ÉCHEC - cette machine ne ressemble pas à un ordinateur neuf", french[AppStringKeys.NewLaptopCheckBannerFail]);
        Assert.Equal("Pas assez de données pour un verdict", french[AppStringKeys.NewLaptopCheckBannerNotEnoughData]);
        Assert.Equal("RÉUSSI", french[AppStringKeys.NewLaptopCheckBannerStatePass]);
        Assert.Equal("ATTENTION", french[AppStringKeys.NewLaptopCheckBannerStateWarning]);
        Assert.Equal("ÉCHEC", french[AppStringKeys.NewLaptopCheckBannerStateFail]);
        Assert.Equal("Pas assez de données", french[AppStringKeys.NewLaptopCheckBannerStateNotEnoughData]);
        Assert.Equal("Authenticité", french[AppStringKeys.NewLaptopCheckAuthenticityTitle]);
        Assert.Equal("Aucun signe d'utilisation antérieure n'a été trouvé dans les signaux d'âge qui ont pu être vérifiés.", french[AppStringKeys.NewLaptopCheckNoPriorUse]);
        Assert.Equal("1 contrôle d'authenticité n'a pas pu être exécuté car un signal n'a pas été signalé : {0}", french[AppStringKeys.NewLaptopCheckSkippedOne]);
        Assert.Equal("{0} contrôles d'authenticité n'ont pas pu être exécutés car un signal n'a pas été signalé : {1}", french[AppStringKeys.NewLaptopCheckSkippedMany]);
        Assert.Equal("Liste de contrôle", french[AppStringKeys.NewLaptopCheckChecklistTitle]);
        Assert.Equal("Imprimer la liste de contrôle", french[AppStringKeys.NewLaptopCheckPrintChecklist]);
        Assert.Equal("RAM annoncée (facultatif)", french[AppStringKeys.NewLaptopCheckAdvertisedRamTitle]);
        Assert.Equal("Utilisée uniquement pour le contrôle de la RAM. Elle n'est jamais traitée comme une mesure matérielle.", french[AppStringKeys.NewLaptopCheckAdvertisedRamNote]);
        Assert.Equal("p. ex. 16 Go", french[AppStringKeys.NewLaptopCheckAdvertisedRamPlaceholder]);
        Assert.Equal("Quoi demander au vendeur", french[AppStringKeys.NewLaptopCheckWhatToAsk]);
        Assert.Equal("Aucun contrôle d'acheteur n'a été produit pour ce scan.", french[AppStringKeys.NewLaptopCheckNoChecks]);
        Assert.Equal("Les contrôles d'acheteur suivent les mêmes règles d'honnêteté que le reste de l'application : uniquement des valeurs mesurées, aucun nombre inventé.", french[AppStringKeys.NewLaptopCheckFooterNote]);
        Assert.Equal("Source 1 : {0}", french[AppStringKeys.NewLaptopCheckSourceOne]);
        Assert.Equal("Source 2 : {0}", french[AppStringKeys.NewLaptopCheckSourceTwo]);
        Assert.Equal("Réussi", french[AppStringKeys.BuyerCheckStatePass]);
        Assert.Equal("Attention", french[AppStringKeys.BuyerCheckStateWarning]);
        Assert.Equal("Échec", french[AppStringKeys.BuyerCheckStateFail]);
        Assert.Equal("Sans objet", french[AppStringKeys.BuyerCheckStateNotApplicable]);
        Assert.Equal("Fort", french[AppStringKeys.AuthenticitySeverityStrong]);
        Assert.Equal("À surveiller", french[AppStringKeys.AuthenticitySeverityWatch]);
        Assert.Equal("Info", french[AppStringKeys.AuthenticitySeverityInfo]);

        // Templates keep the app's number formatting in French too.
        Assert.Equal(
            "1 contrôle d'authenticité n'a pas pu être exécuté car un signal n'a pas été signalé : Windows install date",
            StringLocalizer.GetFormatted(AppStringKeys.NewLaptopCheckSkippedOne, "fr", "Windows install date"));
        Assert.Equal(
            "3 contrôles d'authenticité n'ont pas pu être exécutés car un signal n'a pas été signalé : BIOS date; Windows install date",
            StringLocalizer.GetFormatted(AppStringKeys.NewLaptopCheckSkippedMany, "fr", 3, "BIOS date; Windows install date"));
        Assert.Equal(
            "Source 1 : BIOS date",
            StringLocalizer.GetFormatted(AppStringKeys.NewLaptopCheckSourceOne, "fr", "BIOS date"));
    }

    [Fact]
    public void GermanPack_CrashHistoryPageTerms()
    {
        // Localisation, phase 8 (v2.1.16): the Crash History page —
        // the title, the recent-crash heading, the three fixed alert
        // sentences, the "Failures by class" heading, the table
        // headers, the two fixed empty-state lines, the cleared-log
        // warning template and the footer note. Same pinning rule as
        // the New Laptop Check terms above: changing one is a
        // deliberate translation decision, not a typo fix. The data
        // the page shows (component headline, the "What this means"
        // fallback, class names, counts and dates, plain meanings,
        // the "Not reported" date fallback) is not in this
        // catalogue; only the page chrome is. The captured line
        // reuses the detail page's DetailCaptured template, so it is
        // pinned with the detail terms, not here.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("Absturzverlauf", german[AppStringKeys.CrashTitle]);
        Assert.Equal("Aktueller Absturz", german[AppStringKeys.CrashRecentTitle]);
        Assert.Equal("In den letzten 30 Tagen gab es einen Bluescreen und einen schwerwiegenden Hardwarefehler.", german[AppStringKeys.CrashAlertBoth]);
        Assert.Equal("In den letzten 30 Tagen gab es einen Bluescreen.", german[AppStringKeys.CrashAlertBugCheck]);
        Assert.Equal("In den letzten 30 Tagen gab es einen schwerwiegenden Hardwarefehler.", german[AppStringKeys.CrashAlertWheaFatal]);
        Assert.Equal("Fehler nach Klasse", german[AppStringKeys.CrashFailuresByClass]);
        Assert.Equal("Klasse", german[AppStringKeys.CrashHeaderClass]);
        Assert.Equal("Anzahl (90 Tage)", german[AppStringKeys.CrashHeaderCount]);
        Assert.Equal("Erste", german[AppStringKeys.CrashHeaderFirst]);
        Assert.Equal("Letzte", german[AppStringKeys.CrashHeaderLast]);
        Assert.Equal("Bedeutung", german[AppStringKeys.CrashHeaderMeaning]);
        Assert.Equal("Der Absturzverlauf wurde noch nicht erfasst.", german[AppStringKeys.CrashEmptyNotCollected]);
        Assert.Equal("In den letzten 90 Tagen wurden keine Bluescreens, Hardwarefehler, unerwarteten Abschaltungen oder Datenträgerfehler aufgezeichnet.", german[AppStringKeys.CrashEmptyNoCrashes]);
        Assert.Equal("Das Ereignisprotokoll hat keine Einträge vor dem {0}, daher ist dieser Verlauf möglicherweise unvollständig. Ein gelöschtes Protokoll gilt als Lücke, niemals als sauberer Verlauf.", german[AppStringKeys.CrashLogCleared]);
        Assert.Equal("Die Anzahl deckt die letzten 90 Tage ab; erste und letzte decken den gesamten aufgezeichneten Zeitraum ab. Bedeutungen sind feste Bezeichnungen, keine Diagnosen.", german[AppStringKeys.CrashFooterNote]);

        // Templates keep the app's number formatting in German too.
        Assert.Equal(
            "Das Ereignisprotokoll hat keine Einträge vor dem 2026-10-25, daher ist dieser Verlauf möglicherweise unvollständig. Ein gelöschtes Protokoll gilt als Lücke, niemals als sauberer Verlauf.",
            StringLocalizer.GetFormatted(AppStringKeys.CrashLogCleared, "de", "2026-10-25"));
    }

    [Fact]
    public void GermanPack_ComparisonPageTerms()
    {
        // Localisation, phase 8 (v2.1.16): the Comparison page — the
        // eyebrow, title, intro, Before/After labels and picker
        // placeholders, the Compare button, the Result heading, the
        // DNA note, the no-changes line, the Added/Removed headings,
        // the footer note, the transient status lines and the DNA
        // state/hash lines. Same pinning rule as the Crash History
        // terms above: changing one is a deliberate translation
        // decision, not a typo fix. The data the page shows (the
        // comparison summary sentence, the row cell values, the DNA
        // changed/unidentified part lines, file paths and exception
        // messages) is not in this catalogue; only the page chrome
        // is. The DNA heading and the unidentified-parts heading
        // reuse the Report page's keys, so they are pinned with the
        // Report terms, not here.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("VERGLEICH", german[AppStringKeys.ComparisonEyebrow]);
        Assert.Equal("Zwei Scans im Vergleich", german[AppStringKeys.ComparisonTitle]);
        Assert.Equal("Wählen Sie zwei beliebige gespeicherte Scans. TrueSpec zeigt die Punktzahländerung, jede Komponente, die sich verändert hat, und jede geänderte Spezifikation. Nichts wird erfunden: Eine Komponente, die in keinem der Scans messbar war, sagt dies auch.", german[AppStringKeys.ComparisonIntro]);
        Assert.Equal("Vorher", german[AppStringKeys.ComparisonBefore]);
        Assert.Equal("Nachher", german[AppStringKeys.ComparisonAfter]);
        Assert.Equal("Wählen Sie einen früheren Scan", german[AppStringKeys.ComparisonPickEarlier]);
        Assert.Equal("Wählen Sie einen späteren Scan", german[AppStringKeys.ComparisonPickLater]);
        Assert.Equal("Vergleichen", german[AppStringKeys.ComparisonCompareButton]);
        Assert.Equal("Ergebnis", german[AppStringKeys.ComparisonResultTitle]);
        Assert.Equal("Die DNA ist Identität, nicht Gesundheit: Sie ändert niemals die Punktzahl oben. Sie sagt nur, ob die beiden Scans dasselbe Gerät mit denselben Teilen sind.", german[AppStringKeys.ComparisonDnaNote]);
        Assert.Equal("Zwischen diesen beiden Scans hat sich weder die Gesundheit einer Komponente noch eine Spezifikation geändert.", german[AppStringKeys.ComparisonNoChanges]);
        Assert.Equal("Hinzugefügte Komponenten", german[AppStringKeys.ComparisonAddedTitle]);
        Assert.Equal("Entfernte Komponenten", german[AppStringKeys.ComparisonRemovedTitle]);
        Assert.Equal("Scans werden nach jedem Scan lokal gespeichert. Der Verlauf verlässt diesen Rechner nie.", german[AppStringKeys.ComparisonFooterNote]);
        Assert.Equal("Scanverlauf wird geladen …", german[AppStringKeys.ComparisonLoading]);
        Assert.Equal("Der Scanverlauf ist auf diesem Rechner nicht verfügbar, daher gibt es noch nichts zu vergleichen.", german[AppStringKeys.ComparisonHistoryUnavailable]);
        Assert.Equal("Es sind noch keine Scans gespeichert. Scans werden nach jedem Scan gespeichert, also aktualisieren Sie den Scan und kommen Sie zum Vergleichen zurück.", german[AppStringKeys.ComparisonNoSnapshots]);
        Assert.Equal("Bisher ist nur ein Scan gespeichert. Führen Sie einen weiteren Scan aus (Aktualisieren) und kommen Sie zum Vergleichen zurück.", german[AppStringKeys.ComparisonOnlyOneSnapshot]);
        Assert.Equal("{0} Scans im Verlauf. Wählen Sie zwei aus und vergleichen Sie sie.", german[AppStringKeys.ComparisonSnapshotsCount]);
        Assert.Equal("Der Verlauf konnte nicht gelesen werden: {0}", german[AppStringKeys.ComparisonHistoryReadFailed]);
        Assert.Equal("Wählen Sie zwei verschiedene Scans zum Vergleichen.", german[AppStringKeys.ComparisonPickDifferent]);
        Assert.Equal("Scans werden verglichen …", german[AppStringKeys.ComparisonComparing]);
        Assert.Equal("Einer der Scans konnte nicht aus dem Verlauf gelesen werden.", german[AppStringKeys.ComparisonSnapshotUnreadable]);
        Assert.Equal("Die DNA konnte nicht verglichen werden: Die Scans konnten nicht gelesen werden.", german[AppStringKeys.ComparisonDnaUnreadable]);
        Assert.Equal("{0} mit {1} verglichen.", german[AppStringKeys.ComparisonCompared]);
        Assert.Equal("Der Vergleich ist fehlgeschlagen: {0}", german[AppStringKeys.ComparisonFailed]);
        Assert.Equal("Noch kein DNA-Vergleich. Wählen Sie zwei Scans aus und vergleichen Sie sie.", german[AppStringKeys.ComparisonDnaNoComparison]);
        Assert.Equal("Dasselbe Gerät, dieselben Teile.", german[AppStringKeys.ComparisonDnaIdentical]);
        Assert.Equal("Dasselbe Gerät – Fingerabdruck stimmt überein.", german[AppStringKeys.ComparisonDnaFingerprintMatches]);
        Assert.Equal("Zwischen diesen beiden Scans haben sich Teile geändert.", german[AppStringKeys.ComparisonDnaChanged]);
        Assert.Equal("Die DNA konnte für diese Scans nicht geprüft werden.", german[AppStringKeys.ComparisonDnaUnverifiable]);
        Assert.Equal("Für einen oder beide Scans wurde keine DNA aufgezeichnet.", german[AppStringKeys.ComparisonDnaNotRecordedState]);
        Assert.Equal("Vorher: {0}", german[AppStringKeys.ComparisonDnaHashBefore]);
        Assert.Equal("Nachher: {0}", german[AppStringKeys.ComparisonDnaHashAfter]);
        Assert.Equal("DNA nicht aufgezeichnet", german[AppStringKeys.ComparisonDnaNotRecorded]);

        // Templates keep the app's number formatting in German too.
        Assert.Equal(
            "2 Scans im Verlauf. Wählen Sie zwei aus und vergleichen Sie sie.",
            StringLocalizer.GetFormatted(AppStringKeys.ComparisonSnapshotsCount, "de", 2));
        Assert.Equal(
            "2026-11-10 09:00 mit 2026-12-10 09:00 verglichen.",
            StringLocalizer.GetFormatted(AppStringKeys.ComparisonCompared, "de", "2026-11-10 09:00", "2026-12-10 09:00"));
        Assert.Equal(
            "Vorher: abcdef12…34567890",
            StringLocalizer.GetFormatted(AppStringKeys.ComparisonDnaHashBefore, "de", "abcdef12…34567890"));
        Assert.Equal(
            "Der Verlauf konnte nicht gelesen werden: disk vanished",
            StringLocalizer.GetFormatted(AppStringKeys.ComparisonHistoryReadFailed, "de", "disk vanished"));
    }

    [Fact]
    public void GermanPack_FleetPageTerms()
    {
        // Localisation, phase 8 (v2.1.16): the Fleet page — the
        // eyebrow, title, intro, the Import button and note, the
        // Machines heading, the two export buttons, the table note,
        // the column headers, the row Export button and the
        // Remove/Confirm-remove button, the transient status lines
        // (loading, no scans, the summary templates, the export-all /
        // remove / import lines, and the import skip reasons since
        // v2.1.18). Same pinning rule as the Comparison
        // terms above: changing one is a deliberate translation
        // decision, not a typo fix. The data the page shows (machine
        // names, the row cell values, file and folder paths, exception
        // messages) is not in this catalogue; only the page chrome
        // is. The summary and the export-all / remove / import lines
        // are templates with one/many keys instead of inline plural
        // hacks, and the remove-arm line quotes the translated
        // Confirm-remove label. The row's "Not measurable" score
        // fallback and "Not reported" worst-component fallback stay
        // English like the History table's fallbacks; the summary's
        // lowest-score fallback is its own key because it sits
        // mid-sentence inside a translated line.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("GERÄTEPARK", german[AppStringKeys.FleetEyebrow]);
        Assert.Equal("Jedes Gerät in diesem Datenordner", german[AppStringKeys.FleetTitle]);
        Assert.Equal("Technikermodus: Nehmen Sie die portable App auf einem USB-Stick mit, scannen Sie jedes Gerät, und jeder Scan landet im selben Speicher auf dem Stick. Diese Seite listet den neuesten Scan jedes Geräts, niedrigste Punktzahl zuerst, mit der schlechtesten Komponente benannt, sodass das Gerät, das Aufmerksamkeit braucht, ganz oben steht. Geräte, die mit der installierten App gescannt wurden, können mit „JSON-Berichte importieren\" hereingeholt werden, was die JSON-Dateien liest, die die Berichtsseite exportiert. „Alle Geräte exportieren\" schreibt eine CSV-Datei pro Gerät in den Exportordner, sodass der ganze Gerätepark auf einmal abgelegt werden kann, statt einem Export-Klick pro Zeile. Um ein Gerät aus dem Gerätepark zu nehmen, verwenden Sie „Entfernen\" in seiner Zeile – es löscht alle gespeicherten Scans dieses Geräts aus diesem Datenordner, in zwei Klicks, damit ein Fehlklick nichts löschen kann.", german[AppStringKeys.FleetIntro]);
        Assert.Equal("JSON-Berichte importieren", german[AppStringKeys.FleetImportButton]);
        Assert.Equal("Importiert TrueSpec-JSON-Berichte, die von anderen Geräten exportiert wurden, sodass ihre Scans zu diesem Gerätepark kommen. Eine Datei, die kein TrueSpec-Bericht ist, wird übersprungen, und wenn dieselbe Datei zweimal importiert wird, wird nur ein Scan gespeichert.", german[AppStringKeys.FleetImportNote]);
        Assert.Equal("Geräte", german[AppStringKeys.FleetMachinesTitle]);
        Assert.Equal("Alle Geräte exportieren", german[AppStringKeys.FleetExportAllButton]);
        Assert.Equal("Gerätepark als CSV exportieren", german[AppStringKeys.FleetExportFleetCsvButton]);
        Assert.Equal("Niedrigste Punktzahl zuerst. Ein Strich bedeutet, dass dieser Scan den Wert nicht gemeldet hat.", german[AppStringKeys.FleetTableNote]);
        Assert.Equal("Gerät", german[AppStringKeys.FleetHeaderMachine]);
        Assert.Equal("Scans", german[AppStringKeys.FleetHeaderScans]);
        Assert.Equal("Letzter Scan", german[AppStringKeys.FleetHeaderLastScan]);
        Assert.Equal("Punktzahl", german[AppStringKeys.FleetHeaderScore]);
        Assert.Equal("Urteil", german[AppStringKeys.FleetHeaderVerdict]);
        Assert.Equal("Schlechteste Komponente", german[AppStringKeys.FleetHeaderWorstComponent]);
        Assert.Equal("Export", german[AppStringKeys.FleetHeaderExport]);
        Assert.Equal("Entfernen", german[AppStringKeys.FleetHeaderRemove]);
        Assert.Equal("CSV exportieren", german[AppStringKeys.FleetRowExportButton]);
        Assert.Equal("Entfernen bestätigen", german[AppStringKeys.FleetRowConfirmRemoveButton]);
        Assert.Equal("Gerätepark wird geladen …", german[AppStringKeys.FleetLoading]);
        Assert.Equal("Der Scanverlauf ist auf diesem Rechner nicht verfügbar, daher gibt es noch keinen Gerätepark.", german[AppStringKeys.FleetUnavailable]);
        Assert.Equal("Noch keine gespeicherten Scans. Scannen Sie ein Gerät, oder führen Sie die portable App auf jedem Gerät im Gerätepark aus, und sein neuester Scan erscheint hier. Sie können auch JSON-Berichte von anderen Geräten mit der Schaltfläche „JSON-Berichte importieren\" importieren.", german[AppStringKeys.FleetNoScans]);
        Assert.Equal("1 Gerät in diesem Datenordner, 1 gespeicherter Scan. Niedrigste Punktzahl {0} ({1}).", german[AppStringKeys.FleetSummaryOneOne]);
        Assert.Equal("1 Gerät in diesem Datenordner, {0} gespeicherte Scans. Niedrigste Punktzahl {1} ({2}).", german[AppStringKeys.FleetSummaryOneMany]);
        Assert.Equal("{0} Geräte in diesem Datenordner, 1 gespeicherter Scan. Niedrigste Punktzahl {1} ({2}).", german[AppStringKeys.FleetSummaryManyOne]);
        Assert.Equal("{0} Geräte in diesem Datenordner, {1} gespeicherte Scans. Niedrigste Punktzahl {2} ({3}).", german[AppStringKeys.FleetSummaryManyMany]);
        Assert.Equal("nicht messbar", german[AppStringKeys.FleetScoreNotMeasurable]);
        Assert.Equal("Gerätepark geladen.", german[AppStringKeys.FleetLoaded]);
        Assert.Equal("Der Gerätepark konnte nicht gelesen werden: {0}", german[AppStringKeys.FleetReadFailed]);
        Assert.Equal("Der Geräte-Export ist nicht verfügbar.", german[AppStringKeys.FleetMachineExportUnavailable]);
        Assert.Equal("Der Geräte-Export ist fehlgeschlagen: Für {0} wurde kein gespeicherter Scan gefunden.", german[AppStringKeys.FleetMachineExportNoScan]);
        Assert.Equal("Alle Geräte exportieren ist nicht verfügbar.", german[AppStringKeys.FleetExportAllUnavailable]);
        Assert.Equal("Alle Geräte exportieren ist fehlgeschlagen: Es gibt noch keinen Gerätepark zum Exportieren.", german[AppStringKeys.FleetExportAllNoFleet]);
        Assert.Equal("Alle Geräte exportieren ist fehlgeschlagen: {0}", german[AppStringKeys.FleetExportAllFailed]);
        Assert.Equal("0 Geräte exportiert. {0}", german[AppStringKeys.FleetExportAllZero]);
        Assert.Equal("Es gibt noch keinen Gerätepark zum Exportieren.", german[AppStringKeys.FleetExportAllZeroNoFleet]);
        Assert.Equal("Übersprungen {0}: {1}", german[AppStringKeys.FleetSkipped]);
        Assert.Equal("{0} von 1 Gerät nach {1} exportiert.", german[AppStringKeys.FleetExportAllTotalOne]);
        Assert.Equal("{0} von {1} Geräten nach {2} exportiert.", german[AppStringKeys.FleetExportAllTotalMany]);
        Assert.Equal("Entfernen ist nicht verfügbar: Der Scanverlauf ist auf diesem Rechner nicht verfügbar.", german[AppStringKeys.FleetRemoveUnavailable]);
        Assert.Equal("{0} entfernen? Dadurch wird 1 gespeicherter Scan für dieses Gerät aus diesem Datenordner gelöscht. Klicken Sie auf „{1}\", um zu löschen – bis dahin wird nichts gelöscht.", german[AppStringKeys.FleetRemoveArmOne]);
        Assert.Equal("{0} entfernen? Dadurch werden alle {1} gespeicherten Scans für dieses Gerät aus diesem Datenordner gelöscht. Klicken Sie auf „{2}\", um zu löschen – bis dahin wird nichts gelöscht.", german[AppStringKeys.FleetRemoveArmMany]);
        Assert.Equal("Nichts zu entfernen: Für {0} wurden keine gespeicherten Scans gefunden.", german[AppStringKeys.FleetRemoveNothing]);
        Assert.Equal("{0} entfernt (1 gespeicherter Scan).", german[AppStringKeys.FleetRemovedOne]);
        Assert.Equal("{0} entfernt ({1} gespeicherte Scans).", german[AppStringKeys.FleetRemovedMany]);
        Assert.Equal("Entfernen ist fehlgeschlagen: {0}", german[AppStringKeys.FleetRemoveFailed]);
        Assert.Equal("Import ist nicht verfügbar: Der Scanverlauf ist auf diesem Rechner nicht verfügbar.", german[AppStringKeys.FleetImportUnavailable]);
        Assert.Equal("Import ist nicht verfügbar: Es ist keine Dateiauswahl verfügbar.", german[AppStringKeys.FleetImportNoPicker]);
        Assert.Equal("0 Berichte importiert", german[AppStringKeys.FleetImportedNone]);
        Assert.Equal("1 Bericht importiert ({0})", german[AppStringKeys.FleetImportedOne]);
        Assert.Equal("{0} Berichte importiert ({1})", german[AppStringKeys.FleetImportedMany]);

        // Localisation polish (v2.1.18): the import skip reasons are
        // catalogue keys now, so a German user sees German inside the
        // German import line. The file name and any exception message
        // stay data and are never in the catalogue.
        Assert.Equal("Die Datei ist zu groß für einen TrueSpec-Bericht (über 10 MB) und wurde übersprungen.", german[AppStringKeys.SnapshotImportTooLarge]);
        Assert.Equal("Die Datei ist leer und kein TrueSpec-JSON-Bericht.", german[AppStringKeys.SnapshotImportEmpty]);
        Assert.Equal("Die Datei verwendet die Berichtsschema-Version „{0}“, die diese TrueSpec-Version nicht importieren kann. Nur Berichte der Version 1.0 können importiert werden.", german[AppStringKeys.SnapshotImportWrongSchemaVersion]);
        Assert.Equal("Die Datei enthält keinen Scan, es gibt also nichts zu importieren.", german[AppStringKeys.SnapshotImportNoSnapshot]);
        Assert.Equal("Die Datei enthält einen Scan ohne Gerätenamen, er kann daher nicht im Gerätepark aufgeführt werden.", german[AppStringKeys.SnapshotImportNoMachineName]);
        Assert.Equal("Der Datei fehlt ein Teil des Scans (Komponenten, Bewertung oder Käuferprüfungen), sie kann daher nicht importiert werden.", german[AppStringKeys.SnapshotImportMissingPart]);
        Assert.Equal("Die Datei enthält eine Bewertung, die keine Zahl ist, sie kann daher nicht importiert werden.", german[AppStringKeys.SnapshotImportScoreNotFinite]);
        Assert.Equal("Die Datei ist kein TrueSpec-JSON-Bericht und wurde übersprungen.", german[AppStringKeys.SnapshotImportNotJsonReport]);
        Assert.Equal("konnte nicht importiert werden.", german[AppStringKeys.FleetImportCouldNotBeImported]);
        Assert.Equal("konnte nicht gelesen werden ({0}).", german[AppStringKeys.FleetImportCouldNotBeRead]);

        // Localisation polish (v2.1.19): the export-all skip reasons
        // are catalogue keys now, the same pattern as the import
        // reasons. The machine name and any write error stay data and
        // are never in the catalogue.
        Assert.Equal("kein gespeicherter Scan gefunden wurde", german[AppStringKeys.FleetExportAllSkipNoSavedScan]);
        Assert.Equal("der neueste Scan hat keine Bewertung zum Exportieren", german[AppStringKeys.FleetExportAllSkipNoScore]);
        Assert.Equal("konnte nicht geschrieben werden ({0})", german[AppStringKeys.FleetExportAllSkipWriteFailed]);
        Assert.Equal(
            "konnte nicht geschrieben werden (Zugriff verweigert)",
            StringLocalizer.GetFormatted(AppStringKeys.FleetExportAllSkipWriteFailed, "de", "Zugriff verweigert"));

        // Templates keep the app's number formatting in German too.
        Assert.Equal(
            "2 Geräte in diesem Datenordner, 3 gespeicherte Scans. Niedrigste Punktzahl 58 (SHOP-PC-BAD).",
            StringLocalizer.GetFormatted(AppStringKeys.FleetSummaryManyMany, "de", 2, 3, "58", "SHOP-PC-BAD"));
        Assert.Equal(
            "1 Gerät in diesem Datenordner, 1 gespeicherter Scan. Niedrigste Punktzahl nicht messbar (SHOP-PC-BAD).",
            StringLocalizer.GetFormatted(AppStringKeys.FleetSummaryOneOne, "de", "nicht messbar", "SHOP-PC-BAD"));
        Assert.Equal(
            "2 von 2 Geräten nach C:\\Exports exportiert.",
            StringLocalizer.GetFormatted(AppStringKeys.FleetExportAllTotalMany, "de", 2, 2, "C:\\Exports"));
        Assert.Equal(
            "SHOP-PC-BAD entfernen? Dadurch werden alle 2 gespeicherten Scans für dieses Gerät aus diesem Datenordner gelöscht. Klicken Sie auf „Entfernen bestätigen\", um zu löschen – bis dahin wird nichts gelöscht.",
            StringLocalizer.GetFormatted(AppStringKeys.FleetRemoveArmMany, "de", "SHOP-PC-BAD", 2, "Entfernen bestätigen"));
        Assert.Equal(
            "2 Berichte importiert (SHOP-PC-A, SHOP-PC-B). Übersprungen 1: bad.json: Die Datei ist kein TrueSpec-JSON-Bericht und wurde übersprungen.",
            StringLocalizer.GetFormatted(AppStringKeys.FleetImportedMany, "de", 2, "SHOP-PC-A, SHOP-PC-B")
                + ". " + StringLocalizer.GetFormatted(AppStringKeys.FleetSkipped, "de", 1, "bad.json: Die Datei ist kein TrueSpec-JSON-Bericht und wurde übersprungen."));
        Assert.Equal(
            "Die Datei verwendet die Berichtsschema-Version „2.0“, die diese TrueSpec-Version nicht importieren kann. Nur Berichte der Version 1.0 können importiert werden.",
            StringLocalizer.GetFormatted(AppStringKeys.SnapshotImportWrongSchemaVersion, "de", "2.0"));
        Assert.Equal(
            "bad.json: konnte nicht gelesen werden (Zugriff verweigert).",
            "bad.json: " + StringLocalizer.GetFormatted(AppStringKeys.FleetImportCouldNotBeRead, "de", "Zugriff verweigert"));
    }

    [Fact]
    public void FrenchPack_FleetPageTerms()
    {
        // Localisation, phase 6, French (v2.1.25): the Fleet page — the
        // eyebrow, title, intro, the Import button and note, the
        // Machines heading, the two export buttons, the table note,
        // the column headers, the row Export button and the
        // Remove/Confirm-remove button, the transient status lines
        // (loading, no scans, the summary templates, the export-all /
        // remove / import lines, and the import skip reasons since
        // v2.1.18). Same pinning rule as the German phase 8 terms:
        // changing one is a deliberate translation decision, not a
        // typo fix. The data the page shows (machine names, the row
        // cell values, file and folder paths, exception messages) is
        // not in this catalogue; only the page chrome is. The summary
        // and the export-all / remove / import lines are templates
        // with one/many keys instead of inline plural hacks, and the
        // remove-arm line quotes the translated Confirm-remove label.
        // The row's "Not measurable" score fallback and "Not
        // reported" worst-component fallback stay English like the
        // History table's fallbacks; the summary's lowest-score
        // fallback is its own key because it sits mid-sentence inside
        // a translated line. No new keys — the French pack already
        // covers the whole 523-key catalogue.
var french = LanguagePacks.French.Strings;
        Assert.Equal("PARC", french[AppStringKeys.FleetEyebrow]);
        Assert.Equal("Toutes les machines de ce dossier de données", french[AppStringKeys.FleetTitle]);
        Assert.Equal("Mode technicien : emportez l'application portable sur une clé USB, scannez chaque machine, et chaque scan aboutit dans le même magasin sur la clé. Cette page répertorie le dernier scan de chaque machine, le score le plus bas en premier, avec le pire composant nommé, de sorte que la machine qui a besoin d'attention soit en haut. Les machines scannées avec l'application installée peuvent être importées avec Importer des rapports JSON, qui lit les fichiers JSON que la page Rapport exporte. Exporter toutes les machines écrit un CSV par machine dans le dossier d'exportation, de sorte que tout le parc puisse être archivé d'un coup au lieu d'un clic Exporter par ligne. Pour retirer une machine du parc, utilisez Supprimer sur sa ligne — cela supprime tous les scans enregistrés de cette machine de ce dossier de données, en deux clics pour qu'un faux clic ne puisse rien supprimer.", french[AppStringKeys.FleetIntro]);
        Assert.Equal("Importer des rapports JSON", french[AppStringKeys.FleetImportButton]);
        Assert.Equal("Importe les rapports JSON TrueSpec exportés depuis d'autres machines, afin que leurs scans rejoignent ce parc. Un fichier qui n'est pas un rapport TrueSpec est ignoré, et importer deux fois le même fichier n'enregistre qu'un seul scan.", french[AppStringKeys.FleetImportNote]);
        Assert.Equal("Machines", french[AppStringKeys.FleetMachinesTitle]);
        Assert.Equal("Exporter toutes les machines", french[AppStringKeys.FleetExportAllButton]);
        Assert.Equal("Exporter le parc en CSV", french[AppStringKeys.FleetExportFleetCsvButton]);
        Assert.Equal("Score le plus bas en premier. Un tiret signifie que ce scan n'a pas signalé la valeur.", french[AppStringKeys.FleetTableNote]);
        Assert.Equal("Machine", french[AppStringKeys.FleetHeaderMachine]);
        Assert.Equal("Scans", french[AppStringKeys.FleetHeaderScans]);
        Assert.Equal("Dernier scan", french[AppStringKeys.FleetHeaderLastScan]);
        Assert.Equal("Score", french[AppStringKeys.FleetHeaderScore]);
        Assert.Equal("Verdict", french[AppStringKeys.FleetHeaderVerdict]);
        Assert.Equal("Pire composant", french[AppStringKeys.FleetHeaderWorstComponent]);
        Assert.Equal("Exporter", french[AppStringKeys.FleetHeaderExport]);
        Assert.Equal("Supprimer", french[AppStringKeys.FleetHeaderRemove]);
        Assert.Equal("Exporter en CSV", french[AppStringKeys.FleetRowExportButton]);
        Assert.Equal("Confirmer la suppression", french[AppStringKeys.FleetRowConfirmRemoveButton]);
        Assert.Equal("Chargement du parc...", french[AppStringKeys.FleetLoading]);
        Assert.Equal("L'historique des instantanés n'est pas disponible sur cette machine, il n'y a donc pas encore de parc à afficher.", french[AppStringKeys.FleetUnavailable]);
        Assert.Equal("Aucun scan enregistré pour l'instant. Scannez une machine, ou lancez l'application portable sur chaque machine du parc, et son dernier scan apparaît ici. Vous pouvez aussi importer des rapports JSON exportés depuis d'autres machines avec le bouton Importer.", french[AppStringKeys.FleetNoScans]);
        Assert.Equal("1 machine dans ce dossier de données, 1 scan enregistré. Score le plus bas {0} ({1}).", french[AppStringKeys.FleetSummaryOneOne]);
        Assert.Equal("1 machine dans ce dossier de données, {0} scans enregistrés. Score le plus bas {1} ({2}).", french[AppStringKeys.FleetSummaryOneMany]);
        Assert.Equal("{0} machines dans ce dossier de données, 1 scan enregistré. Score le plus bas {1} ({2}).", french[AppStringKeys.FleetSummaryManyOne]);
        Assert.Equal("{0} machines dans ce dossier de données, {1} scans enregistrés. Score le plus bas {2} ({3}).", french[AppStringKeys.FleetSummaryManyMany]);
        Assert.Equal("non mesurable", french[AppStringKeys.FleetScoreNotMeasurable]);
        Assert.Equal("Parc chargé.", french[AppStringKeys.FleetLoaded]);
        Assert.Equal("Le parc n'a pas pu être lu : {0}", french[AppStringKeys.FleetReadFailed]);
        Assert.Equal("L'exportation de la machine n'est pas disponible.", french[AppStringKeys.FleetMachineExportUnavailable]);
        Assert.Equal("Échec de l'exportation de la machine : aucun scan enregistré n'a été trouvé pour {0}.", french[AppStringKeys.FleetMachineExportNoScan]);
        Assert.Equal("Exporter tout n'est pas disponible.", french[AppStringKeys.FleetExportAllUnavailable]);
        Assert.Equal("Échec de l'exportation de tout : il n'y a pas encore de parc à exporter.", french[AppStringKeys.FleetExportAllNoFleet]);
        Assert.Equal("Échec de l'exportation de tout : {0}", french[AppStringKeys.FleetExportAllFailed]);
        Assert.Equal("0 machine exportée. {0}", french[AppStringKeys.FleetExportAllZero]);
        Assert.Equal("il n'y a pas encore de parc à exporter.", french[AppStringKeys.FleetExportAllZeroNoFleet]);
        Assert.Equal("Ignoré {0} : {1}", french[AppStringKeys.FleetSkipped]);
        Assert.Equal("{0} machine sur 1 exportée vers {1}.", french[AppStringKeys.FleetExportAllTotalOne]);
        Assert.Equal("{0} machines sur {1} exportées vers {2}.", french[AppStringKeys.FleetExportAllTotalMany]);
        Assert.Equal("La suppression n'est pas disponible : l'historique des instantanés n'est pas disponible sur cette machine.", french[AppStringKeys.FleetRemoveUnavailable]);
        Assert.Equal("Supprimer {0} ? Cela supprime le seul scan enregistré de cette machine de ce dossier de données. Cliquez sur « {1} » pour supprimer — rien n'est supprimé avant cela.", french[AppStringKeys.FleetRemoveArmOne]);
        Assert.Equal("Supprimer {0} ? Cela supprime les {1} scans enregistrés de cette machine de ce dossier de données. Cliquez sur « {2} » pour supprimer — rien n'est supprimé avant cela.", french[AppStringKeys.FleetRemoveArmMany]);
        Assert.Equal("Rien à supprimer : aucun scan enregistré n'a été trouvé pour {0}.", french[AppStringKeys.FleetRemoveNothing]);
        Assert.Equal("{0} supprimé (1 scan enregistré).", french[AppStringKeys.FleetRemovedOne]);
        Assert.Equal("{0} supprimé ({1} scans enregistrés).", french[AppStringKeys.FleetRemovedMany]);
        Assert.Equal("Échec de la suppression : {0}", french[AppStringKeys.FleetRemoveFailed]);
        Assert.Equal("L'importation n'est pas disponible : l'historique des instantanés n'est pas disponible sur cette machine.", french[AppStringKeys.FleetImportUnavailable]);
        Assert.Equal("L'importation n'est pas disponible : aucun sélecteur de fichier n'est disponible.", french[AppStringKeys.FleetImportNoPicker]);
        Assert.Equal("0 rapport importé", french[AppStringKeys.FleetImportedNone]);
        Assert.Equal("1 rapport importé ({0})", french[AppStringKeys.FleetImportedOne]);
        Assert.Equal("{0} rapports importés ({1})", french[AppStringKeys.FleetImportedMany]);

        // Localisation polish (v2.1.18): the import skip reasons are
        // catalogue keys now, so a German user sees German inside the
        // German import line. The file name and any exception message
        // stay data and are never in the catalogue.
        Assert.Equal("Le fichier est trop volumineux pour être un rapport TrueSpec (plus de 10 Mo) et a été ignoré.", french[AppStringKeys.SnapshotImportTooLarge]);
        Assert.Equal("Le fichier est vide et n'est pas un rapport JSON TrueSpec.", french[AppStringKeys.SnapshotImportEmpty]);
        Assert.Equal("Le fichier utilise la version « {0} » du schéma de rapport, que cette version de TrueSpec ne peut pas importer. Seuls les rapports de version 1.0 peuvent être importés.", french[AppStringKeys.SnapshotImportWrongSchemaVersion]);
        Assert.Equal("Le fichier ne contient aucun scan, il n'y a donc rien à importer.", french[AppStringKeys.SnapshotImportNoSnapshot]);
        Assert.Equal("Le fichier contient un scan sans nom de machine, il ne peut donc pas être répertorié dans le parc.", french[AppStringKeys.SnapshotImportNoMachineName]);
        Assert.Equal("Il manque une partie du scan dans le fichier (composants, score ou contrôles d'acheteur), il ne peut donc pas être importé.", french[AppStringKeys.SnapshotImportMissingPart]);
        Assert.Equal("Le fichier contient un score qui n'est pas un nombre, il ne peut donc pas être importé.", french[AppStringKeys.SnapshotImportScoreNotFinite]);
        Assert.Equal("Le fichier n'est pas un rapport JSON TrueSpec, il a donc été ignoré.", french[AppStringKeys.SnapshotImportNotJsonReport]);
        Assert.Equal("n'a pas pu être importé.", french[AppStringKeys.FleetImportCouldNotBeImported]);
        Assert.Equal("n'a pas pu être lu ({0}).", french[AppStringKeys.FleetImportCouldNotBeRead]);

        // Localisation polish (v2.1.19): the export-all skip reasons
        // are catalogue keys now, the same pattern as the import
        // reasons. The machine name and any write error stay data and
        // are never in the catalogue.
        Assert.Equal("aucun scan enregistré n'a été trouvé", french[AppStringKeys.FleetExportAllSkipNoSavedScan]);
        Assert.Equal("le dernier scan n'a pas de score à exporter", french[AppStringKeys.FleetExportAllSkipNoScore]);
        Assert.Equal("n'a pas pu être écrit ({0})", french[AppStringKeys.FleetExportAllSkipWriteFailed]);
        Assert.Equal(
            "n'a pas pu être écrit (Accès refusé)",
            StringLocalizer.GetFormatted(AppStringKeys.FleetExportAllSkipWriteFailed, "fr", "Accès refusé"));

        // Templates keep the app's number formatting in French too.
        Assert.Equal(
            "2 machines dans ce dossier de données, 3 scans enregistrés. Score le plus bas 58 (SHOP-PC-BAD).",
            StringLocalizer.GetFormatted(AppStringKeys.FleetSummaryManyMany, "fr", 2, 3, "58", "SHOP-PC-BAD"));
        Assert.Equal(
            "1 machine dans ce dossier de données, 1 scan enregistré. Score le plus bas non mesurable (SHOP-PC-BAD).",
            StringLocalizer.GetFormatted(AppStringKeys.FleetSummaryOneOne, "fr", "non mesurable", "SHOP-PC-BAD"));
        Assert.Equal(
            "2 machines sur 2 exportées vers C:\\Exports.",
            StringLocalizer.GetFormatted(AppStringKeys.FleetExportAllTotalMany, "fr", 2, 2, "C:\\Exports"));
        Assert.Equal(
            "Supprimer SHOP-PC-BAD ? Cela supprime les 2 scans enregistrés de cette machine de ce dossier de données. Cliquez sur « Confirmer la suppression » pour supprimer — rien n'est supprimé avant cela.",
            StringLocalizer.GetFormatted(AppStringKeys.FleetRemoveArmMany, "fr", "SHOP-PC-BAD", 2, "Confirmer la suppression"));
        Assert.Equal(
            "2 rapports importés (SHOP-PC-A, SHOP-PC-B). Ignoré 1 : bad.json: Le fichier n'est pas un rapport JSON TrueSpec, il a donc été ignoré.",
            StringLocalizer.GetFormatted(AppStringKeys.FleetImportedMany, "fr", 2, "SHOP-PC-A, SHOP-PC-B")
                + ". " + StringLocalizer.GetFormatted(AppStringKeys.FleetSkipped, "fr", 1, "bad.json: Le fichier n'est pas un rapport JSON TrueSpec, il a donc été ignoré."));
        Assert.Equal(
            "Le fichier utilise la version « 2.0 » du schéma de rapport, que cette version de TrueSpec ne peut pas importer. Seuls les rapports de version 1.0 peuvent être importés.",
            StringLocalizer.GetFormatted(AppStringKeys.SnapshotImportWrongSchemaVersion, "fr", "2.0"));
        Assert.Equal(
            "bad.json: n'a pas pu être lu (Accès refusé).",
            "bad.json: " + StringLocalizer.GetFormatted(AppStringKeys.FleetImportCouldNotBeRead, "fr", "Accès refusé"));
    }

    [Fact]
    public void GermanPack_CoreDiagnosticStrings()
    {
        // Localisation, phase 3 (v2.1.9): the Core diagnostic strings
        // (plug-in status/reason, manifest errors, portable-toggle
        // messages). Same pinning rule as the rail and Settings terms:
        // changing one is a deliberate translation decision, not a typo
        // fix. File names, manifest field names and collector ids stay
        // untranslated inside the sentences; only the words around
        // them change.
        var german = LanguagePacks.German.Strings;
        Assert.Equal("Bereit — noch nicht geladen", german[AppStringKeys.PluginStatusReady]);
        Assert.Equal("Braucht ein neueres TrueSpec", german[AppStringKeys.PluginStatusNeedsNewer]);
        Assert.Equal("Für einen älteren Vertrag geschrieben", german[AppStringKeys.PluginStatusOlderContract]);
        Assert.Equal("Manifestproblem", german[AppStringKeys.PluginStatusManifestProblem]);
        Assert.Equal("Assembly fehlt", german[AppStringKeys.PluginStatusAssemblyMissing]);
        Assert.Equal("Unbekannt", german[AppStringKeys.PluginStatusUnknown]);
        Assert.Equal("Kein plugin.json-Manifest in diesem Ordner.", german[AppStringKeys.PluginReasonNoManifest]);
        Assert.Equal("Die Plug-in-Datei '{0}' fehlt in diesem Ordner.", german[AppStringKeys.PluginReasonAssemblyMissing]);
        Assert.Equal("Dieses Plug-in wurde für die Collector-Vertragsversion {0} geschrieben; dieses TrueSpec spricht Vertragsversion {1}.", german[AppStringKeys.PluginReasonContractVersion]);
        Assert.Equal("Manifest gültig. Noch nicht geladen: Nur signierte Plug-ins werden geladen, und die Plug-in-Signierung ist noch nicht eingerichtet.", german[AppStringKeys.PluginReasonReady]);
        Assert.Equal("Der Plug-ins-Ordner konnte nicht gelesen werden, daher werden keine Plug-ins aufgelistet.", german[AppStringKeys.PluginErrorFolderUnreadable]);
        Assert.Equal("Mehr als {0} Plug-in-Ordner wurden gefunden; nur die ersten {1} (alphabetisch) werden aufgelistet.", german[AppStringKeys.PluginErrorTooMany]);
        Assert.Equal("Das Manifest ist zu groß (über 64 KB) und wurde übersprungen.", german[AppStringKeys.ManifestTooLarge]);
        Assert.Equal("Das Manifest hat ein unbekanntes Feld '{0}' und wurde daher übersprungen.", german[AppStringKeys.ManifestUnknownField]);
        Assert.Equal("Dem Manifest fehlt die 'id', daher wurde es übersprungen.", german[AppStringKeys.ManifestMissingId]);
        Assert.Equal("Das Manifest ist kein gültiges JSON, daher wurde es übersprungen.", german[AppStringKeys.ManifestInvalidJson]);
        Assert.Equal("Die Collector-ID '{0}' ist keine gültige ID (Kleinbuchstaben, Ziffern und Bindestriche, beginnend mit einem Buchstaben), daher wurde das Manifest übersprungen.", german[AppStringKeys.ManifestCollectorBadId]);
        Assert.Equal("Der tragbare Modus ist an. Starten Sie TrueSpec neu: Von da an werden Einstellungen, Verlauf und Sensorprotokoll neben der App gespeichert statt im Standardordner. Bereits gespeicherte Scans bleiben im alten Ordner.", german[AppStringKeys.PortableToggleEnabled]);
        Assert.Equal("Der tragbare Modus ist bereits aus.", german[AppStringKeys.PortableToggleAlreadyOff]);
        Assert.Equal("Es gibt keine Marker-Datei zu entfernen: Der tragbare Modus ist über die Umgebungsvariable TRUESPEC_PORTABLE an. Heben Sie die Variable auf, um ihn auszuschalten.", german[AppStringKeys.PortableToggleNoMarkerEnv]);

        // Templates keep the app's number formatting in German too.
        Assert.Equal(
            "Dieses Plug-in wurde für die Collector-Vertragsversion 2 geschrieben; dieses TrueSpec spricht Vertragsversion 1.",
            StringLocalizer.GetFormatted(AppStringKeys.PluginReasonContractVersion, "de", 2, 1));
        Assert.Equal(
            "Mehr als 100 Plug-in-Ordner wurden gefunden; nur die ersten 100 (alphabetisch) werden aufgelistet.",
            StringLocalizer.GetFormatted(AppStringKeys.PluginErrorTooMany, "de", 100, 100));
    }

    [Fact]
    public void FrenchPack_CoreDiagnosticStrings()
    {
        // Localisation, phase 3, French (v2.1.22): the Core diagnostic
        // strings (plug-in status/reason, manifest errors,
        // portable-toggle messages), mirror of
        // GermanPack_CoreDiagnosticStrings. Same pinning rule as the
        // rail and Settings terms: changing one is a deliberate
        // translation decision, not a typo fix. File names, manifest
        // field names and collector ids stay untranslated inside the
        // sentences; only the words around them change. No new keys:
        // the French pack already covers the whole 523-key catalogue.
        var french = LanguagePacks.French.Strings;
        Assert.Equal("Prêt — pas encore chargé", french[AppStringKeys.PluginStatusReady]);
        Assert.Equal("Nécessite un TrueSpec plus récent", french[AppStringKeys.PluginStatusNeedsNewer]);
        Assert.Equal("Écrit pour un contrat plus ancien", french[AppStringKeys.PluginStatusOlderContract]);
        Assert.Equal("Problème de manifeste", french[AppStringKeys.PluginStatusManifestProblem]);
        Assert.Equal("Assembly manquant", french[AppStringKeys.PluginStatusAssemblyMissing]);
        Assert.Equal("Inconnu", french[AppStringKeys.PluginStatusUnknown]);
        Assert.Equal("Aucun manifeste plugin.json dans ce dossier.", french[AppStringKeys.PluginReasonNoManifest]);
        Assert.Equal("Le fichier de plug-in « {0} » est absent de ce dossier.", french[AppStringKeys.PluginReasonAssemblyMissing]);
        Assert.Equal("Ce plug-in a été écrit pour la version {0} du contrat de collecteur ; ce TrueSpec parle la version {1} du contrat.", french[AppStringKeys.PluginReasonContractVersion]);
        Assert.Equal("Manifeste valide. Pas encore chargé : seuls les plug-ins signés sont chargés, et la signature des plug-ins n'est pas encore en place.", french[AppStringKeys.PluginReasonReady]);
        Assert.Equal("Le dossier des plug-ins n'a pas pu être lu, donc aucun plug-in n'est répertorié.", french[AppStringKeys.PluginErrorFolderUnreadable]);
        Assert.Equal("Plus de {0} dossiers de plug-ins ont été trouvés ; seuls les {1} premiers (par ordre alphabétique) sont répertoriés.", french[AppStringKeys.PluginErrorTooMany]);
        Assert.Equal("Le manifeste est trop volumineux (plus de 64 Ko) et a été ignoré.", french[AppStringKeys.ManifestTooLarge]);
        Assert.Equal("Le manifeste contient un champ inconnu « {0} », il a donc été ignoré.", french[AppStringKeys.ManifestUnknownField]);
        Assert.Equal("Le manifeste ne contient pas son « id », il a donc été ignoré.", french[AppStringKeys.ManifestMissingId]);
        Assert.Equal("Le manifeste n'est pas du JSON valide, il a donc été ignoré.", french[AppStringKeys.ManifestInvalidJson]);
        Assert.Equal("L'id de collecteur « {0} » n'est pas un id valide (lettres minuscules, chiffres et tirets, commençant par une lettre), le manifeste a donc été ignoré.", french[AppStringKeys.ManifestCollectorBadId]);
        Assert.Equal("Le mode portable est activé. Redémarrez TrueSpec : à partir de là, les paramètres, l'historique et le journal des capteurs sont conservés à côté de l'application au lieu du dossier par défaut. Les scans déjà enregistrés restent dans l'ancien dossier.", french[AppStringKeys.PortableToggleEnabled]);
        Assert.Equal("Le mode portable est déjà désactivé.", french[AppStringKeys.PortableToggleAlreadyOff]);
        Assert.Equal("Il n'y a aucun fichier marqueur à supprimer : le mode portable est activé via la variable d'environnement TRUESPEC_PORTABLE. Désactivez la variable pour le désactiver.", french[AppStringKeys.PortableToggleNoMarkerEnv]);

        // Templates keep the app's number formatting in French too.
        Assert.Equal(
            "Ce plug-in a été écrit pour la version 2 du contrat de collecteur ; ce TrueSpec parle la version 1 du contrat.",
            StringLocalizer.GetFormatted(AppStringKeys.PluginReasonContractVersion, "fr", 2, 1));
        Assert.Equal(
            "Plus de 100 dossiers de plug-ins ont été trouvés ; seuls les 100 premiers (par ordre alphabétique) sont répertoriés.",
            StringLocalizer.GetFormatted(AppStringKeys.PluginErrorTooMany, "fr", 100, 100));
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

    [Fact]
    public void FrenchPack_ComparisonPageTerms()
    {
        // Localisation, phase 7, French (v2.1.26): the Comparison page,
        // mirror of GermanPack_ComparisonPageTerms. Same pinning rule
        // as the Fleet terms: changing one is a deliberate translation
        // decision, not a typo fix. The data the page shows (the
        // comparison summary sentence, the row cell values, the DNA
        // changed/unidentified part lines, file paths and exception
        // messages) is not in this catalogue; only the page chrome
        // is. The DNA heading and the unidentified-parts heading
        // reuse the Report page's keys, so they are pinned with the
        // Report terms, not here. No new keys — the French pack
        // already covers the whole 523-key catalogue.
        var french = LanguagePacks.French.Strings;
        Assert.Equal("COMPARER", french[AppStringKeys.ComparisonEyebrow]);
        Assert.Equal("Deux instantanés, côte à côte", french[AppStringKeys.ComparisonTitle]);
        Assert.Equal("Choisissez deux scans enregistrés. TrueSpec montre l'évolution du score, chaque composant qui a bougé et chaque caractéristique qui a changé. Rien n'est inventé : un composant qui n'était mesurable dans aucun des deux scans le dit.", french[AppStringKeys.ComparisonIntro]);
        Assert.Equal("Avant", french[AppStringKeys.ComparisonBefore]);
        Assert.Equal("Après", french[AppStringKeys.ComparisonAfter]);
        Assert.Equal("Choisir un instantané plus ancien", french[AppStringKeys.ComparisonPickEarlier]);
        Assert.Equal("Choisir un instantané plus récent", french[AppStringKeys.ComparisonPickLater]);
        Assert.Equal("Comparer", french[AppStringKeys.ComparisonCompareButton]);
        Assert.Equal("Résultat", french[AppStringKeys.ComparisonResultTitle]);
        Assert.Equal("L'ADN est une identité, pas une santé : il ne change jamais le score ci-dessus. Il dit seulement si les deux scans proviennent de la même machine avec les mêmes pièces.", french[AppStringKeys.ComparisonDnaNote]);
        Assert.Equal("Aucune santé de composant n'a changé et aucune caractéristique n'a changé entre ces deux instantanés.", french[AppStringKeys.ComparisonNoChanges]);
        Assert.Equal("Composants ajoutés", french[AppStringKeys.ComparisonAddedTitle]);
        Assert.Equal("Composants supprimés", french[AppStringKeys.ComparisonRemovedTitle]);
        Assert.Equal("Les instantanés sont enregistrés localement après chaque scan. L'historique ne quitte jamais cette machine.", french[AppStringKeys.ComparisonFooterNote]);
        Assert.Equal("Chargement de l'historique des instantanés...", french[AppStringKeys.ComparisonLoading]);
        Assert.Equal("L'historique des instantanés n'est pas disponible sur cette machine, il n'y a donc encore rien à comparer.", french[AppStringKeys.ComparisonHistoryUnavailable]);
        Assert.Equal("Aucun instantané n'est encore enregistré. Les instantanés sont enregistrés après chaque scan, actualisez donc le scan et revenez pour comparer.", french[AppStringKeys.ComparisonNoSnapshots]);
        Assert.Equal("Un seul instantané est enregistré pour l'instant. Lancez un autre scan (actualiser) et revenez pour comparer.", french[AppStringKeys.ComparisonOnlyOneSnapshot]);
        Assert.Equal("{0} instantanés dans l'historique. Choisissez-en deux et comparez.", french[AppStringKeys.ComparisonSnapshotsCount]);
        Assert.Equal("L'historique n'a pas pu être lu : {0}", french[AppStringKeys.ComparisonHistoryReadFailed]);
        Assert.Equal("Choisissez deux instantanés différents à comparer.", french[AppStringKeys.ComparisonPickDifferent]);
        Assert.Equal("Comparaison des instantanés...", french[AppStringKeys.ComparisonComparing]);
        Assert.Equal("L'un des instantanés n'a pas pu être lu depuis l'historique.", french[AppStringKeys.ComparisonSnapshotUnreadable]);
        Assert.Equal("L'ADN n'a pas pu être comparé : les instantanés n'ont pas pu être lus.", french[AppStringKeys.ComparisonDnaUnreadable]);
        Assert.Equal("{0} comparé à {1}.", french[AppStringKeys.ComparisonCompared]);
        Assert.Equal("Échec de la comparaison : {0}", french[AppStringKeys.ComparisonFailed]);
        Assert.Equal("Aucune comparaison ADN pour l'instant. Choisissez deux instantanés et comparez.", french[AppStringKeys.ComparisonDnaNoComparison]);
        Assert.Equal("Même machine, mêmes pièces.", french[AppStringKeys.ComparisonDnaIdentical]);
        Assert.Equal("Même machine — l'empreinte correspond.", french[AppStringKeys.ComparisonDnaFingerprintMatches]);
        Assert.Equal("Des pièces ont changé entre ces deux scans.", french[AppStringKeys.ComparisonDnaChanged]);
        Assert.Equal("L'ADN n'a pas pu être vérifié pour ces scans.", french[AppStringKeys.ComparisonDnaUnverifiable]);
        Assert.Equal("ADN non enregistré pour l'un des instantanés ou les deux.", french[AppStringKeys.ComparisonDnaNotRecordedState]);
        Assert.Equal("Avant : {0}", french[AppStringKeys.ComparisonDnaHashBefore]);
        Assert.Equal("Après : {0}", french[AppStringKeys.ComparisonDnaHashAfter]);
        Assert.Equal("ADN non enregistré", french[AppStringKeys.ComparisonDnaNotRecorded]);

        // Templates keep the app's number formatting in French too.
        Assert.Equal(
            "2 instantanés dans l'historique. Choisissez-en deux et comparez.",
            StringLocalizer.GetFormatted(AppStringKeys.ComparisonSnapshotsCount, "fr", 2));
        Assert.Equal(
            "2026-11-10 09:00 comparé à 2026-12-10 09:00.",
            StringLocalizer.GetFormatted(AppStringKeys.ComparisonCompared, "fr", "2026-11-10 09:00", "2026-12-10 09:00"));
        Assert.Equal(
            "Avant : abcdef12…34567890",
            StringLocalizer.GetFormatted(AppStringKeys.ComparisonDnaHashBefore, "fr", "abcdef12…34567890"));
        Assert.Equal(
            "L'historique n'a pas pu être lu : disk vanished",
            StringLocalizer.GetFormatted(AppStringKeys.ComparisonHistoryReadFailed, "fr", "disk vanished"));
    }

    [Fact]
    public void FrenchPack_CrashHistoryPageTerms()
    {
        // Localisation, phase 7, French (v2.1.26): the Crash History
        // page, mirror of GermanPack_CrashHistoryPageTerms. Same
        // pinning rule as the Comparison terms above: changing one is
        // a deliberate translation decision, not a typo fix. The data
        // the page shows (component headline, the "What this means"
        // fallback, class names, counts and dates, plain meanings,
        // the "Not reported" date fallback) is not in this
        // catalogue; only the page chrome is. The captured line
        // reuses the detail page's DetailCaptured template, so it is
        // pinned with the detail terms, not here. No new keys — the
        // French pack already covers the whole 523-key catalogue.
        var french = LanguagePacks.French.Strings;
        Assert.Equal("Historique des incidents", french[AppStringKeys.CrashTitle]);
        Assert.Equal("Incident récent", french[AppStringKeys.CrashRecentTitle]);
        Assert.Equal("Un écran bleu et une erreur matérielle fatale se sont produits au cours des 30 derniers jours.", french[AppStringKeys.CrashAlertBoth]);
        Assert.Equal("Un écran bleu s'est produit au cours des 30 derniers jours.", french[AppStringKeys.CrashAlertBugCheck]);
        Assert.Equal("Une erreur matérielle fatale s'est produite au cours des 30 derniers jours.", french[AppStringKeys.CrashAlertWheaFatal]);
        Assert.Equal("Échecs par classe", french[AppStringKeys.CrashFailuresByClass]);
        Assert.Equal("Classe", french[AppStringKeys.CrashHeaderClass]);
        Assert.Equal("Nombre (90 jours)", french[AppStringKeys.CrashHeaderCount]);
        Assert.Equal("Premier", french[AppStringKeys.CrashHeaderFirst]);
        Assert.Equal("Dernier", french[AppStringKeys.CrashHeaderLast]);
        Assert.Equal("Signification", french[AppStringKeys.CrashHeaderMeaning]);
        Assert.Equal("L'historique des incidents n'a pas encore été collecté.", french[AppStringKeys.CrashEmptyNotCollected]);
        Assert.Equal("Aucun écran bleu, aucune erreur matérielle, aucun arrêt inattendu ni aucune erreur de disque n'ont été enregistrés au cours des 90 derniers jours.", french[AppStringKeys.CrashEmptyNoCrashes]);
        Assert.Equal("Le journal des événements ne contient aucune entrée avant le {0}, cet historique peut donc être incomplet. Un journal effacé se lit comme un trou, jamais comme un dossier vierge.", french[AppStringKeys.CrashLogCleared]);
        Assert.Equal("Les nombres couvrent les 90 derniers jours ; le premier et le dernier couvrent toute la période enregistrée. Les significations sont des libellés fixes, pas des diagnostics.", french[AppStringKeys.CrashFooterNote]);

        // Templates keep the app's number formatting in French too.
        Assert.Equal(
            "Le journal des événements ne contient aucune entrée avant le 2026-10-25, cet historique peut donc être incomplet. Un journal effacé se lit comme un trou, jamais comme un dossier vierge.",
            StringLocalizer.GetFormatted(AppStringKeys.CrashLogCleared, "fr", "2026-10-25"));
    }

    [Fact]
    public void FrenchPack_ReportPageTerms()
    {
        // Localisation, phase 7, French (v2.1.26): the Report page,
        // mirror of GermanPack_ReportPageTerms. Same pinning rule as
        // the Crash History terms above: changing one is a deliberate
        // translation decision, not a typo fix. The data the page
        // shows (machine name, verdict text, score explanation,
        // buyer banner and summary, generated-text line, DNA
        // state/hash/verify result texts, export folder path) is not
        // in this catalogue; only the page chrome is. The format
        // headings (JSON, HTML, CSV, PDF) are format names and stay
        // untranslated, like CPU and GPU. No new keys — the French
        // pack already covers the whole 523-key catalogue.
        var french = LanguagePacks.French.Strings;
        Assert.Equal("RAPPORT", french[AppStringKeys.ReportEyebrow]);
        Assert.Equal("Exporter la vérité", french[AppStringKeys.ReportTitle]);
        Assert.Equal(
            "Un seul modèle de données avec des sorties JSON, HTML, CSV et PDF. Tout est généré sur cette machine.",
            french[AppStringKeys.ReportIntro]);
        Assert.Equal("score", french[AppStringKeys.ReportScoreLabel]);
        Assert.Equal("Exporter en JSON", french[AppStringKeys.ReportExportJson]);
        Assert.Equal("Exporter en HTML", french[AppStringKeys.ReportExportHtml]);
        Assert.Equal("Exporter en CSV", french[AppStringKeys.ReportExportCsv]);
        Assert.Equal("Exporter en PDF", french[AppStringKeys.ReportExportPdf]);
        Assert.Equal("Certificat d'acheteur (PDF)", french[AppStringKeys.ReportExportCertificate]);
        Assert.Equal("Vérifier cette machine", french[AppStringKeys.ReportVerifyMachine]);
        Assert.Equal("ADN de la machine", french[AppStringKeys.ReportDnaTitle]);
        Assert.Equal("Vérification de cette machine, nouvelle analyse du matériel...", french[AppStringKeys.ReportVerifyingMachine]);
        Assert.Equal("Non identifié dans l'un des scans :", french[AppStringKeys.ReportDnaUnidentifiedTitle]);
        Assert.Equal(
            "L'ADN est une identité, pas une santé : il ne change jamais le score ni le verdict ci-dessus. Il certifie seulement à quelle machine le score appartient. Un scan en utilisateur standard sans élévation vérifie quand même, mais les pièces qui nécessitent une élévation pour être lues peuvent apparaître comme non identifiées au lieu d'une correspondance.",
            french[AppStringKeys.ReportDnaNote]);
        Assert.Equal("Des données lisibles par machine pour les scripts et le support.", french[AppStringKeys.ReportFormatJsonDescription]);
        Assert.Equal("Une page propre que vous pouvez ouvrir dans n'importe quel navigateur.", french[AppStringKeys.ReportFormatHtmlDescription]);
        Assert.Equal("Un tableur de toutes les caractéristiques, sûr à ouvrir dans Excel. Les valeurs qui ressemblent à des formules sont neutralisées.", french[AppStringKeys.ReportFormatCsvDescription]);
        Assert.Equal("Un rapport imprimable à remettre au vendeur ou à conserver avec le reçu. Construit sur cette machine, sans logiciel supplémentaire.", french[AppStringKeys.ReportFormatPdfDescription]);
        Assert.Equal("Disponible", french[AppStringKeys.ReportFormatAvailable]);
        Assert.Equal("Aperçu du rapport", french[AppStringKeys.ReportPreviewTitle]);
        Assert.Equal("Les rapports exportés sont écrits dans le dossier indiqué ci-dessus. Aucun envoi, aucun cloud.", french[AppStringKeys.ReportFooterNote]);
        Assert.Equal(
            "Les rapports exportés contiennent les numéros de série matériels de cette machine, les adresses MAC, les adresses IP et les noms de réseaux Wi-Fi. Partagez-les uniquement avec des personnes de confiance.",
            french[AppStringKeys.ReportExportPrivacyNote]);
    }

    [Fact]
    public void FrenchPack_TestsPageTerms()
    {
        // Localisation, phase 7, French (v2.1.26): the Tests page,
        // mirror of GermanPack_TestsPageTerms. Same pinning rule as
        // the Report terms above: changing one is a deliberate
        // translation decision, not a typo fix. The data the page
        // shows (per-test names and workload descriptions, measured
        // result summaries and details, load-test measured results,
        // verdict text and live values) is not in this catalogue;
        // only the page chrome is. The load test reuses the test-item
        // status keys ("Pas encore exécuté.", "En cours...", "Échec
        // du test", the no-result template) instead of a second copy
        // of the same words. No new keys — the French pack already
        // covers the whole 523-key catalogue.
        var french = LanguagePacks.French.Strings;
        Assert.Equal("TESTS", french[AppStringKeys.TestsEyebrow]);
        Assert.Equal("Tests intégrés", french[AppStringKeys.TestsTitle]);
        Assert.Equal("Un clic chacun. Aucun envoi caché, aucun compte, et rien ne s'exécute sans que vous le lanciez.", french[AppStringKeys.TestsIntro]);
        Assert.Equal("Les tests du processeur et du disque effectuent de vraies mesures locales. Les tests non implémentés restent désactivés et n'affichent aucun résultat.", french[AppStringKeys.TestsInfoNote]);
        Assert.Equal(
            "Ces benchmarks mesurent uniquement cette machine, avec la charge de travail exacte indiquée sur chaque résultat. Ils ne remplacent pas Geekbench (comparaison multiplateforme) ni 3DMark (rendu GPU). Un résultat obtenu pendant qu'un autre programme utilisait le processeur le dit sur le résultat lui-même.",
            french[AppStringKeys.TestsBenchmarkScopeNote]);
        Assert.Equal("Non disponible dans cette version", french[AppStringKeys.TestsItemNotAvailable]);
        Assert.Equal("Prêt", french[AppStringKeys.TestsItemReady]);
        Assert.Equal("En cours...", french[AppStringKeys.TestsItemRunning]);
        Assert.Equal("Échec du test", french[AppStringKeys.TestsItemFailed]);
        Assert.Equal("Pas encore exécuté.", french[AppStringKeys.TestsItemNotRunYet]);
        Assert.Equal("Ce test n'est pas encore implémenté, aucun résultat n'est donc affiché.", french[AppStringKeys.TestsItemNotImplementedResult]);
        Assert.Equal("Mesure en local. Rien n'est envoyé.", french[AppStringKeys.TestsItemMeasuring]);
        Assert.Equal("Aucun résultat n'est affiché car le test ne s'est pas terminé : {0}", french[AppStringKeys.TestsItemNoResult]);
        Assert.Equal("Lancer le test", french[AppStringKeys.TestsItemRun]);
        Assert.Equal("En cours…", french[AppStringKeys.TestsItemRunningButton]);
        Assert.Equal("Non disponible", french[AppStringKeys.TestsItemNotAvailableButton]);
        Assert.Equal("Test de charge", french[AppStringKeys.LoadTestTitle]);
        Assert.Equal("Charge le processeur sur tous les cœurs, sollicite la mémoire et le disque, et surveille la température et la fréquence d'horloge chaque seconde.", french[AppStringKeys.LoadTestIntro]);
        Assert.Equal("Cette machine fonctionne sur batterie ; les résultats peuvent être limités par l'alimentation.", french[AppStringKeys.LoadTestBatteryNote]);
        Assert.Equal("Durée (secondes) :", french[AppStringKeys.LoadTestDurationLabel]);
        Assert.Equal("Démarrer le test de charge", french[AppStringKeys.LoadTestStart]);
        Assert.Equal("Annuler", french[AppStringKeys.LoadTestCancel]);
        Assert.Equal("Santé des ventilateurs", french[AppStringKeys.LoadTestFanHealthTitle]);
        Assert.Equal("TrueSpec signale uniquement la santé des ventilateurs ; il ne les contrôle jamais.", french[AppStringKeys.LoadTestFanHealthNote]);
        Assert.Equal("Température pendant le test (°C). Un trou dans la ligne signifie que le capteur n'a pas signalé cette seconde-là.", french[AppStringKeys.LoadTestTemperatureCaption]);
        Assert.Equal("Cela va charger votre processeur pendant {0} secondes. Il s'arrête automatiquement si la température dépasse {1} °C.", french[AppStringKeys.LoadTestWarning]);
        Assert.Equal("Un test de {0} secondes est un contrôle fonctionnel, pas un certificat de rodage.", french[AppStringKeys.LoadTestFunctionalCheck]);
        Assert.Equal("Arrêté automatiquement : limite de température atteinte.", french[AppStringKeys.LoadTestStoppedTemperature]);
        Assert.Equal("Arrêté : les capteurs ont cessé de signaler.", french[AppStringKeys.LoadTestStoppedSensorLost]);
        Assert.Equal("Annulé.", french[AppStringKeys.LoadTestCancelled]);
        Assert.Equal("Test de charge terminé.", french[AppStringKeys.LoadTestComplete]);
        Assert.Equal("Température : test non lancé", french[AppStringKeys.LoadTestTempNotRunning]);
        Assert.Equal("Température : mesure en cours...", french[AppStringKeys.LoadTestTempMeasuring]);
        Assert.Equal("Température : test terminé", french[AppStringKeys.LoadTestTempFinished]);
        Assert.Equal("Température : non signalé par le matériel", french[AppStringKeys.LoadTestTempNotReported]);
        Assert.Equal("Horloge : test non lancé", french[AppStringKeys.LoadTestClockNotRunning]);
        Assert.Equal("Horloge : mesure en cours...", french[AppStringKeys.LoadTestClockMeasuring]);
        Assert.Equal("Horloge : test terminé", french[AppStringKeys.LoadTestClockFinished]);
        Assert.Equal("Horloge : non signalé par le matériel", french[AppStringKeys.LoadTestClockNotReported]);

        // Localisation polish (v2.1.18): the measured live lines and
        // the elapsed line translate their labels too, via templates;
        // only the measured value stays data.
        Assert.Equal("Température : {0}", french[AppStringKeys.LoadTestTempMeasured]);
        Assert.Equal("Horloge : {0}", french[AppStringKeys.LoadTestClockMeasured]);
        Assert.Equal("Terminé en {0} s.", french[AppStringKeys.LoadTestElapsedFinished]);

        // Templates keep the app's number formatting in French too.
        Assert.Equal(
            "Cela va charger votre processeur pendant 60 secondes. Il s'arrête automatiquement si la température dépasse 95 °C.",
            StringLocalizer.GetFormatted(AppStringKeys.LoadTestWarning, "fr", "60", "95"));
        Assert.Equal(
            "Un test de 60 secondes est un contrôle fonctionnel, pas un certificat de rodage.",
            StringLocalizer.GetFormatted(AppStringKeys.LoadTestFunctionalCheck, "fr", "60"));
        Assert.Equal(
            "Aucun résultat n'est affiché car le test ne s'est pas terminé : disk vanished",
            StringLocalizer.GetFormatted(AppStringKeys.TestsItemNoResult, "fr", "disk vanished"));
        Assert.Equal(
            "Température : 72.3 C",
            StringLocalizer.GetFormatted(AppStringKeys.LoadTestTempMeasured, "fr", "72.3 C"));
        Assert.Equal(
            "Horloge : 4200 MHz",
            StringLocalizer.GetFormatted(AppStringKeys.LoadTestClockMeasured, "fr", "4200 MHz"));
        Assert.Equal(
            "Terminé en 60 s.",
            StringLocalizer.GetFormatted(AppStringKeys.LoadTestElapsedFinished, "fr", "60"));
    }
}
