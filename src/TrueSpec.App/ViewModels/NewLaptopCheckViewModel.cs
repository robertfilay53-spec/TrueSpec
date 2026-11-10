using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
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
        Action<BuyerCheckResult, string?>? onChecksChanged = null)
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
        Summary = BuildSummary(_result);
    }

    public string MachineName { get; }

    public string BannerText => _result.BannerText;

    public BuyerCheckState OverallState => _result.OverallState;

    public string Summary { get; private set; }

    public ObservableCollection<BuyerCheckRowViewModel> Checks { get; private set; }

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
