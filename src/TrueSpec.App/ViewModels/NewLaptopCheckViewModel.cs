using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.History;
using TrueSpec.Core.Localization;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// Buyer mode: a PASS / WARNING / FAIL banner plus the checklist.
///
/// Localisation, phase 8 (v2.1.16): the page chrome renders in the
/// current language — the eyebrow, the banner's fixed words, the
/// Authenticity heading, the no-prior-use line, the skipped-checks
/// line, the checklist heading and buttons, the advertised-RAM card,
/// the "What to ask the seller" label, the no-checks line, the footer
/// note, the row status words and the source labels — so the page
/// follows the language the user picked in Settings, with no restart.
/// The banner is rebuilt here from the stored result, so its fixed
/// words translate while the finding title it cites stays English:
/// the four no-finding sentences are whole keys, and the finding
/// form is a bare state word plus the finding title verbatim. The
/// data the page shows stays English: the machine name, the buyer
/// summary and the authenticity verdict line (verdict text, like the
/// Report page's buyer banner and summary), the check titles,
/// details and what-to-ask lines, the authenticity evidence lines
/// and the source strings themselves. The page is cached by the
/// shell, so the strings resolve from the stored language code and
/// re-render through <see cref="ApplyLanguage"/>, the same in-place
/// pattern the Overview and History pages use.
/// </summary>
public sealed partial class NewLaptopCheckViewModel : ViewModelBase
{
    private readonly SystemSnapshot _snapshot;
    private readonly IBuyerCheckEvaluator _evaluator;
    private readonly Action<BuyerCheckResult, string?>? _onChecksChanged;
    private string _languageCode;

    private BuyerCheckResult _result;

    /// <summary>The skipped authenticity signal names, joined once so a language switch re-resolves the line instead of rebuilding it.</summary>
    private readonly int _skippedCount;

    /// <summary>The skipped authenticity signal names, joined once (data, stays English inside the translated sentence).</summary>
    private readonly string _skippedNames;

    public NewLaptopCheckViewModel(
        SystemSnapshot snapshot,
        ICommand exportCommand,
        ICommand? exportChecklistCommand = null,
        IBuyerCheckEvaluator? evaluator = null,
        Action<BuyerCheckResult, string?>? onChecksChanged = null,
        string? advertisedRam = null,
        string? languageCode = null)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        _snapshot = snapshot;
        _evaluator = evaluator ?? new BuyerCheckEvaluator();
        _onChecksChanged = onChecksChanged;
        ExportCommand = exportCommand;
        ExportChecklistCommand = exportChecklistCommand;
        MachineName = snapshot.MachineName;
        _result = snapshot.BuyerChecks;
        Checks = new ObservableCollection<BuyerCheckRowViewModel>(
            _result.Checks.Select(c => new BuyerCheckRowViewModel(c, _languageCode)));
        HasChecks = Checks.Count > 0;
        Summary = BuildSummary(_result);

        // U1 Authenticity (Blueprint 1): the age-signal cross-checks as
        // their own section, with the raw verdict and every fired check.
        // The analyzer is pure over the snapshot, so this never re-runs a
        // collector and never changes when the advertised RAM changes.
        Authenticity = AuthenticityAnalyzer.Analyze(snapshot);
        AuthenticityChecks = new ObservableCollection<AuthenticityCheckRowViewModel>(
            Authenticity.Checks.Select(c => new AuthenticityCheckRowViewModel(c, _languageCode)));
        HasAuthenticityChecks = AuthenticityChecks.Count > 0;
        AuthenticitySummary = Authenticity.Summary;
        _skippedCount = Authenticity.SkippedChecks.Count;
        _skippedNames = string.Join("; ", Authenticity.SkippedChecks);

