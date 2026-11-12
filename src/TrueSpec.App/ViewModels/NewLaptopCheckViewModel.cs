using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.App.ViewModels;

/// <summary>Buyer mode: a PASS / WARNING / FAIL banner plus the checklist.</summary>
public sealed partial class NewLaptopCheckViewModel : ViewModelBase
{
    private readonly SystemSnapshot _snapshot;
    private readonly IBuyerCheckEvaluator _evaluator;
    private readonly Action<BuyerCheckResult, string?>? _onChecksChanged;

    private BuyerCheckResult _result;

    public NewLaptopCheckViewModel(
        SystemSnapshot snapshot,
        ICommand exportCommand,
        ICommand? exportChecklistCommand = null,
        IBuyerCheckEvaluator? evaluator = null,
        Action<BuyerCheckResult, string?>? onChecksChanged = null,
        string? advertisedRam = null)
    {
        _snapshot = snapshot;
        _evaluator = evaluator ?? new BuyerCheckEvaluator();
        _onChecksChanged = onChecksChanged;
        ExportCommand = exportCommand;
        ExportChecklistCommand = exportChecklistCommand;
        MachineName = snapshot.MachineName;
        _result = snapshot.BuyerChecks;
        Checks = new ObservableCollection<BuyerCheckRowViewModel>(
            _result.Checks.Select(c => new BuyerCheckRowViewModel(c)));
        HasChecks = Checks.Count > 0;
        Summary = BuildSummary(_result);

        // U1 Authenticity (Blueprint 1): the age-signal cross-checks as
        // their own section, with the raw verdict and every fired check.
        // The analyzer is pure over the snapshot, so this never re-runs a
        // collector and never changes when the advertised RAM changes.
        Authenticity = AuthenticityAnalyzer.Analyze(snapshot);
        AuthenticityChecks = new ObservableCollection<AuthenticityCheckRowViewModel>(
            Authenticity.Checks.Select(c => new AuthenticityCheckRowViewModel(c)));
        HasAuthenticityChecks = AuthenticityChecks.Count > 0;
        AuthenticitySummary = Authenticity.Summary;
        AuthenticitySkippedText = Authenticity.SkippedChecks.Count == 0
            ? string.Empty
            : $"{Authenticity.SkippedChecks.Count} authenticity check(s) could not run because a signal was not reported: {string.Join("; ", Authenticity.SkippedChecks)}";

        // Restore the advertised size the shell kept (for example after a
        // refresh rebuilt this page): the text box must not come back empty.
        // Setting the property re-evaluates the checks with the same context,
        // so the result is identical to the one the scan already computed.
        if (!string.IsNullOrWhiteSpace(advertisedRam))
        {
            AdvertisedRam = advertisedRam;
        }
    }

    public string MachineName { get; }

    public string BannerText => _result.BannerText;

    public BuyerCheckState OverallState => _result.OverallState;

    public string Summary { get; private set; }

    public ObservableCollection<BuyerCheckRowViewModel> Checks { get; private set; }

    /// <summary>U1 verdict: "Signs of prior use" or "No signs of prior use".</summary>
    public AuthenticityResult Authenticity { get; }

    /// <summary>One row per fired authenticity check, with its two sources.</summary>
    public ObservableCollection<AuthenticityCheckRowViewModel> AuthenticityChecks { get; }

    /// <summary>False when no authenticity check fired: the view shows the verdict line instead of a blank list.</summary>
    public bool HasAuthenticityChecks { get; }

    /// <summary>Plain-English verdict line for the Authenticity section.</summary>
    public string AuthenticitySummary { get; }

    /// <summary>Names the rules that could not run, or empty when all ran.</summary>
    public string AuthenticitySkippedText { get; }

    /// <summary>True when at least one authenticity rule could not run.</summary>
    public bool HasAuthenticitySkippedText => AuthenticitySkippedText.Length > 0;

    /// <summary>False when the scan produced no buyer checks: the view shows an empty state instead of a blank list.</summary>
    [ObservableProperty]
    public partial bool HasChecks { get; set; }

    public ICommand ExportCommand { get; }

    /// <summary>Exports the printable buyer checklist. Null when the shell did not wire it.</summary>
    public ICommand? ExportChecklistCommand { get; }

    [ObservableProperty]
    public partial string AdvertisedRam { get; set; } = string.Empty;

    partial void OnAdvertisedRamChanged(string value)
    {
        var context = new BuyerCheckContext(
            string.IsNullOrWhiteSpace(value) ? null : value.Trim());
        _result = _evaluator.Evaluate(_snapshot.Components, context);
        Checks = new ObservableCollection<BuyerCheckRowViewModel>(
            _result.Checks.Select(c => new BuyerCheckRowViewModel(c)));
        HasChecks = Checks.Count > 0;
        Summary = BuildSummary(_result);

        OnPropertyChanged(nameof(Checks));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(BannerText));
        OnPropertyChanged(nameof(OverallState));
        _onChecksChanged?.Invoke(_result, value);
    }

    private static string BuildSummary(BuyerCheckResult result)
    {
        var passed = result.Checks.Count(c => c.IsPass);
        return $"{passed} of {result.Checks.Count} checks passed.";
    }
}

public sealed class BuyerCheckRowViewModel
{
    public BuyerCheckRowViewModel(BuyerCheck check)
    {
        Title = check.Title;
        Detail = check.Detail;
        WhatToAsk = check.WhatToAsk;
        State = check.State;
        StateText = check.StateText;
        Status = check.ToHealthStatus();
        NeedsAttention = check.State is BuyerCheckState.Fail or BuyerCheckState.Warning;
    }

    public string Title { get; }

    public string Detail { get; }

    public string WhatToAsk { get; }

    public BuyerCheckState State { get; }

    public string StateText { get; }

    public HealthStatus Status { get; }

    public bool NeedsAttention { get; }
}

/// <summary>
/// One fired U1 authenticity check: the rule title, the plain-English
/// evidence line, and the two sources the rule compared. Severity maps to
/// the shared health colours (Strong = Problem, Watch = Watch, Info =
/// Healthy) so the view reuses the existing Status brushes, no new tokens.
/// </summary>
public sealed class AuthenticityCheckRowViewModel
{
    public AuthenticityCheckRowViewModel(AuthenticityCheck check)
    {
        Title = check.Title;
        EvidenceLine = check.EvidenceLine;
        Severity = check.Severity;
        SeverityText = check.Severity.ToString();
        SourceA = check.SourceA;
        SourceB = check.SourceB;
        Status = check.Severity switch
        {
            AuthenticitySeverity.Strong => HealthStatus.Problem,
            AuthenticitySeverity.Watch => HealthStatus.Watch,
            _ => HealthStatus.Healthy,
        };
    }

    public string Title { get; }

    public string EvidenceLine { get; }

    public AuthenticitySeverity Severity { get; }

    public string SeverityText { get; }

    public string SourceA { get; }

    public string SourceB { get; }

    public HealthStatus Status { get; }
}
