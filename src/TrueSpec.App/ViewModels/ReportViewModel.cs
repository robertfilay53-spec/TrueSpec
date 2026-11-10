using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;

namespace TrueSpec.App.ViewModels;

/// <summary>Report page: one model, three planned outputs, JSON works in P0.</summary>
public sealed class ReportViewModel : ViewModelBase
{
    public ReportViewModel(SystemSnapshot snapshot, ICommand exportCommand)
    {
        ExportCommand = exportCommand;
        MachineName = snapshot.MachineName;
        OperatingSystem = snapshot.OperatingSystem;
        GeneratedText = $"Snapshot captured {snapshot.CapturedAt:yyyy-MM-dd HH:mm:ss}";
        ScoreText = snapshot.Score.HasMeasurableComponents ? $"{snapshot.Score.OverallScore:0}" : "--";
        VerdictText = snapshot.Score.Verdict.ToDisplayText();
        VerdictStatus = snapshot.Score.Verdict.ToHealthStatus();
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

    public string BuyerBanner { get; }

    public string BuyerSummary { get; }

    public IReadOnlyList<ReportComponentRow> Components { get; }

    public IReadOnlyList<ReportFormat> Formats { get; } = new[]
    {
        ReportFormat.Json,
        ReportFormat.Html,
        ReportFormat.Pdf,
    };

    public ICommand ExportCommand { get; }
}

public sealed record ReportComponentRow(string Name, string Headline, string HealthText, HealthStatus Status);
