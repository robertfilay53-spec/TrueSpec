using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.History;
using TrueSpec.Core.Localization;
using TrueSpec.Core.Models;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// Shared detail page for any component collector result.
///
/// Localisation, phase 4 (v2.1.10): the page chrome renders in the
/// current language — the six section labels in the view, the captured
/// line, the empty states and the age-signals group — so all 15
/// component pages translate at once through this one shared view.
/// The data the page shows stays English in this version: component
/// names, headlines, health and status texts, spec labels, values and
/// sources, and the "What this means" text are measured data produced
/// by the collectors; translating them is a later slice. The shell
/// re-languages a cached detail page through
/// <see cref="ApplyLanguage"/> when the user picks a language in
/// Settings, the same in-place pattern the Settings page uses.
/// </summary>
public sealed partial class ComponentDetailViewModel : ViewModelBase
{
    private readonly IReadOnlyList<SpecValue> _rawSpecs;
    private readonly IReadOnlyList<AuthenticitySignal> _ageSignalsRaw;
    private readonly int _skippedCheckCount;
    private readonly DateTimeOffset _capturedAt;
    private string _languageCode;

    /// <summary>Spec rows in the current language; rebuilt when the language changes.</summary>
    private IReadOnlyList<SpecRowViewModel> _allSpecs = Array.Empty<SpecRowViewModel>();

    public ComponentDetailViewModel(SystemSnapshot snapshot, string componentId, string? languageCode = null)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;

        var component = snapshot.Find(componentId) ?? new ComponentSnapshot(
            componentId,
            componentId,
            componentId,
            componentId,
            StringLocalizer.Get(AppStringKeys.DetailNotCollectedHeadline, _languageCode),
            null,
            Array.Empty<SpecValue>(),
            StringLocalizer.Get(AppStringKeys.DetailNotCollectedText, _languageCode),
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
        _capturedAt = component.CapturedAt;

        _rawSpecs = component.Specs;
        _allSpecs = BuildSpecRows(_rawSpecs, _languageCode);
        ApplyFilter();

        // U1 "Age signals" (Blueprint 1): on the System page only, the raw
        // numbers behind the authenticity verdict, each with its source, so
        // a buyer can see what the verdict was computed from. The analyzer
        // is pure over the snapshot; other components get no group.
        if (string.Equals(componentId, "system", StringComparison.OrdinalIgnoreCase))
        {
            var authenticity = AuthenticityAnalyzer.Analyze(snapshot);
            _ageSignalsRaw = authenticity.Signals;
            _skippedCheckCount = authenticity.SkippedChecks.Count;
        }
        else
        {
            _ageSignalsRaw = Array.Empty<AuthenticitySignal>();
            _skippedCheckCount = 0;
        }

        AgeSignals = new ObservableCollection<AgeSignalRowViewModel>(
            _ageSignalsRaw.Select(s => new AgeSignalRowViewModel(s, _languageCode)));
        HasAgeSignals = AgeSignals.Count > 0;
        HasAgeSignalsNote = AgeSignalsNote.Length > 0;
    }

    /// <summary>Section heading above the specification list.</summary>
    public string SpecificationsTitleText =>
        StringLocalizer.Get(AppStringKeys.DetailSpecificationsTitle, _languageCode);

    /// <summary>Placeholder of the specification search box.</summary>
    public string SearchPlaceholderText =>
        StringLocalizer.Get(AppStringKeys.DetailSearchPlaceholder, _languageCode);

    /// <summary>Hint under the specification list: click to copy, hover for the source.</summary>
    public string CopyHintText =>
        StringLocalizer.Get(AppStringKeys.DetailCopyHint, _languageCode);

    /// <summary>Section heading of the age-signals group (System page only).</summary>
    public string AgeSignalsTitleText =>
        StringLocalizer.Get(AppStringKeys.DetailAgeSignalsTitle, _languageCode);

    /// <summary>Intro line of the age-signals group.</summary>
    public string AgeSignalsIntroText =>
        StringLocalizer.Get(AppStringKeys.DetailAgeSignalsIntro, _languageCode);

    /// <summary>Section heading above the "What this means" text.</summary>
    public string WhatThisMeansTitleText =>
        StringLocalizer.Get(AppStringKeys.DetailWhatThisMeansTitle, _languageCode);

