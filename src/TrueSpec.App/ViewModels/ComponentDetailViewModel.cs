using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;

namespace TrueSpec.App.ViewModels;

/// <summary>Shared detail page for any component collector result.</summary>
public sealed partial class ComponentDetailViewModel : ViewModelBase
{
    private readonly IReadOnlyList<SpecRowViewModel> _allSpecs;

    public ComponentDetailViewModel(SystemSnapshot snapshot, string componentId)
    {
        var component = snapshot.Find(componentId) ?? new ComponentSnapshot(
            componentId,
            componentId,
            componentId,
            componentId,
            "Not collected",
            null,
            Array.Empty<SpecValue>(),
            "This component has not been collected yet.",
            DateTimeOffset.Now);

        Name = component.Name;
        Headline = component.Headline;
        IconData = IconLibrary.Get(component.IconKey);
        HealthPercent = component.HealthPercent;
        HealthValue = component.HealthPercent ?? 0d;
        HealthText = component.HealthText;
        Status = component.Status;
        StatusText = component.Status.ToDisplayText();
        HasHealth = component.HealthPercent is not null;
        WhatThisMeans = component.WhatThisMeans;
        CapturedText = $"Reading captured {component.CapturedAt:yyyy-MM-dd HH:mm:ss}";

        _allSpecs = component.Specs
            .Select(s => new SpecRowViewModel(s.Label, s.Value, s.Source, s.IsEstimate))
            .ToList();
        ApplyFilter();

        // U1 "Age signals" (Blueprint 1): on the System page only, the raw
        // numbers behind the authenticity verdict, each with its source, so
        // a buyer can see what the verdict was computed from. The analyzer
        // is pure over the snapshot; other components get no group.
        if (string.Equals(componentId, "system", StringComparison.OrdinalIgnoreCase))
        {
            var authenticity = AuthenticityAnalyzer.Analyze(snapshot);
            AgeSignals = new ObservableCollection<AgeSignalRowViewModel>(
                authenticity.Signals.Select(s => new AgeSignalRowViewModel(s)));
            AgeSignalsNote = authenticity.SkippedChecks.Count == 0
                ? string.Empty
                : $"{authenticity.SkippedChecks.Count} authenticity check(s) could not run because a signal was not reported.";
        }
        else
        {
            AgeSignals = new ObservableCollection<AgeSignalRowViewModel>();
            AgeSignalsNote = string.Empty;
        }

        HasAgeSignals = AgeSignals.Count > 0;
        HasAgeSignalsNote = AgeSignalsNote.Length > 0;
    }

    public string Name { get; }

    public string Headline { get; }

    public string IconData { get; }

    public double? HealthPercent { get; }

    public double HealthValue { get; }

    public string HealthText { get; }

    public HealthStatus Status { get; }

    public string StatusText { get; }

    public bool HasHealth { get; }

    public string WhatThisMeans { get; }

    public string CapturedText { get; }

    public ObservableCollection<SpecRowViewModel> Specs { get; } = new();

    /// <summary>U1 age signals, System page only: the raw numbers behind the authenticity verdict.</summary>
    public ObservableCollection<AgeSignalRowViewModel> AgeSignals { get; }

    /// <summary>False when the page has no age signals: the group is hidden instead of rendering empty.</summary>
    public bool HasAgeSignals { get; }

    /// <summary>Names the authenticity rules that could not run, or empty when all ran.</summary>
    public string AgeSignalsNote { get; }

    /// <summary>True when at least one authenticity rule could not run.</summary>
    public bool HasAgeSignalsNote { get; }

    /// <summary>False when the component has no specs, or when the search matches none: the view shows an empty state instead of a blank list.</summary>
    [ObservableProperty]
    public partial bool HasVisibleSpecs { get; set; }

    /// <summary>Explains the empty list: nothing collected, or nothing matching the search.</summary>
    [ObservableProperty]
    public partial string NoSpecsMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filter = SearchText?.Trim() ?? string.Empty;
        Specs.Clear();
        foreach (var spec in _allSpecs)
        {
            if (filter.Length == 0 ||
                spec.Label.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                spec.Value.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                Specs.Add(spec);
            }
        }

        HasVisibleSpecs = Specs.Count > 0;
        NoSpecsMessage = _allSpecs.Count == 0
            ? "No specifications were collected for this component."
            : Specs.Count == 0
                ? $"No specifications match \"{filter}\". Clear the search to see them all."
                : string.Empty;
    }
}

public sealed record SpecRowViewModel(string Label, string Value, string Source, bool IsEstimate)
{
    public string DisplayValue => IsEstimate ? $"{Value} (ESTIMATE)" : Value;
}

public sealed record AgeSignalRowViewModel(string Name, string Value, string Source, bool IsReliable)
{
    public AgeSignalRowViewModel(AuthenticitySignal signal)
        : this(signal.Name, signal.Value, signal.Source, signal.IsReliable)
    {
    }

    /// <summary>Empty for trusted signals; a plain warning for the one case where another rule proved the signal impossible (R4).</summary>
    public string ReliabilityText => IsReliable
        ? string.Empty
        : "Not trusted: another check proved this date impossible.";

    /// <summary>True when the signal was proved unreliable.</summary>
    public bool HasReliabilityText => ReliabilityText.Length > 0;
}
