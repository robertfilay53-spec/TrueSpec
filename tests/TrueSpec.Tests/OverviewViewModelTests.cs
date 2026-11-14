using System.Windows.Input;
using TrueSpec.App.ViewModels;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;
using TrueSpec.Core.Settings;

namespace TrueSpec.Tests;

/// <summary>
/// Localisation, phase 5 (v2.1.11): the Overview page in German — the
/// landing page, the first screen a user sees. The chrome follows the
/// language picked in Settings, in place and with no restart; the data
/// stays English by design: component names, headlines, health and
/// status texts, spec labels, values and sources, the score
/// explanation and the verdict text are measured data from the
/// collectors, translated in a later slice — the tests pin that the
/// data does not change when the chrome does. The live strip is
/// updated by stable key, never by display name, so a switch cannot
/// strand a metric.
/// </summary>
public sealed class OverviewViewModelTests
{
    private sealed class TestCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }

    private static OverviewViewModel Create(SystemSnapshot snapshot, string? languageCode = null, bool isScanning = false) =>
        new(
            snapshot,
            _ => { },
            new TestCommand(),
            new TestCommand(),
            new UnavailableSensorMonitor(),
            languageCode: languageCode,
            isScanning: isScanning);

    private static SensorSnapshot CpuSnapshot(double cpuTemp) =>
        new(true, "test", new DateTimeOffset(2026, 11, 12, 14, 32, 0, TimeSpan.FromHours(2)), new[]
        {
            new SensorReading("CPU", "Package", SensorKind.Temperature, "C", cpuTemp, "Test"),
            new SensorReading("GPU", "GPU Core", SensorKind.Temperature, "C", 55, "Test"),
        });

    [Fact]
    public void EnglishDefault_RendersChromeInEnglish()
    {
        using var overview = Create(TestSnapshots.Healthy());

        Assert.Equal("RUN AS ADMINISTRATOR FOR FULL RESULTS", overview.AdminEyebrowText);
        Assert.Equal("out of 100", overview.OutOf100Text);
        Assert.Equal("Overall health", overview.OverallHealthText);
        Assert.Equal(
            "Score = weighted average of measurable components. A component without a real health number does not count.",
            overview.ScoreNoteText);
        Assert.Equal("New Laptop Check", overview.NewLaptopCheckText);
        Assert.Equal("Export Report", overview.ExportReportText);
        Assert.Equal("Component health", overview.ComponentHealthText);
        Assert.Equal("Live sensors", overview.LiveSensorsText);
        Assert.Equal("TEMPERATURE ALERT", overview.AlertEyebrowText);
        Assert.Equal(
            "Alerts fire only after the temperature stays above your threshold for the full duration you set. Change the threshold in Settings.",
            overview.AlertNoteText);
        Assert.Equal("Every reading keeps its source. No telemetry, no network calls.", overview.FooterText);
        Assert.Equal(
            "Live sensors refresh every 2 seconds. Values appear only when LibreHardwareMonitor reports them.",
            overview.RefreshText);
        Assert.Equal("Sensor logging is unavailable on this machine.", overview.SensorLogText);
        Assert.Equal("No temperature alert set. TrueSpec ships with no threshold; set one in Settings if you want an alert.", overview.AlertSettingsText);
    }

    [Fact]
    public void GermanSeeded_RendersWholePageInGerman()
    {
        using var overview = Create(TestSnapshots.Healthy(), "de");

        Assert.Equal("FÜR VOLLSTÄNDIGE ERGEBNISSE ALS ADMINISTRATOR AUSFÜHREN", overview.AdminEyebrowText);
        Assert.Equal("von 100", overview.OutOf100Text);
        Assert.Equal("Gesamtzustand", overview.OverallHealthText);
        Assert.Equal(
            "Die Punktzahl ist der gewichtete Durchschnitt der messbaren Komponenten. Eine Komponente ohne echten Gesundheitswert zählt nicht.",
            overview.ScoreNoteText);
        Assert.Equal("Neuen Laptop prüfen", overview.NewLaptopCheckText);
        Assert.Equal("Bericht exportieren", overview.ExportReportText);
        Assert.Equal("Komponentenzustand", overview.ComponentHealthText);
        Assert.Equal("Live-Sensoren", overview.LiveSensorsText);
        Assert.Equal("TEMPERATURWARNUNG", overview.AlertEyebrowText);
        Assert.Equal(
            "Warnungen lösen erst aus, wenn die Temperatur die volle von Ihnen eingestellte Dauer über dem Schwellenwert bleibt. Ändern Sie den Schwellenwert in den Einstellungen.",
            overview.AlertNoteText);
        Assert.Equal("Jeder Messwert behält seine Quelle. Keine Telemetrie, keine Netzwerkaufrufe.", overview.FooterText);
        Assert.Equal(
            "Live-Sensoren werden alle 2 Sekunden neu gelesen. Werte erscheinen nur, wenn LibreHardwareMonitor sie meldet.",
            overview.RefreshText);
        Assert.Equal("Sensorprotokollierung ist auf diesem Rechner nicht verfügbar.", overview.SensorLogText);
        Assert.Equal(
            "Keine Temperaturwarnung eingestellt. TrueSpec wird ohne Schwellenwert ausgeliefert; stellen Sie einen in den Einstellungen ein, wenn Sie eine Warnung möchten.",
            overview.AlertSettingsText);
    }

    [Fact]
    public void ApplyLanguage_SwitchesBothWaysAndKeepsTheScore()
    {
        using var overview = Create(TestSnapshots.Healthy());

        var scoreText = overview.ScoreText;
        var verdictText = overview.VerdictText;
        var explanation = overview.ScoreExplanation;

        overview.ApplyLanguage("de");
        Assert.Equal("Gesamtzustand", overview.OverallHealthText);
        Assert.Equal("Neuen Laptop prüfen", overview.NewLaptopCheckText);
        Assert.Equal(scoreText, overview.ScoreText);
        Assert.Equal(verdictText, overview.VerdictText);
        Assert.Equal(explanation, overview.ScoreExplanation);

        overview.ApplyLanguage("en");
        Assert.Equal("Overall health", overview.OverallHealthText);
        Assert.Equal("New Laptop Check", overview.NewLaptopCheckText);
        Assert.Equal(scoreText, overview.ScoreText);
        Assert.Equal(verdictText, overview.VerdictText);
        Assert.Equal(explanation, overview.ScoreExplanation);
    }

    [Fact]
    public void UnknownLanguage_FallsBackToEnglish()
    {
        using var overview = Create(TestSnapshots.Healthy(), "xx");

        Assert.Equal("Overall health", overview.OverallHealthText);
        Assert.Equal("New Laptop Check", overview.NewLaptopCheckText);
        Assert.Equal("Live sensors", overview.LiveSensorsText);
    }

    [Fact]
    public void DataStaysEnglish_WhenChromeIsGerman()
    {
        var snapshot = TestSnapshots.Healthy();
        using var english = Create(snapshot);
        using var german = Create(snapshot, "de");

        Assert.Equal(english.ScoreText, german.ScoreText);
        Assert.Equal(english.VerdictText, german.VerdictText);
        Assert.Equal(english.ScoreExplanation, german.ScoreExplanation);
        Assert.Equal(english.Cards.Select(c => c.Name), german.Cards.Select(c => c.Name));
        Assert.Equal(english.Cards.Select(c => c.Headline), german.Cards.Select(c => c.Headline));
        Assert.Equal(english.Cards.Select(c => c.HealthText), german.Cards.Select(c => c.HealthText));
        Assert.Equal(english.KeyStats.Select(s => s.Value), german.KeyStats.Select(s => s.Value));
        Assert.Equal(english.KeyStats.Select(s => s.Source), german.KeyStats.Select(s => s.Source));
        Assert.Equal(english.ScoreBreakdown.Select(r => r.HealthText), german.ScoreBreakdown.Select(r => r.HealthText));
    }

    [Fact]
    public void KeyStatLabels_AreLocalized_ValuesAreMeasured()
    {
        using var english = Create(TestSnapshots.Healthy());
        using var german = Create(TestSnapshots.Healthy(), "de");

        Assert.Equal(new[] { "CPU temperature", "Memory", "Disk health", "Battery health" }, english.KeyStats.Select(s => s.Label));
        Assert.Equal(new[] { "CPU-Temperatur", "Arbeitsspeicher", "Datenträgerzustand", "Akkuzustand" }, german.KeyStats.Select(s => s.Label));

        var cpuTemp = german.KeyStats.Single(s => s.Label == "CPU-Temperatur");
        Assert.Equal("55 C", cpuTemp.Value);
        Assert.Equal("LibreHardwareMonitor", cpuTemp.Source);
        var memory = german.KeyStats.Single(s => s.Label == "Arbeitsspeicher");
        Assert.Equal("16 GB", memory.Value);
        Assert.Equal("CIM Win32_PhysicalMemory", memory.Source);
    }

    [Fact]
    public void ScoreBreakdown_WeightAndContributionTemplates_AgreeAcrossLanguages()
    {
        using var english = Create(TestSnapshots.Healthy());
        using var german = Create(TestSnapshots.Healthy(), "de");

        Assert.Equal(english.ScoreBreakdown.Count, german.ScoreBreakdown.Count);
        Assert.NotEmpty(german.ScoreBreakdown);
        for (var i = 0; i < english.ScoreBreakdown.Count; i++)
        {
            var expected = english.ScoreBreakdown[i];
            var actual = german.ScoreBreakdown[i];
            Assert.Equal(expected.Component, actual.Component);
            // Same number, different words: "Weight 2.5" / "Gewicht 2.5",
            // "+5 pts" / "+5 Pkt." — the invariant formatting never changes.
            Assert.Equal("Gewicht " + expected.WeightText["Weight ".Length..], actual.WeightText);
            Assert.Equal("+" + expected.ContributionText[1..^" pts".Length] + " Pkt.", actual.ContributionText);
        }
    }

    [Fact]
    public void ComponentCards_HealthAndDetailsLabels_AreLocalized()
    {
        using var english = Create(TestSnapshots.Healthy());
        using var german = Create(TestSnapshots.Healthy(), "de");

        Assert.NotEmpty(english.Cards);
        Assert.NotEmpty(german.Cards);
        Assert.All(english.Cards, card => Assert.Equal("Health", card.HealthLabelText));
        Assert.All(english.Cards, card => Assert.Equal("Details", card.DetailsText));
        Assert.All(german.Cards, card => Assert.Equal("Zustand", card.HealthLabelText));
        Assert.All(german.Cards, card => Assert.Equal("Details", card.DetailsText));

        // A switch rebuilds the cards in the new language.
        german.ApplyLanguage("en");
        Assert.All(german.Cards, card => Assert.Equal("Health", card.HealthLabelText));
    }

    [Fact]
    public void ScanningHero_IsLocalized()
    {
        using var english = Create(TestSnapshots.Healthy(), isScanning: true);
        using var german = Create(TestSnapshots.Healthy(), "de", isScanning: true);

        Assert.Equal("…", english.ScoreText);
        Assert.Equal("Scanning…", english.VerdictText);
        Assert.Equal("The hardware scan is still running. The score appears when the scan finishes.", english.ScoreExplanation);

        Assert.Equal("…", german.ScoreText);
        Assert.Equal("Scan läuft…", german.VerdictText);
        Assert.Equal("Der Hardware-Scan läuft noch. Die Punktzahl erscheint, wenn der Scan fertig ist.", german.ScoreExplanation);
    }

    [Fact]
    public void LiveStrip_NamesAreLocalized_AndUpdatesFollowTheKey()
    {
        using var overview = Create(TestSnapshots.Healthy(), "de");

        Assert.Equal(
            new[] { "CPU-Temperatur", "GPU-Temperatur", "CPU-Auslastung", "Akkuleistung" },
            overview.LiveMetrics.Select(m => m.Name));
        Assert.Equal(
            new[] { "cpu-temp", "gpu-temp", "cpu-usage", "battery-power" },
            overview.LiveMetrics.Select(m => m.MetricKey));

        var cpu = overview.LiveMetrics.Single(m => m.MetricKey == LiveMetricViewModel.CpuTempKey);
        Assert.Equal("Nicht verfügbar", cpu.CurrentText);

        overview.ProcessSensorSnapshot(CpuSnapshot(72.5));
        Assert.Equal("72.5 C", cpu.CurrentText);
        Assert.Equal("CPU-Temperatur", cpu.Name);

        // After a switch the same cell keeps updating: the strip looks
        // the metric up by key, never by its translated display name.
        overview.ApplyLanguage("en");
        Assert.Equal("CPU temp", cpu.Name);
        Assert.Equal("72.5 C", cpu.CurrentText);
        overview.ProcessSensorSnapshot(CpuSnapshot(71));
        Assert.Equal("71 C", cpu.CurrentText);
        Assert.Equal("CPU temp", cpu.Name);
    }

    [Fact]
    public void SensorSentences_WithWriterAndThreshold_AreLocalized()
    {
        var logPath = TempLogPath();
        try
        {
            var writer = new SensorLogWriter(logPath);
            var settings = new AppSettings
            {
                SensorLoggingEnabled = true,
                CpuTemperatureThresholdC = 90,
                CpuTemperatureThresholdSeconds = 30,
            };
            using var overview = new OverviewViewModel(
                TestSnapshots.Healthy(),
                _ => { },
                new TestCommand(),
                new TestCommand(),
                new UnavailableSensorMonitor(),
                sensorLogWriter: writer,
                settings: settings,
                languageCode: "de");

            Assert.Equal(
                $"Sensormesswerte werden in {logPath} aufgezeichnet. Das Diagramm ist auf der Verlaufsseite.",
                overview.SensorLogText);
            Assert.Equal(
                "Warnung, wenn die CPU-Temperatur 90 °C für 30 Sekunden erreicht. Stellen Sie sie in den Einstellungen ein.",
                overview.AlertSettingsText);

            overview.ApplyLanguage("en");
            Assert.Equal(
                $"Sensor readings are recorded to {logPath}. The chart is on the History page.",
                overview.SensorLogText);
            Assert.Equal(
                "Alert when the CPU temperature reaches 90 C for 30 seconds. Set it in Settings.",
                overview.AlertSettingsText);
        }
        finally
        {
            DeleteDirectory(logPath);
        }
    }

    [Fact]
    public void SensorSentences_LoggingOff_AreLocalized()
    {
        var logPath = TempLogPath();
        try
        {
            var writer = new SensorLogWriter(logPath);
            var settings = new AppSettings { SensorLoggingEnabled = false };
            using var overview = new OverviewViewModel(
                TestSnapshots.Healthy(),
                _ => { },
                new TestCommand(),
                new TestCommand(),
                new UnavailableSensorMonitor(),
                sensorLogWriter: writer,
                settings: settings,
                languageCode: "de");

            Assert.Equal(
                "Sensorprotokollierung ist aus. Schalten Sie sie in den Einstellungen ein, um Messwerte aufzuzeichnen.",
                overview.SensorLogText);
        }
        finally
        {
            DeleteDirectory(logPath);
        }
    }

    private static string TempLogPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "sensor-log.csv");
    }

    private static void DeleteDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
