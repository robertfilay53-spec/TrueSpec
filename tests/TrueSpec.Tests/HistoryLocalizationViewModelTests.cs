using TrueSpec.App.ViewModels;
using TrueSpec.Core.History;
using TrueSpec.Core.Sensors;
using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

/// <summary>
/// Localisation, phases 6-7 (v2.1.12-13): the History page in German — the
/// trend cards, the saved-scans table, the two-machine comparison and
/// the value comparison, plus the CPU temperature log. The chrome
/// follows the language picked in Settings, in place and with no
/// restart. Phase 7 localizes the Core-owned wordings too: the trend
/// summary, the battery alert, the driver-age summary, the forecast
/// basis texts and the value no-verdict reason are re-derived in the
/// new language from the same snapshots. The data that stays English
/// by design: machine names, the value verdict sentence (Core-owned,
/// ValueComparer, shown verbatim) and the table's cell fallbacks
/// ("Not measurable" / "Not reported", which the CSV export agrees
/// with row for row) — the tests pin that the data does not change
/// when the chrome does. Transient status lines (remove
/// confirmation, removed-scan counts, comparison and value prompts)
/// are re-derived from stored state, so a switch re-resolves them
/// instead of silently resetting them.
/// Windows leg only (it drives TrueSpec.App's ViewModel), like
/// HistoryForecastViewModelTests: excluded from the net10.0 leg in the
/// csproj.
/// </summary>
public class HistoryLocalizationViewModelTests
{
    private static async Task<SqliteSnapshotStore> StoreWithAsync(
        string directory,
        params (string Machine, double Score, string Battery, string Cycles, string Wear, string Hours, DateTimeOffset At)[] scans)
    {
        var databasePath = Path.Combine(directory, "snapshots.db");
        var store = new SqliteSnapshotStore(databasePath);
        await store.InitializeAsync();
        foreach (var scan in scans)
        {
            await store.SaveAsync(TestSnapshots.HistorySnapshot(
                scan.Score,
                scan.Battery,
                scan.Cycles,
                scan.Wear,
                scan.Hours,
                scan.At,
                machineName: scan.Machine));
        }

        return store;
    }

    /// <summary>
    /// The ViewModel loads its trend on a fire-and-forget task from its
    /// constructor: wait for it instead of sleeping a fixed time. The
    /// loading status is language-dependent, so both wordings end the
    /// wait; a finished load shows a count, one-scan or no-scans line.
    /// </summary>
    private static async Task WaitForLoadAsync(HistoryViewModel viewModel)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while ((viewModel.StatusMessage == "Loading snapshot history..."
                || viewModel.StatusMessage == "Scanverlauf wird geladen …")
            && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// The load assigns the compare pair and clears IsLoading on the
    /// same path that sets the status line, so a test that only waited
    /// for the status can still execute the compare before the pair is
    /// assigned, and the compare then silently no-ops (the pair is
    /// null). Wait for the compare to be possible instead: on a loaded
    /// runner the status line alone is not enough.
    /// </summary>
    private static async Task WaitForCompareReadyAsync(HistoryViewModel viewModel)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!viewModel.CanCompareMachines && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.True(viewModel.CanCompareMachines, $"Compare never became possible (status: {viewModel.StatusMessage}).");
    }

