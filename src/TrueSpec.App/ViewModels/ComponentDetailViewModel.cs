using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
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
