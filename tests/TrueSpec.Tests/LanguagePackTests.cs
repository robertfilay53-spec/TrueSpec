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
            "Navigation, Einstellungen, Komponentendetailseiten, die Übersichtsseite und die Verlaufsseite sind übersetzt. Andere Seiten sind noch auf Englisch und folgen in späteren Versionen.",
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