        // Restore the advertised size the shell kept (for example after a
        // refresh rebuilt this page): the text box must not come back empty.
        // Setting the property re-evaluates the checks with the same context,
        // so the result is identical to the one the scan already computed.
        if (!string.IsNullOrWhiteSpace(advertisedRam))
        {
            AdvertisedRam = advertisedRam;
        }
    }

    /// <summary>The page eyebrow, in the page language.</summary>
    public string NewLaptopCheckEyebrowText =>
        StringLocalizer.Get(AppStringKeys.NewLaptopCheckEyebrow, _languageCode);

    /// <summary>The "Authenticity" section heading, in the page language.</summary>
    public string AuthenticityTitleText =>
        StringLocalizer.Get(AppStringKeys.NewLaptopCheckAuthenticityTitle, _languageCode);

    /// <summary>The empty-state line shown when no authenticity check fired, in the page language.</summary>
    public string NoPriorUseText =>
        StringLocalizer.Get(AppStringKeys.NewLaptopCheckNoPriorUse, _languageCode);

    /// <summary>
    /// Names the rules that could not run, or empty when all ran. The
    /// count and the signal names are values; only the surrounding
    /// words translate, with one/many keys instead of the old inline
    /// plural hack. In the page language.
    /// </summary>
    public string AuthenticitySkippedText => _skippedCount == 0
        ? string.Empty
        : _skippedCount == 1
            ? StringLocalizer.GetFormatted(AppStringKeys.NewLaptopCheckSkippedOne, _languageCode, _skippedNames)
            : StringLocalizer.GetFormatted(AppStringKeys.NewLaptopCheckSkippedMany, _languageCode, _skippedCount, _skippedNames);

    /// <summary>The "Checklist" section heading, in the page language.</summary>
    public string ChecklistTitleText =>
        StringLocalizer.Get(AppStringKeys.NewLaptopCheckChecklistTitle, _languageCode);

    /// <summary>The "Print checklist" button, in the page language.</summary>
    public string PrintChecklistText =>
        StringLocalizer.Get(AppStringKeys.NewLaptopCheckPrintChecklist, _languageCode);

    /// <summary>The "Export Report" button, in the page language. Reuses the Overview key: the same words, not a second copy.</summary>
    public string ExportReportText =>
        StringLocalizer.Get(AppStringKeys.OverviewExportReport, _languageCode);

    /// <summary>The advertised-RAM card heading, in the page language.</summary>
    public string AdvertisedRamTitleText =>
        StringLocalizer.Get(AppStringKeys.NewLaptopCheckAdvertisedRamTitle, _languageCode);

    /// <summary>The advertised-RAM card note, in the page language.</summary>
    public string AdvertisedRamNoteText =>
        StringLocalizer.Get(AppStringKeys.NewLaptopCheckAdvertisedRamNote, _languageCode);

    /// <summary>The advertised-RAM text box placeholder, in the page language.</summary>
    public string AdvertisedRamPlaceholderText =>
        StringLocalizer.Get(AppStringKeys.NewLaptopCheckAdvertisedRamPlaceholder, _languageCode);

    /// <summary>The empty-state line shown when the scan produced no buyer checks, in the page language.</summary>
    public string NoChecksText =>
        StringLocalizer.Get(AppStringKeys.NewLaptopCheckNoChecks, _languageCode);

    /// <summary>The footer note under the checklist, in the page language.</summary>
    public string FooterNoteText =>
        StringLocalizer.Get(AppStringKeys.NewLaptopCheckFooterNote, _languageCode);

    /// <summary>
    /// Re-renders this page in the given language, in place: every
    /// localized text is re-resolved, and each checklist and
    /// authenticity row re-renders too. Called by the shell when the
    /// user picks a language in Settings; a page created after the
    /// switch is already built in the new language by the
    /// constructor. An unknown code falls back to English through the
    /// localizer, never to a blank. Measured data (machine name,
    /// summary, authenticity verdict, check titles and details,
    /// evidence lines, source strings, the finding title inside the
    /// banner) is untouched.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        foreach (var check in Checks)
        {
            check.ApplyLanguage(_languageCode);
        }

        foreach (var check in AuthenticityChecks)
        {
            check.ApplyLanguage(_languageCode);
        }

        OnPropertyChanged(nameof(NewLaptopCheckEyebrowText));
        OnPropertyChanged(nameof(BannerText));
        OnPropertyChanged(nameof(AuthenticityTitleText));
        OnPropertyChanged(nameof(NoPriorUseText));
        OnPropertyChanged(nameof(AuthenticitySkippedText));
        OnPropertyChanged(nameof(ChecklistTitleText));
        OnPropertyChanged(nameof(PrintChecklistText));
        OnPropertyChanged(nameof(ExportReportText));
        OnPropertyChanged(nameof(AdvertisedRamTitleText));
        OnPropertyChanged(nameof(AdvertisedRamNoteText));
        OnPropertyChanged(nameof(AdvertisedRamPlaceholderText));
        OnPropertyChanged(nameof(NoChecksText));
        OnPropertyChanged(nameof(FooterNoteText));
    }

    public string MachineName { get; }

    /// <summary>
    /// The banner headline, rebuilt from the stored result so its
    /// fixed words follow the page language: the four no-finding
    /// sentences are whole keys, and the finding form is a bare state
    /// word plus the finding title verbatim (the title is data, so it
    /// stays English). Identical to Core's BannerText in English.
    /// </summary>
    public string BannerText => ResolveBannerText();

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
            _result.Checks.Select(c => new BuyerCheckRowViewModel(c, _languageCode)));
        HasChecks = Checks.Count > 0;
        Summary = BuildSummary(_result);

        OnPropertyChanged(nameof(Checks));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(BannerText));
        OnPropertyChanged(nameof(OverallState));
        _onChecksChanged?.Invoke(_result, value);
    }

    private string ResolveBannerText()
    {
        var finding = StrongestFindingTitle();
        if (finding is null)
        {
            return OverallState switch
            {
                BuyerCheckState.Pass => StringLocalizer.Get(AppStringKeys.NewLaptopCheckBannerPass, _languageCode),
                BuyerCheckState.Warning => StringLocalizer.Get(AppStringKeys.NewLaptopCheckBannerWarning, _languageCode),
                BuyerCheckState.Fail => StringLocalizer.Get(AppStringKeys.NewLaptopCheckBannerFail, _languageCode),
                _ => StringLocalizer.Get(AppStringKeys.NewLaptopCheckBannerNotEnoughData, _languageCode),
            };
        }

        // The banner names the strongest finding, so the buyer sees why
        // the verdict dropped without hunting through the checklist.
        // Only the state word translates; the finding title is data.
        var stateText = OverallState switch
        {
            BuyerCheckState.Pass => StringLocalizer.Get(AppStringKeys.NewLaptopCheckBannerStatePass, _languageCode),
            BuyerCheckState.Warning => StringLocalizer.Get(AppStringKeys.NewLaptopCheckBannerStateWarning, _languageCode),
            BuyerCheckState.Fail => StringLocalizer.Get(AppStringKeys.NewLaptopCheckBannerStateFail, _languageCode),
            _ => StringLocalizer.Get(AppStringKeys.NewLaptopCheckBannerStateNotEnoughData, _languageCode),
        };

        return $"{stateText} - {finding}";
    }

    /// <summary>
    /// The title of the strongest Fail findings entry, or null when no
    /// finding failed. Shared from Core, not mirrored here: the
    /// ViewModel calls <see cref="BuyerCheckResult.StrongestFailingFinding"/>
    /// so the ranking can never drift from the banner Core builds.
    /// </summary>
    private string? StrongestFindingTitle() => _result.StrongestFailingFinding()?.Title;

    private static string BuildSummary(BuyerCheckResult result) =>
        $"{result.SummaryText}.";
}