    private static string TempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EnglishDefault_RendersChromeInEnglish()
    {
        var viewModel = new HistoryViewModel(null, TestSnapshots.Healthy());

        Assert.Equal("HISTORY", viewModel.HistoryEyebrowText);
        Assert.Equal("How this machine changes over time", viewModel.HistoryTitleText);
        Assert.Equal(
            "Every scan is saved to a local database on this machine. TrueSpec trends the overall score, battery health and storage wear across those scans. A value a scan did not report is a gap, never a zero.",
            viewModel.HistoryIntroText);
        Assert.Equal("Machine", viewModel.HistoryMachineTitleText);
        Assert.Equal("Pick a machine", viewModel.HistoryPickMachineText);
        Assert.Equal(
            "History is shown for one machine at a time. Pick another machine to see its own trend.",
            viewModel.HistoryMachineNoteText);
        Assert.Equal("BATTERY HEALTH ALERT", viewModel.HistoryBatteryAlertEyebrowText);
        Assert.Equal("Overall score", viewModel.HistoryScoreTitleText);
        Assert.Equal("Battery health", viewModel.HistoryBatteryTitleText);
        Assert.Equal("Storage wear", viewModel.HistoryWearTitleText);
        Assert.Equal("FORECAST", viewModel.HistoryForecastEyebrowText);
        Assert.Equal("CPU temperature log", viewModel.HistoryCpuLogTitleText);
        Assert.Equal("Refresh chart", viewModel.HistoryRefreshChartText);
        Assert.Equal(
            "The horizontal axis is time, not sample count. Gaps in the line are samples where the CPU temperature sensor did not report, or stretches where logging was not running (the app was closed or the machine slept). The line never connects across a gap. The dashed line is your alert threshold, when one is set. The log is a local CSV under %LocalAppData%\\TrueSpec; nothing is uploaded.",
            viewModel.HistoryChartCaptionText);
        Assert.Equal("DRIVER AGE", viewModel.HistoryDriverAgeEyebrowText);
        Assert.Equal("Saved scans", viewModel.HistorySavedScansTitleText);
        Assert.Equal("Export history as CSV", viewModel.HistoryExportCsvText);
        Assert.Equal(
            "Oldest first. A dash means that scan did not report the value. Remove deletes that one scan from this data folder — the first click asks, the second deletes.",
            viewModel.HistoryTableNoteText);
        Assert.Equal("Scanned", viewModel.HistoryHeaderScannedText);
        Assert.Equal("Score", viewModel.HistoryHeaderScoreText);
        Assert.Equal("Cycles", viewModel.HistoryHeaderCyclesText);
        Assert.Equal("Battery health", viewModel.HistoryHeaderBatteryText);
        Assert.Equal("Storage wear", viewModel.HistoryHeaderWearText);
        Assert.Equal("Power-on hours", viewModel.HistoryHeaderPowerOnHoursText);
        Assert.Equal("Compare two machines", viewModel.HistoryCompareTitleText);
        Assert.Equal("Different machines — shown side by side, never mixed in one trend.", viewModel.HistoryCompareNoteText);
        Assert.Equal("Machine A", viewModel.HistoryMachineAText);
        Assert.Equal("Machine B", viewModel.HistoryMachineBText);
        Assert.Equal("Compare", viewModel.HistoryCompareButtonText);
        Assert.Equal("Which one is the better buy?", viewModel.HistoryValueTitleText);
        Assert.Equal(
            "Type in each seller's asking price. The price is yours, not measured, and it is only used for this comparison. It is never saved with the scan.",
            viewModel.HistoryValueIntroText);
        Assert.Equal("Asking price (you enter this, not measured)", viewModel.HistoryAskingPriceLabelText);
        Assert.Equal("For example 500", viewModel.HistoryPriceExampleText);
        Assert.Equal("Compare value", viewModel.HistoryCompareValueButtonText);
        Assert.Equal("Per-component winner (measured health only)", viewModel.HistoryWinnersTitleText);
        Assert.Equal("How Test Laptop changes over time", viewModel.MachineSubtitleText);
        Assert.Equal("Snapshot history is unavailable on this machine, so there is nothing to trend yet.", viewModel.StatusMessage);
        Assert.Equal("Snapshot history is unavailable on this machine, so there is nothing to compare yet.", viewModel.ComparisonStatusText);
        Assert.Equal("Enter each seller's asking price to compare value.", viewModel.ValueStatusText);
    }

    [Fact]
    public void GermanSeeded_RendersWholePageInGerman()
    {
        var viewModel = new HistoryViewModel(null, TestSnapshots.Healthy(), languageCode: "de");

        Assert.Equal("VERLAUF", viewModel.HistoryEyebrowText);
        Assert.Equal("Wie sich dieses Gerät im Laufe der Zeit verändert", viewModel.HistoryTitleText);
        Assert.Equal(
            "Jeder Scan wird in einer lokalen Datenbank auf diesem Gerät gespeichert. TrueSpec zeigt den Verlauf von Gesamtpunktzahl, Akkugesundheit und Speicherverschleiß über diese Scans. Ein Wert, den ein Scan nicht gemeldet hat, ist eine Lücke, niemals eine Null.",
            viewModel.HistoryIntroText);
        Assert.Equal("Gerät", viewModel.HistoryMachineTitleText);
        Assert.Equal("Gerät auswählen", viewModel.HistoryPickMachineText);
        Assert.Equal(
            "Der Verlauf wird jeweils für ein Gerät angezeigt. Wählen Sie ein anderes Gerät, um dessen Verlauf zu sehen.",
            viewModel.HistoryMachineNoteText);
        Assert.Equal("WARNUNG ZUR AKKUGESUNDHEIT", viewModel.HistoryBatteryAlertEyebrowText);
        Assert.Equal("Gesamtpunktzahl", viewModel.HistoryScoreTitleText);
        Assert.Equal("Akkugesundheit", viewModel.HistoryBatteryTitleText);
        Assert.Equal("Speicherverschleiß", viewModel.HistoryWearTitleText);
        Assert.Equal("PROGNOSE", viewModel.HistoryForecastEyebrowText);
        Assert.Equal("CPU-Temperaturprotokoll", viewModel.HistoryCpuLogTitleText);
        Assert.Equal("Diagramm aktualisieren", viewModel.HistoryRefreshChartText);
        Assert.Equal(
            "Die horizontale Achse ist die Zeit, nicht die Anzahl der Messwerte. Lücken in der Linie sind Messwerte, bei denen der CPU-Temperatursensor nichts gemeldet hat, oder Zeiträume, in denen die Protokollierung nicht lief (die App war geschlossen oder das Gerät im Ruhezustand). Die Linie verbindet niemals über eine Lücke hinweg. Die gestrichelte Linie ist Ihr Warnschwellenwert, sofern einer festgelegt ist. Das Protokoll ist eine lokale CSV-Datei unter %LocalAppData%\\TrueSpec; nichts wird hochgeladen.",
            viewModel.HistoryChartCaptionText);
        Assert.Equal("TREIBERALTER", viewModel.HistoryDriverAgeEyebrowText);
        Assert.Equal("Gespeicherte Scans", viewModel.HistorySavedScansTitleText);
        Assert.Equal("Verlauf als CSV exportieren", viewModel.HistoryExportCsvText);
        Assert.Equal(
            "Älteste zuerst. Ein Strich bedeutet, dass dieser Scan den Wert nicht gemeldet hat. „Entfernen\" löscht genau diesen einen Scan aus diesem Datenordner — der erste Klick fragt nach, der zweite löscht.",
            viewModel.HistoryTableNoteText);
        Assert.Equal("Gescannt", viewModel.HistoryHeaderScannedText);
        Assert.Equal("Punktzahl", viewModel.HistoryHeaderScoreText);
        Assert.Equal("Zyklen", viewModel.HistoryHeaderCyclesText);
        Assert.Equal("Akkugesundheit", viewModel.HistoryHeaderBatteryText);
        Assert.Equal("Speicherverschleiß", viewModel.HistoryHeaderWearText);
        Assert.Equal("Betriebsstunden", viewModel.HistoryHeaderPowerOnHoursText);
        Assert.Equal("Zwei Geräte vergleichen", viewModel.HistoryCompareTitleText);
        Assert.Equal("Unterschiedliche Geräte — nebeneinander angezeigt, niemals in einem Verlauf vermischt.", viewModel.HistoryCompareNoteText);
        Assert.Equal("Gerät A", viewModel.HistoryMachineAText);
        Assert.Equal("Gerät B", viewModel.HistoryMachineBText);
        Assert.Equal("Vergleichen", viewModel.HistoryCompareButtonText);
        Assert.Equal("Welches ist der bessere Kauf?", viewModel.HistoryValueTitleText);
        Assert.Equal(
            "Geben Sie den Angebotspreis jedes Verkäufers ein. Der Preis stammt von Ihnen, ist nicht gemessen und wird nur für diesen Vergleich verwendet. Er wird niemals mit dem Scan gespeichert.",
            viewModel.HistoryValueIntroText);
        Assert.Equal("Angebotspreis (Sie geben ihn ein, nicht gemessen)", viewModel.HistoryAskingPriceLabelText);
        Assert.Equal("Zum Beispiel 500", viewModel.HistoryPriceExampleText);
        Assert.Equal("Wert vergleichen", viewModel.HistoryCompareValueButtonText);
        Assert.Equal("Sieger pro Komponente (nur gemessene Gesundheit)", viewModel.HistoryWinnersTitleText);
        Assert.Equal("Wie sich Test Laptop im Laufe der Zeit verändert", viewModel.MachineSubtitleText);
        Assert.Equal("Der Scanverlauf ist auf diesem Gerät nicht verfügbar, es gibt also noch nichts zu zeigen.", viewModel.StatusMessage);
        Assert.Equal("Der Scanverlauf ist auf diesem Gerät nicht verfügbar, es gibt also noch nichts zu vergleichen.", viewModel.ComparisonStatusText);
        Assert.Equal("Geben Sie den Angebotspreis jedes Verkäufers ein, um den Wert zu vergleichen.", viewModel.ValueStatusText);
    }

