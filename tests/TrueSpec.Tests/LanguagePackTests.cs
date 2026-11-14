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
        // status words and the fixed live-line states). Same pinning
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
        // remove / import lines). Same pinning rule as the Comparison
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
            "2 Berichte importiert (SHOP-PC-A, SHOP-PC-B). Übersprungen 1: bad.json: could not be imported.",
            StringLocalizer.GetFormatted(AppStringKeys.FleetImportedMany, "de", 2, "SHOP-PC-A, SHOP-PC-B")
                + ". " + StringLocalizer.GetFormatted(AppStringKeys.FleetSkipped, "de", 1, "bad.json: could not be imported."));
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