public sealed class BuyerCheckRowViewModel : ViewModelBase
{
    private string _languageCode;

    public BuyerCheckRowViewModel(BuyerCheck check, string? languageCode = null)
    {
        Title = check.Title;
        Detail = check.Detail;
        WhatToAsk = check.WhatToAsk;
        State = check.State;
        Status = check.ToHealthStatus();
        NeedsAttention = check.State is BuyerCheckState.Fail or BuyerCheckState.Warning;
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
    }

    public string Title { get; }

    public string Detail { get; }

    public string WhatToAsk { get; }

    public BuyerCheckState State { get; }

    /// <summary>The fixed state word (Pass / Warning / Fail / Not applicable), in the page language, re-resolved from the stored state.</summary>
    public string StateText => State switch
    {
        BuyerCheckState.Pass => StringLocalizer.Get(AppStringKeys.BuyerCheckStatePass, _languageCode),
        BuyerCheckState.Warning => StringLocalizer.Get(AppStringKeys.BuyerCheckStateWarning, _languageCode),
        BuyerCheckState.Fail => StringLocalizer.Get(AppStringKeys.BuyerCheckStateFail, _languageCode),
        _ => StringLocalizer.Get(AppStringKeys.BuyerCheckStateNotApplicable, _languageCode),
    };