    [Fact]
    public void ApplyLanguage_SwitchesBothWays()
    {
        var viewModel = new HistoryViewModel(null, TestSnapshots.Healthy());

        viewModel.ApplyLanguage("de");
        Assert.Equal("VERLAUF", viewModel.HistoryEyebrowText);
        Assert.Equal("Gesamtpunktzahl", viewModel.HistoryScoreTitleText);
        Assert.Equal("Wie sich Test Laptop im Laufe der Zeit verändert", viewModel.MachineSubtitleText);
        Assert.Equal("Der Scanverlauf ist auf diesem Gerät nicht verfügbar, es gibt also noch nichts zu zeigen.", viewModel.StatusMessage);
        Assert.Equal("Geben Sie den Angebotspreis jedes Verkäufers ein, um den Wert zu vergleichen.", viewModel.ValueStatusText);

        viewModel.ApplyLanguage("en");
        Assert.Equal("HISTORY", viewModel.HistoryEyebrowText);
        Assert.Equal("Overall score", viewModel.HistoryScoreTitleText);
        Assert.Equal("How Test Laptop changes over time", viewModel.MachineSubtitleText);
        Assert.Equal("Snapshot history is unavailable on this machine, so there is nothing to trend yet.", viewModel.StatusMessage);
        Assert.Equal("Enter each seller's asking price to compare value.", viewModel.ValueStatusText);

        viewModel.ApplyLanguage("de");
        Assert.Equal("VERLAUF", viewModel.HistoryEyebrowText);
        Assert.Equal("Der Scanverlauf ist auf diesem Gerät nicht verfügbar, es gibt also noch nichts zu zeigen.", viewModel.StatusMessage);
    }

    [Fact]
    public void UnknownLanguage_FallsBackToEnglish()
    {
        var viewModel = new HistoryViewModel(null, TestSnapshots.Healthy(), languageCode: "fr");

        Assert.Equal("HISTORY", viewModel.HistoryEyebrowText);
        Assert.Equal("Overall score", viewModel.HistoryScoreTitleText);
        Assert.Equal("Snapshot history is unavailable on this machine, so there is nothing to trend yet.", viewModel.StatusMessage);
        Assert.Equal("Enter each seller's asking price to compare value.", viewModel.ValueStatusText);
    }

    [Fact]
    public async Task TwoScans_StatusAndRanges_AreLocalized_CoreWordingsFollowLanguage()
    {
        var directory = TempDirectory();
        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("Test Laptop", 90d, "90.0%", "10", "2%", "100", baseTime),
                ("Test Laptop", 85d, "85.0%", "12", "5%", "120", baseTime.AddHours(1)));

