using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using TrueSpec.Core.Models;

namespace TrueSpec.App.ViewModels;

/// <summary>Buyer mode: a PASS / WARNING / FAIL banner plus the checklist.</summary>
public sealed class NewLaptopCheckViewModel : ViewModelBase
{
    public NewLaptopCheckViewModel(SystemSnapshot snapshot, ICommand exportCommand)
    {
        ExportCommand = exportCommand;
        MachineName = snapshot.MachineName;
        BannerText = snapshot.BuyerChecks.BannerText;
        OverallState = snapshot.BuyerChecks.OverallState;
        Checks = new ObservableCollection<BuyerCheckRowViewModel>(
            snapshot.BuyerChecks.Checks.Select(c => new BuyerCheckRowViewModel(c)));

        var passed = snapshot.BuyerChecks.Checks.Count(c => c.IsPass);
        Summary = $"{passed} of {snapshot.BuyerChecks.Checks.Count} checks passed.";
    }

    public string MachineName { get; }

    public string BannerText { get; }

    public BuyerCheckState OverallState { get; }

    public string Summary { get; }

    public ObservableCollection<BuyerCheckRowViewModel> Checks { get; }

    public ICommand ExportCommand { get; }
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
