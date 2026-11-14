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
/// strand a metric. French phase 5 (v2.1.24) mirrors the German
/// phase 5 tests in French: the feature already worked in French —
/// the view model takes a language code generically and the French
/// pack already covers the whole 523-key catalogue — but there were
/// no French-pinned tests; this version adds them. No new keys and
/// no source change was needed. Spanish phase 5 (v2.1.31) mirrors
/// the French phase 5 tests in Spanish the same way.
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
    public void FrenchSeeded_RendersWholePageInFrench()
    {
        // Localisation, phase 5, French (v2.1.24): the Overview page,
        // mirroring the German-seeded test above.
        using var overview = Create(TestSnapshots.Healthy(), "fr");

        Assert.Equal("EXÉCUTER EN TANT QU'ADMINISTRATEUR POUR DES RÉSULTATS COMPLETS", overview.AdminEyebrowText);
        Assert.Equal("sur 100", overview.OutOf100Text);
        Assert.Equal("Santé globale", overview.OverallHealthText);
        Assert.Equal(
            "Score = moyenne pondérée des composants mesurables. Un composant sans véritable nombre de santé ne compte pas.",
            overview.ScoreNoteText);
        Assert.Equal("Vérification d'ordinateur portable neuf", overview.NewLaptopCheckText);
        Assert.Equal("Exporter le rapport", overview.ExportReportText);
        Assert.Equal("Santé des composants", overview.ComponentHealthText);
        Assert.Equal("Capteurs en direct", overview.LiveSensorsText);
        Assert.Equal("ALERTE DE TEMPÉRATURE", overview.AlertEyebrowText);
        Assert.Equal(
            "Les alertes ne se déclenchent que lorsque la température reste au-dessus de votre seuil pendant toute la durée définie. Modifiez le seuil dans les Paramètres.",
            overview.AlertNoteText);
        Assert.Equal("Chaque mesure conserve sa source. Aucune télémétrie, aucun appel réseau.", overview.FooterText);
        Assert.Equal(
            "Les capteurs en direct s'actualisent toutes les 2 secondes. Les valeurs n'apparaissent que lorsque LibreHardwareMonitor les signale.",
            overview.RefreshText);
        Assert.Equal("La journalisation des capteurs n'est pas disponible sur cette machine.", overview.SensorLogText);
        Assert.Equal(
            "Aucune alerte de température définie. TrueSpec est livré sans seuil ; définissez-en un dans les Paramètres si vous voulez une alerte.",
            overview.AlertSettingsText);
    }

    [Fact]
    public void SpanishSeeded_RendersWholePageInSpanish()
    {
        // Localisation, phase 5, Spanish (v2.1.31): the Overview page,
        // mirroring the French-seeded test above.
        using var overview = Create(TestSnapshots.Healthy(), "es");

        Assert.Equal("EJECUTAR COMO ADMINISTRADOR PARA OBTENER RESULTADOS COMPLETOS", overview.AdminEyebrowText);
        Assert.Equal("de 100", overview.OutOf100Text);
        Assert.Equal("Salud general", overview.OverallHealthText);
        Assert.Equal(
            "Puntuación = media ponderada de los componentes medibles. Un componente sin un número de salud real no cuenta.",
            overview.ScoreNoteText);
        Assert.Equal("Comprobación de portátil nuevo", overview.NewLaptopCheckText);
        Assert.Equal("Exportar informe", overview.ExportReportText);
        Assert.Equal("Salud de los componentes", overview.ComponentHealthText);
        Assert.Equal("Sensores en vivo", overview.LiveSensorsText);
        Assert.Equal("ALERTA DE TEMPERATURA", overview.AlertEyebrowText);
        Assert.Equal(
            "Las alertas solo se disparan después de que la temperatura se mantenga por encima del umbral durante toda la duración que estableciste. Cambia el umbral en Configuración.",
            overview.AlertNoteText);
        Assert.Equal("Cada lectura conserva su origen. Sin telemetría, sin llamadas de red.", overview.FooterText);
        Assert.Equal(
            "Los sensores en vivo se actualizan cada 2 segundos. Los valores solo aparecen cuando LibreHardwareMonitor los informa.",
            overview.RefreshText);
        Assert.Equal("El registro de sensores no está disponible en este equipo.", overview.SensorLogText);
        Assert.Equal(
            "No hay ninguna alerta de temperatura establecida. TrueSpec se entrega sin umbral; establece uno en Configuración si quieres una alerta.",
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
    public void ApplyLanguage_SwitchesToFrenchAndBackAndKeepsTheScore()
    {
        // Localisation, phase 5, French (v2.1.24): same switch, French.
        using var overview = Create(TestSnapshots.Healthy());

        var scoreText = overview.ScoreText;
        var verdictText = overview.VerdictText;
        var explanation = overview.ScoreExplanation;

        overview.ApplyLanguage("fr");
        Assert.Equal("Santé globale", overview.OverallHealthText);
        Assert.Equal("Vérification d'ordinateur portable neuf", overview.NewLaptopCheckText);
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
    public void ApplyLanguage_SwitchesToSpanishAndBackAndKeepsTheScore()
    {
        // Localisation, phase 5, Spanish (v2.1.31): same switch, Spanish.
        using var overview = Create(TestSnapshots.Healthy());

        var scoreText = overview.ScoreText;
        var verdictText = overview.VerdictText;
        var explanation = overview.ScoreExplanation;

        overview.ApplyLanguage("es");
        Assert.Equal("Salud general", overview.OverallHealthText);
        Assert.Equal("Comprobación de portátil nuevo", overview.NewLaptopCheckText);
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
    public void DataStaysEnglish_WhenChromeIsFrench()
    {
        // Localisation, phase 5, French (v2.1.24): same rule, French.
        var snapshot = TestSnapshots.Healthy();
        using var english = Create(snapshot);
        using var french = Create(snapshot, "fr");

        Assert.Equal(english.ScoreText, french.ScoreText);
        Assert.Equal(english.VerdictText, french.VerdictText);
        Assert.Equal(english.ScoreExplanation, french.ScoreExplanation);
        Assert.Equal(english.Cards.Select(c => c.Name), french.Cards.Select(c => c.Name));
        Assert.Equal(english.Cards.Select(c => c.Headline), french.Cards.Select(c => c.Headline));
        Assert.Equal(english.Cards.Select(c => c.HealthText), french.Cards.Select(c => c.HealthText));
        Assert.Equal(english.KeyStats.Select(s => s.Value), french.KeyStats.Select(s => s.Value));
        Assert.Equal(english.KeyStats.Select(s => s.Source), french.KeyStats.Select(s => s.Source));
        Assert.Equal(english.ScoreBreakdown.Select(r => r.HealthText), french.ScoreBreakdown.Select(r => r.HealthText));
    }

    [Fact]
    public void DataStaysEnglish_WhenChromeIsSpanish()
    {
        // Localisation, phase 5, Spanish (v2.1.31): same rule, Spanish.
        var snapshot = TestSnapshots.Healthy();
        using var english = Create(snapshot);
        using var spanish = Create(snapshot, "es");

        Assert.Equal(english.ScoreText, spanish.ScoreText);
        Assert.Equal(english.VerdictText, spanish.VerdictText);
        Assert.Equal(english.ScoreExplanation, spanish.ScoreExplanation);
        Assert.Equal(english.Cards.Select(c => c.Name), spanish.Cards.Select(c => c.Name));
        Assert.Equal(english.Cards.Select(c => c.Headline), spanish.Cards.Select(c => c.Headline));
        Assert.Equal(english.Cards.Select(c => c.HealthText), spanish.Cards.Select(c => c.HealthText));
        Assert.Equal(english.KeyStats.Select(s => s.Value), spanish.KeyStats.Select(s => s.Value));
        Assert.Equal(english.KeyStats.Select(s => s.Source), spanish.KeyStats.Select(s => s.Source));
        Assert.Equal(english.ScoreBreakdown.Select(r => r.HealthText), spanish.ScoreBreakdown.Select(r => r.HealthText));
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
    public void KeyStatLabels_AreLocalizedInFrench_ValuesAreMeasured()
    {
        // Localisation, phase 5, French (v2.1.24): same rule, French.
        // The labels translate; the values and sources are measured
        // data and stay exactly as collected.
        using var english = Create(TestSnapshots.Healthy());
        using var french = Create(TestSnapshots.Healthy(), "fr");

        Assert.Equal(new[] { "CPU temperature", "Memory", "Disk health", "Battery health" }, english.KeyStats.Select(s => s.Label));
        Assert.Equal(new[] { "Température du processeur", "Mémoire", "Santé du disque", "Santé de la batterie" }, french.KeyStats.Select(s => s.Label));

        var cpuTemp = french.KeyStats.Single(s => s.Label == "Température du processeur");
        Assert.Equal("55 C", cpuTemp.Value);
        Assert.Equal("LibreHardwareMonitor", cpuTemp.Source);
        var memory = french.KeyStats.Single(s => s.Label == "Mémoire");
        Assert.Equal("16 GB", memory.Value);
        Assert.Equal("CIM Win32_PhysicalMemory", memory.Source);
    }

    [Fact]
    public void KeyStatLabels_AreLocalizedInSpanish_ValuesAreMeasured()
    {
        // Localisation, phase 5, Spanish (v2.1.31): same rule, Spanish.
        // The labels translate; the values and sources are measured
        // data and stay exactly as collected.
        using var english = Create(TestSnapshots.Healthy());
        using var spanish = Create(TestSnapshots.Healthy(), "es");

        Assert.Equal(new[] { "CPU temperature", "Memory", "Disk health", "Battery health" }, english.KeyStats.Select(s => s.Label));
        Assert.Equal(new[] { "Temperatura de la CPU", "Memoria", "Salud del disco", "Salud de la batería" }, spanish.KeyStats.Select(s => s.Label));

        var cpuTemp = spanish.KeyStats.Single(s => s.Label == "Temperatura de la CPU");
        Assert.Equal("55 C", cpuTemp.Value);
        Assert.Equal("LibreHardwareMonitor", cpuTemp.Source);
        var memory = spanish.KeyStats.Single(s => s.Label == "Memoria");
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
    public void ScoreBreakdown_WeightAndContributionTemplates_AgreeInFrench()
    {
        // Localisation, phase 5, French (v2.1.24): same rule, French.
        // The weight word changes ("Weight" / "Poids"); the
        // contribution happens to read the same in both languages
        // ("+5 pts"), which the test pins explicitly instead of
        // assuming it.
        using var english = Create(TestSnapshots.Healthy());
        using var french = Create(TestSnapshots.Healthy(), "fr");

        Assert.Equal(english.ScoreBreakdown.Count, french.ScoreBreakdown.Count);
        Assert.NotEmpty(french.ScoreBreakdown);
        for (var i = 0; i < english.ScoreBreakdown.Count; i++)
        {
            var expected = english.ScoreBreakdown[i];
            var actual = french.ScoreBreakdown[i];
            Assert.Equal(expected.Component, actual.Component);
            Assert.Equal("Poids " + expected.WeightText["Weight ".Length..], actual.WeightText);
            Assert.Equal(expected.ContributionText, actual.ContributionText);
        }
    }

    [Fact]
    public void ScoreBreakdown_WeightAndContributionTemplates_AgreeInSpanish()
    {
        // Localisation, phase 5, Spanish (v2.1.31): same rule, Spanish.
        // The weight word changes ("Weight" / "Peso"); the
        // contribution happens to read the same in both languages
        // ("+5 pts"), which the test pins explicitly instead of
        // assuming it.
        using var english = Create(TestSnapshots.Healthy());
        using var spanish = Create(TestSnapshots.Healthy(), "es");

        Assert.Equal(english.ScoreBreakdown.Count, spanish.ScoreBreakdown.Count);
        Assert.NotEmpty(spanish.ScoreBreakdown);
        for (var i = 0; i < english.ScoreBreakdown.Count; i++)
        {
            var expected = english.ScoreBreakdown[i];
            var actual = spanish.ScoreBreakdown[i];
            Assert.Equal(expected.Component, actual.Component);
            Assert.Equal("Peso " + expected.WeightText["Weight ".Length..], actual.WeightText);
            Assert.Equal(expected.ContributionText, actual.ContributionText);
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
    public void ComponentCards_HealthAndDetailsLabels_AreLocalizedInFrench()
    {
        // Localisation, phase 5, French (v2.1.24): same rule, French.
        using var english = Create(TestSnapshots.Healthy());
        using var french = Create(TestSnapshots.Healthy(), "fr");

        Assert.NotEmpty(english.Cards);
        Assert.NotEmpty(french.Cards);
        Assert.All(english.Cards, card => Assert.Equal("Health", card.HealthLabelText));
        Assert.All(english.Cards, card => Assert.Equal("Details", card.DetailsText));
        Assert.All(french.Cards, card => Assert.Equal("Santé", card.HealthLabelText));
        Assert.All(french.Cards, card => Assert.Equal("Détails", card.DetailsText));

        // A switch rebuilds the cards in the new language.
        french.ApplyLanguage("en");
        Assert.All(french.Cards, card => Assert.Equal("Health", card.HealthLabelText));
    }

    [Fact]
    public void ComponentCards_HealthAndDetailsLabels_AreLocalizedInSpanish()
    {
        // Localisation, phase 5, Spanish (v2.1.31): same rule, Spanish.
        using var english = Create(TestSnapshots.Healthy());
        using var spanish = Create(TestSnapshots.Healthy(), "es");

        Assert.NotEmpty(english.Cards);
        Assert.NotEmpty(spanish.Cards);
        Assert.All(english.Cards, card => Assert.Equal("Health", card.HealthLabelText));
        Assert.All(english.Cards, card => Assert.Equal("Details", card.DetailsText));
        Assert.All(spanish.Cards, card => Assert.Equal("Salud", card.HealthLabelText));
        Assert.All(spanish.Cards, card => Assert.Equal("Detalles", card.DetailsText));

        // A switch rebuilds the cards in the new language.
        spanish.ApplyLanguage("en");
        Assert.All(spanish.Cards, card => Assert.Equal("Health", card.HealthLabelText));
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
    public void ScanningHero_IsLocalizedInFrench()
    {
        // Localisation, phase 5, French (v2.1.24): same rule, French.
        using var english = Create(TestSnapshots.Healthy(), isScanning: true);
        using var french = Create(TestSnapshots.Healthy(), "fr", isScanning: true);

        Assert.Equal("…", english.ScoreText);
        Assert.Equal("Scanning…", english.VerdictText);
        Assert.Equal("The hardware scan is still running. The score appears when the scan finishes.", english.ScoreExplanation);

        Assert.Equal("…", french.ScoreText);
        Assert.Equal("Analyse en cours…", french.VerdictText);
        Assert.Equal("L'analyse matérielle est toujours en cours. Le score apparaît lorsque l'analyse se termine.", french.ScoreExplanation);
    }

    [Fact]
    public void ScanningHero_IsLocalizedInSpanish()
    {
        // Localisation, phase 5, Spanish (v2.1.31): same rule, Spanish.
        using var english = Create(TestSnapshots.Healthy(), isScanning: true);
        using var spanish = Create(TestSnapshots.Healthy(), "es", isScanning: true);

        Assert.Equal("…", english.ScoreText);
        Assert.Equal("Scanning…", english.VerdictText);
        Assert.Equal("The hardware scan is still running. The score appears when the scan finishes.", english.ScoreExplanation);

        Assert.Equal("…", spanish.ScoreText);
        Assert.Equal("Analizando…", spanish.VerdictText);
        Assert.Equal("El análisis de hardware sigue en curso. La puntuación aparece cuando termina el análisis.", spanish.ScoreExplanation);
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
    public void LiveStrip_NamesAreLocalizedInFrench_AndUpdatesFollowTheKey()
    {
        // Localisation, phase 5, French (v2.1.24): same rule, French.
        // Note the abbreviated metric names ("Temp. processeur") versus
        // the full key-stat labels ("Température du processeur").
        using var overview = Create(TestSnapshots.Healthy(), "fr");

        Assert.Equal(
            new[] { "Temp. processeur", "Temp. GPU", "Utilisation du processeur", "Puissance de la batterie" },
            overview.LiveMetrics.Select(m => m.Name));
        Assert.Equal(
            new[] { "cpu-temp", "gpu-temp", "cpu-usage", "battery-power" },
            overview.LiveMetrics.Select(m => m.MetricKey));

        var cpu = overview.LiveMetrics.Single(m => m.MetricKey == LiveMetricViewModel.CpuTempKey);
        Assert.Equal("Non disponible", cpu.CurrentText);

        overview.ProcessSensorSnapshot(CpuSnapshot(72.5));
        Assert.Equal("72.5 C", cpu.CurrentText);
        Assert.Equal("Temp. processeur", cpu.Name);

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
    public void LiveStrip_NamesAreLocalizedInSpanish_AndUpdatesFollowTheKey()
    {
        // Localisation, phase 5, Spanish (v2.1.31): same rule, Spanish.
        // Note the abbreviated metric names ("Temp. de CPU") versus
        // the full key-stat labels ("Temperatura de la CPU").
        using var overview = Create(TestSnapshots.Healthy(), "es");

        Assert.Equal(
            new[] { "Temp. de CPU", "Temp. de GPU", "Uso de CPU", "Potencia de la batería" },
            overview.LiveMetrics.Select(m => m.Name));
        Assert.Equal(
            new[] { "cpu-temp", "gpu-temp", "cpu-usage", "battery-power" },
            overview.LiveMetrics.Select(m => m.MetricKey));

        var cpu = overview.LiveMetrics.Single(m => m.MetricKey == LiveMetricViewModel.CpuTempKey);
        Assert.Equal("No disponible", cpu.CurrentText);

        overview.ProcessSensorSnapshot(CpuSnapshot(72.5));
        Assert.Equal("72.5 C", cpu.CurrentText);
        Assert.Equal("Temp. de CPU", cpu.Name);

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
    public void SensorSentences_WithWriterAndThreshold_AreLocalizedInFrench()
    {
        // Localisation, phase 5, French (v2.1.24): same rule, French.
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
                languageCode: "fr");

            Assert.Equal(
                $"Les mesures des capteurs sont enregistrées dans {logPath}. Le graphique est sur la page Historique.",
                overview.SensorLogText);
            Assert.Equal(
                "Alerte lorsque la température du processeur atteint 90 °C pendant 30 secondes. Définissez-la dans les Paramètres.",
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
    public void SensorSentences_WithWriterAndThreshold_AreLocalizedInSpanish()
    {
        // Localisation, phase 5, Spanish (v2.1.31): same rule, Spanish.
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
                languageCode: "es");

            Assert.Equal(
                $"Las lecturas de los sensores se registran en {logPath}. El gráfico está en la página Historial.",
                overview.SensorLogText);
            Assert.Equal(
                "Avisar cuando la temperatura de la CPU llegue a 90 °C durante 30 segundos. Establécelo en Configuración.",
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

    [Fact]
    public void SensorSentences_LoggingOff_AreLocalizedInFrench()
    {
        // Localisation, phase 5, French (v2.1.24): same rule, French.
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
                languageCode: "fr");

            Assert.Equal(
                "La journalisation des capteurs est désactivée. Activez-la dans les Paramètres pour enregistrer les mesures.",
                overview.SensorLogText);
        }
        finally
        {
            DeleteDirectory(logPath);
        }
    }

    [Fact]
    public void SensorSentences_LoggingOff_AreLocalizedInSpanish()
    {
        // Localisation, phase 5, Spanish (v2.1.31): same rule, Spanish.
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
                languageCode: "es");

            Assert.Equal(
                "El registro de sensores está desactivado. Actívalo en Configuración para registrar lecturas.",
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