            var english = new HistoryViewModel(store, TestSnapshots.Healthy());
            await WaitForLoadAsync(english);
            var german = new HistoryViewModel(store, TestSnapshots.Healthy(), languageCode: "de");
            await WaitForLoadAsync(german);

            Assert.True(english.HasHistory, "English history never loaded.");
            Assert.True(german.HasHistory, "German history never loaded.");

            Assert.Equal("2 scans in history, oldest first.", english.StatusMessage);
            Assert.Equal("2 Scans im Verlauf, älteste zuerst.", german.StatusMessage);
            Assert.Equal("Score: 90 to 85 over 2 scans.", english.ScoreRangeText);
            Assert.Equal("Punktzahl: 90 bis 85 über 2 Scans.", german.ScoreRangeText);
            Assert.Equal("Battery health: 90% to 85% over 2 scans.", english.BatteryRangeText);
            Assert.Equal("Akkugesundheit: 90% bis 85% über 2 Scans.", german.BatteryRangeText);
            Assert.Equal("Storage wear: 2% to 5% over 2 scans.", english.WearRangeText);
            Assert.Equal("Speicherverschleiß: 2% bis 5% über 2 Scans.", german.WearRangeText);

            // Measured values do not change with the chrome: the same row
            // cells, the same machine names. The Core-owned wordings
            // (trend summary, forecast texts) follow the language
            // instead: they are re-derived from the same snapshots.
            Assert.Equal(
                "2 scans from 2026-11-10 to 2026-11-10. Score 90 to 85 (down 5 points). Battery health 90 to 85 (down 5 points). Storage wear 2 to 5 (up 3 points).",
                english.SummaryText);
            Assert.Equal(
                "2 Scans von 2026-11-10 bis 2026-11-10. Punktzahl 90 bis 85 (um 5 Punkte gefallen). Akkugesundheit 90 bis 85 (um 5 Punkte gefallen). Speicherverschleiß 2 bis 5 (um 3 Punkte gestiegen).",
                german.SummaryText);
            Assert.Equal(
                "Battery health has 2 scans over 0 days. A forecast needs at least 3 scans over at least 30 days: 1 more scan needed.",
                english.BatteryForecastText);
            Assert.Equal(
                "Akkugesundheit hat 2 Scans über 0 Tage. Eine Prognose braucht mindestens 3 Scans über mindestens 30 Tage: 1 weiterer Scan nötig.",
                german.BatteryForecastText);
            Assert.Equal(
                "SSD wear has 2 scans over 0 days. A forecast needs at least 3 scans over at least 30 days: 1 more scan needed.",
                english.StorageWearForecastText);
            Assert.Equal(
                "SSD-Verschleiß hat 2 Scans über 0 Tage. Eine Prognose braucht mindestens 3 Scans über mindestens 30 Tage: 1 weiterer Scan nötig.",
                german.StorageWearForecastText);
            Assert.Equal(english.Rows.Select(r => r.ScoreText), german.Rows.Select(r => r.ScoreText));
            Assert.Equal(english.Rows.Select(r => r.BatteryHealthText), german.Rows.Select(r => r.BatteryHealthText));
            Assert.Equal(english.Rows.Select(r => r.BatteryCyclesText), german.Rows.Select(r => r.BatteryCyclesText));
            Assert.Equal(english.Rows.Select(r => r.StorageWearText), german.Rows.Select(r => r.StorageWearText));
            Assert.Equal(english.Rows.Select(r => r.StorageHoursText), german.Rows.Select(r => r.StorageHoursText));
            Assert.Equal(english.Rows.Select(r => r.CapturedAt), german.Rows.Select(r => r.CapturedAt));
            Assert.Equal(english.MachineNames, german.MachineNames);

            // A switch re-renders the loaded page in place, keeping the data.
            german.ApplyLanguage("en");
            Assert.Equal("2 scans in history, oldest first.", german.StatusMessage);
            Assert.Equal("Score: 90 to 85 over 2 scans.", german.ScoreRangeText);
            Assert.Equal(english.SummaryText, german.SummaryText);
            Assert.Equal(english.Rows.Select(r => r.ScoreText), german.Rows.Select(r => r.ScoreText));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task OneScan_AndNoScans_StatusLines_AreLocalized()
    {
        var directory = TempDirectory();
        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("Test Laptop", 90d, "90.0%", "10", "2%", "100", baseTime));

            var german = new HistoryViewModel(store, TestSnapshots.Healthy(), languageCode: "de");
            await WaitForLoadAsync(german);

            Assert.Equal("Bisher ein Scan gespeichert. Führen Sie einen weiteren Scan aus (aktualisieren) und kommen Sie für den Verlauf zurück.", german.StatusMessage);
            Assert.Equal("Punktzahl: 90 (nur ein Scan).", german.ScoreRangeText);

