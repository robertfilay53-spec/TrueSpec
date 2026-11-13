using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using TrueSpec.App.Services;
using TrueSpec.Core.Models;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// Report page: JSON and HTML exports share the same report model, so the
/// Report screen always agrees with the Overview and the exports: all of
/// them read the same snapshot. While a scan is running the hero says
/// "Scanning..." instead of "Not measurable" — "no score yet" is not the
/// same as "no measurable health value".
/// </summary>
public sealed class ReportViewModel : ViewModelBase
{
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
        bool isVerifyingMachine = false)
    {
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
    /// </summary>
    public string ExportPrivacyNote { get; } = "Exported reports contain this machine's hardware serials, MAC addresses, IP addresses and Wi-Fi network names. Share them only with people you trust.";
}

public sealed record ReportComponentRow(string Name, string Headline, string HealthText, HealthStatus Status);
