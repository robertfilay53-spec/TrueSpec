using TrueSpec.App.Services;
using TrueSpec.App.ViewModels;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

/// <summary>
/// Localisation, phase 8 (v2.1.16): the Comparison and Fleet pages
/// in German — the page chrome follows the language picked in
/// Settings, in place and with no restart. The transient status
/// lines are re-derived from stored state, so a switch re-resolves
/// them instead of silently resetting them. The data that stays
/// English by design: machine names, the comparison summary
/// sentence, the row cell values (component names, health numbers,
/// delta words, changed-spec lines, verdicts, worst components,
/// scores, dates, counts), the DNA changed/unidentified part lines,
/// file and folder paths, and exception messages. The Fleet row's
/// "Not measurable" score fallback and "Not reported"
/// worst-component fallback stay English like the History table's.
/// Windows leg only (it drives TrueSpec.App's ViewModels), like
/// ReportTestsLocalizationViewModelTests: excluded from the net10.0
/// leg in the csproj. French phase 6 (v2.1.25) mirrors the German
/// Fleet tests in French; the Comparison page is not French yet, so
/// only the Fleet half is mirrored here.
/// </summary>
public class ComparisonFleetLocalizationViewModelTests
{
    private static async Task WaitForAsync(Func<bool> ready, string what, int seconds = 10)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(seconds);
        while (!ready() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.True(ready(), what + " never became ready.");
    }

    private static async Task<SqliteSnapshotStore> StoreWithTwoSnapshots(
        string databasePath, SystemSnapshot older, SystemSnapshot newer)
    {
        var store = new SqliteSnapshotStore(databasePath);
        await store.InitializeAsync();
        await store.SaveAsync(older);
        await store.SaveAsync(newer);
        return store;
    }

    private static (SystemSnapshot Older, SystemSnapshot Newer) TwoSnapshots()
    {
        var start = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
        var older = TestSnapshots.HistorySnapshot(82, "90%", "120", "4", "12400", start);
        var newer = TestSnapshots.HistorySnapshot(70, "75%", "300", "12", "45000", start.AddDays(30));
        return (older, newer);
    }

    private static async Task<ComparisonViewModel> ComparedPairAsync(
        string databasePath, string? languageCode = null, IMachineDnaVerifier? verifier = null)
    {
        var (older, newer) = TwoSnapshots();
        var store = await StoreWithTwoSnapshots(databasePath, older, newer);

        var viewModel = new ComparisonViewModel(store, newer, verifier, languageCode);
        await WaitForAsync(() => viewModel.HasComparison, "Comparison never completed.");
        return viewModel;
    }

    private static async Task<SqliteSnapshotStore> FleetStoreWithAsync(
        string directory,
        params (string Machine, double Score, string Wear, DateTimeOffset At)[] scans)
    {
        var databasePath = Path.Combine(directory, "snapshots.db");
        var store = new SqliteSnapshotStore(databasePath);
        await store.InitializeAsync();
        foreach (var scan in scans)
        {
            await store.SaveAsync(TestSnapshots.HistorySnapshot(
                scan.Score,
                "90.0%",
                "10",
                scan.Wear,
                "100",
                scan.At,
                machineName: scan.Machine));
        }

        return store;
    }

    private static async Task<FleetViewModel> LoadedFleetAsync(
        string directory, string? languageCode = null,
        Func<IReadOnlyList<FleetMachineRow>, IReadOnlyDictionary<string, SystemSnapshot>, Task<ExportAllMachinesResult>>? exportAll = null)
    {
        var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
        var store = await FleetStoreWithAsync(
            directory,
            ("SHOP-PC-GOOD", 92d, "8%", baseTime),
            ("SHOP-PC-BAD", 55d, "45%", baseTime.AddHours(1)),
            ("SHOP-PC-BAD", 58d, "42%", baseTime.AddHours(2)));

        var viewModel = new FleetViewModel(store, null, null, exportAll, languageCode);
        await WaitForAsync(() => viewModel.HasFleet, "Fleet never loaded from the store.");
        return viewModel;
    }