            german.ApplyLanguage("en");
            Assert.Equal("One scan saved so far. Run another scan (refresh) and come back for the trend.", german.StatusMessage);
            Assert.Equal("Score: 90 (one scan only).", german.ScoreRangeText);
        }
        finally
        {
            DeleteDirectory(directory);
        }

        directory = TempDirectory();
        try
        {
            // Only the other machine has scans: the current machine's
            // trend is empty, and the compare card offers the pair.
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("OTHER-PC", 70d, "75.0%", "300", "12%", "45000", baseTime));

            var german = new HistoryViewModel(store, TestSnapshots.Healthy(), languageCode: "de");
            await WaitForLoadAsync(german);

            Assert.False(german.HasHistory);
            Assert.Equal("Noch keine gespeicherten Scans. Führen Sie einen Scan aus (aktualisieren) und kommen Sie zurück: Jeder Scan wird im lokalen Verlauf gespeichert.", german.StatusMessage);
            Assert.Equal("Wählen Sie zwei Geräte und vergleichen Sie sie. Unterschiedliche Geräte werden nebeneinander angezeigt, niemals in einem Verlauf vermischt.", german.ComparisonStatusText);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task RemoveButtonText_InBothLanguages_AndArmedConfirmSurvivesSwitch()
    {
        var directory = TempDirectory();
        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("Test Laptop", 90d, "90.0%", "10", "2%", "100", baseTime),
                ("Test Laptop", 85d, "85.0%", "12", "5%", "120", baseTime.AddHours(1)));

            // A row built by hand follows its own language code.
            var row = new HistoryRowViewModel(
                Guid.NewGuid(),
                baseTime,
                90d,
                90d,
                10d,
                2d,
                100d,
                languageCode: "de");
            Assert.Equal("Entfernen", row.RemoveButtonText);
            row.IsRemoveArmed = true;
            Assert.Equal("Entfernen bestätigen", row.RemoveButtonText);
            row.ApplyLanguage("en");
            Assert.Equal("Confirm remove", row.RemoveButtonText);

            var viewModel = new HistoryViewModel(store, TestSnapshots.Healthy(), languageCode: "de");
            await WaitForLoadAsync(viewModel);

            Assert.Equal(2, viewModel.Rows.Count);
            Assert.All(viewModel.Rows, r => Assert.Equal("Entfernen", r.RemoveButtonText));

            // First click arms: the status names the scan date in German.
            var firstRow = viewModel.Rows[0];
            await viewModel.RemoveScanCommand.ExecuteAsync(firstRow);

            Assert.True(firstRow.IsRemoveArmed);
            Assert.Equal("Entfernen bestätigen", firstRow.RemoveButtonText);
            Assert.Contains("Den Scan vom 2026-11-10 09:00 entfernen?", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("bis dahin wird nichts gelöscht", viewModel.StatusMessage, StringComparison.Ordinal);

            // A switch re-resolves the armed confirmation instead of
            // resetting it; the rebuilt rows carry the new language.
            viewModel.ApplyLanguage("en");
            Assert.Contains("Remove the scan from 2026-11-10 09:00?", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("nothing is deleted until then", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.All(viewModel.Rows, r => Assert.Equal("Remove", r.RemoveButtonText));

            viewModel.ApplyLanguage("de");
            Assert.Contains("Den Scan vom 2026-11-10 09:00 entfernen?", viewModel.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task RemoveScan_RemovedMessages_AreLocalizedInBothLanguages()
    {
        var directory = TempDirectory();
        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("Test Laptop", 90d, "90.0%", "10", "2%", "100", baseTime),
                ("Test Laptop", 85d, "85.0%", "12", "5%", "120", baseTime.AddHours(1)));

            var viewModel = new HistoryViewModel(store, TestSnapshots.Healthy(), languageCode: "de");
            await WaitForLoadAsync(viewModel);

            var firstRow = viewModel.Rows[0];
            await viewModel.RemoveScanCommand.ExecuteAsync(firstRow);
            await viewModel.RemoveScanCommand.ExecuteAsync(viewModel.Rows[0]);

            Assert.Single(viewModel.Rows);
            Assert.Equal("Den Scan vom 2026-11-10 09:00 entfernt. Es verbleibt 1 Scan.", viewModel.StatusMessage);

            viewModel.ApplyLanguage("en");
            Assert.Equal("Removed the scan from 2026-11-10 09:00. 1 scan remains.", viewModel.StatusMessage);
        }
        finally
        {
            DeleteDirectory(directory);
        }

        directory = TempDirectory();
        try
        {
            // Removing the only scan leaves the none-remain wording.
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("Test Laptop", 90d, "90.0%", "10", "2%", "100", baseTime));

            var viewModel = new HistoryViewModel(store, TestSnapshots.Healthy(), languageCode: "de");
            await WaitForLoadAsync(viewModel);

            var row = viewModel.Rows[0];
            await viewModel.RemoveScanCommand.ExecuteAsync(row);
            await viewModel.RemoveScanCommand.ExecuteAsync(viewModel.Rows[0]);

            Assert.Empty(viewModel.Rows);
            Assert.Equal("Den Scan vom 2026-11-10 09:00 entfernt. Es sind keine gespeicherten Scans mehr vorhanden.", viewModel.StatusMessage);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ComparisonRows_ValueRows_AndWinners_AreGerman_VerdictStaysEnglish()
    {
        var directory = TempDirectory();
        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("Test Laptop", 90d, "90.0%", "10", "2%", "100", baseTime),
                ("Test Laptop", 85d, "85.0%", "12", "5%", "120", baseTime.AddHours(1)),
                ("OTHER-PC", 70d, "75.0%", "300", "12%", "45000", baseTime),
                ("OTHER-PC", 75d, "80.0%", "310", "12%", "45100", baseTime.AddHours(1)));

            var viewModel = new HistoryViewModel(store, TestSnapshots.Healthy(), languageCode: "de");
            await WaitForLoadAsync(viewModel);

            Assert.Equal("Test Laptop", viewModel.CompareMachineA);
            Assert.Equal("OTHER-PC", viewModel.CompareMachineB);
            Assert.Equal("Wählen Sie zwei Geräte und vergleichen Sie sie. Unterschiedliche Geräte werden nebeneinander angezeigt, niemals in einem Verlauf vermischt.", viewModel.ComparisonStatusText);

            await WaitForCompareReadyAsync(viewModel);
            await viewModel.CompareMachinesCommand.ExecuteAsync(null);

            Assert.True(viewModel.HasMachineComparison, "Comparison never ran.");
            Assert.Equal("Test Laptop", viewModel.MachineAName);
            Assert.Equal("OTHER-PC", viewModel.MachineBName);
            Assert.Equal("Test Laptop mit OTHER-PC verglichen. Unterschiedliche Geräte werden nebeneinander angezeigt, niemals in einem Verlauf vermischt.", viewModel.ComparisonStatusText);
            Assert.Equal(
                new[] { "Scans im Verlauf", "Erster Scan", "Letzter Scan", "Neueste Punktzahl", "Neueste Akkugesundheit", "Neuester Speicherverschleiß", "Neuestes ältestes Treiberdatum", "Letzte Updateinstallation" },
                viewModel.ComparisonRows.Select(r => r.Label));
            Assert.Equal(new[] { "2", "2" }, new[] { viewModel.ComparisonRows.Single(r => r.Label == "Scans im Verlauf").MachineAText, viewModel.ComparisonRows.Single(r => r.Label == "Scans im Verlauf").MachineBText });
            var scoreRow = viewModel.ComparisonRows.Single(r => r.Label == "Neueste Punktzahl");
            Assert.Equal("85", scoreRow.MachineAText);
            Assert.Equal("75", scoreRow.MachineBText);
            var batteryRow = viewModel.ComparisonRows.Single(r => r.Label == "Neueste Akkugesundheit");
            Assert.Equal("85%", batteryRow.MachineAText);
            Assert.Equal("80%", batteryRow.MachineBText);
            var wearRow = viewModel.ComparisonRows.Single(r => r.Label == "Neuester Speicherverschleiß");
            Assert.Equal("5%", wearRow.MachineAText);
            Assert.Equal("12%", wearRow.MachineBText);

            // A switch rebuilds the comparison rows in the new language.
            viewModel.ApplyLanguage("en");
            Assert.Equal("Compared Test Laptop with OTHER-PC. Different machines are shown side by side, never mixed in one trend.", viewModel.ComparisonStatusText);
            Assert.Equal(
                new[] { "Scans in history", "First scan", "Last scan", "Latest score", "Latest battery health", "Latest storage wear", "Latest oldest driver date", "Last update install" },
                viewModel.ComparisonRows.Select(r => r.Label));
            viewModel.ApplyLanguage("de");

            // A bad price is a German prompt, not a verdict.
            viewModel.PriceAText = string.Empty;
            viewModel.PriceBText = "400";
            viewModel.CompareValueCommand.Execute(null);
            Assert.False(viewModel.HasValueComparison);
            Assert.Equal("Geben Sie für jedes Gerät einen Angebotspreis über null ein (nur Zahlen, zum Beispiel 500). Der Preis stammt von Ihnen, ist nicht gemessen.", viewModel.ValueStatusText);

            // Valid prices: the honesty note and the per-dollar label
            // are chrome (German); the verdict sentence itself is
            // Core-owned and stays English by design.
            viewModel.PriceAText = "500";
            viewModel.PriceBText = "400";
            viewModel.CompareValueCommand.Execute(null);

            Assert.True(viewModel.HasValueComparison);
            Assert.Equal("Die Preise stammen von Ihnen, sind nicht gemessen. Die Pro-Dollar-Zahl ist eine Rechnung aus der gemessenen Punktzahl und dem von Ihnen eingegebenen Preis, gekennzeichnet als „geschätzt\". Es werden keine Marktpreisdaten verwendet.", viewModel.ValueStatusText);
            Assert.Contains("Machine B", viewModel.ValueVerdictText, StringComparison.Ordinal);
            Assert.Contains("better buy", viewModel.ValueVerdictText, StringComparison.Ordinal);
            Assert.Equal(2, viewModel.ValueRows.Count);
            // The asking-price row label follows the page language
            // through the catalogue key, like the other row labels.
            Assert.Equal("Angebotspreis (Sie geben ihn ein, nicht gemessen)", viewModel.ValueRows[0].Label);
            Assert.Equal("$500", viewModel.ValueRows[0].MachineAText);
            Assert.Equal("$400", viewModel.ValueRows[0].MachineBText);
            Assert.Equal("Punktzahl pro 1 $ (geschätzt)", viewModel.ValueRows[1].Label);
            Assert.Equal("0.17", viewModel.ValueRows[1].MachineAText);

            // Winners: storage is measurable on both sides (95% vs
            // 88%), battery is measurable on neither (both null health).
            Assert.Equal(2, viewModel.ValueComponentWinners.Count);
            var storageWinner = viewModel.ValueComponentWinners.Single(w => w.ComponentName == "Storage");
            Assert.Equal("Gerät A gewinnt (95% vs 88%)", storageWinner.WinnerText);
            var batteryWinner = viewModel.ValueComponentWinners.Single(w => w.ComponentName == "Battery");
            Assert.Equal("Auf einem oder beiden Geräten nicht messbar", batteryWinner.WinnerText);

            // A switch rebuilds the value rows and winners too.
            viewModel.ApplyLanguage("en");
            Assert.Equal("Asking price (you enter this, not measured)", viewModel.ValueRows[0].Label);
            Assert.Equal("Score per $1 (ESTIMATE)", viewModel.ValueRows[1].Label);
            Assert.Equal("Machine A wins (95% vs 88%)", viewModel.ValueComponentWinners.Single(w => w.ComponentName == "Storage").WinnerText);
            Assert.Equal("Not measurable on one or both machines", viewModel.ValueComponentWinners.Single(w => w.ComponentName == "Battery").WinnerText);
            Assert.Equal("Prices are yours, not measured. The per-dollar figure is arithmetic on the measured score and the price you entered, labelled ESTIMATE. No market-price data is used.", viewModel.ValueStatusText);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task LanguageSwitch_ReAnalysesTrendSummaryAndForecasts()
    {
        var directory = TempDirectory();
        try
        {
            var baseTime = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("Test Laptop", 90d, "90.0%", "10", "2%", "100", baseTime),
                ("Test Laptop", 89d, "89.0%", "11", "4%", "150", baseTime.AddDays(30)),
                ("Test Laptop", 88d, "88.0%", "12", "6%", "200", baseTime.AddDays(60)),
                ("Test Laptop", 87d, "87.0%", "13", "8%", "250", baseTime.AddDays(90)));

            var viewModel = new HistoryViewModel(store, TestSnapshots.Healthy(), languageCode: "en");
            await WaitForLoadAsync(viewModel);

            Assert.Equal(
                "4 scans from 2026-01-10 to 2026-04-10. Score 90 to 87 (down 3 points). Battery health 90 to 87 (down 3 points). Storage wear 2 to 8 (up 6 points).",
                viewModel.SummaryText);
            Assert.Equal(
                "Battery health fell 3 points over 4 scans (90 days). At that measured rate, it reaches the Replace threshold (battery health below 60%) in 23 to 30 months.",
                viewModel.BatteryForecastText);
            Assert.Equal(
                "SSD wear rose 6 points over 4 scans (90 days). At that measured rate, it reaches the Replace threshold (storage wear above 40%) in 14 to 18 months.",
                viewModel.StorageWearForecastText);

            // A switch re-analyses the same snapshots in German: the
            // wordings change, the measured values do not.
            viewModel.ApplyLanguage("de");
            Assert.Equal(
                "4 Scans von 2026-01-10 bis 2026-04-10. Punktzahl 90 bis 87 (um 3 Punkte gefallen). Akkugesundheit 90 bis 87 (um 3 Punkte gefallen). Speicherverschleiß 2 bis 8 (um 6 Punkte gestiegen).",
                viewModel.SummaryText);
            Assert.Equal(
                "Akkugesundheit ist um 3 Punkte über 4 Scans (90 Tage) gefallen. Bei dieser gemessenen Rate wird Austausch-Schwelle (Akkugesundheit unter 60 %) in 23 bis 30 Monaten erreicht.",
                viewModel.BatteryForecastText);
            Assert.Equal(
                "SSD-Verschleiß ist um 6 Punkte über 4 Scans (90 Tage) gestiegen. Bei dieser gemessenen Rate wird Austausch-Schwelle (Speicherverschleiß über 40 %) in 14 bis 18 Monaten erreicht.",
                viewModel.StorageWearForecastText);

            viewModel.ApplyLanguage("en");
            Assert.Equal(
                "4 scans from 2026-01-10 to 2026-04-10. Score 90 to 87 (down 3 points). Battery health 90 to 87 (down 3 points). Storage wear 2 to 8 (up 6 points).",
                viewModel.SummaryText);
            Assert.Equal(
                "Battery health fell 3 points over 4 scans (90 days). At that measured rate, it reaches the Replace threshold (battery health below 60%) in 23 to 30 months.",
                viewModel.BatteryForecastText);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task LanguageSwitch_ReRunsValueComparison_NoVerdictReasonFollowsLanguage()
    {
        var directory = TempDirectory();
        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("Test Laptop", 90d, "90.0%", "10", "2%", "100", baseTime),
                ("Test Laptop", 85d, "85.0%", "12", "5%", "120", baseTime.AddHours(1)),
                ("OTHER-PC", 70d, "75.0%", "300", "12%", "45000", baseTime),
                ("OTHER-PC", 75d, "80.0%", "310", "12%", "45100", baseTime.AddHours(1)));

            var viewModel = new HistoryViewModel(store, TestSnapshots.Healthy(), languageCode: "en");
            await WaitForLoadAsync(viewModel);
            await WaitForCompareReadyAsync(viewModel);
            await viewModel.CompareMachinesCommand.ExecuteAsync(null);
            Assert.True(viewModel.HasMachineComparison, "Comparison never ran.");

            // A zero price parses but means "no price entered": no
            // verdict, and the reason says which input is missing.
            viewModel.PriceAText = "500";
            viewModel.PriceBText = "0";
            viewModel.CompareValueCommand.Execute(null);

            Assert.True(viewModel.HasValueComparison);
            Assert.Equal(string.Empty, viewModel.ValueVerdictText);
            Assert.Equal(
                "Enter an asking price above zero for each machine to compare value.",
                viewModel.ValueStatusText);

            // A switch re-runs the comparison in German; the verdict
            // (none here) is unchanged, the reason follows the language.
            viewModel.ApplyLanguage("de");
            Assert.Equal(string.Empty, viewModel.ValueVerdictText);
            Assert.Equal(
                "Geben Sie für jedes Gerät einen Angebotspreis über null ein, um den Wert zu vergleichen.",
                viewModel.ValueStatusText);
            Assert.Equal("Angebotspreis (Sie geben ihn ein, nicht gemessen)", viewModel.ValueRows[0].Label);

            viewModel.ApplyLanguage("en");
            Assert.Equal(
                "Enter an asking price above zero for each machine to compare value.",
                viewModel.ValueStatusText);

            // A price typed since the last compare is NOT applied by a
            // language switch: the stored result (and its reason) stay
            // until Compare runs again.
            viewModel.PriceBText = "400";
            viewModel.ApplyLanguage("de");
            Assert.Equal(
                "Enter an asking price above zero for each machine to compare value.",
                viewModel.ValueStatusText);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ValueCompareFirst_IsLocalized()
    {
        var viewModel = new HistoryViewModel(null, TestSnapshots.Healthy(), languageCode: "de");

        Assert.False(viewModel.HasMachineComparison);
        viewModel.CompareValueCommand.Execute(null);

        Assert.Equal("Vergleichen Sie zuerst zwei Geräte und geben Sie dann die Angebotspreise ein.", viewModel.ValueStatusText);
        Assert.False(viewModel.HasValueComparison);
    }

    [Fact]
    public void SensorLogStatuses_AreLocalized_AndSwitchReReadsTheLog()
    {
        // Fixed UTC+5 zone, standing in for PKT in the laptop test.
        var zone = TimeZoneInfo.CreateCustomTimeZone("PKT-test", TimeSpan.FromHours(5), "PKT-test", "PKT-test");
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "sensor-log.csv");
        try
        {
            // Logging off: the German off sentence, in both languages.
            var off = new HistoryViewModel(null, TestSnapshots.Healthy(), sensorLogPath: path, sensorLoggingEnabled: false, displayTimeZone: zone, languageCode: "de");
            off.LoadSensorLog();
            Assert.Equal("Die Sensorprotokollierung ist aus. Schalten Sie sie in den Einstellungen ein, dann erscheint hier das CPU-Temperaturdiagramm.", off.SensorLogStatusText);
            off.ApplyLanguage("en");
            Assert.Equal("Sensor logging is off. Turn it on in Settings and the CPU temperature chart will appear here.", off.SensorLogStatusText);

            // No log file yet: the no-samples sentence.
            var empty = new HistoryViewModel(null, TestSnapshots.Healthy(), sensorLogPath: path, displayTimeZone: zone, languageCode: "de");
            empty.LoadSensorLog();
            Assert.Equal("Noch keine Sensormesswerte aufgezeichnet. Lassen Sie TrueSpec laufen, dann erscheint hier das CPU-Temperaturdiagramm.", empty.SensorLogStatusText);

            // All gaps: the no-readings sentence with the sample count.
            WriteLog(path, new DateTimeOffset(2026, 11, 12, 8, 45, 0, TimeSpan.Zero), null, null, null);
            var gaps = new HistoryViewModel(null, TestSnapshots.Healthy(), sensorLogPath: path, displayTimeZone: zone, languageCode: "de");
            gaps.LoadSensorLog();
            Assert.False(gaps.HasSensorLog);
            Assert.Equal(
                "In 3 Messwerten wurden keine Temperaturwerte aufgezeichnet: Die Hardware dieses Geräts meldet keinen CPU-Temperatursensor. Das ist bei Geräten zu erwarten, deren Sensor nicht offengelegt wird, es gibt also kein CPU-Temperaturdiagramm zu zeigen.",
                gaps.SensorLogStatusText);

            // One gap among measured values: the chart stays, and the
            // German summary names the gap; a switch re-reads the log
            // and re-resolves both sentences. A fresh file: the writer
            // appends, so the all-gaps rows above must go first.
            File.Delete(path);
            WriteLog(path, new DateTimeOffset(2026, 11, 12, 8, 45, 0, TimeSpan.Zero), 70, null, 72);
            File.AppendAllText(path, "2026-11-12T08:46:00.0000000Z,73\n");
            var partial = new HistoryViewModel(null, TestSnapshots.Healthy(), sensorLogPath: path, displayTimeZone: zone);
            partial.LoadSensorLog();
            Assert.True(partial.HasSensorLog);
            Assert.Equal("CPU temperature, 4 samples from 13:45 to 13:46.", partial.SensorLogStatusText);
            Assert.Equal("4 samples, 1 gap where the sensor did not report.", partial.SensorLogSummaryText);

            partial.ApplyLanguage("de");
            Assert.Equal("CPU-Temperatur, 4 Messwerte von 13:45 bis 13:46.", partial.SensorLogStatusText);
            Assert.Equal("4 Messwerte, 1 Lücke, in der der Sensor nichts gemeldet hat.", partial.SensorLogSummaryText);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static void WriteLog(string path, DateTimeOffset firstAtUtc, params double?[] values)
    {
        var writer = new SensorLogWriter(path);
        for (var i = 0; i < values.Length; i++)
        {
            writer.Append(new SensorSnapshot(
                true,
                "test",
                firstAtUtc.AddSeconds(2 * i),
                new[] { new SensorReading("CPU", "Package", SensorKind.Temperature, "C", values[i], "Test") }));
        }
    }
}
