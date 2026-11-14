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
/// net10.0 leg in the csproj.
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
        var report = Report("fr");

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
        var viewModel = Tests("fr");

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