    /// <summary>The "What to ask the seller" label, in the page language, re-resolved from the stored language code.</summary>
    public string WhatToAskLabelText =>
        StringLocalizer.Get(AppStringKeys.NewLaptopCheckWhatToAsk, _languageCode);

    public HealthStatus Status { get; }

    public bool NeedsAttention { get; }

    /// <summary>Re-renders this row in the given language, in place: the state word is re-resolved from the stored state.</summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(WhatToAskLabelText));
    }
}

/// <summary>
/// One fired U1 authenticity check: the rule title, the plain-English
/// evidence line, and the two sources the rule compared. Severity maps to
/// the shared health colours (Strong = Problem, Watch = Watch, Info =
/// Healthy) so the view reuses the existing Status brushes, no new tokens.
/// </summary>
public sealed class AuthenticityCheckRowViewModel : ViewModelBase
{
    private string _languageCode;

    public AuthenticityCheckRowViewModel(AuthenticityCheck check, string? languageCode = null)
    {
        Title = check.Title;
        EvidenceLine = check.EvidenceLine;
        Severity = check.Severity;
        SourceA = check.SourceA;
        SourceB = check.SourceB;
        Status = check.Severity switch
        {
            AuthenticitySeverity.Strong => HealthStatus.Problem,
            AuthenticitySeverity.Watch => HealthStatus.Watch,
            _ => HealthStatus.Healthy,
        };
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
    }

    public string Title { get; }

    public string EvidenceLine { get; }

    public AuthenticitySeverity Severity { get; }

    /// <summary>The fixed severity word (Strong / Watch / Info), in the page language, re-resolved from the stored severity.</summary>
    public string SeverityText => Severity switch
    {
        AuthenticitySeverity.Strong => StringLocalizer.Get(AppStringKeys.AuthenticitySeverityStrong, _languageCode),
        AuthenticitySeverity.Watch => StringLocalizer.Get(AppStringKeys.AuthenticitySeverityWatch, _languageCode),
        _ => StringLocalizer.Get(AppStringKeys.AuthenticitySeverityInfo, _languageCode),
    };

    public string SourceA { get; }

    public string SourceB { get; }

    /// <summary>The first source, with its fixed "Source 1:" label in the page language; the source string itself is data and stays English.</summary>
    public string SourceAText =>
        StringLocalizer.GetFormatted(AppStringKeys.NewLaptopCheckSourceOne, _languageCode, SourceA);

    /// <summary>The second source, with its fixed "Source 2:" label in the page language; the source string itself is data and stays English.</summary>
    public string SourceBText =>
        StringLocalizer.GetFormatted(AppStringKeys.NewLaptopCheckSourceTwo, _languageCode, SourceB);

    public HealthStatus Status { get; }

    /// <summary>Re-renders this row in the given language, in place: the severity word and the source labels are re-resolved from the stored values.</summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        OnPropertyChanged(nameof(SeverityText));
        OnPropertyChanged(nameof(SourceAText));
        OnPropertyChanged(nameof(SourceBText));
    }
}
