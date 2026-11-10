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
        Specs = new ObservableCollection<SpecRowViewModel>(_allSpecs);
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

    public ObservableCollection<SpecRowViewModel> Specs { get; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        Specs.Clear();
        var filter = value?.Trim() ?? string.Empty;
        foreach (var spec in _allSpecs)
        {
            if (filter.Length == 0 ||
                spec.Label.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                spec.Value.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                Specs.Add(spec);
            }
        }
    }
}

public sealed record SpecRowViewModel(string Label, string Value, string Source, bool IsEstimate)
{
    public string DisplayValue => IsEstimate ? $"{Value} (ESTIMATE)" : Value;
}
