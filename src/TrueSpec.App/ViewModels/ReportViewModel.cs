using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using TrueSpec.App.Services;
using TrueSpec.Core.Localization;
using TrueSpec.Core.Models;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// Report page: JSON and HTML exports share the same report model, so the
/// Report screen always agrees with the Overview and the exports: all of
/// them read the same snapshot. While a scan is running the hero says
/// "Scanning..." instead of "Not measurable" — "no score yet" is not the
/// same as "no measurable health value".
///
/// Localisation, phase 8 (v2.1.16): the page chrome renders in the
/// current language — the headings, the export buttons, the DNA card
/// notes, the format descriptions, the preview heading, the footer
/// note and the export privacy note — so the page follows the language
/// the user picked in Settings, with no restart. The data the page
/// shows stays English: the machine name, the verdict text, the score
/// explanation, the buyer banner and summary, the generated-text line,
/// the DNA state/hash/verify result texts and the export folder path.
/// The page is cached by the shell, so the strings resolve from the
/// stored language code and re-render through
/// <see cref="ApplyLanguage"/>, the same in-place pattern the
/// Overview and History pages use.
/// </summary>
public sealed class ReportViewModel : ViewModelBase
{
    private string _languageCode;

    /// <param name="exportCertificateCommand">Null on older callers: the Buyer certificate button hides itself.</param>
    /// <param name="verifyMachineCommand">Null on older callers: the Verify this machine button hides itself.</param>
    /// <param name="dnaVerification">The result of the last "Verify this machine" run against this snapshot, or null when no verification has run yet.</param>
    /// <param name="isVerifyingMachine">True while a verification re-scan is running: the Verify button stays disabled so it cannot be double-run.</param>
    public ReportViewModel(
        SystemSnapshot snapshot,
        ICommand exportJsonCommand,
        ICommand exportHtmlCommand,
        ICommand exportCsvCommand,
        string exportFolder,
        bool isScanning = false,
        ICommand? exportPdfCommand = null,
        ICommand? exportCertificateCommand = null,
        ICommand? verifyMachineCommand = null,
        MachineDnaVerification? dnaVerification = null,
        bool isVerifyingMachine = false,
        string? languageCode = null)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        ExportJsonCommand = exportJsonCommand;
        ExportHtmlCommand = exportHtmlCommand;
        ExportCsvCommand = exportCsvCommand;
        ExportPdfCommand = exportPdfCommand;
        ExportCertificateCommand = exportCertificateCommand;
        VerifyMachineCommand = verifyMachineCommand;
        IsVerifyingMachine = isVerifyingMachine;
        DnaVerification = dnaVerification;
        // No verification has run yet: say whether this scan even carries
        // DNA. A scan saved before DNA existed has none ("not recorded");
        // a current scan does, and only the verification is still to run.
        // The old text said "not recorded" for both, so a clean machine
        // read as if it had no fingerprint at all.
        var dnaRecorded = snapshot.MachineDnaParts is not null;
        DnaStateText = dnaVerification switch
        {
            null => dnaRecorded
                ? "DNA recorded for this scan. Verify to check no part was swapped."
                : "DNA not recorded for this scan yet.",
            { State: DnaVerificationState.Identical } => "Same machine, same parts.",
            { State: DnaVerificationState.FingerprintMatches } => "Same machine — fingerprint matches.",
            { State: DnaVerificationState.Changed } => "Parts changed since the earlier scan.",
            { State: DnaVerificationState.Unverifiable } => "DNA could not be verified.",
            _ => "DNA not recorded for this scan.",
        };
        DnaHashText = dnaVerification?.HashDisplay is { } hash
            ? $"Machine DNA: {hash}"
            : snapshot.MachineDnaHash is { } ownHash
                ? $"Machine DNA: {MachineDnaVerification.TruncateHash(ownHash)}"
                : "Machine DNA: not recorded";
        VerifyResultText = dnaVerification?.SummaryText
            ?? (dnaRecorded
                ? "Re-scan this machine and compare its DNA against this snapshot to check no part was swapped since the scan."
                : "This scan was saved before machine DNA existed, so it carries no fingerprint to verify against.");
        DnaChangedParts = dnaVerification?.ChangedParts
            .Select(p => $"{p.Kind} {p.Label}: {p.Detail}")
            .ToList() ?? new List<string>();
        DnaUnidentifiedParts = dnaVerification?.UnidentifiedParts.ToList() ?? new List<string>();
        ExportFolderText = $"Exports are written to {exportFolder}. No upload, no cloud.";
        MachineName = snapshot.MachineName;
        OperatingSystem = snapshot.OperatingSystem;
        GeneratedText = $"Snapshot captured {snapshot.CapturedAt:yyyy-MM-dd HH:mm:ss}";
        if (isScanning)
        {
            ScoreText = "…";
            VerdictText = "Scanning…";
            VerdictStatus = HealthStatus.NotMeasurable;
            ScoreExplanation = "The hardware scan is still running. The score appears when the scan finishes.";
        }
        else
        {
            ScoreText = snapshot.Score.HasMeasurableComponents ? $"{snapshot.Score.OverallScore:0.#}" : "--";
            VerdictText = snapshot.Score.Verdict.ToDisplayText();
            VerdictStatus = snapshot.Score.Verdict.ToHealthStatus();
            ScoreExplanation = snapshot.Score.Explanation;
        }
        BuyerBanner = snapshot.BuyerChecks.BannerText;

