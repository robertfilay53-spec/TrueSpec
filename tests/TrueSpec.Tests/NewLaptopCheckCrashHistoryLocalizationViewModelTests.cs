using System.Globalization;
using System.Windows.Input;
using TrueSpec.App.ViewModels;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

/// <summary>
/// Localisation, phase 8 (v2.1.16): the New Laptop Check and Crash
/// History pages in German — the page chrome follows the language
/// picked in Settings, in place and with no restart. The banner is
/// rebuilt from the stored result, so its fixed words translate
/// while the finding title it cites stays English. The data that
/// stays English by design: machine names, the buyer summary and the
/// authenticity verdict line, the check titles, details and
/// what-to-ask lines, the authenticity evidence lines and source
/// strings, the crash component headline, the "What this means"
/// fallback, the crash class names, counts, dates and plain meanings,
/// and the "Not reported" date fallback. Transient status lines (the
/// banner, the skipped-checks line, the alert card, the cleared-log
/// warning) are re-derived from stored state, so a switch
/// re-resolves them instead of silently resetting them.
/// Windows leg only (it drives TrueSpec.App's ViewModels), like
/// ReportTestsLocalizationViewModelTests: excluded from the net10.0
/// leg in the csproj. French phase 6 (v2.1.25) mirrors the German
/// New Laptop Check tests in French; Spanish phase 6 (v2.1.32) mirrors
/// the French New Laptop Check tests in Spanish; the Crash History page is not
/// French yet, so only the New Laptop Check half is mirrored here.
/// French phase 7 (v2.1.26) mirrors the German Crash History tests
/// in French too, so both halves are mirrored here now.
/// </summary>
public class NewLaptopCheckCrashHistoryLocalizationViewModelTests
{
    private sealed class TestCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }

    private static NewLaptopCheckViewModel NewLaptopCheck(SystemSnapshot snapshot, string? languageCode = null) =>
        new(snapshot, new TestCommand(), new TestCommand(), languageCode: languageCode);

    private static CrashHistoryViewModel CrashHistory(SystemSnapshot snapshot, string? languageCode = null) =>
        new(snapshot, languageCode);

    private static SystemSnapshot SnapshotWith(params ComponentSnapshot[] components)
    {
        var list = components.ToList();
        return new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Test Laptop",
            "Windows 11 Pro",
            list,
            new ScoringEngine().Score(list),
            new BuyerCheckEvaluator().Evaluate(list, new BuyerCheckContext("16 GB")));
    }

    /// <summary>
    /// A snapshot whose only buyer check is a failed authenticity
    /// finding, so the banner cites it. The empty component list
    /// also leaves six authenticity rules skipped (no Windows
    /// install date was reported), which exercises the skipped line.
    /// </summary>
    private static SystemSnapshot FindingSnapshot()
    {
        var checks = new BuyerCheckResult(new[]
        {
            new BuyerCheck(
                BuyerCheckResult.AuthenticityCheckId,
                "Signs of prior use (BIOS date vs Windows install date)",
                "The BIOS date is before the Windows install date.",
                BuyerCheckState.Fail,
                "Ask the seller why the dates differ."),
        });
        return new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Test Laptop",
            "Windows 11 Pro",
            Array.Empty<ComponentSnapshot>(),
            ScoreResult.Empty,
            checks);
    }

    private static ComponentSnapshot CrashComponent(DateTimeOffset now, bool includeGap)
    {
        var specs = new List<SpecValue>
        {
            new("Blue screens (last 90 days)", "2", "Windows Event Log (System)"),
            new("Blue screens - first recorded", now.AddDays(-40).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Windows Event Log (System)"),
            new("Blue screens - most recent", now.AddDays(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Windows Event Log (System)"),
            new("Fatal hardware errors (last 90 days)", "1", "Windows Event Log (System)"),
            new("Fatal hardware errors - first recorded", now.AddDays(-10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Windows Event Log (System)"),
            new("Fatal hardware errors - most recent", now.AddDays(-3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Windows Event Log (System)"),
            new("Earliest event log entry", now.AddDays(-20).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Windows Event Log (System)"),
        };
        if (includeGap)
        {
            specs.Add(new SpecValue("Event log gap", "Yes", "Windows Event Log (System)"));
        }

        return new ComponentSnapshot(
            "crash",
            "Crash history",
            "Diagnostics",
            "crash",
            "2 blue screens in the last 90 days",
            null,
            specs,
            "Two blue screens were recorded in the last 90 days.",
            now);
    }

    [Fact]
    public void NewLaptopCheck_EnglishDefault_RendersChromeInEnglish()
    {
        var viewModel = NewLaptopCheck(TestSnapshots.Healthy());

        Assert.Equal("NEW LAPTOP CHECK", viewModel.NewLaptopCheckEyebrowText);
        Assert.Equal("PASS - this machine matches a new laptop", viewModel.BannerText);
        Assert.Equal("Authenticity", viewModel.AuthenticityTitleText);
        Assert.Equal("No signs of prior use were found in the age signals that could be checked.", viewModel.NoPriorUseText);
        Assert.Equal("Checklist", viewModel.ChecklistTitleText);
        Assert.Equal("Print checklist", viewModel.PrintChecklistText);
        Assert.Equal("Export Report", viewModel.ExportReportText);
        Assert.Equal("Advertised RAM (optional)", viewModel.AdvertisedRamTitleText);
        Assert.Equal("Used only for the RAM check. It is never treated as a hardware reading.", viewModel.AdvertisedRamNoteText);
        Assert.Equal("e.g. 16 GB", viewModel.AdvertisedRamPlaceholderText);
        Assert.Equal("No buyer checks were produced for this scan.", viewModel.NoChecksText);
        Assert.Equal("Buyer checks use the same honest rules as the rest of the app: measured values only, no invented numbers.", viewModel.FooterNoteText);
        Assert.StartsWith("3 authenticity checks could not run because a signal was not reported: ", viewModel.AuthenticitySkippedText, StringComparison.Ordinal);

        var failedRow = new BuyerCheckRowViewModel(
            new BuyerCheck("x", "Title", "Detail", BuyerCheckState.Fail, "Ask the seller."));
        Assert.Equal("Fail", failedRow.StateText);
        Assert.Equal("What to ask the seller", failedRow.WhatToAskLabelText);

        var strongRow = new AuthenticityCheckRowViewModel(
            new AuthenticityCheck("Title", "Evidence.", AuthenticitySeverity.Strong, "BIOS date", "Windows install date"));
        Assert.Equal("Strong", strongRow.SeverityText);
        Assert.Equal("Source 1: BIOS date", strongRow.SourceAText);
        Assert.Equal("Source 2: Windows install date", strongRow.SourceBText);
    }

    [Fact]
    public void NewLaptopCheck_GermanSeeded_RendersWholePageInGerman()
    {
        var viewModel = NewLaptopCheck(TestSnapshots.Healthy(), "de");

        Assert.Equal("NEUER LAPTOP CHECK", viewModel.NewLaptopCheckEyebrowText);
        Assert.Equal("BESTANDEN - Dieses Gerät entspricht einem neuen Laptop", viewModel.BannerText);
        Assert.Equal("Authentizität", viewModel.AuthenticityTitleText);
        Assert.Equal("In den prübaren Alterssignalen wurden keine Anzeichen für eine frühere Nutzung gefunden.", viewModel.NoPriorUseText);
        Assert.Equal("Checkliste", viewModel.ChecklistTitleText);
        Assert.Equal("Checkliste drucken", viewModel.PrintChecklistText);
        Assert.Equal("Bericht exportieren", viewModel.ExportReportText);
        Assert.Equal("Angegebener RAM (optional)", viewModel.AdvertisedRamTitleText);
        Assert.Equal("Wird nur für die RAM-Prüfung verwendet. Es wird niemals als Hardwaremesswert behandelt.", viewModel.AdvertisedRamNoteText);
        Assert.Equal("z. B. 16 GB", viewModel.AdvertisedRamPlaceholderText);
        Assert.Equal("Für diesen Scan wurden keine Käuferprüfungen erstellt.", viewModel.NoChecksText);
        Assert.Equal("Für Käuferprüfungen gelten dieselben ehrlichen Regeln wie für den Rest der App: nur gemessene Werte, keine erfundenen Zahlen.", viewModel.FooterNoteText);
        Assert.StartsWith("3 Authentizitätsprüfungen konnten nicht ausgeführt werden, weil ein Signal nicht gemeldet wurde: ", viewModel.AuthenticitySkippedText, StringComparison.Ordinal);

        var failedRow = new BuyerCheckRowViewModel(
            new BuyerCheck("x", "Title", "Detail", BuyerCheckState.Fail, "Ask the seller."),
            "de");
        Assert.Equal("Fehlgeschlagen", failedRow.StateText);
        Assert.Equal("Was Sie den Verkäufer fragen sollten", failedRow.WhatToAskLabelText);

        var strongRow = new AuthenticityCheckRowViewModel(
            new AuthenticityCheck("Title", "Evidence.", AuthenticitySeverity.Strong, "BIOS date", "Windows install date"),
            "de");
        Assert.Equal("Stark", strongRow.SeverityText);
        Assert.Equal("Quelle 1: BIOS date", strongRow.SourceAText);
        Assert.Equal("Quelle 2: Windows install date", strongRow.SourceBText);
    }

    [Fact]
    public void NewLaptopCheck_ApplyLanguage_SwitchesBothWays()
    {
        var viewModel = NewLaptopCheck(TestSnapshots.Healthy());
        var failedRow = new BuyerCheckRowViewModel(
            new BuyerCheck("x", "Title", "Detail", BuyerCheckState.Fail, "Ask the seller."));

        viewModel.ApplyLanguage("de");
        failedRow.ApplyLanguage("de");
        Assert.Equal("BESTANDEN - Dieses Gerät entspricht einem neuen Laptop", viewModel.BannerText);
        Assert.Equal("Authentizität", viewModel.AuthenticityTitleText);
        Assert.Equal("Checkliste drucken", viewModel.PrintChecklistText);
        Assert.Equal("Bericht exportieren", viewModel.ExportReportText);
        Assert.Equal("Fehlgeschlagen", failedRow.StateText);
        Assert.Equal("Was Sie den Verkäufer fragen sollten", failedRow.WhatToAskLabelText);

        viewModel.ApplyLanguage("en");
        failedRow.ApplyLanguage("en");
        Assert.Equal("PASS - this machine matches a new laptop", viewModel.BannerText);
        Assert.Equal("Authenticity", viewModel.AuthenticityTitleText);
        Assert.Equal("Print checklist", viewModel.PrintChecklistText);
        Assert.Equal("Export Report", viewModel.ExportReportText);
        Assert.Equal("Fail", failedRow.StateText);
        Assert.Equal("What to ask the seller", failedRow.WhatToAskLabelText);

        viewModel.ApplyLanguage("de");
        Assert.Equal("BESTANDEN - Dieses Gerät entspricht einem neuen Laptop", viewModel.BannerText);
    }

    [Fact]
    public void NewLaptopCheck_UnknownLanguage_FallsBackToEnglish()
    {
        var viewModel = NewLaptopCheck(TestSnapshots.Healthy(), "xx");

        Assert.Equal("NEW LAPTOP CHECK", viewModel.NewLaptopCheckEyebrowText);
        Assert.Equal("PASS - this machine matches a new laptop", viewModel.BannerText);
        Assert.Equal("Authenticity", viewModel.AuthenticityTitleText);
        Assert.Equal("Print checklist", viewModel.PrintChecklistText);
        Assert.Equal("Export Report", viewModel.ExportReportText);
    }

    [Fact]
    public void NewLaptopCheck_FindingBanner_KeepsFindingTitleEnglish()
    {
        var english = NewLaptopCheck(FindingSnapshot());
        var german = NewLaptopCheck(FindingSnapshot(), "de");

        // The finding title is data, so it stays English inside the
        // translated banner; only the state word translates.
        Assert.Equal(BuyerCheckState.Warning, english.OverallState);
        Assert.Equal("WARNING - Signs of prior use (BIOS date vs Windows install date)", english.BannerText);
        Assert.Equal("WARNUNG - Signs of prior use (BIOS date vs Windows install date)", german.BannerText);

        // Six authenticity rules could not run on the empty snapshot:
        // the skipped line translates its words, not the signal names.
        // These assertions run before the ApplyLanguage("en") below:
        // after the switch the German VM's skipped line is English.
        Assert.Equal(6, english.Authenticity.SkippedChecks.Count);
        Assert.StartsWith("6 authenticity checks could not run because a signal was not reported: ", english.AuthenticitySkippedText, StringComparison.Ordinal);
        Assert.StartsWith("6 Authentizitätsprüfungen konnten nicht ausgeführt werden, weil ein Signal nicht gemeldet wurde: ", german.AuthenticitySkippedText, StringComparison.Ordinal);

        german.ApplyLanguage("en");
        Assert.Equal("WARNING - Signs of prior use (BIOS date vs Windows install date)", german.BannerText);
    }

    [Fact]
    public void NewLaptopCheck_BannerFinding_MatchesCoreRanking()
    {
        // Localisation polish (v2.1.18): the page shares Core's
        // ranking instead of mirroring it, so the banner can never
        // name a different finding than Core's own banner.
        var allFailing = new BuyerCheckResult(new[]
        {
            new BuyerCheck(BuyerCheckResult.LoadTestCheckId, "Load test finding", "Detail.", BuyerCheckState.Fail, "Ask."),
            new BuyerCheck(BuyerCheckResult.RecentCrashesCheckId, "Recent crashes finding", "Detail.", BuyerCheckState.Fail, "Ask."),
            new BuyerCheck(BuyerCheckResult.AuthenticityCheckId, "Authenticity finding", "Detail.", BuyerCheckState.Fail, "Ask."),
        });
        Assert.Equal(BuyerCheckResult.AuthenticityCheckId, allFailing.StrongestFailingFinding()?.Id);

        var crashesOnly = new BuyerCheckResult(new[]
        {
            new BuyerCheck(BuyerCheckResult.LoadTestCheckId, "Load test finding", "Detail.", BuyerCheckState.Fail, "Ask."),
            new BuyerCheck(BuyerCheckResult.RecentCrashesCheckId, "Recent crashes finding", "Detail.", BuyerCheckState.Fail, "Ask."),
        });
        Assert.Equal(BuyerCheckResult.RecentCrashesCheckId, crashesOnly.StrongestFailingFinding()?.Id);

        var loadOnly = new BuyerCheckResult(new[]
        {
            new BuyerCheck(BuyerCheckResult.LoadTestCheckId, "Load test finding", "Detail.", BuyerCheckState.Fail, "Ask."),
        });
        Assert.Equal(BuyerCheckResult.LoadTestCheckId, loadOnly.StrongestFailingFinding()?.Id);

        var noneFailing = new BuyerCheckResult(new[]
        {
            new BuyerCheck(BuyerCheckResult.AuthenticityCheckId, "Authenticity finding", "Detail.", BuyerCheckState.Pass, "Ask."),
        });
        Assert.Null(noneFailing.StrongestFailingFinding());

        // The page banner names the same finding Core's banner names.
        var snapshot = new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Test Laptop",
            "Windows 11 Pro",
            Array.Empty<ComponentSnapshot>(),
            ScoreResult.Empty,
            allFailing);
        var viewModel = NewLaptopCheck(snapshot);
        Assert.Equal(allFailing.BannerText, viewModel.BannerText);
        Assert.Contains("Authenticity finding", viewModel.BannerText, StringComparison.Ordinal);
    }

    [Fact]
    public void NewLaptopCheck_DataStaysEnglish_WhenChromeIsGerman()
    {
        var english = NewLaptopCheck(TestSnapshots.Healthy());
        var german = NewLaptopCheck(TestSnapshots.Healthy(), "de");

        Assert.Equal(english.MachineName, german.MachineName);
        Assert.Equal(english.Summary, german.Summary);
        Assert.Equal(english.AuthenticitySummary, german.AuthenticitySummary);
        Assert.Equal(english.Checks.Select(c => c.Title), german.Checks.Select(c => c.Title));
        Assert.Equal(english.Checks.Select(c => c.Detail), german.Checks.Select(c => c.Detail));
        Assert.Equal(english.Checks.Select(c => c.WhatToAsk), german.Checks.Select(c => c.WhatToAsk));
        Assert.Equal(
            english.AuthenticityChecks.Select(c => c.EvidenceLine),
            german.AuthenticityChecks.Select(c => c.EvidenceLine));
        Assert.Equal(
            english.AuthenticityChecks.Select(c => c.SourceA),
            german.AuthenticityChecks.Select(c => c.SourceA));
        Assert.Equal(
            english.AuthenticityChecks.Select(c => c.SourceB),
            german.AuthenticityChecks.Select(c => c.SourceB));

        // A switch re-renders the chrome in place, keeping the data.
        german.ApplyLanguage("en");
        Assert.Equal(english.NewLaptopCheckEyebrowText, german.NewLaptopCheckEyebrowText);
        Assert.Equal(english.BannerText, german.BannerText);
        Assert.Equal(english.MachineName, german.MachineName);
        Assert.Equal(english.Summary, german.Summary);
    }

    [Fact]
    public void NewLaptopCheck_FrenchSeeded_RendersWholePageInFrench()
    {
        var viewModel = NewLaptopCheck(TestSnapshots.Healthy(), "fr");

        Assert.Equal("VÉRIFICATION D'ORDINATEUR PORTABLE NEUF", viewModel.NewLaptopCheckEyebrowText);
        Assert.Equal("RÉUSSI - cette machine correspond à un ordinateur portable neuf", viewModel.BannerText);
        Assert.Equal("Authenticité", viewModel.AuthenticityTitleText);
        Assert.Equal("Aucun signe d'utilisation antérieure n'a été trouvé dans les signaux d'âge qui ont pu être vérifiés.", viewModel.NoPriorUseText);
        Assert.Equal("Liste de contrôle", viewModel.ChecklistTitleText);
        Assert.Equal("Imprimer la liste de contrôle", viewModel.PrintChecklistText);
        Assert.Equal("Exporter le rapport", viewModel.ExportReportText);
        Assert.Equal("RAM annoncée (facultatif)", viewModel.AdvertisedRamTitleText);
        Assert.Equal("Utilisée uniquement pour le contrôle de la RAM. Elle n'est jamais traitée comme une mesure matérielle.", viewModel.AdvertisedRamNoteText);
        Assert.Equal("p. ex. 16 Go", viewModel.AdvertisedRamPlaceholderText);
        Assert.Equal("Aucun contrôle d'acheteur n'a été produit pour ce scan.", viewModel.NoChecksText);
        Assert.Equal("Les contrôles d'acheteur suivent les mêmes règles d'honnêteté que le reste de l'application : uniquement des valeurs mesurées, aucun nombre inventé.", viewModel.FooterNoteText);
        Assert.StartsWith("3 contrôles d'authenticité n'ont pas pu être exécutés car un signal n'a pas été signalé : ", viewModel.AuthenticitySkippedText, StringComparison.Ordinal);

        var failedRow = new BuyerCheckRowViewModel(
            new BuyerCheck("x", "Title", "Detail", BuyerCheckState.Fail, "Ask the seller."),
            "fr");
        Assert.Equal("Échec", failedRow.StateText);
        Assert.Equal("Quoi demander au vendeur", failedRow.WhatToAskLabelText);

        var strongRow = new AuthenticityCheckRowViewModel(
            new AuthenticityCheck("Title", "Evidence.", AuthenticitySeverity.Strong, "BIOS date", "Windows install date"),
            "fr");
        Assert.Equal("Fort", strongRow.SeverityText);
        Assert.Equal("Source 1 : BIOS date", strongRow.SourceAText);
        Assert.Equal("Source 2 : Windows install date", strongRow.SourceBText);
    }

    [Fact]
    public void NewLaptopCheck_ApplyLanguage_SwitchesToFrenchAndBack()
    {
        var viewModel = NewLaptopCheck(TestSnapshots.Healthy());
        var failedRow = new BuyerCheckRowViewModel(
            new BuyerCheck("x", "Title", "Detail", BuyerCheckState.Fail, "Ask the seller."));

        viewModel.ApplyLanguage("fr");
        failedRow.ApplyLanguage("fr");
        Assert.Equal("RÉUSSI - cette machine correspond à un ordinateur portable neuf", viewModel.BannerText);
        Assert.Equal("Authenticité", viewModel.AuthenticityTitleText);
        Assert.Equal("Imprimer la liste de contrôle", viewModel.PrintChecklistText);
        Assert.Equal("Exporter le rapport", viewModel.ExportReportText);
        Assert.Equal("Échec", failedRow.StateText);
        Assert.Equal("Quoi demander au vendeur", failedRow.WhatToAskLabelText);

        viewModel.ApplyLanguage("en");
        failedRow.ApplyLanguage("en");
        Assert.Equal("PASS - this machine matches a new laptop", viewModel.BannerText);
        Assert.Equal("Authenticity", viewModel.AuthenticityTitleText);
        Assert.Equal("Print checklist", viewModel.PrintChecklistText);
        Assert.Equal("Export Report", viewModel.ExportReportText);
        Assert.Equal("Fail", failedRow.StateText);
        Assert.Equal("What to ask the seller", failedRow.WhatToAskLabelText);

        viewModel.ApplyLanguage("fr");
        Assert.Equal("RÉUSSI - cette machine correspond à un ordinateur portable neuf", viewModel.BannerText);
    }

    [Fact]
    public void NewLaptopCheck_FindingBanner_KeepsFindingTitleEnglishInFrench()
    {
        var english = NewLaptopCheck(FindingSnapshot());
        var french = NewLaptopCheck(FindingSnapshot(), "fr");

        // The finding title is data, so it stays English inside the
        // translated banner; only the state word translates.
        Assert.Equal(BuyerCheckState.Warning, english.OverallState);
        Assert.Equal("WARNING - Signs of prior use (BIOS date vs Windows install date)", english.BannerText);
        Assert.Equal("ATTENTION - Signs of prior use (BIOS date vs Windows install date)", french.BannerText);

        // Six authenticity rules could not run on the empty snapshot:
        // the skipped line translates its words, not the signal names.
        // These assertions run before the ApplyLanguage("en") below:
        // after the switch the French VM's skipped line is English.
        Assert.Equal(6, english.Authenticity.SkippedChecks.Count);
        Assert.StartsWith("6 authenticity checks could not run because a signal was not reported: ", english.AuthenticitySkippedText, StringComparison.Ordinal);
        Assert.StartsWith("6 contrôles d'authenticité n'ont pas pu être exécutés car un signal n'a pas été signalé : ", french.AuthenticitySkippedText, StringComparison.Ordinal);

        french.ApplyLanguage("en");
        Assert.Equal("WARNING - Signs of prior use (BIOS date vs Windows install date)", french.BannerText);
    }

    [Fact]
    public void NewLaptopCheck_DataStaysEnglish_WhenChromeIsFrench()
    {
        var english = NewLaptopCheck(TestSnapshots.Healthy());
        var french = NewLaptopCheck(TestSnapshots.Healthy(), "fr");

        Assert.Equal(english.MachineName, french.MachineName);
        Assert.Equal(english.Summary, french.Summary);
        Assert.Equal(english.AuthenticitySummary, french.AuthenticitySummary);
        Assert.Equal(english.Checks.Select(c => c.Title), french.Checks.Select(c => c.Title));
        Assert.Equal(english.Checks.Select(c => c.Detail), french.Checks.Select(c => c.Detail));
        Assert.Equal(english.Checks.Select(c => c.WhatToAsk), french.Checks.Select(c => c.WhatToAsk));
        Assert.Equal(
            english.AuthenticityChecks.Select(c => c.EvidenceLine),
            french.AuthenticityChecks.Select(c => c.EvidenceLine));
        Assert.Equal(
            english.AuthenticityChecks.Select(c => c.SourceA),
            french.AuthenticityChecks.Select(c => c.SourceA));
        Assert.Equal(
            english.AuthenticityChecks.Select(c => c.SourceB),
            french.AuthenticityChecks.Select(c => c.SourceB));

        // A switch re-renders the chrome in place, keeping the data.
        french.ApplyLanguage("en");
        Assert.Equal(english.NewLaptopCheckEyebrowText, french.NewLaptopCheckEyebrowText);
        Assert.Equal(english.BannerText, french.BannerText);
        Assert.Equal(english.MachineName, french.MachineName);
        Assert.Equal(english.Summary, french.Summary);
    }

    [Fact]
    public void CrashHistory_EnglishDefault_RendersChromeInEnglish()
    {
        var now = DateTimeOffset.UtcNow;
        var viewModel = CrashHistory(SnapshotWith(CrashComponent(now, includeGap: true)));

        Assert.Equal("Crash history", viewModel.CrashTitleText);
        Assert.Equal("2 blue screens in the last 90 days", viewModel.Headline);
        Assert.Equal(
            $"Reading captured {now:yyyy-MM-dd HH:mm:ss}",
            viewModel.CapturedText);
        Assert.Equal("Recent crash", viewModel.RecentCrashTitleText);
        Assert.True(viewModel.HasRecentAlert);
        Assert.Equal("A blue screen and a fatal hardware error occurred in the last 30 days.", viewModel.AlertText);
        Assert.Equal("Failures by class", viewModel.FailuresByClassText);
        Assert.Equal("Class", viewModel.HeaderClassText);
        Assert.Equal("Count (90 days)", viewModel.HeaderCountText);
        Assert.Equal("First", viewModel.HeaderFirstText);
        Assert.Equal("Last", viewModel.HeaderLastText);
        Assert.Equal("Meaning", viewModel.HeaderMeaningText);
        Assert.Equal(
            $"The event log has no entries before {now.AddDays(-20):yyyy-MM-dd}, so this history may be incomplete. A cleared log reads as a gap, never as a clean record.",
            viewModel.LogClearedText);
        Assert.Equal("Counts cover the last 90 days; first and last cover all recorded time. Meanings are fixed labels, not diagnoses.", viewModel.FooterNoteText);

        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Equal("Blue screens", viewModel.Rows[0].ClassName);
        Assert.Equal(2, viewModel.Rows[0].Count90d);
        Assert.Equal("Fatal hardware errors", viewModel.Rows[1].ClassName);
        Assert.Equal(1, viewModel.Rows[1].Count90d);
    }

    [Fact]
    public void CrashHistory_GermanSeeded_RendersWholePageInGerman()
    {
        var now = DateTimeOffset.UtcNow;
        var viewModel = CrashHistory(SnapshotWith(CrashComponent(now, includeGap: true)), "de");

        Assert.Equal("Absturzverlauf", viewModel.CrashTitleText);
        Assert.Equal("2 blue screens in the last 90 days", viewModel.Headline);
        Assert.Equal(
            $"Messwert erfasst {now:yyyy-MM-dd HH:mm:ss}",
            viewModel.CapturedText);
        Assert.Equal("Aktueller Absturz", viewModel.RecentCrashTitleText);
        Assert.True(viewModel.HasRecentAlert);
        Assert.Equal("In den letzten 30 Tagen gab es einen Bluescreen und einen schwerwiegenden Hardwarefehler.", viewModel.AlertText);
        Assert.Equal("Fehler nach Klasse", viewModel.FailuresByClassText);
        Assert.Equal("Klasse", viewModel.HeaderClassText);
        Assert.Equal("Anzahl (90 Tage)", viewModel.HeaderCountText);
        Assert.Equal("Erste", viewModel.HeaderFirstText);
        Assert.Equal("Letzte", viewModel.HeaderLastText);
        Assert.Equal("Bedeutung", viewModel.HeaderMeaningText);
        Assert.Equal(
            $"Das Ereignisprotokoll hat keine Einträge vor dem {now.AddDays(-20):yyyy-MM-dd}, daher ist dieser Verlauf möglicherweise unvollständig. Ein gelöschtes Protokoll gilt als Lücke, niemals als sauberer Verlauf.",
            viewModel.LogClearedText);
        Assert.Equal("Die Anzahl deckt die letzten 90 Tage ab; erste und letzte decken den gesamten aufgezeichneten Zeitraum ab. Bedeutungen sind feste Bezeichnungen, keine Diagnosen.", viewModel.FooterNoteText);

        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Equal("Blue screens", viewModel.Rows[0].ClassName);
        Assert.Equal(2, viewModel.Rows[0].Count90d);
        Assert.Equal("Fatal hardware errors", viewModel.Rows[1].ClassName);
        Assert.Equal(1, viewModel.Rows[1].Count90d);
    }

    [Fact]
    public void CrashHistory_ApplyLanguage_SwitchesBothWays()
    {
        var now = DateTimeOffset.UtcNow;
        var viewModel = CrashHistory(SnapshotWith(CrashComponent(now, includeGap: true)));

        viewModel.ApplyLanguage("de");
        Assert.Equal("Absturzverlauf", viewModel.CrashTitleText);
        Assert.Equal("In den letzten 30 Tagen gab es einen Bluescreen und einen schwerwiegenden Hardwarefehler.", viewModel.AlertText);
        Assert.Equal("Fehler nach Klasse", viewModel.FailuresByClassText);
        Assert.Equal("Anzahl (90 Tage)", viewModel.HeaderCountText);
        Assert.Equal(
            $"Das Ereignisprotokoll hat keine Einträge vor dem {now.AddDays(-20):yyyy-MM-dd}, daher ist dieser Verlauf möglicherweise unvollständig. Ein gelöschtes Protokoll gilt als Lücke, niemals als sauberer Verlauf.",
            viewModel.LogClearedText);

        viewModel.ApplyLanguage("en");
        Assert.Equal("Crash history", viewModel.CrashTitleText);
        Assert.Equal("A blue screen and a fatal hardware error occurred in the last 30 days.", viewModel.AlertText);
        Assert.Equal("Failures by class", viewModel.FailuresByClassText);
        Assert.Equal("Count (90 days)", viewModel.HeaderCountText);
        Assert.Equal(
            $"The event log has no entries before {now.AddDays(-20):yyyy-MM-dd}, so this history may be incomplete. A cleared log reads as a gap, never as a clean record.",
            viewModel.LogClearedText);

        viewModel.ApplyLanguage("de");
        Assert.Equal("Absturzverlauf", viewModel.CrashTitleText);
    }

    [Fact]
    public void CrashHistory_UnknownLanguage_FallsBackToEnglish()
    {
        var now = DateTimeOffset.UtcNow;
        var viewModel = CrashHistory(SnapshotWith(CrashComponent(now, includeGap: true)), "xx");

        Assert.Equal("Crash history", viewModel.CrashTitleText);
        Assert.Equal("A blue screen and a fatal hardware error occurred in the last 30 days.", viewModel.AlertText);
        Assert.Equal("Failures by class", viewModel.FailuresByClassText);
        Assert.Equal("Count (90 days)", viewModel.HeaderCountText);
    }

    [Fact]
    public void CrashHistory_DataStaysEnglish_WhenChromeIsGerman()
    {
        var now = DateTimeOffset.UtcNow;
        var english = CrashHistory(SnapshotWith(CrashComponent(now, includeGap: true)));
        var german = CrashHistory(SnapshotWith(CrashComponent(now, includeGap: true)), "de");

        Assert.Equal(english.Headline, german.Headline);
        Assert.Equal(english.Rows.Select(r => r.ClassName), german.Rows.Select(r => r.ClassName));
        Assert.Equal(english.Rows.Select(r => r.Count90d), german.Rows.Select(r => r.Count90d));
        Assert.Equal(english.Rows.Select(r => r.FirstText), german.Rows.Select(r => r.FirstText));
        Assert.Equal(english.Rows.Select(r => r.LastText), german.Rows.Select(r => r.LastText));
        Assert.Equal(english.Rows.Select(r => r.Meaning), german.Rows.Select(r => r.Meaning));

        // The date inside the captured line and the cleared-log
        // warning is the same in both languages; only the words
        // around it translate.
        Assert.Contains($"{now:yyyy-MM-dd HH:mm:ss}", german.CapturedText, StringComparison.Ordinal);
        Assert.Contains($"{now.AddDays(-20):yyyy-MM-dd}", german.LogClearedText, StringComparison.Ordinal);

        // A switch re-renders the chrome in place, keeping the data.
        german.ApplyLanguage("en");
        Assert.Equal(english.CrashTitleText, german.CrashTitleText);
        Assert.Equal(english.Headline, german.Headline);
        Assert.Equal(english.Rows.Select(r => r.Meaning), german.Rows.Select(r => r.Meaning));
    }

    [Fact]
    public void NewLaptopCheck_SpanishSeeded_RendersWholePageInSpanish()
    {
        var viewModel = NewLaptopCheck(TestSnapshots.Healthy(), "es");

        Assert.Equal("COMPROBACIÓN DE PORTÁTIL NUEVO", viewModel.NewLaptopCheckEyebrowText);
        Assert.Equal("APROBADO: este equipo coincide con un portátil nuevo", viewModel.BannerText);
        Assert.Equal("Autenticidad", viewModel.AuthenticityTitleText);
        Assert.Equal("No se encontraron indicios de uso previo en las señales de antigüedad que se pudieron comprobar.", viewModel.NoPriorUseText);
        Assert.Equal("Lista de verificación", viewModel.ChecklistTitleText);
        Assert.Equal("Imprimir lista de verificación", viewModel.PrintChecklistText);
        Assert.Equal("Exportar informe", viewModel.ExportReportText);
        Assert.Equal("RAM anunciada (opcional)", viewModel.AdvertisedRamTitleText);
        Assert.Equal("Solo se usa para la comprobación de RAM. Nunca se trata como una lectura de hardware.", viewModel.AdvertisedRamNoteText);
        Assert.Equal("p. ej., 16 GB", viewModel.AdvertisedRamPlaceholderText);
        Assert.Equal("No se produjeron comprobaciones de comprador para este análisis.", viewModel.NoChecksText);
        Assert.Equal("Las comprobaciones de comprador usan las mismas reglas honestas que el resto de la aplicación: solo valores medidos, sin números inventados.", viewModel.FooterNoteText);
        Assert.StartsWith("3 comprobaciones de autenticidad no se pudieron ejecutar porque no se informó de una señal: ", viewModel.AuthenticitySkippedText, StringComparison.Ordinal);

        var failedRow = new BuyerCheckRowViewModel(
            new BuyerCheck("x", "Title", "Detail", BuyerCheckState.Fail, "Ask the seller."),
            "es");
        Assert.Equal("Suspenso", failedRow.StateText);
        Assert.Equal("Qué preguntar al vendedor", failedRow.WhatToAskLabelText);

        var strongRow = new AuthenticityCheckRowViewModel(
            new AuthenticityCheck("Title", "Evidence.", AuthenticitySeverity.Strong, "BIOS date", "Windows install date"),
            "es");
        Assert.Equal("Fuerte", strongRow.SeverityText);
        Assert.Equal("Fuente 1: BIOS date", strongRow.SourceAText);
        Assert.Equal("Fuente 2: Windows install date", strongRow.SourceBText);
    }

    [Fact]
    public void NewLaptopCheck_ApplyLanguage_SwitchesToSpanishAndBack()
    {
        var viewModel = NewLaptopCheck(TestSnapshots.Healthy());
        var failedRow = new BuyerCheckRowViewModel(
            new BuyerCheck("x", "Title", "Detail", BuyerCheckState.Fail, "Ask the seller."));

        viewModel.ApplyLanguage("es");
        failedRow.ApplyLanguage("es");
        Assert.Equal("APROBADO: este equipo coincide con un portátil nuevo", viewModel.BannerText);
        Assert.Equal("Autenticidad", viewModel.AuthenticityTitleText);
        Assert.Equal("Imprimir lista de verificación", viewModel.PrintChecklistText);
        Assert.Equal("Exportar informe", viewModel.ExportReportText);
        Assert.Equal("Suspenso", failedRow.StateText);
        Assert.Equal("Qué preguntar al vendedor", failedRow.WhatToAskLabelText);

        viewModel.ApplyLanguage("en");
        failedRow.ApplyLanguage("en");
        Assert.Equal("PASS - this machine matches a new laptop", viewModel.BannerText);
        Assert.Equal("Authenticity", viewModel.AuthenticityTitleText);
        Assert.Equal("Print checklist", viewModel.PrintChecklistText);
        Assert.Equal("Export Report", viewModel.ExportReportText);
        Assert.Equal("Fail", failedRow.StateText);
        Assert.Equal("What to ask the seller", failedRow.WhatToAskLabelText);

        viewModel.ApplyLanguage("es");
        Assert.Equal("APROBADO: este equipo coincide con un portátil nuevo", viewModel.BannerText);
    }

    [Fact]
    public void NewLaptopCheck_FindingBanner_KeepsFindingTitleEnglishInSpanish()
    {
        var english = NewLaptopCheck(FindingSnapshot());
        var spanish = NewLaptopCheck(FindingSnapshot(), "es");

        // The finding title is data, so it stays English inside the
        // translated banner; only the state word translates.
        Assert.Equal(BuyerCheckState.Warning, english.OverallState);
        Assert.Equal("WARNING - Signs of prior use (BIOS date vs Windows install date)", english.BannerText);
        Assert.Equal("ADVERTENCIA - Signs of prior use (BIOS date vs Windows install date)", spanish.BannerText);

        // Six authenticity rules could not run on the empty snapshot:
        // the skipped line translates its words, not the signal names.
        // These assertions run before the ApplyLanguage("en") below:
        // after the switch the Spanish VM's skipped line is English.
        Assert.Equal(6, english.Authenticity.SkippedChecks.Count);
        Assert.StartsWith("6 authenticity checks could not run because a signal was not reported: ", english.AuthenticitySkippedText, StringComparison.Ordinal);
        Assert.StartsWith("6 comprobaciones de autenticidad no se pudieron ejecutar porque no se informó de una señal: ", spanish.AuthenticitySkippedText, StringComparison.Ordinal);

        spanish.ApplyLanguage("en");
        Assert.Equal("WARNING - Signs of prior use (BIOS date vs Windows install date)", spanish.BannerText);
    }

    [Fact]
    public void NewLaptopCheck_DataStaysEnglish_WhenChromeIsSpanish()
    {
        var english = NewLaptopCheck(TestSnapshots.Healthy());
        var spanish = NewLaptopCheck(TestSnapshots.Healthy(), "es");

        Assert.Equal(english.MachineName, spanish.MachineName);
        Assert.Equal(english.Summary, spanish.Summary);
        Assert.Equal(english.AuthenticitySummary, spanish.AuthenticitySummary);
        Assert.Equal(english.Checks.Select(c => c.Title), spanish.Checks.Select(c => c.Title));
        Assert.Equal(english.Checks.Select(c => c.Detail), spanish.Checks.Select(c => c.Detail));
        Assert.Equal(english.Checks.Select(c => c.WhatToAsk), spanish.Checks.Select(c => c.WhatToAsk));
        Assert.Equal(
            english.AuthenticityChecks.Select(c => c.EvidenceLine),
            spanish.AuthenticityChecks.Select(c => c.EvidenceLine));
        Assert.Equal(
            english.AuthenticityChecks.Select(c => c.SourceA),
            spanish.AuthenticityChecks.Select(c => c.SourceA));
        Assert.Equal(
            english.AuthenticityChecks.Select(c => c.SourceB),
            spanish.AuthenticityChecks.Select(c => c.SourceB));

        // A switch re-renders the chrome in place, keeping the data.
        spanish.ApplyLanguage("en");
        Assert.Equal(english.NewLaptopCheckEyebrowText, spanish.NewLaptopCheckEyebrowText);
        Assert.Equal(english.BannerText, spanish.BannerText);
        Assert.Equal(english.MachineName, spanish.MachineName);
        Assert.Equal(english.Summary, spanish.Summary);
    }


    [Fact]
    public void CrashHistory_FrenchSeeded_RendersWholePageInFrench()
    {
        var now = DateTimeOffset.UtcNow;
        var viewModel = CrashHistory(SnapshotWith(CrashComponent(now, includeGap: true)), "fr");

        Assert.Equal("Historique des incidents", viewModel.CrashTitleText);
        Assert.Equal("2 blue screens in the last 90 days", viewModel.Headline);
        Assert.Equal(
            $"Mesure enregistrée le {now:yyyy-MM-dd HH:mm:ss}",
            viewModel.CapturedText);
        Assert.Equal("Incident récent", viewModel.RecentCrashTitleText);
        Assert.True(viewModel.HasRecentAlert);
        Assert.Equal("Un écran bleu et une erreur matérielle fatale se sont produits au cours des 30 derniers jours.", viewModel.AlertText);
        Assert.Equal("Échecs par classe", viewModel.FailuresByClassText);
        Assert.Equal("Classe", viewModel.HeaderClassText);
        Assert.Equal("Nombre (90 jours)", viewModel.HeaderCountText);
        Assert.Equal("Premier", viewModel.HeaderFirstText);
        Assert.Equal("Dernier", viewModel.HeaderLastText);
        Assert.Equal("Signification", viewModel.HeaderMeaningText);
        Assert.Equal(
            $"Le journal des événements ne contient aucune entrée avant le {now.AddDays(-20):yyyy-MM-dd}, cet historique peut donc être incomplet. Un journal effacé se lit comme un trou, jamais comme un dossier vierge.",
            viewModel.LogClearedText);
        Assert.Equal("Les nombres couvrent les 90 derniers jours ; le premier et le dernier couvrent toute la période enregistrée. Les significations sont des libellés fixes, pas des diagnostics.", viewModel.FooterNoteText);

        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Equal("Blue screens", viewModel.Rows[0].ClassName);
        Assert.Equal(2, viewModel.Rows[0].Count90d);
        Assert.Equal("Fatal hardware errors", viewModel.Rows[1].ClassName);
        Assert.Equal(1, viewModel.Rows[1].Count90d);
    }

    [Fact]
    public void CrashHistory_ApplyLanguage_SwitchesToFrenchAndBack()
    {
        var now = DateTimeOffset.UtcNow;
        var viewModel = CrashHistory(SnapshotWith(CrashComponent(now, includeGap: true)));

        viewModel.ApplyLanguage("fr");
        Assert.Equal("Historique des incidents", viewModel.CrashTitleText);
        Assert.Equal("Un écran bleu et une erreur matérielle fatale se sont produits au cours des 30 derniers jours.", viewModel.AlertText);
        Assert.Equal("Échecs par classe", viewModel.FailuresByClassText);
        Assert.Equal("Nombre (90 jours)", viewModel.HeaderCountText);
        Assert.Equal(
            $"Le journal des événements ne contient aucune entrée avant le {now.AddDays(-20):yyyy-MM-dd}, cet historique peut donc être incomplet. Un journal effacé se lit comme un trou, jamais comme un dossier vierge.",
            viewModel.LogClearedText);

        viewModel.ApplyLanguage("en");
        Assert.Equal("Crash history", viewModel.CrashTitleText);
        Assert.Equal("A blue screen and a fatal hardware error occurred in the last 30 days.", viewModel.AlertText);
        Assert.Equal("Failures by class", viewModel.FailuresByClassText);
        Assert.Equal("Count (90 days)", viewModel.HeaderCountText);
        Assert.Equal(
            $"The event log has no entries before {now.AddDays(-20):yyyy-MM-dd}, so this history may be incomplete. A cleared log reads as a gap, never as a clean record.",
            viewModel.LogClearedText);

        viewModel.ApplyLanguage("fr");
        Assert.Equal("Historique des incidents", viewModel.CrashTitleText);
    }

    [Fact]
    public void CrashHistory_DataStaysEnglish_WhenChromeIsFrench()
    {
        var now = DateTimeOffset.UtcNow;
        var english = CrashHistory(SnapshotWith(CrashComponent(now, includeGap: true)));
        var french = CrashHistory(SnapshotWith(CrashComponent(now, includeGap: true)), "fr");

        Assert.Equal(english.Headline, french.Headline);
        Assert.Equal(english.Rows.Select(r => r.ClassName), french.Rows.Select(r => r.ClassName));
        Assert.Equal(english.Rows.Select(r => r.Count90d), french.Rows.Select(r => r.Count90d));
        Assert.Equal(english.Rows.Select(r => r.FirstText), french.Rows.Select(r => r.FirstText));
        Assert.Equal(english.Rows.Select(r => r.LastText), french.Rows.Select(r => r.LastText));
        Assert.Equal(english.Rows.Select(r => r.Meaning), french.Rows.Select(r => r.Meaning));

        // The date inside the captured line and the cleared-log
        // warning is the same in both languages; only the words
        // around it translate.
        Assert.Contains($"{now:yyyy-MM-dd HH:mm:ss}", french.CapturedText, StringComparison.Ordinal);
        Assert.Contains($"{now.AddDays(-20):yyyy-MM-dd}", french.LogClearedText, StringComparison.Ordinal);

        // A switch re-renders the chrome in place, keeping the data.
        french.ApplyLanguage("en");
        Assert.Equal(english.CrashTitleText, french.CrashTitleText);
        Assert.Equal(english.Headline, french.Headline);
        Assert.Equal(english.Rows.Select(r => r.Meaning), french.Rows.Select(r => r.Meaning));
    }

    [Fact]
    public void CrashHistory_EmptyStates_StayHonestInFrench()
    {
        var noComponent = CrashHistory(SnapshotWith(TestSnapshots.Ram("16 GB")), "fr");
        Assert.False(noComponent.HasHistory);
        Assert.Equal("L'historique des incidents n'a pas encore été collecté.", noComponent.EmptyMessage);
        Assert.Equal("Historique des incidents", noComponent.Headline);
        Assert.Equal(string.Empty, noComponent.CapturedText);

        // The component's own "What this means" is data: it shows
        // verbatim even when the chrome is French.
        var now = DateTimeOffset.UtcNow;
        var whatThisMeans = new ComponentSnapshot(
            "crash",
            "Crash history",
            "Diagnostics",
            "crash",
            "Crash history",
            null,
            Array.Empty<SpecValue>(),
            "The crash collector could not read the event log.",
            now);
        var fallback = CrashHistory(SnapshotWith(whatThisMeans), "fr");
        Assert.False(fallback.HasHistory);
        Assert.Equal("The crash collector could not read the event log.", fallback.EmptyMessage);

        // A collected but empty log shows the fixed no-crashes line.
        var emptyLog = CrashHistory(SnapshotWith(TestSnapshots.Crash(now)), "fr");
        Assert.True(emptyLog.HasHistory);
        Assert.False(emptyLog.HasRows);
        Assert.Equal("Aucun écran bleu, aucune erreur matérielle, aucun arrêt inattendu ni aucune erreur de disque n'ont été enregistrés au cours des 90 derniers jours.", emptyLog.EmptyMessage);
    }

    [Fact]
    public void CrashHistory_EmptyStates_StayHonestInBothLanguages()
    {
        var noComponent = CrashHistory(SnapshotWith(TestSnapshots.Ram("16 GB")));
        Assert.False(noComponent.HasHistory);
        Assert.Equal("Crash history has not been collected yet.", noComponent.EmptyMessage);
        Assert.Equal("Crash history", noComponent.Headline);
        Assert.Equal(string.Empty, noComponent.CapturedText);

        var noComponentGerman = CrashHistory(SnapshotWith(TestSnapshots.Ram("16 GB")), "de");
        Assert.Equal("Der Absturzverlauf wurde noch nicht erfasst.", noComponentGerman.EmptyMessage);
        Assert.Equal("Absturzverlauf", noComponentGerman.Headline);

        // The component's own "What this means" is data: it shows
        // verbatim even when the chrome is German.
        var now = DateTimeOffset.UtcNow;
        var whatThisMeans = new ComponentSnapshot(
            "crash",
            "Crash history",
            "Diagnostics",
            "crash",
            "Crash history",
            null,
            Array.Empty<SpecValue>(),
            "The crash collector could not read the event log.",
            now);
        var fallback = CrashHistory(SnapshotWith(whatThisMeans), "de");
        Assert.False(fallback.HasHistory);
        Assert.Equal("The crash collector could not read the event log.", fallback.EmptyMessage);

        // A collected but empty log shows the fixed no-crashes line.
        var emptyLog = CrashHistory(SnapshotWith(TestSnapshots.Crash(now)));
        Assert.True(emptyLog.HasHistory);
        Assert.False(emptyLog.HasRows);
        Assert.Equal("No blue screens, hardware errors, unexpected shutdowns or disk errors were recorded in the last 90 days.", emptyLog.EmptyMessage);

        var emptyLogGerman = CrashHistory(SnapshotWith(TestSnapshots.Crash(now)), "de");
        Assert.Equal("In den letzten 90 Tagen wurden keine Bluescreens, Hardwarefehler, unerwarteten Abschaltungen oder Datenträgerfehler aufgezeichnet.", emptyLogGerman.EmptyMessage);
    }
}