    private static string TempDirectory() =>
        Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Comparison_EnglishDefault_RendersChromeInEnglish()
    {
        var directory = TempDirectory();
        try
        {
            var viewModel = await ComparedPairAsync(Path.Combine(directory, "snapshots.db"));

            Assert.Equal("COMPARE", viewModel.ComparisonEyebrowText);
            Assert.Equal("Two snapshots, side by side", viewModel.ComparisonTitleText);
            Assert.Equal("Pick any two saved scans. TrueSpec shows the score change, every component that moved, and every specification that changed. Nothing is invented: a component that was not measurable in either scan says so.", viewModel.ComparisonIntroText);
            Assert.Equal("Before", viewModel.ComparisonBeforeText);
            Assert.Equal("After", viewModel.ComparisonAfterText);
            Assert.Equal("Pick an earlier snapshot", viewModel.ComparisonPickEarlierText);
            Assert.Equal("Pick a later snapshot", viewModel.ComparisonPickLaterText);
            Assert.Equal("Compare", viewModel.ComparisonCompareButtonText);
            Assert.Equal("Result", viewModel.ComparisonResultTitleText);
            Assert.Equal("Machine DNA", viewModel.ComparisonDnaTitleText);
            Assert.Equal("Not identified in one of the scans:", viewModel.ComparisonDnaUnidentifiedTitleText);
            Assert.Equal("DNA is identity, not health: it never changes the score above. It only says whether the two scans are the same machine with the same parts.", viewModel.ComparisonDnaNoteText);
            Assert.Equal("No component health changed and no specifications changed between these two snapshots.", viewModel.ComparisonNoChangesText);
            Assert.Equal("Added components", viewModel.ComparisonAddedTitleText);
            Assert.Equal("Removed components", viewModel.ComparisonRemovedTitleText);
            Assert.Equal("Snapshots are saved locally after every scan. History never leaves this machine.", viewModel.ComparisonFooterNoteText);
            Assert.Equal(
                $"Compared {viewModel.SelectedBefore!.CapturedAt:yyyy-MM-dd HH:mm} with {viewModel.SelectedAfter!.CapturedAt:yyyy-MM-dd HH:mm}.",
                viewModel.StatusMessage);
            Assert.Equal("DNA not recorded for one or both snapshots.", viewModel.DnaStateText);
            Assert.Equal("Before: DNA not recorded", viewModel.DnaHashBeforeText);
            Assert.Equal("After: DNA not recorded", viewModel.DnaHashAfterText);
            Assert.True(viewModel.HasChanges);
            Assert.NotEmpty(viewModel.Rows);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Comparison_GermanSeeded_RendersWholePageInGerman()
    {
        var directory = TempDirectory();
        try
        {
            var viewModel = await ComparedPairAsync(Path.Combine(directory, "snapshots.db"), "de");

            Assert.Equal("VERGLEICH", viewModel.ComparisonEyebrowText);
            Assert.Equal("Zwei Scans im Vergleich", viewModel.ComparisonTitleText);
            Assert.Equal("Wählen Sie zwei beliebige gespeicherte Scans. TrueSpec zeigt die Punktzahländerung, jede Komponente, die sich verändert hat, und jede geänderte Spezifikation. Nichts wird erfunden: Eine Komponente, die in keinem der Scans messbar war, sagt dies auch.", viewModel.ComparisonIntroText);
            Assert.Equal("Vorher", viewModel.ComparisonBeforeText);
            Assert.Equal("Nachher", viewModel.ComparisonAfterText);
            Assert.Equal("Wählen Sie einen früheren Scan", viewModel.ComparisonPickEarlierText);
            Assert.Equal("Wählen Sie einen späteren Scan", viewModel.ComparisonPickLaterText);
            Assert.Equal("Vergleichen", viewModel.ComparisonCompareButtonText);
            Assert.Equal("Ergebnis", viewModel.ComparisonResultTitleText);
            Assert.Equal("Geräte-DNA", viewModel.ComparisonDnaTitleText);
            Assert.Equal("In einem der Scans nicht identifiziert:", viewModel.ComparisonDnaUnidentifiedTitleText);
            Assert.Equal("Die DNA ist Identität, nicht Gesundheit: Sie ändert niemals die Punktzahl oben. Sie sagt nur, ob die beiden Scans dasselbe Gerät mit denselben Teilen sind.", viewModel.ComparisonDnaNoteText);
            Assert.Equal("Zwischen diesen beiden Scans hat sich weder die Gesundheit einer Komponente noch eine Spezifikation geändert.", viewModel.ComparisonNoChangesText);
            Assert.Equal("Hinzugefügte Komponenten", viewModel.ComparisonAddedTitleText);
            Assert.Equal("Entfernte Komponenten", viewModel.ComparisonRemovedTitleText);
            Assert.Equal("Scans werden nach jedem Scan lokal gespeichert. Der Verlauf verlässt diesen Rechner nie.", viewModel.ComparisonFooterNoteText);
            Assert.Equal(
                $"{viewModel.SelectedBefore!.CapturedAt:yyyy-MM-dd HH:mm} mit {viewModel.SelectedAfter!.CapturedAt:yyyy-MM-dd HH:mm} verglichen.",
                viewModel.StatusMessage);
            Assert.Equal("Für einen oder beide Scans wurde keine DNA aufgezeichnet.", viewModel.DnaStateText);
            Assert.Equal("Vorher: DNA nicht aufgezeichnet", viewModel.DnaHashBeforeText);
            Assert.Equal("Nachher: DNA nicht aufgezeichnet", viewModel.DnaHashAfterText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Comparison_GermanSeeded_IdenticalDna_SaysSameMachineInGerman()
    {
        var directory = TempDirectory();
        try
        {
            var fake = new FakeMachineDnaVerifier
            {
                DescribeResult = MachineDnaVerification.Identical("abcdef12…34567890"),
                CompareResult = MachineDnaVerification.Identical("abcdef12…34567890"),
            };
            var viewModel = await ComparedPairAsync(Path.Combine(directory, "snapshots.db"), "de", fake);

            Assert.Equal("Dasselbe Gerät, dieselben Teile.", viewModel.DnaStateText);
            Assert.Equal("Vorher: abcdef12…34567890", viewModel.DnaHashBeforeText);
            Assert.Equal("Nachher: abcdef12…34567890", viewModel.DnaHashAfterText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Comparison_ApplyLanguage_SwitchesBothWays()
    {
        var directory = TempDirectory();
        try
        {
            var viewModel = await ComparedPairAsync(Path.Combine(directory, "snapshots.db"));

            viewModel.ApplyLanguage("de");
            Assert.Equal("VERGLEICH", viewModel.ComparisonEyebrowText);
            Assert.Equal("Zwei Scans im Vergleich", viewModel.ComparisonTitleText);
            Assert.Equal("Vergleichen", viewModel.ComparisonCompareButtonText);
            Assert.Equal("Für einen oder beide Scans wurde keine DNA aufgezeichnet.", viewModel.DnaStateText);
            Assert.Equal("Vorher: DNA nicht aufgezeichnet", viewModel.DnaHashBeforeText);
            Assert.Equal(
                $"{viewModel.SelectedBefore!.CapturedAt:yyyy-MM-dd HH:mm} mit {viewModel.SelectedAfter!.CapturedAt:yyyy-MM-dd HH:mm} verglichen.",
                viewModel.StatusMessage);

            viewModel.ApplyLanguage("en");
            Assert.Equal("COMPARE", viewModel.ComparisonEyebrowText);
            Assert.Equal("Two snapshots, side by side", viewModel.ComparisonTitleText);
            Assert.Equal("Compare", viewModel.ComparisonCompareButtonText);
            Assert.Equal("DNA not recorded for one or both snapshots.", viewModel.DnaStateText);
            Assert.Equal("Before: DNA not recorded", viewModel.DnaHashBeforeText);
            Assert.Equal(
                $"Compared {viewModel.SelectedBefore!.CapturedAt:yyyy-MM-dd HH:mm} with {viewModel.SelectedAfter!.CapturedAt:yyyy-MM-dd HH:mm}.",
                viewModel.StatusMessage);

            viewModel.ApplyLanguage("de");
            Assert.Equal("VERGLEICH", viewModel.ComparisonEyebrowText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Comparison_UnknownLanguage_FallsBackToEnglish()
    {
        var directory = TempDirectory();
        try
        {
            var viewModel = await ComparedPairAsync(Path.Combine(directory, "snapshots.db"), "xx");

            Assert.Equal("COMPARE", viewModel.ComparisonEyebrowText);
            Assert.Equal("Two snapshots, side by side", viewModel.ComparisonTitleText);
            Assert.Equal("Compare", viewModel.ComparisonCompareButtonText);
            Assert.Equal("DNA not recorded for one or both snapshots.", viewModel.DnaStateText);
            Assert.Equal("Before: DNA not recorded", viewModel.DnaHashBeforeText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Comparison_DataStaysEnglish_WhenChromeIsGerman()
    {
        var directory = TempDirectory();
        try
        {
            var english = await ComparedPairAsync(Path.Combine(directory, "en.db"));
            var german = await ComparedPairAsync(Path.Combine(directory, "de.db"), "de");

            Assert.Equal(english.SummaryText, german.SummaryText);
            Assert.Equal(english.Rows.Select(r => r.ComponentName), german.Rows.Select(r => r.ComponentName));
            Assert.Equal(english.Rows.Select(r => r.HealthBefore), german.Rows.Select(r => r.HealthBefore));
            Assert.Equal(english.Rows.Select(r => r.HealthAfter), german.Rows.Select(r => r.HealthAfter));
            Assert.Equal(english.Rows.Select(r => r.DeltaText), german.Rows.Select(r => r.DeltaText));
            Assert.Equal(english.Rows.Select(r => r.ChangedSpecs), german.Rows.Select(r => r.ChangedSpecs));
            Assert.Equal(english.AddedComponents, german.AddedComponents);
            Assert.Equal(english.RemovedComponents, german.RemovedComponents);
            Assert.Equal(english.DnaChangedParts, german.DnaChangedParts);
            Assert.Equal(english.DnaUnidentifiedParts, german.DnaUnidentifiedParts);

            // A switch re-renders the chrome in place, keeping the data.
            german.ApplyLanguage("en");
            Assert.Equal(english.ComparisonEyebrowText, german.ComparisonEyebrowText);
            Assert.Equal(english.StatusMessage, german.StatusMessage);
            Assert.Equal(english.SummaryText, german.SummaryText);
            Assert.Equal(english.Rows.Select(r => r.ChangedSpecs), german.Rows.Select(r => r.ChangedSpecs));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Comparison_EmptyStates_StayHonestInBothLanguages()
    {
        var noStore = new ComparisonViewModel(null, TestSnapshots.Healthy());
        await WaitForAsync(
            () => noStore.StatusMessage != "Loading snapshot history...",
            "Status never left loading.");
        Assert.Equal("Snapshot history is unavailable on this machine, so there is nothing to compare yet.", noStore.StatusMessage);

        var noStoreGerman = new ComparisonViewModel(null, TestSnapshots.Healthy(), null, "de");
        await WaitForAsync(
            () => noStoreGerman.StatusMessage != "Scanverlauf wird geladen …",
            "Status never left loading.");
        Assert.Equal("Der Scanverlauf ist auf diesem Rechner nicht verfügbar, daher gibt es noch nichts zu vergleichen.", noStoreGerman.StatusMessage);

        var directory = TempDirectory();
        try
        {
            var store = new SqliteSnapshotStore(Path.Combine(directory, "snapshots.db"));
            await store.InitializeAsync();
            var only = TestSnapshots.HistorySnapshot(82, "90%", "120", "4", "12400", DateTimeOffset.UtcNow);
            await store.SaveAsync(only);

            var oneSnapshot = new ComparisonViewModel(store, only);
            await WaitForAsync(
                () => oneSnapshot.StatusMessage != "Loading snapshot history...",
                "Status never left loading.");
            Assert.Equal("Only one snapshot is saved so far. Run another scan (refresh) and come back to compare.", oneSnapshot.StatusMessage);

            var oneSnapshotGerman = new ComparisonViewModel(store, only, null, "de");
            await WaitForAsync(
                () => oneSnapshotGerman.StatusMessage != "Scanverlauf wird geladen …",
                "Status never left loading.");
            Assert.Equal("Bisher ist nur ein Scan gespeichert. Führen Sie einen weiteren Scan aus (Aktualisieren) und kommen Sie zum Vergleichen zurück.", oneSnapshotGerman.StatusMessage);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_EnglishDefault_RendersChromeInEnglish()
    {
        var directory = TempDirectory();
        try
        {
            var viewModel = await LoadedFleetAsync(directory);

            Assert.Equal("FLEET", viewModel.FleetEyebrowText);
            Assert.Equal("Every machine in this data folder", viewModel.FleetTitleText);
            Assert.Equal("Technician mode: carry the portable app on a USB stick, scan each machine, and every scan lands in the same store on the stick. This page lists each machine's latest scan, lowest score first, with the worst component named, so the machine that needs attention is at the top. Machines scanned with the installed app can be brought in with Import JSON reports, which reads the JSON files the Report page exports. Export all machines writes one CSV per machine into the export folder, so the whole fleet can be filed at once instead of one Export click per row. To take a machine out of the fleet, use Remove on its row — it deletes all of that machine's saved scans from this data folder, in two clicks so a misclick cannot delete anything.", viewModel.FleetIntroText);
            Assert.Equal("Import JSON reports", viewModel.FleetImportButtonText);
            Assert.Equal("Imports TrueSpec JSON reports exported from other machines, so their scans join this fleet. A file that is not a TrueSpec report is skipped, and importing the same file twice stores one scan.", viewModel.FleetImportNoteText);
            Assert.Equal("Machines", viewModel.FleetMachinesTitleText);
            Assert.Equal("Export all machines", viewModel.FleetExportAllButtonText);
            Assert.Equal("Export fleet as CSV", viewModel.FleetExportFleetCsvButtonText);
            Assert.Equal("Lowest score first. A dash means that scan did not report the value.", viewModel.FleetTableNoteText);
            Assert.Equal("Machine", viewModel.FleetHeaderMachineText);
            Assert.Equal("Scans", viewModel.FleetHeaderScansText);
            Assert.Equal("Last scan", viewModel.FleetHeaderLastScanText);
            Assert.Equal("Score", viewModel.FleetHeaderScoreText);
            Assert.Equal("Verdict", viewModel.FleetHeaderVerdictText);
            Assert.Equal("Worst component", viewModel.FleetHeaderWorstComponentText);
            Assert.Equal("Export", viewModel.FleetHeaderExportText);
            Assert.Equal("Remove", viewModel.FleetHeaderRemoveText);
            Assert.Equal("Fleet loaded.", viewModel.StatusMessage);
            Assert.Equal("2 machines in this data folder, 3 saved scans. Lowest score 58 (SHOP-PC-BAD).", viewModel.SummaryText);

            Assert.Equal(2, viewModel.Rows.Count);
            Assert.Equal("SHOP-PC-BAD", viewModel.Rows[0].MachineName);
            Assert.Equal("Export CSV", viewModel.Rows[0].ExportButtonText);
            Assert.Equal("Remove", viewModel.Rows[0].RemoveButtonText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_GermanSeeded_RendersWholePageInGerman()
    {
        var directory = TempDirectory();
        try
        {
            var viewModel = await LoadedFleetAsync(directory, "de");

            Assert.Equal("GERÄTEPARK", viewModel.FleetEyebrowText);
            Assert.Equal("Jedes Gerät in diesem Datenordner", viewModel.FleetTitleText);
            Assert.Equal("Technikermodus: Nehmen Sie die portable App auf einem USB-Stick mit, scannen Sie jedes Gerät, und jeder Scan landet im selben Speicher auf dem Stick. Diese Seite listet den neuesten Scan jedes Geräts, niedrigste Punktzahl zuerst, mit der schlechtesten Komponente benannt, sodass das Gerät, das Aufmerksamkeit braucht, ganz oben steht. Geräte, die mit der installierten App gescannt wurden, können mit „JSON-Berichte importieren\" hereingeholt werden, was die JSON-Dateien liest, die die Berichtsseite exportiert. „Alle Geräte exportieren\" schreibt eine CSV-Datei pro Gerät in den Exportordner, sodass der ganze Gerätepark auf einmal abgelegt werden kann, statt einem Export-Klick pro Zeile. Um ein Gerät aus dem Gerätepark zu nehmen, verwenden Sie „Entfernen\" in seiner Zeile – es löscht alle gespeicherten Scans dieses Geräts aus diesem Datenordner, in zwei Klicks, damit ein Fehlklick nichts löschen kann.", viewModel.FleetIntroText);
            Assert.Equal("JSON-Berichte importieren", viewModel.FleetImportButtonText);
            Assert.Equal("Importiert TrueSpec-JSON-Berichte, die von anderen Geräten exportiert wurden, sodass ihre Scans zu diesem Gerätepark kommen. Eine Datei, die kein TrueSpec-Bericht ist, wird übersprungen, und wenn dieselbe Datei zweimal importiert wird, wird nur ein Scan gespeichert.", viewModel.FleetImportNoteText);
            Assert.Equal("Geräte", viewModel.FleetMachinesTitleText);
            Assert.Equal("Alle Geräte exportieren", viewModel.FleetExportAllButtonText);
            Assert.Equal("Gerätepark als CSV exportieren", viewModel.FleetExportFleetCsvButtonText);
            Assert.Equal("Niedrigste Punktzahl zuerst. Ein Strich bedeutet, dass dieser Scan den Wert nicht gemeldet hat.", viewModel.FleetTableNoteText);
            Assert.Equal("Gerät", viewModel.FleetHeaderMachineText);
            Assert.Equal("Scans", viewModel.FleetHeaderScansText);
            Assert.Equal("Letzter Scan", viewModel.FleetHeaderLastScanText);
            Assert.Equal("Punktzahl", viewModel.FleetHeaderScoreText);
            Assert.Equal("Urteil", viewModel.FleetHeaderVerdictText);
            Assert.Equal("Schlechteste Komponente", viewModel.FleetHeaderWorstComponentText);
            Assert.Equal("Export", viewModel.FleetHeaderExportText);
            Assert.Equal("Entfernen", viewModel.FleetHeaderRemoveText);
            Assert.Equal("Gerätepark geladen.", viewModel.StatusMessage);
            Assert.Equal("2 Geräte in diesem Datenordner, 3 gespeicherte Scans. Niedrigste Punktzahl 58 (SHOP-PC-BAD).", viewModel.SummaryText);

            Assert.Equal(2, viewModel.Rows.Count);
            Assert.Equal("SHOP-PC-BAD", viewModel.Rows[0].MachineName);
            Assert.Equal("CSV exportieren", viewModel.Rows[0].ExportButtonText);
            Assert.Equal("Entfernen", viewModel.Rows[0].RemoveButtonText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_ApplyLanguage_SwitchesBothWays()
    {
        var directory = TempDirectory();
        try
        {
            var viewModel = await LoadedFleetAsync(directory);

            viewModel.ApplyLanguage("de");
            Assert.Equal("GERÄTEPARK", viewModel.FleetEyebrowText);
            Assert.Equal("Jedes Gerät in diesem Datenordner", viewModel.FleetTitleText);
            Assert.Equal("Alle Geräte exportieren", viewModel.FleetExportAllButtonText);
            Assert.Equal("Gerätepark geladen.", viewModel.StatusMessage);
            Assert.Equal("2 Geräte in diesem Datenordner, 3 gespeicherte Scans. Niedrigste Punktzahl 58 (SHOP-PC-BAD).", viewModel.SummaryText);
            Assert.Equal("CSV exportieren", viewModel.Rows[0].ExportButtonText);
            Assert.Equal("Entfernen", viewModel.Rows[0].RemoveButtonText);

            viewModel.ApplyLanguage("en");
            Assert.Equal("FLEET", viewModel.FleetEyebrowText);
            Assert.Equal("Every machine in this data folder", viewModel.FleetTitleText);
            Assert.Equal("Export all machines", viewModel.FleetExportAllButtonText);
            Assert.Equal("Fleet loaded.", viewModel.StatusMessage);
            Assert.Equal("2 machines in this data folder, 3 saved scans. Lowest score 58 (SHOP-PC-BAD).", viewModel.SummaryText);
            Assert.Equal("Export CSV", viewModel.Rows[0].ExportButtonText);
            Assert.Equal("Remove", viewModel.Rows[0].RemoveButtonText);

            viewModel.ApplyLanguage("de");
            Assert.Equal("GERÄTEPARK", viewModel.FleetEyebrowText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_UnknownLanguage_FallsBackToEnglish()
    {
        var directory = TempDirectory();
        try
        {
            var viewModel = await LoadedFleetAsync(directory, "xx");

            Assert.Equal("FLEET", viewModel.FleetEyebrowText);
            Assert.Equal("Every machine in this data folder", viewModel.FleetTitleText);
            Assert.Equal("Export all machines", viewModel.FleetExportAllButtonText);
            Assert.Equal("Fleet loaded.", viewModel.StatusMessage);
            Assert.Equal("2 machines in this data folder, 3 saved scans. Lowest score 58 (SHOP-PC-BAD).", viewModel.SummaryText);
            Assert.Equal("Export CSV", viewModel.Rows[0].ExportButtonText);
            Assert.Equal("Remove", viewModel.Rows[0].RemoveButtonText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_DataStaysEnglish_WhenChromeIsGerman()
    {
        var directory = TempDirectory();
        try
        {
            var english = await LoadedFleetAsync(Path.Combine(directory, "en"));
            var german = await LoadedFleetAsync(Path.Combine(directory, "de"), "de");

            Assert.Equal(english.Rows.Select(r => r.MachineName), german.Rows.Select(r => r.MachineName));
            Assert.Equal(english.Rows.Select(r => r.ScanCountText), german.Rows.Select(r => r.ScanCountText));
            Assert.Equal(english.Rows.Select(r => r.LastScanText), german.Rows.Select(r => r.LastScanText));
            Assert.Equal(english.Rows.Select(r => r.ScoreText), german.Rows.Select(r => r.ScoreText));
            Assert.Equal(english.Rows.Select(r => r.VerdictText), german.Rows.Select(r => r.VerdictText));
            Assert.Equal(english.Rows.Select(r => r.WorstComponentText), german.Rows.Select(r => r.WorstComponentText));

            // A switch re-renders the chrome in place, keeping the data.
            german.ApplyLanguage("en");
            Assert.Equal(english.FleetEyebrowText, german.FleetEyebrowText);
            Assert.Equal(english.SummaryText, german.SummaryText);
            Assert.Equal(english.Rows.Select(r => r.WorstComponentText), german.Rows.Select(r => r.WorstComponentText));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void FleetRow_RemoveButton_FollowsArmedStateInBothLanguages()
    {
        var row = new FleetMachineRow(
            "SHOP-PC-BAD",
            2,
            new DateTimeOffset(2026, 11, 10, 11, 0, 0, TimeSpan.Zero),
            58d,
            "Poor",
            "storage",
            58d);

        var english = new FleetRowViewModel(row);
        Assert.Equal("Export CSV", english.ExportButtonText);
        Assert.Equal("Remove", english.RemoveButtonText);
        english.IsRemoveArmed = true;
        Assert.Equal("Confirm remove", english.RemoveButtonText);

        var german = new FleetRowViewModel(row, null, null, "de");
        Assert.Equal("CSV exportieren", german.ExportButtonText);
        Assert.Equal("Entfernen", german.RemoveButtonText);
        german.IsRemoveArmed = true;
        Assert.Equal("Entfernen bestätigen", german.RemoveButtonText);

        german.ApplyLanguage("en");
        Assert.Equal("Export CSV", german.ExportButtonText);
        Assert.Equal("Confirm remove", german.RemoveButtonText);
        german.ApplyLanguage("de");
        Assert.Equal("CSV exportieren", german.ExportButtonText);
        Assert.Equal("Entfernen bestätigen", german.RemoveButtonText);

        var unknown = new FleetRowViewModel(row, null, null, "xx");
        Assert.Equal("Export CSV", unknown.ExportButtonText);
        Assert.Equal("Remove", unknown.RemoveButtonText);
    }

    [Fact]
    public async Task Fleet_RemoveArmAndConfirm_StatusTranslates()
    {
        var directory = TempDirectory();
        try
        {
            var german = await LoadedFleetAsync(Path.Combine(directory, "de"), "de");
            var badRow = german.Rows[0];
            await german.RemoveMachineCommand.ExecuteAsync(badRow);

            Assert.True(badRow.IsRemoveArmed);
            Assert.Equal("Entfernen bestätigen", badRow.RemoveButtonText);
            Assert.Equal(
                "SHOP-PC-BAD entfernen? Dadurch werden alle 2 gespeicherten Scans für dieses Gerät aus diesem Datenordner gelöscht. Klicken Sie auf „Entfernen bestätigen\", um zu löschen – bis dahin wird nichts gelöscht.",
                german.StatusMessage);

            // The same flow in English, through to the confirmed delete.
            var english = await LoadedFleetAsync(Path.Combine(directory, "en"));
            var englishBadRow = english.Rows[0];
            await english.RemoveMachineCommand.ExecuteAsync(englishBadRow);
            Assert.Equal(
                "Remove SHOP-PC-BAD? This deletes all 2 saved scans for this machine from this data folder. Click 'Confirm remove' to delete — nothing is deleted until then.",
                english.StatusMessage);
            await english.RemoveMachineCommand.ExecuteAsync(englishBadRow);
            Assert.Equal("Removed SHOP-PC-BAD (2 saved scans).", english.StatusMessage);
            Assert.Single(english.Rows);
            Assert.Equal("SHOP-PC-GOOD", english.Rows[0].MachineName);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_ExportAll_StatusTranslates()
    {
        var directory = TempDirectory();
        try
        {
            var folder = Path.Combine(directory, "exports");
            Task<ExportAllMachinesResult> ExportAll(
                IReadOnlyList<FleetMachineRow> rows,
                IReadOnlyDictionary<string, SystemSnapshot> snapshots) =>
                Task.FromResult(new ExportAllMachinesResult(
                    folder,
                    new[] { Path.Combine(folder, "SHOP-PC-BAD.csv"), Path.Combine(folder, "SHOP-PC-GOOD.csv") },
                    Array.Empty<ExportAllSkippedMachine>()));

            var german = await LoadedFleetAsync(Path.Combine(directory, "de"), "de", ExportAll);
            await german.ExportAllMachinesCommand.ExecuteAsync(null);
            Assert.Equal(
                $"2 von 2 Geräten nach {folder} exportiert.",
                german.StatusMessage);

            // A switch re-resolves the stored result instead of resetting it.
            german.ApplyLanguage("en");
            Assert.Equal(
                $"Exported 2 of 2 machines to {folder}.",
                german.StatusMessage);
            german.ApplyLanguage("de");
            Assert.Equal(
                $"2 von 2 Geräten nach {folder} exportiert.",
                german.StatusMessage);

            // Nothing exported: the zero line names what was skipped,
            // and the skip reasons render in the page language now
            // (v2.1.19), instead of the old English fragments inside
            // the German line.
            Task<ExportAllMachinesResult> ExportNone(
                IReadOnlyList<FleetMachineRow> rows,
                IReadOnlyDictionary<string, SystemSnapshot> snapshots) =>
                Task.FromResult(new ExportAllMachinesResult(
                    folder,
                    Array.Empty<string>(),
                    new[]
                    {
                        new ExportAllSkippedMachine("SHOP-PC-BAD", ExportAllSkipReasonKind.NoScore),
                        new ExportAllSkippedMachine("SHOP-PC-GOOD", ExportAllSkipReasonKind.NoScore),
                    }));

            var noneGerman = await LoadedFleetAsync(Path.Combine(directory, "none"), "de", ExportNone);
            await noneGerman.ExportAllMachinesCommand.ExecuteAsync(null);
            Assert.Equal(
                "0 Geräte exportiert. Übersprungen 2: SHOP-PC-BAD: der neueste Scan hat keine Bewertung zum Exportieren; SHOP-PC-GOOD: der neueste Scan hat keine Bewertung zum Exportieren.",
                noneGerman.StatusMessage);

            // A switch re-resolves the stored result instead of resetting it.
            noneGerman.ApplyLanguage("en");
            Assert.Equal(
                "Exported 0 machines. Skipped 2: SHOP-PC-BAD: the latest scan has no score to export; SHOP-PC-GOOD: the latest scan has no score to export.",
                noneGerman.StatusMessage);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_EmptyStates_StayHonestInBothLanguages()
    {
        var directory = TempDirectory();
        try
        {
            var databasePath = Path.Combine(directory, "snapshots.db");
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var viewModel = new FleetViewModel(store);
            await WaitForAsync(
                () => viewModel.StatusMessage != "Loading fleet...",
                "Status never left loading.");
            Assert.Equal("No saved scans yet. Scan a machine, or run the portable app on each machine in the fleet, and its latest scan appears here. You can also import JSON reports exported from other machines with the Import button.", viewModel.StatusMessage);

            var german = new FleetViewModel(store, null, null, null, "de");
            await WaitForAsync(
                () => german.StatusMessage != "Gerätepark wird geladen …",
                "Status never left loading.");
            Assert.Equal("Noch keine gespeicherten Scans. Scannen Sie ein Gerät, oder führen Sie die portable App auf jedem Gerät im Gerätepark aus, und sein neuester Scan erscheint hier. Sie können auch JSON-Berichte von anderen Geräten mit der Schaltfläche „JSON-Berichte importieren\" importieren.", german.StatusMessage);

            var noStore = new FleetViewModel(null);
            await WaitForAsync(
                () => noStore.StatusMessage != "Loading fleet...",
                "Status never left loading.");
            Assert.Equal("Snapshot history is unavailable on this machine, so there is no fleet to show yet.", noStore.StatusMessage);

            var noStoreGerman = new FleetViewModel(null, null, null, null, "de");
            await WaitForAsync(
                () => noStoreGerman.StatusMessage != "Gerätepark wird geladen …",
                "Status never left loading.");
            Assert.Equal("Der Scanverlauf ist auf diesem Rechner nicht verfügbar, daher gibt es noch keinen Gerätepark.", noStoreGerman.StatusMessage);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_ImportSkippedFile_StatusTranslates()
    {
        var directory = TempDirectory();
        try
        {
            var databasePath = Path.Combine(directory, "snapshots.db");
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var missing = Path.Combine(directory, "bad.json");
            var viewModel = new FleetViewModel(store, null, null, null, "de")
            {
                ReportFilePicker = () => Task.FromResult<IReadOnlyList<string>>(new[] { missing }),
            };
            await WaitForAsync(
                () => viewModel.StatusMessage != "Gerätepark wird geladen …",
                "Status never left loading.");
            await viewModel.ImportReportsCommand.ExecuteAsync(null);

            // The head of the line and the skipped count translate;
            // the file name and the read-failure reason stay English,
            // like exception messages elsewhere on the page.
            Assert.Contains("0 Berichte importiert. Übersprungen 1: bad.json:", viewModel.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
    [Fact]
    public async Task Fleet_FrenchSeeded_RendersWholePageInFrench()
    {
        var directory = TempDirectory();
        try
        {
            var viewModel = await LoadedFleetAsync(directory, "fr");

            Assert.Equal("PARC", viewModel.FleetEyebrowText);
            Assert.Equal("Toutes les machines de ce dossier de données", viewModel.FleetTitleText);
            Assert.Equal("Mode technicien : emportez l'application portable sur une clé USB, scannez chaque machine, et chaque scan aboutit dans le même magasin sur la clé. Cette page répertorie le dernier scan de chaque machine, le score le plus bas en premier, avec le pire composant nommé, de sorte que la machine qui a besoin d'attention soit en haut. Les machines scannées avec l'application installée peuvent être importées avec Importer des rapports JSON, qui lit les fichiers JSON que la page Rapport exporte. Exporter toutes les machines écrit un CSV par machine dans le dossier d'exportation, de sorte que tout le parc puisse être archivé d'un coup au lieu d'un clic Exporter par ligne. Pour retirer une machine du parc, utilisez Supprimer sur sa ligne — cela supprime tous les scans enregistrés de cette machine de ce dossier de données, en deux clics pour qu'un faux clic ne puisse rien supprimer.", viewModel.FleetIntroText);
            Assert.Equal("Importer des rapports JSON", viewModel.FleetImportButtonText);
            Assert.Equal("Importe les rapports JSON TrueSpec exportés depuis d'autres machines, afin que leurs scans rejoignent ce parc. Un fichier qui n'est pas un rapport TrueSpec est ignoré, et importer deux fois le même fichier n'enregistre qu'un seul scan.", viewModel.FleetImportNoteText);
            Assert.Equal("Machines", viewModel.FleetMachinesTitleText);
            Assert.Equal("Exporter toutes les machines", viewModel.FleetExportAllButtonText);
            Assert.Equal("Exporter le parc en CSV", viewModel.FleetExportFleetCsvButtonText);
            Assert.Equal("Score le plus bas en premier. Un tiret signifie que ce scan n'a pas signalé la valeur.", viewModel.FleetTableNoteText);
            Assert.Equal("Machine", viewModel.FleetHeaderMachineText);
            Assert.Equal("Scans", viewModel.FleetHeaderScansText);
            Assert.Equal("Dernier scan", viewModel.FleetHeaderLastScanText);
            Assert.Equal("Score", viewModel.FleetHeaderScoreText);
            Assert.Equal("Verdict", viewModel.FleetHeaderVerdictText);
            Assert.Equal("Pire composant", viewModel.FleetHeaderWorstComponentText);
            Assert.Equal("Exporter", viewModel.FleetHeaderExportText);
            Assert.Equal("Supprimer", viewModel.FleetHeaderRemoveText);
            Assert.Equal("Parc chargé.", viewModel.StatusMessage);
            Assert.Equal("2 machines dans ce dossier de données, 3 scans enregistrés. Score le plus bas 58 (SHOP-PC-BAD).", viewModel.SummaryText);

            Assert.Equal(2, viewModel.Rows.Count);
            Assert.Equal("SHOP-PC-BAD", viewModel.Rows[0].MachineName);
            Assert.Equal("Exporter en CSV", viewModel.Rows[0].ExportButtonText);
            Assert.Equal("Supprimer", viewModel.Rows[0].RemoveButtonText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_ApplyLanguage_SwitchesToFrenchAndBack()
    {
        var directory = TempDirectory();
        try
        {
            var viewModel = await LoadedFleetAsync(directory);

            viewModel.ApplyLanguage("fr");
            Assert.Equal("PARC", viewModel.FleetEyebrowText);
            Assert.Equal("Toutes les machines de ce dossier de données", viewModel.FleetTitleText);
            Assert.Equal("Exporter toutes les machines", viewModel.FleetExportAllButtonText);
            Assert.Equal("Parc chargé.", viewModel.StatusMessage);
            Assert.Equal("2 machines dans ce dossier de données, 3 scans enregistrés. Score le plus bas 58 (SHOP-PC-BAD).", viewModel.SummaryText);
            Assert.Equal("Exporter en CSV", viewModel.Rows[0].ExportButtonText);
            Assert.Equal("Supprimer", viewModel.Rows[0].RemoveButtonText);

            viewModel.ApplyLanguage("en");
            Assert.Equal("FLEET", viewModel.FleetEyebrowText);
            Assert.Equal("Every machine in this data folder", viewModel.FleetTitleText);
            Assert.Equal("Export all machines", viewModel.FleetExportAllButtonText);
            Assert.Equal("Fleet loaded.", viewModel.StatusMessage);
            Assert.Equal("2 machines in this data folder, 3 saved scans. Lowest score 58 (SHOP-PC-BAD).", viewModel.SummaryText);
            Assert.Equal("Export CSV", viewModel.Rows[0].ExportButtonText);
            Assert.Equal("Remove", viewModel.Rows[0].RemoveButtonText);

            viewModel.ApplyLanguage("fr");
            Assert.Equal("PARC", viewModel.FleetEyebrowText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_DataStaysEnglish_WhenChromeIsFrench()
    {
        var directory = TempDirectory();
        try
        {
            var english = await LoadedFleetAsync(Path.Combine(directory, "en"));
            var french = await LoadedFleetAsync(Path.Combine(directory, "fr"), "fr");

            Assert.Equal(english.Rows.Select(r => r.MachineName), french.Rows.Select(r => r.MachineName));
            Assert.Equal(english.Rows.Select(r => r.ScanCountText), french.Rows.Select(r => r.ScanCountText));
            Assert.Equal(english.Rows.Select(r => r.LastScanText), french.Rows.Select(r => r.LastScanText));
            Assert.Equal(english.Rows.Select(r => r.ScoreText), french.Rows.Select(r => r.ScoreText));
            Assert.Equal(english.Rows.Select(r => r.VerdictText), french.Rows.Select(r => r.VerdictText));
            Assert.Equal(english.Rows.Select(r => r.WorstComponentText), french.Rows.Select(r => r.WorstComponentText));

            // A switch re-renders the chrome in place, keeping the data.
            french.ApplyLanguage("en");
            Assert.Equal(english.FleetEyebrowText, french.FleetEyebrowText);
            Assert.Equal(english.SummaryText, french.SummaryText);
            Assert.Equal(english.Rows.Select(r => r.WorstComponentText), french.Rows.Select(r => r.WorstComponentText));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void FleetRow_RemoveButton_FollowsArmedStateInFrench()
    {
        var row = new FleetMachineRow(
            "SHOP-PC-BAD",
            2,
            new DateTimeOffset(2026, 11, 10, 11, 0, 0, TimeSpan.Zero),
            58d,
            "Poor",
            "storage",
            58d);

        var english = new FleetRowViewModel(row);
        Assert.Equal("Export CSV", english.ExportButtonText);
        Assert.Equal("Remove", english.RemoveButtonText);
        english.IsRemoveArmed = true;
        Assert.Equal("Confirm remove", english.RemoveButtonText);

        var french = new FleetRowViewModel(row, null, null, "fr");
        Assert.Equal("Exporter en CSV", french.ExportButtonText);
        Assert.Equal("Supprimer", french.RemoveButtonText);
        french.IsRemoveArmed = true;
        Assert.Equal("Confirmer la suppression", french.RemoveButtonText);

        french.ApplyLanguage("en");
        Assert.Equal("Export CSV", french.ExportButtonText);
        Assert.Equal("Confirm remove", french.RemoveButtonText);
        french.ApplyLanguage("fr");
        Assert.Equal("Exporter en CSV", french.ExportButtonText);
        Assert.Equal("Confirmer la suppression", french.RemoveButtonText);

        var unknown = new FleetRowViewModel(row, null, null, "xx");
        Assert.Equal("Export CSV", unknown.ExportButtonText);
        Assert.Equal("Remove", unknown.RemoveButtonText);
    }

    [Fact]
    public async Task Fleet_RemoveArmAndConfirm_StatusTranslatesInFrench()
    {
        var directory = TempDirectory();
        try
        {
            var french = await LoadedFleetAsync(Path.Combine(directory, "fr"), "fr");
            var badRow = french.Rows[0];
            await french.RemoveMachineCommand.ExecuteAsync(badRow);

            Assert.True(badRow.IsRemoveArmed);
            Assert.Equal("Confirmer la suppression", badRow.RemoveButtonText);
            Assert.Equal(
                "Supprimer SHOP-PC-BAD ? Cela supprime les 2 scans enregistrés de cette machine de ce dossier de données. Cliquez sur « Confirmer la suppression » pour supprimer — rien n'est supprimé avant cela.",
                french.StatusMessage);

            // The same flow through to the confirmed delete, in French.
            await french.RemoveMachineCommand.ExecuteAsync(badRow);
            Assert.Equal("SHOP-PC-BAD supprimé (2 scans enregistrés).", french.StatusMessage);
            Assert.Single(french.Rows);
            Assert.Equal("SHOP-PC-GOOD", french.Rows[0].MachineName);

            // A switch re-resolves the stored result instead of resetting it.
            french.ApplyLanguage("en");
            Assert.Equal("Removed SHOP-PC-BAD (2 saved scans).", french.StatusMessage);
            french.ApplyLanguage("fr");
            Assert.Equal("SHOP-PC-BAD supprimé (2 scans enregistrés).", french.StatusMessage);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_ExportAll_StatusTranslatesInFrench()
    {
        var directory = TempDirectory();
        try
        {
            var folder = Path.Combine(directory, "exports");
            Task<ExportAllMachinesResult> ExportAll(
                IReadOnlyList<FleetMachineRow> rows,
                IReadOnlyDictionary<string, SystemSnapshot> snapshots) =>
                Task.FromResult(new ExportAllMachinesResult(
                    folder,
                    new[] { Path.Combine(folder, "SHOP-PC-BAD.csv"), Path.Combine(folder, "SHOP-PC-GOOD.csv") },
                    Array.Empty<ExportAllSkippedMachine>()));

            var french = await LoadedFleetAsync(Path.Combine(directory, "fr"), "fr", ExportAll);
            await french.ExportAllMachinesCommand.ExecuteAsync(null);
            Assert.Equal(
                $"2 machines sur 2 exportées vers {folder}.",
                french.StatusMessage);

            // A switch re-resolves the stored result instead of resetting it.
            french.ApplyLanguage("en");
            Assert.Equal(
                $"Exported 2 of 2 machines to {folder}.",
                french.StatusMessage);
            french.ApplyLanguage("fr");
            Assert.Equal(
                $"2 machines sur 2 exportées vers {folder}.",
                french.StatusMessage);

            // Nothing exported: the zero line names what was skipped,
            // and the skip reasons render in the page language now
            // (v2.1.19), instead of the old English fragments inside
            // the French line.
            Task<ExportAllMachinesResult> ExportNone(
                IReadOnlyList<FleetMachineRow> rows,
                IReadOnlyDictionary<string, SystemSnapshot> snapshots) =>
                Task.FromResult(new ExportAllMachinesResult(
                    folder,
                    Array.Empty<string>(),
                    new[]
                    {
                        new ExportAllSkippedMachine("SHOP-PC-BAD", ExportAllSkipReasonKind.NoScore),
                        new ExportAllSkippedMachine("SHOP-PC-GOOD", ExportAllSkipReasonKind.NoScore),
                    }));

            var noneFrench = await LoadedFleetAsync(Path.Combine(directory, "none"), "fr", ExportNone);
            await noneFrench.ExportAllMachinesCommand.ExecuteAsync(null);
            Assert.Equal(
                "0 machine exportée. Ignoré 2 : SHOP-PC-BAD: le dernier scan n'a pas de score à exporter; SHOP-PC-GOOD: le dernier scan n'a pas de score à exporter.",
                noneFrench.StatusMessage);

            // A switch re-resolves the stored result instead of resetting it.
            noneFrench.ApplyLanguage("en");
            Assert.Equal(
                "Exported 0 machines. Skipped 2: SHOP-PC-BAD: the latest scan has no score to export; SHOP-PC-GOOD: the latest scan has no score to export.",
                noneFrench.StatusMessage);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_EmptyStates_StayHonestInFrench()
    {
        var directory = TempDirectory();
        try
        {
            var databasePath = Path.Combine(directory, "snapshots.db");
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var french = new FleetViewModel(store, null, null, null, "fr");
            await WaitForAsync(
                () => french.StatusMessage != "Chargement du parc...",
                "Status never left loading.");
            Assert.Equal("Aucun scan enregistré pour l'instant. Scannez une machine, ou lancez l'application portable sur chaque machine du parc, et son dernier scan apparaît ici. Vous pouvez aussi importer des rapports JSON exportés depuis d'autres machines avec le bouton Importer.", french.StatusMessage);

            var noStoreFrench = new FleetViewModel(null, null, null, null, "fr");
            await WaitForAsync(
                () => noStoreFrench.StatusMessage != "Chargement du parc...",
                "Status never left loading.");
            Assert.Equal("L'historique des instantanés n'est pas disponible sur cette machine, il n'y a donc pas encore de parc à afficher.", noStoreFrench.StatusMessage);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Fleet_ImportSkippedFile_StatusTranslatesInFrench()
    {
        var directory = TempDirectory();
        try
        {
            var databasePath = Path.Combine(directory, "snapshots.db");
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var missing = Path.Combine(directory, "bad.json");
            var viewModel = new FleetViewModel(store, null, null, null, "fr")
            {
                ReportFilePicker = () => Task.FromResult<IReadOnlyList<string>>(new[] { missing }),
            };
            await WaitForAsync(
                () => viewModel.StatusMessage != "Chargement du parc...",
                "Status never left loading.");
            await viewModel.ImportReportsCommand.ExecuteAsync(null);

            // The head of the line and the skipped count translate;
            // the file name and the read-failure reason stay English,
            // like exception messages elsewhere on the page.
            Assert.Contains("0 rapport importé. Ignoré 1 : bad.json:", viewModel.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }


}
