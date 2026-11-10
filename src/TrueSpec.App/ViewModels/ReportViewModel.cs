using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
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
    public ReportViewModel(
        SystemSnapshot snapshot,
        ICommand exportJsonCommand,
        ICommand exportHtmlCommand,
        ICommand exportCsvCommand,
        string exportFolder,
        bool isScanning = false)
    {
        ExportJsonCommand = exportJsonCommand;
        ExportHtmlCommand = exportHtmlCommand;
        ExportCsvCommand = exportCsvCommand;
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
            ScoreText = snapshot.Score.HasMeasurableComponents ? $"{snapshot.Score.OverallScore:0}" : "--";
            VerdictText = snapshot.Score.Verdict.ToDisplayText();
            VerdictStatus = snapshot.Score.Verdict.ToHealthStatus();
            ScoreExplanation = snapshot.Score.Explanation;
        }
        BuyerBanner = snapshot.BuyerChecks.BannerText;

        var passed = snapshot.BuyerChecks.Checks.Count(c => c.IsPass);
        BuyerSummary = $"{passed} of {snapshot.BuyerChecks.Checks.Count} buyer checks passed.";

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

    public string ExportFolderText { get; }
}

public sealed record ReportComponentRow(string Name, string Headline, string HealthText, HealthStatus Status);
