using System.Windows.Input;
using TrueSpec.App.ViewModels;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Localisation, phase 8 (v2.1.16): the Report and Tests pages in
/// German — the page chrome follows the language picked in Settings,
/// in place and with no restart. The data that stays English by
/// design: machine names, the verdict text, the score explanation,
/// the buyer banner and summary, the generated-text line, the DNA
/// state/hash/verify result texts, the export folder path, the
/// per-test names and workload descriptions, the measured result
/// summaries and details, and the load-test measured results, verdict
/// text and live values. Transient status lines (a test item's
/// running/failed state, the load test's status and live-line states)
/// are re-derived from stored state, so a switch re-resolves them
/// instead of silently resetting them.
/// Windows leg only (it drives TrueSpec.App's ViewModels), like
/// ReportViewModelTests and TestsViewModelTests: excluded from the
/// net10.0 leg in the csproj. French phase 7 (v2.1.26) mirrors the
/// German Report and Tests tests in French.
/// </summary>
public class ReportTestsLocalizationViewModelTests
{
    private sealed class TestCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }

    private static ReportViewModel Report(string? languageCode = null) =>
        new(
            TestSnapshots.Healthy(),
            new TestCommand(),
            new TestCommand(),
            new TestCommand(),
            "/tmp",
            languageCode: languageCode);

    private static TestsViewModel Tests(string? languageCode = null) =>
        new(new UnavailableSensorMonitor(), null, false, null, languageCode);

    [Fact]
    public void Report_EnglishDefault_RendersChromeInEnglish()
    {
        var report = Report();

        Assert.Equal("REPORT", report.ReportEyebrowText);
        Assert.Equal("Export the truth", report.ReportTitleText);
        Assert.Equal("One data model with JSON, HTML, CSV and PDF outputs. Everything is generated on this machine.", report.ReportIntroText);
        Assert.Equal("score", report.ReportScoreLabelText);
        Assert.Equal("Export JSON", report.ReportExportJsonText);
        Assert.Equal("Export HTML", report.ReportExportHtmlText);
        Assert.Equal("Export CSV", report.ReportExportCsvText);
        Assert.Equal("Export PDF", report.ReportExportPdfText);
        Assert.Equal("Buyer certificate (PDF)", report.ReportExportCertificateText);
        Assert.Equal("Verify this machine", report.ReportVerifyMachineText);
        Assert.Equal("Machine DNA", report.ReportDnaTitleText);
        Assert.Equal("Verifying this machine, re-scanning hardware...", report.ReportVerifyingMachineText);
        Assert.Equal("Not identified in one of the scans:", report.ReportDnaUnidentifiedTitleText);
        Assert.Equal(
            "DNA is identity, not health: it never changes the score or the verdict above. It only certifies which machine the score belongs to. A standard-user scan without elevation still verifies, but parts that need elevation to read may show as unidentified instead of a match.",
            report.ReportDnaNoteText);
        Assert.Equal("Machine readable data for scripts and support.", report.ReportFormatJsonDescriptionText);
        Assert.Equal("A clean page you can open in any browser.", report.ReportFormatHtmlDescriptionText);
        Assert.Equal("A spreadsheet of every spec, safe to open in Excel. Values that look like formulas are neutralised.", report.ReportFormatCsvDescriptionText);
        Assert.Equal("A printable report to hand to a seller or keep with the receipt. Built on this machine, no extra software.", report.ReportFormatPdfDescriptionText);
        Assert.Equal("Available", report.ReportFormatAvailableText);
        Assert.Equal("Report preview", report.ReportPreviewTitleText);
        Assert.Equal("Report exports are written to the folder shown above. No upload, no cloud.", report.ReportFooterNoteText);
        Assert.Equal(
            "Exported reports contain this machine's hardware serials, MAC addresses, IP addresses and Wi-Fi network names. Share them only with people you trust.",
            report.ExportPrivacyNote);
    }

    [Fact]
    public void Report_GermanSeeded_RendersWholePageInGerman()
    {
        var report = Report("de");

        Assert.Equal("BERICHT", report.ReportEyebrowText);
        Assert.Equal("Die Wahrheit exportieren", report.ReportTitleText);
        Assert.Equal("Ein Datenmodell mit JSON-, HTML-, CSV- und PDF-Ausgaben. Alles wird auf diesem Rechner erzeugt.", report.ReportIntroText);
        Assert.Equal("Punktzahl", report.ReportScoreLabelText);
        Assert.Equal("JSON exportieren", report.ReportExportJsonText);
        Assert.Equal("HTML exportieren", report.ReportExportHtmlText);
        Assert.Equal("CSV exportieren", report.ReportExportCsvText);
        Assert.Equal("PDF exportieren", report.ReportExportPdfText);
        Assert.Equal("Käuferzertifikat (PDF)", report.ReportExportCertificateText);
        Assert.Equal("Dieses Gerät prüfen", report.ReportVerifyMachineText);
        Assert.Equal("Geräte-DNA", report.ReportDnaTitleText);
        Assert.Equal("Dieses Gerät wird geprüft, die Hardware wird erneut gescannt …", report.ReportVerifyingMachineText);
        Assert.Equal("In einem der Scans nicht identifiziert:", report.ReportDnaUnidentifiedTitleText);
        Assert.Equal(
            "Die DNA ist Identität, nicht Gesundheit: Sie ändert niemals die Punktzahl oder das Urteil oben. Sie bestätigt nur, zu welchem Gerät die Punktzahl gehört. Ein Scan als Standardbenutzer ohne erhöhte Rechte prüft trotzdem, aber Teile, die erhöhte Rechte zum Lesen brauchen, können als nicht identifiziert statt als übereinstimmend angezeigt werden.",
            report.ReportDnaNoteText);
        Assert.Equal("Maschinenlesbare Daten für Skripte und Support.", report.ReportFormatJsonDescriptionText);
        Assert.Equal("Eine übersichtliche Seite, die Sie in jedem Browser öffnen können.", report.ReportFormatHtmlDescriptionText);
        Assert.Equal("Eine Tabelle mit allen Spezifikationen, sicher in Excel zu öffnen. Werte, die wie Formeln aussehen, werden neutralisiert.", report.ReportFormatCsvDescriptionText);
        Assert.Equal("Ein ausdruckbarer Bericht für den Verkäufer oder zum Aufbewahren beim Kaufbeleg. Auf diesem Rechner erstellt, keine zusätzliche Software.", report.ReportFormatPdfDescriptionText);
        Assert.Equal("Verfügbar", report.ReportFormatAvailableText);
        Assert.Equal("Berichtsvorschau", report.ReportPreviewTitleText);
        Assert.Equal("Berichtsexporte werden in den oben angezeigten Ordner geschrieben. Kein Hochladen, keine Cloud.", report.ReportFooterNoteText);
        Assert.Equal(
            "Exportierte Berichte enthalten die Hardware-Seriennummern, MAC-Adressen, IP-Adressen und WLAN-Namen dieses Geräts. Teilen Sie sie nur mit Personen, denen Sie vertrauen.",
            report.ExportPrivacyNote);
    }

    [Fact]
    public void Report_ApplyLanguage_SwitchesBothWays()
    {
        var report = Report();

        report.ApplyLanguage("de");
        Assert.Equal("BERICHT", report.ReportEyebrowText);
        Assert.Equal("Die Wahrheit exportieren", report.ReportTitleText);
        Assert.Equal("JSON exportieren", report.ReportExportJsonText);
        Assert.Equal("Geräte-DNA", report.ReportDnaTitleText);
        Assert.Equal("Verfügbar", report.ReportFormatAvailableText);

        report.ApplyLanguage("en");
        Assert.Equal("REPORT", report.ReportEyebrowText);
        Assert.Equal("Export the truth", report.ReportTitleText);
        Assert.Equal("Export JSON", report.ReportExportJsonText);
        Assert.Equal("Machine DNA", report.ReportDnaTitleText);
        Assert.Equal("Available", report.ReportFormatAvailableText);

        report.ApplyLanguage("de");
        Assert.Equal("BERICHT", report.ReportEyebrowText);
    }

    [Fact]
    public void Report_UnknownLanguage_FallsBackToEnglish()
    {
        var report = Report("xx");

        Assert.Equal("REPORT", report.ReportEyebrowText);
        Assert.Equal("Export the truth", report.ReportTitleText);
        Assert.Equal("Available", report.ReportFormatAvailableText);
        Assert.Equal(
            "Exported reports contain this machine's hardware serials, MAC addresses, IP addresses and Wi-Fi network names. Share them only with people you trust.",
            report.ExportPrivacyNote);
    }

    [Fact]
    public void Report_DataStaysEnglish_WhenChromeIsGerman()
    {
        var english = Report();
        var german = Report("de");

        Assert.Equal(english.MachineName, german.MachineName);
        Assert.Equal(english.VerdictText, german.VerdictText);
        Assert.Equal(english.ScoreExplanation, german.ScoreExplanation);
        Assert.Equal(english.BuyerBanner, german.BuyerBanner);
        Assert.Equal(english.BuyerSummary, german.BuyerSummary);
        Assert.Equal(english.GeneratedText, german.GeneratedText);
        Assert.Equal(english.DnaStateText, german.DnaStateText);
        Assert.Equal(english.DnaHashText, german.DnaHashText);
        Assert.Equal(english.VerifyResultText, german.VerifyResultText);
        Assert.Equal(english.ExportFolderText, german.ExportFolderText);
        Assert.Equal(english.Components.Select(c => c.Name), german.Components.Select(c => c.Name));
        Assert.Equal(english.Components.Select(c => c.Headline), german.Components.Select(c => c.Headline));
        Assert.Equal(english.Components.Select(c => c.HealthText), german.Components.Select(c => c.HealthText));

        // A switch re-renders the chrome in place, keeping the data.
        german.ApplyLanguage("en");
        Assert.Equal(english.ReportEyebrowText, german.ReportEyebrowText);
        Assert.Equal(english.MachineName, german.MachineName);
        Assert.Equal(english.VerdictText, german.VerdictText);
    }

    [Fact]
    public void Tests_EnglishDefault_RendersChromeInEnglish()
    {
        var viewModel = Tests();

        Assert.Equal("TESTS", viewModel.TestsEyebrowText);
        Assert.Equal("Built-in tests", viewModel.TestsTitleText);
        Assert.Equal("One click each. No hidden uploads, no account, and nothing runs without you starting it.", viewModel.TestsIntroText);
        Assert.Equal("CPU and disk tests run real local measurements. Tests that are not implemented stay disabled and show no result.", viewModel.TestsInfoNoteText);
        Assert.Equal(
            "These benchmarks measure this machine only, with the exact workload shown on each result. They do not replace Geekbench (cross-platform comparison) or 3DMark (GPU rendering). A result made while another program was using the CPU says so on the result itself.",
            viewModel.BenchmarkScopeNote);

        var loadTest = viewModel.LoadTest;
        Assert.Equal("Load test", loadTest.LoadTestTitleText);
        Assert.Equal("Loads the CPU on all cores, exercises memory and disk, and watches temperature and clock speed every second.", loadTest.LoadTestIntroText);
        Assert.Equal("This machine is running on battery; results may be power-limited.", loadTest.LoadTestBatteryNoteText);
        Assert.Equal("Duration (seconds):", loadTest.LoadTestDurationLabelText);
        Assert.Equal("Start load test", loadTest.LoadTestStartText);
        Assert.Equal("Cancel", loadTest.LoadTestCancelText);
        Assert.Equal("Fan health", loadTest.LoadTestFanHealthTitleText);
        Assert.Equal("TrueSpec reports fan health only; it never controls fans.", loadTest.LoadTestFanHealthNoteText);
        Assert.Equal("Temperature during the test (C). A gap in the line means the sensor did not report that second.", loadTest.LoadTestTemperatureCaptionText);
        Assert.Equal("This will load your CPU for 60 seconds. It stops automatically if the temperature exceeds 95 C.", loadTest.WarningText);
        Assert.Equal("A 60-second test is a functional check, not a burn-in certificate.", loadTest.FunctionalCheckText);
        Assert.Equal("Not run yet.", loadTest.StatusText);
        Assert.Equal("Temperature: not running", loadTest.LiveTemperatureText);
        Assert.Equal("Clock: not running", loadTest.LiveClockText);

        Assert.Equal("Run test", viewModel.Tests[0].RunButtonText);
        Assert.Equal("Ready", viewModel.Tests[0].Status);
        Assert.Equal("Not run yet.", viewModel.Tests[0].ResultText);
        Assert.Equal("Not available", viewModel.Tests[6].RunButtonText);
        Assert.Equal("Not available in this build", viewModel.Tests[6].Status);
        Assert.Equal("This test is not implemented yet, so no result is shown.", viewModel.Tests[6].ResultText);
    }

    [Fact]
    public void Tests_GermanSeeded_RendersWholePageInGerman()
    {
        var viewModel = Tests("de");

        Assert.Equal("TESTS", viewModel.TestsEyebrowText);
        Assert.Equal("Eingebaute Tests", viewModel.TestsTitleText);
        Assert.Equal("Jeweils ein Klick. Keine versteckten Uploads, kein Konto, und nichts läuft, ohne dass Sie es starten.", viewModel.TestsIntroText);
        Assert.Equal("CPU- und Datenträgertests führen echte lokale Messungen durch. Tests, die nicht umgesetzt sind, bleiben deaktiviert und zeigen kein Ergebnis.", viewModel.TestsInfoNoteText);
        Assert.Equal(
            "Diese Benchmarks messen nur dieses Gerät, mit der genauen Arbeitslast, die bei jedem Ergebnis angegeben ist. Sie ersetzen weder Geekbench (plattformübergreifender Vergleich) noch 3DMark (GPU-Rendering). Ein Ergebnis, das entstand, während ein anderes Programm die CPU nutzte, sagt dies auf dem Ergebnis selbst.",
            viewModel.BenchmarkScopeNote);

        var loadTest = viewModel.LoadTest;
        Assert.Equal("Belastungstest", loadTest.LoadTestTitleText);
        Assert.Equal("Belastet die CPU auf allen Kernen, beansprucht Arbeitsspeicher und Datenträger und beobachtet jede Sekunde Temperatur und Takt.", loadTest.LoadTestIntroText);
        Assert.Equal("Dieses Gerät läuft auf Akku; die Ergebnisse können leistungsbegrenzt sein.", loadTest.LoadTestBatteryNoteText);
        Assert.Equal("Dauer (Sekunden):", loadTest.LoadTestDurationLabelText);
        Assert.Equal("Belastungstest starten", loadTest.LoadTestStartText);
        Assert.Equal("Abbrechen", loadTest.LoadTestCancelText);
        Assert.Equal("Lüfterzustand", loadTest.LoadTestFanHealthTitleText);
        Assert.Equal("TrueSpec meldet nur den Lüfterzustand; es steuert niemals Lüfter.", loadTest.LoadTestFanHealthNoteText);
        Assert.Equal("Temperatur während des Tests (°C). Eine Lücke in der Linie bedeutet, dass der Sensor in dieser Sekunde nichts gemeldet hat.", loadTest.LoadTestTemperatureCaptionText);
        Assert.Equal("Dadurch wird Ihre CPU 60 Sekunden lang belastet. Der Test stoppt automatisch, wenn die Temperatur 95 °C übersteigt.", loadTest.WarningText);
        Assert.Equal("Ein 60-Sekunden-Test ist eine Funktionsprüfung, kein Dauerbelastungszertifikat.", loadTest.FunctionalCheckText);
        Assert.Equal("Noch nicht ausgeführt.", loadTest.StatusText);
        Assert.Equal("Temperatur: läuft nicht", loadTest.LiveTemperatureText);
        Assert.Equal("Takt: läuft nicht", loadTest.LiveClockText);

        Assert.Equal("Test starten", viewModel.Tests[0].RunButtonText);
        Assert.Equal("Bereit", viewModel.Tests[0].Status);
        Assert.Equal("Noch nicht ausgeführt.", viewModel.Tests[0].ResultText);
        Assert.Equal("Nicht verfügbar", viewModel.Tests[6].RunButtonText);
        Assert.Equal("In dieser Version nicht verfügbar", viewModel.Tests[6].Status);
        Assert.Equal("Dieser Test ist noch nicht umgesetzt, daher wird kein Ergebnis angezeigt.", viewModel.Tests[6].ResultText);
    }

    [Fact]
    public void Tests_ApplyLanguage_SwitchesBothWays()
    {
        var viewModel = Tests();

        viewModel.ApplyLanguage("de");
        Assert.Equal("Eingebaute Tests", viewModel.TestsTitleText);
        Assert.Equal("Belastungstest", viewModel.LoadTest.LoadTestTitleText);
        Assert.Equal("Dadurch wird Ihre CPU 60 Sekunden lang belastet. Der Test stoppt automatisch, wenn die Temperatur 95 °C übersteigt.", viewModel.LoadTest.WarningText);
        Assert.Equal("Noch nicht ausgeführt.", viewModel.LoadTest.StatusText);
        Assert.Equal("Temperatur: läuft nicht", viewModel.LoadTest.LiveTemperatureText);
        Assert.Equal("Test starten", viewModel.Tests[0].RunButtonText);
        Assert.Equal("Bereit", viewModel.Tests[0].Status);
        Assert.Equal("In dieser Version nicht verfügbar", viewModel.Tests[6].Status);

        viewModel.ApplyLanguage("en");
        Assert.Equal("Built-in tests", viewModel.TestsTitleText);
        Assert.Equal("Load test", viewModel.LoadTest.LoadTestTitleText);
        Assert.Equal("This will load your CPU for 60 seconds. It stops automatically if the temperature exceeds 95 C.", viewModel.LoadTest.WarningText);
        Assert.Equal("Not run yet.", viewModel.LoadTest.StatusText);
        Assert.Equal("Temperature: not running", viewModel.LoadTest.LiveTemperatureText);
        Assert.Equal("Run test", viewModel.Tests[0].RunButtonText);
        Assert.Equal("Ready", viewModel.Tests[0].Status);
        Assert.Equal("Not available in this build", viewModel.Tests[6].Status);

        viewModel.ApplyLanguage("de");
        Assert.Equal("Eingebaute Tests", viewModel.TestsTitleText);
        Assert.Equal("Belastungstest", viewModel.LoadTest.LoadTestTitleText);
    }

    [Fact]
    public void Tests_UnknownLanguage_FallsBackToEnglish()
    {
        var viewModel = Tests("xx");

        Assert.Equal("Built-in tests", viewModel.TestsTitleText);
        Assert.Equal("Load test", viewModel.LoadTest.LoadTestTitleText);
        Assert.Equal("Not run yet.", viewModel.LoadTest.StatusText);
        Assert.Equal("Run test", viewModel.Tests[0].RunButtonText);
        Assert.Equal("Ready", viewModel.Tests[0].Status);
    }

    [Fact]
    public void Tests_DataStaysEnglish_WhenChromeIsGerman()
    {
        var english = Tests();
        var german = Tests("de");

        Assert.Equal(
            english.Tests.Select(t => t.Name),
            german.Tests.Select(t => t.Name));
        Assert.Equal(
            english.Tests.Select(t => t.Description),
            german.Tests.Select(t => t.Description));

        // The load-test result lines are measured data: the static
        // builders keep their English wording in both languages.
        var result = new LoadTestResult(
            Array.Empty<LoadTestSample>(),
            PeakTempC: 70d,
            AvgClockMHz: null,
            RatedMaxMHz: 4100d,
            ClockHeldPercent: null,
            Throttled: false,
            PowerLimited: false,
            AbortReason: null,
            VerdictText: "Test verdict.",
            DurationSeconds: 60,
            GapCount: 0,
            MemoryPatternVerified: true,
            DiskReadMBps: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow.AddSeconds(60));
        Assert.Equal("Rated max clock: 4100 MHz", LoadTestViewModel.RatedMaxText(result));
        Assert.Equal("Held % of rated clock: not reported by hardware", LoadTestViewModel.HeldPercentText(result));
    }

    [Fact]
    public void Report_FrenchSeeded_RendersWholePageInFrench()
    {
        var report = Report("fr");

        Assert.Equal("RAPPORT", report.ReportEyebrowText);
        Assert.Equal("Exporter la vérité", report.ReportTitleText);
        Assert.Equal("Un seul modèle de données avec des sorties JSON, HTML, CSV et PDF. Tout est généré sur cette machine.", report.ReportIntroText);
        Assert.Equal("score", report.ReportScoreLabelText);
        Assert.Equal("Exporter en JSON", report.ReportExportJsonText);
        Assert.Equal("Exporter en HTML", report.ReportExportHtmlText);
        Assert.Equal("Exporter en CSV", report.ReportExportCsvText);
        Assert.Equal("Exporter en PDF", report.ReportExportPdfText);
        Assert.Equal("Certificat d'acheteur (PDF)", report.ReportExportCertificateText);
        Assert.Equal("Vérifier cette machine", report.ReportVerifyMachineText);
        Assert.Equal("ADN de la machine", report.ReportDnaTitleText);
        Assert.Equal("Vérification de cette machine, nouvelle analyse du matériel...", report.ReportVerifyingMachineText);
        Assert.Equal("Non identifié dans l'un des scans :", report.ReportDnaUnidentifiedTitleText);
        Assert.Equal(
            "L'ADN est une identité, pas une santé : il ne change jamais le score ni le verdict ci-dessus. Il certifie seulement à quelle machine le score appartient. Un scan en utilisateur standard sans élévation vérifie quand même, mais les pièces qui nécessitent une élévation pour être lues peuvent apparaître comme non identifiées au lieu d'une correspondance.",
            report.ReportDnaNoteText);
        Assert.Equal("Des données lisibles par machine pour les scripts et le support.", report.ReportFormatJsonDescriptionText);
        Assert.Equal("Une page propre que vous pouvez ouvrir dans n'importe quel navigateur.", report.ReportFormatHtmlDescriptionText);
        Assert.Equal("Un tableur de toutes les caractéristiques, sûr à ouvrir dans Excel. Les valeurs qui ressemblent à des formules sont neutralisées.", report.ReportFormatCsvDescriptionText);
        Assert.Equal("Un rapport imprimable à remettre au vendeur ou à conserver avec le reçu. Construit sur cette machine, sans logiciel supplémentaire.", report.ReportFormatPdfDescriptionText);
        Assert.Equal("Disponible", report.ReportFormatAvailableText);
        Assert.Equal("Aperçu du rapport", report.ReportPreviewTitleText);
        Assert.Equal("Les rapports exportés sont écrits dans le dossier indiqué ci-dessus. Aucun envoi, aucun cloud.", report.ReportFooterNoteText);
        Assert.Equal(
            "Les rapports exportés contiennent les numéros de série matériels de cette machine, les adresses MAC, les adresses IP et les noms de réseaux Wi-Fi. Partagez-les uniquement avec des personnes de confiance.",
            report.ExportPrivacyNote);
    }

    [Fact]
    public void Report_ApplyLanguage_SwitchesToFrenchAndBack()
    {
        var report = Report();

        report.ApplyLanguage("fr");
        Assert.Equal("RAPPORT", report.ReportEyebrowText);
        Assert.Equal("Exporter la vérité", report.ReportTitleText);
        Assert.Equal("Exporter en JSON", report.ReportExportJsonText);
        Assert.Equal("ADN de la machine", report.ReportDnaTitleText);
        Assert.Equal("Disponible", report.ReportFormatAvailableText);

        report.ApplyLanguage("en");
        Assert.Equal("REPORT", report.ReportEyebrowText);
        Assert.Equal("Export the truth", report.ReportTitleText);
        Assert.Equal("Export JSON", report.ReportExportJsonText);
        Assert.Equal("Machine DNA", report.ReportDnaTitleText);
        Assert.Equal("Available", report.ReportFormatAvailableText);

        report.ApplyLanguage("fr");
        Assert.Equal("RAPPORT", report.ReportEyebrowText);
    }

    [Fact]
    public void Report_DataStaysEnglish_WhenChromeIsFrench()
    {
        var english = Report();
        var french = Report("fr");

        Assert.Equal(english.MachineName, french.MachineName);
        Assert.Equal(english.VerdictText, french.VerdictText);
        Assert.Equal(english.ScoreExplanation, french.ScoreExplanation);
        Assert.Equal(english.BuyerBanner, french.BuyerBanner);
        Assert.Equal(english.BuyerSummary, french.BuyerSummary);
        Assert.Equal(english.GeneratedText, french.GeneratedText);
        Assert.Equal(english.DnaStateText, french.DnaStateText);
        Assert.Equal(english.DnaHashText, french.DnaHashText);
        Assert.Equal(english.VerifyResultText, french.VerifyResultText);
        Assert.Equal(english.ExportFolderText, french.ExportFolderText);
        Assert.Equal(english.Components.Select(c => c.Name), french.Components.Select(c => c.Name));
        Assert.Equal(english.Components.Select(c => c.Headline), french.Components.Select(c => c.Headline));
        Assert.Equal(english.Components.Select(c => c.HealthText), french.Components.Select(c => c.HealthText));

        // A switch re-renders the chrome in place, keeping the data.
        french.ApplyLanguage("en");
        Assert.Equal(english.ReportEyebrowText, french.ReportEyebrowText);
        Assert.Equal(english.MachineName, french.MachineName);
        Assert.Equal(english.VerdictText, french.VerdictText);
    }

    [Fact]
    public void Tests_FrenchSeeded_RendersWholePageInFrench()
    {
        var viewModel = Tests("fr");

        Assert.Equal("TESTS", viewModel.TestsEyebrowText);
        Assert.Equal("Tests intégrés", viewModel.TestsTitleText);
        Assert.Equal("Un clic chacun. Aucun envoi caché, aucun compte, et rien ne s'exécute sans que vous le lanciez.", viewModel.TestsIntroText);
        Assert.Equal("Les tests du processeur et du disque effectuent de vraies mesures locales. Les tests non implémentés restent désactivés et n'affichent aucun résultat.", viewModel.TestsInfoNoteText);
        Assert.Equal(
            "Ces benchmarks mesurent uniquement cette machine, avec la charge de travail exacte indiquée sur chaque résultat. Ils ne remplacent pas Geekbench (comparaison multiplateforme) ni 3DMark (rendu GPU). Un résultat obtenu pendant qu'un autre programme utilisait le processeur le dit sur le résultat lui-même.",
            viewModel.BenchmarkScopeNote);

        var loadTest = viewModel.LoadTest;
        Assert.Equal("Test de charge", loadTest.LoadTestTitleText);
        Assert.Equal("Charge le processeur sur tous les cœurs, sollicite la mémoire et le disque, et surveille la température et la fréquence d'horloge chaque seconde.", loadTest.LoadTestIntroText);
        Assert.Equal("Cette machine fonctionne sur batterie ; les résultats peuvent être limités par l'alimentation.", loadTest.LoadTestBatteryNoteText);
        Assert.Equal("Durée (secondes) :", loadTest.LoadTestDurationLabelText);
        Assert.Equal("Démarrer le test de charge", loadTest.LoadTestStartText);
        Assert.Equal("Annuler", loadTest.LoadTestCancelText);
        Assert.Equal("Santé des ventilateurs", loadTest.LoadTestFanHealthTitleText);
        Assert.Equal("TrueSpec signale uniquement la santé des ventilateurs ; il ne les contrôle jamais.", loadTest.LoadTestFanHealthNoteText);
        Assert.Equal("Température pendant le test (°C). Un trou dans la ligne signifie que le capteur n'a pas signalé cette seconde-là.", loadTest.LoadTestTemperatureCaptionText);
        Assert.Equal("Cela va charger votre processeur pendant 60 secondes. Il s'arrête automatiquement si la température dépasse 95 °C.", loadTest.WarningText);
        Assert.Equal("Un test de 60 secondes est un contrôle fonctionnel, pas un certificat de rodage.", loadTest.FunctionalCheckText);
        Assert.Equal("Pas encore exécuté.", loadTest.StatusText);
        Assert.Equal("Température : test non lancé", loadTest.LiveTemperatureText);
        Assert.Equal("Horloge : test non lancé", loadTest.LiveClockText);

        Assert.Equal("Lancer le test", viewModel.Tests[0].RunButtonText);
        Assert.Equal("Prêt", viewModel.Tests[0].Status);
        Assert.Equal("Pas encore exécuté.", viewModel.Tests[0].ResultText);
        Assert.Equal("Non disponible", viewModel.Tests[6].RunButtonText);
        Assert.Equal("Non disponible dans cette version", viewModel.Tests[6].Status);
        Assert.Equal("Ce test n'est pas encore implémenté, aucun résultat n'est donc affiché.", viewModel.Tests[6].ResultText);
    }

    [Fact]
    public void Tests_ApplyLanguage_SwitchesToFrenchAndBack()
    {
        var viewModel = Tests();

        viewModel.ApplyLanguage("fr");
        Assert.Equal("Tests intégrés", viewModel.TestsTitleText);
        Assert.Equal("Test de charge", viewModel.LoadTest.LoadTestTitleText);
        Assert.Equal("Cela va charger votre processeur pendant 60 secondes. Il s'arrête automatiquement si la température dépasse 95 °C.", viewModel.LoadTest.WarningText);
        Assert.Equal("Pas encore exécuté.", viewModel.LoadTest.StatusText);
        Assert.Equal("Température : test non lancé", viewModel.LoadTest.LiveTemperatureText);
        Assert.Equal("Lancer le test", viewModel.Tests[0].RunButtonText);
        Assert.Equal("Prêt", viewModel.Tests[0].Status);
        Assert.Equal("Non disponible dans cette version", viewModel.Tests[6].Status);

        viewModel.ApplyLanguage("en");
        Assert.Equal("Built-in tests", viewModel.TestsTitleText);
        Assert.Equal("Load test", viewModel.LoadTest.LoadTestTitleText);
        Assert.Equal("This will load your CPU for 60 seconds. It stops automatically if the temperature exceeds 95 C.", viewModel.LoadTest.WarningText);
        Assert.Equal("Not run yet.", viewModel.LoadTest.StatusText);
        Assert.Equal("Temperature: not running", viewModel.LoadTest.LiveTemperatureText);
        Assert.Equal("Run test", viewModel.Tests[0].RunButtonText);
        Assert.Equal("Ready", viewModel.Tests[0].Status);
        Assert.Equal("Not available in this build", viewModel.Tests[6].Status);

        viewModel.ApplyLanguage("fr");
        Assert.Equal("Tests intégrés", viewModel.TestsTitleText);
        Assert.Equal("Test de charge", viewModel.LoadTest.LoadTestTitleText);
    }

    [Fact]
    public void Tests_DataStaysEnglish_WhenChromeIsFrench()
    {
        var english = Tests();
        var french = Tests("fr");

        Assert.Equal(
            english.Tests.Select(t => t.Name),
            french.Tests.Select(t => t.Name));
        Assert.Equal(
            english.Tests.Select(t => t.Description),
            french.Tests.Select(t => t.Description));

        // The load-test result lines are measured data: the static
        // builders keep their English wording in both languages.
        var result = new LoadTestResult(
            Array.Empty<LoadTestSample>(),
            PeakTempC: 70d,
            AvgClockMHz: null,
            RatedMaxMHz: 4100d,
            ClockHeldPercent: null,
            Throttled: false,
            PowerLimited: false,
            AbortReason: null,
            VerdictText: "Test verdict.",
            DurationSeconds: 60,
            GapCount: 0,
            MemoryPatternVerified: true,
            DiskReadMBps: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow.AddSeconds(60));
        Assert.Equal("Rated max clock: 4100 MHz", LoadTestViewModel.RatedMaxText(result));
        Assert.Equal("Held % of rated clock: not reported by hardware", LoadTestViewModel.HeldPercentText(result));
    }

    [Fact]
    public async Task TestItem_RunAsync_Failure_IsLocalized_AndSwitchReResolvesIt_French()
    {
        var item = new TestItemViewModel(
            "Fake test",
            "A test double.",
            _ => throw new InvalidOperationException("disk vanished"),
            "fr");

        await item.RunCommand.ExecuteAsync(null);

        Assert.False(item.IsRunning);
        Assert.Equal("Échec du test", item.Status);
        Assert.Equal("Aucun résultat n'est affiché car le test ne s'est pas terminé : disk vanished", item.ResultText);
        Assert.Equal("Lancer le test", item.RunButtonText);

        // A switch re-resolves the failure line from the stored
        // exception message instead of resetting it.
        item.ApplyLanguage("en");
        Assert.Equal("Test failed", item.Status);
        Assert.Equal("No result is shown because the test did not complete: disk vanished", item.ResultText);

        item.ApplyLanguage("fr");
        Assert.Equal("Échec du test", item.Status);
        Assert.Equal("Aucun résultat n'est affiché car le test ne s'est pas terminé : disk vanished", item.ResultText);
    }

    [Fact]
    public async Task TestItem_RunAsync_Success_KeepsMeasuredResult_WhenChromeIsFrench()
    {
        var item = new TestItemViewModel(
            "Fake test",
            "A test double.",
            _ => Task.FromResult(new BenchmarkResult(
                "Fake test", true, "1,234 ops/s", "Measured 1,234 operations per second.", "Local benchmark", 1234d, "ops/s")),
            "fr");

        await item.RunCommand.ExecuteAsync(null);

        Assert.False(item.IsRunning);
        Assert.Equal("1,234 ops/s", item.Status);
        Assert.Equal("Measured 1,234 operations per second. Source: Local benchmark.", item.ResultText);
        Assert.Equal("Lancer le test", item.RunButtonText);

        item.ApplyLanguage("en");
        Assert.Equal("1,234 ops/s", item.Status);
        Assert.Equal("Measured 1,234 operations per second. Source: Local benchmark.", item.ResultText);
        Assert.Equal("Run test", item.RunButtonText);
    }

    [Fact]
    public async Task TestItem_RunAsync_Failure_IsLocalized_AndSwitchReResolvesIt()
    {
        var item = new TestItemViewModel(
            "Fake test",
            "A test double.",
            _ => throw new InvalidOperationException("disk vanished"),
            "de");

        await item.RunCommand.ExecuteAsync(null);

        Assert.False(item.IsRunning);
        Assert.Equal("Test fehlgeschlagen", item.Status);
        Assert.Equal("Es wird kein Ergebnis angezeigt, weil der Test nicht abgeschlossen wurde: disk vanished", item.ResultText);
        Assert.Equal("Test starten", item.RunButtonText);

        // A switch re-resolves the failure line from the stored
        // exception message instead of resetting it.
        item.ApplyLanguage("en");
        Assert.Equal("Test failed", item.Status);
        Assert.Equal("No result is shown because the test did not complete: disk vanished", item.ResultText);

        item.ApplyLanguage("de");
        Assert.Equal("Test fehlgeschlagen", item.Status);
        Assert.Equal("Es wird kein Ergebnis angezeigt, weil der Test nicht abgeschlossen wurde: disk vanished", item.ResultText);
    }

    [Fact]
    public async Task TestItem_RunAsync_Success_KeepsMeasuredResult_WhenChromeIsGerman()
    {
        var item = new TestItemViewModel(
            "Fake test",
            "A test double.",
            _ => Task.FromResult(new BenchmarkResult(
                "Fake test", true, "1,234 ops/s", "Measured 1,234 operations per second.", "Local benchmark", 1234d, "ops/s")),
            "de");

        await item.RunCommand.ExecuteAsync(null);

        Assert.False(item.IsRunning);
        Assert.Equal("1,234 ops/s", item.Status);
        Assert.Equal("Measured 1,234 operations per second. Source: Local benchmark.", item.ResultText);
        Assert.Equal("Test starten", item.RunButtonText);

        item.ApplyLanguage("en");
        Assert.Equal("1,234 ops/s", item.Status);
        Assert.Equal("Measured 1,234 operations per second. Source: Local benchmark.", item.ResultText);
        Assert.Equal("Run test", item.RunButtonText);
    }
}