        BuyerSummary = $"{snapshot.BuyerChecks.SummaryText}.";

        Components = snapshot.Components
            .Select(c => new ReportComponentRow(c.Name, c.Headline, c.HealthText, c.Status))
            .ToList();
    }

    /// <summary>The page eyebrow, in the page language.</summary>
    public string ReportEyebrowText =>
        StringLocalizer.Get(AppStringKeys.ReportEyebrow, _languageCode);

    /// <summary>The page title, in the page language.</summary>
    public string ReportTitleText =>
        StringLocalizer.Get(AppStringKeys.ReportTitle, _languageCode);

    /// <summary>The page introduction, in the page language.</summary>
    public string ReportIntroText =>
        StringLocalizer.Get(AppStringKeys.ReportIntro, _languageCode);

    /// <summary>The small label under the score number, in the page language.</summary>
    public string ReportScoreLabelText =>
        StringLocalizer.Get(AppStringKeys.ReportScoreLabel, _languageCode);

    /// <summary>The Export JSON button, in the page language.</summary>
    public string ReportExportJsonText =>
        StringLocalizer.Get(AppStringKeys.ReportExportJson, _languageCode);

    /// <summary>The Export HTML button, in the page language.</summary>
    public string ReportExportHtmlText =>
        StringLocalizer.Get(AppStringKeys.ReportExportHtml, _languageCode);

    /// <summary>The Export CSV button, in the page language.</summary>
    public string ReportExportCsvText =>
        StringLocalizer.Get(AppStringKeys.ReportExportCsv, _languageCode);

    /// <summary>The Export PDF button, in the page language.</summary>
    public string ReportExportPdfText =>
        StringLocalizer.Get(AppStringKeys.ReportExportPdf, _languageCode);

    /// <summary>The Buyer certificate button, in the page language.</summary>
    public string ReportExportCertificateText =>
        StringLocalizer.Get(AppStringKeys.ReportExportCertificate, _languageCode);

    /// <summary>The Verify this machine button, in the page language.</summary>
    public string ReportVerifyMachineText =>
        StringLocalizer.Get(AppStringKeys.ReportVerifyMachine, _languageCode);

    /// <summary>The Machine DNA card heading, in the page language.</summary>
    public string ReportDnaTitleText =>
        StringLocalizer.Get(AppStringKeys.ReportDnaTitle, _languageCode);

    /// <summary>The line shown while a verification re-scan is running, in the page language.</summary>
    public string ReportVerifyingMachineText =>
        StringLocalizer.Get(AppStringKeys.ReportVerifyingMachine, _languageCode);

    /// <summary>The heading over the parts a scan could not identify, in the page language.</summary>
    public string ReportDnaUnidentifiedTitleText =>
        StringLocalizer.Get(AppStringKeys.ReportDnaUnidentifiedTitle, _languageCode);

    /// <summary>The fixed DNA explainer note, in the page language.</summary>
    public string ReportDnaNoteText =>
        StringLocalizer.Get(AppStringKeys.ReportDnaNote, _languageCode);

    /// <summary>The JSON format card description, in the page language.</summary>
    public string ReportFormatJsonDescriptionText =>
        StringLocalizer.Get(AppStringKeys.ReportFormatJsonDescription, _languageCode);

    /// <summary>The HTML format card description, in the page language.</summary>
    public string ReportFormatHtmlDescriptionText =>
        StringLocalizer.Get(AppStringKeys.ReportFormatHtmlDescription, _languageCode);

    /// <summary>The CSV format card description, in the page language.</summary>
    public string ReportFormatCsvDescriptionText =>
        StringLocalizer.Get(AppStringKeys.ReportFormatCsvDescription, _languageCode);

    /// <summary>The PDF format card description, in the page language.</summary>
    public string ReportFormatPdfDescriptionText =>
        StringLocalizer.Get(AppStringKeys.ReportFormatPdfDescription, _languageCode);

    /// <summary>The "Available" line on every format card, in the page language.</summary>
    public string ReportFormatAvailableText =>
        StringLocalizer.Get(AppStringKeys.ReportFormatAvailable, _languageCode);

    /// <summary>The report preview heading, in the page language.</summary>
    public string ReportPreviewTitleText =>
        StringLocalizer.Get(AppStringKeys.ReportPreviewTitle, _languageCode);

    /// <summary>The footer note under the preview, in the page language.</summary>
    public string ReportFooterNoteText =>
        StringLocalizer.Get(AppStringKeys.ReportFooterNote, _languageCode);

    /// <summary>
    /// Re-renders this page in the given language, in place: every
    /// localized text is re-resolved. Called by the shell when the user
    /// picks a language in Settings; a page created after the switch is
    /// already built in the new language by the constructor. An unknown
    /// code falls back to English through the localizer, never to a
    /// blank. Measured data (machine name, verdict, score explanation,
    /// buyer texts, DNA texts, export folder) is untouched.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        OnPropertyChanged(nameof(ReportEyebrowText));
        OnPropertyChanged(nameof(ReportTitleText));
        OnPropertyChanged(nameof(ReportIntroText));
        OnPropertyChanged(nameof(ReportScoreLabelText));
        OnPropertyChanged(nameof(ReportExportJsonText));
        OnPropertyChanged(nameof(ReportExportHtmlText));
        OnPropertyChanged(nameof(ReportExportCsvText));
        OnPropertyChanged(nameof(ReportExportPdfText));
        OnPropertyChanged(nameof(ReportExportCertificateText));
        OnPropertyChanged(nameof(ReportVerifyMachineText));
        OnPropertyChanged(nameof(ReportDnaTitleText));
        OnPropertyChanged(nameof(ReportVerifyingMachineText));
        OnPropertyChanged(nameof(ReportDnaUnidentifiedTitleText));
        OnPropertyChanged(nameof(ReportDnaNoteText));
        OnPropertyChanged(nameof(ReportFormatJsonDescriptionText));
        OnPropertyChanged(nameof(ReportFormatHtmlDescriptionText));
        OnPropertyChanged(nameof(ReportFormatCsvDescriptionText));
        OnPropertyChanged(nameof(ReportFormatPdfDescriptionText));
        OnPropertyChanged(nameof(ReportFormatAvailableText));
        OnPropertyChanged(nameof(ReportPreviewTitleText));
        OnPropertyChanged(nameof(ReportFooterNoteText));
        OnPropertyChanged(nameof(ExportPrivacyNote));
    }

    public string MachineName { get; }

    public string OperatingSystem { get; }

    public string GeneratedText { get; }

    public string ScoreText { get; }

    public string VerdictText { get; }

    public HealthStatus VerdictStatus { get; }

    /// <summary>What the score is based on: only components with a real sensor are included.</summary>
    public string ScoreExplanation { get; }

    public string BuyerBanner { get; }

    public string BuyerSummary { get; }

    public IReadOnlyList<ReportComponentRow> Components { get; }

    public ICommand ExportJsonCommand { get; }

    public ICommand ExportHtmlCommand { get; }

    public ICommand ExportCsvCommand { get; }

    /// <summary>Null on older callers: the PDF button hides itself.</summary>
    public ICommand? ExportPdfCommand { get; }

    public bool HasPdfExport => ExportPdfCommand is not null;

    /// <summary>
    /// One-page buyer certificate (PDF). Null on older callers: the button hides itself.
    /// DNA is identity, not health: the certificate never changes the score or the verdict above.
    /// </summary>
    public ICommand? ExportCertificateCommand { get; }

    public bool HasCertificateExport => ExportCertificateCommand is not null;

    /// <summary>Re-scans this machine and compares its DNA against this snapshot. Null on older callers: the button hides itself.</summary>
    public ICommand? VerifyMachineCommand { get; }

    public bool HasVerifyMachine => VerifyMachineCommand is not null;

    /// <summary>True while a verification re-scan is running.</summary>
    public bool IsVerifyingMachine { get; }

    /// <summary>The last verification result, or null when no verification has run yet.</summary>
    public MachineDnaVerification? DnaVerification { get; }

    /// <summary>One-line DNA state: "Same machine, same parts", "Parts changed", or "DNA not recorded". Never a mismatch for a scan with no DNA.</summary>
    public string DnaStateText { get; }

    /// <summary>Truncated DNA hash for display, or "Machine DNA: not recorded". The full hash is only ever written into the local report file.</summary>
    public string DnaHashText { get; }

    /// <summary>What the last verification concluded, in plain words; or what to do when no verification has run yet.</summary>
    public string VerifyResultText { get; }

    /// <summary>Changed parts from the last verification, empty when the machine is identical or no verification ran.</summary>
    public IReadOnlyList<string> DnaChangedParts { get; }

    /// <summary>Parts either scan could not identify, as "Kind Label" strings; empty when every part was identified or no verification ran.</summary>
    public IReadOnlyList<string> DnaUnidentifiedParts { get; }

    /// <summary>True when the last verification named a part it could not identify.</summary>
    public bool HasDnaUnidentifiedParts => DnaUnidentifiedParts.Count > 0;

    public string ExportFolderText { get; }

    /// <summary>
    /// What an exported report contains that identifies this machine. A
    /// buyer hands the file to a seller, so the page says so before the
    /// export happens instead of after. The file itself is unchanged.
    /// In the page language.
    /// </summary>
    public string ExportPrivacyNote =>
        StringLocalizer.Get(AppStringKeys.ReportExportPrivacyNote, _languageCode);
}

public sealed record ReportComponentRow(string Name, string Headline, string HealthText, HealthStatus Status);