    /// <summary>
    /// When the reading was captured. The date keeps the app's existing
    /// "yyyy-MM-dd HH:mm:ss" shape, formatted invariantly, so only the
    /// words around it change with the language.
    /// </summary>
    public string CapturedText => StringLocalizer.GetFormatted(
        AppStringKeys.DetailCaptured,
        _languageCode,
        _capturedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

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

    public ObservableCollection<SpecRowViewModel> Specs { get; } = new();

    /// <summary>U1 age signals, System page only: the raw numbers behind the authenticity verdict.</summary>
    public ObservableCollection<AgeSignalRowViewModel> AgeSignals { get; }

    /// <summary>False when the page has no age signals: the group is hidden instead of rendering empty.</summary>
    public bool HasAgeSignals { get; }

    /// <summary>Names the authenticity rules that could not run, or empty when all ran.</summary>
    public string AgeSignalsNote => _skippedCheckCount == 0
        ? string.Empty
        : StringLocalizer.GetFormatted(
            AppStringKeys.DetailAgeSignalsSkipped, _languageCode, _skippedCheckCount);

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

    /// <summary>
    /// Re-renders this page in the given language, in place: every
    /// localized text is re-resolved, the spec rows and age-signal rows
    /// are rebuilt (their annotations resolve at construction time),
    /// and the empty-state message is recomputed for the current
    /// search. Called by the shell when the user picks a language in
    /// Settings; a page created after the switch is already built in
    /// the new language by the constructor. An unknown code falls back
    /// to English through the localizer, never to a blank.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        _allSpecs = BuildSpecRows(_rawSpecs, _languageCode);
        AgeSignals.Clear();
        foreach (var signal in _ageSignalsRaw)
        {
            AgeSignals.Add(new AgeSignalRowViewModel(signal, _languageCode));
        }

        ApplyFilter();
        OnPropertyChanged(nameof(SpecificationsTitleText));
        OnPropertyChanged(nameof(SearchPlaceholderText));
        OnPropertyChanged(nameof(CopyHintText));
        OnPropertyChanged(nameof(AgeSignalsTitleText));
        OnPropertyChanged(nameof(AgeSignalsIntroText));
        OnPropertyChanged(nameof(WhatThisMeansTitleText));
        OnPropertyChanged(nameof(CapturedText));
        OnPropertyChanged(nameof(AgeSignalsNote));
        OnPropertyChanged(nameof(HasAgeSignalsNote));
    }

    private static IReadOnlyList<SpecRowViewModel> BuildSpecRows(
        IReadOnlyList<SpecValue> specs, string languageCode) =>
        specs.Select(s => new SpecRowViewModel(s.Label, s.Value, s.Source, s.IsEstimate, languageCode))
            .ToList();

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
            ? StringLocalizer.Get(AppStringKeys.DetailNoSpecs, _languageCode)
            : Specs.Count == 0
                ? StringLocalizer.GetFormatted(AppStringKeys.DetailNoSpecsMatch, _languageCode, filter)
                : string.Empty;
    }
}

public sealed record SpecRowViewModel(
    string Label,
    string Value,
    string Source,
    bool IsEstimate,
    string? LanguageCode = null)
{
    /// <summary>
    /// The value, with the "(ESTIMATE)" annotation when the reading is
    /// an estimate. The annotation is page chrome (the view adds it),
    /// so it renders in the page language; the value itself never
    /// changes.
    /// </summary>
    public string DisplayValue => IsEstimate
        ? Value + StringLocalizer.Get(
            AppStringKeys.DetailEstimateSuffix,
            LanguageCode ?? LanguagePacks.EnglishCode)
        : Value;
}

public sealed record AgeSignalRowViewModel(
    string Name,
    string Value,
    string Source,
    bool IsReliable,
    string? LanguageCode = null)
{
    public AgeSignalRowViewModel(AuthenticitySignal signal, string? languageCode = null)
        : this(signal.Name, signal.Value, signal.Source, signal.IsReliable, languageCode)
    {
    }

    /// <summary>Empty for trusted signals; a plain warning for the one case where another rule proved the signal impossible (R4).</summary>
    public string ReliabilityText => IsReliable
        ? string.Empty
        : StringLocalizer.Get(
            AppStringKeys.DetailSignalUntrusted,
            LanguageCode ?? LanguagePacks.EnglishCode);

    /// <summary>True when the signal was proved unreliable.</summary>
    public bool HasReliabilityText => ReliabilityText.Length > 0;
}
