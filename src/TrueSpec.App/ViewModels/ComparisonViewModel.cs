using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.App.Services;
using TrueSpec.Core.Comparison;
using TrueSpec.Core.Localization;
using TrueSpec.Core.Models;
using TrueSpec.Core.Storage;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// Compare two snapshots side by side: score delta, per-component health
/// changes and every specification that changed. History comes from the
/// local SQLite store; when there are fewer than two saved snapshots the
/// page says so instead of inventing a comparison. The DNA row at the top
/// of the result says whether the two scans are the same machine with the
/// same parts; DNA is identity, not health, so it never changes the score.
///
/// Localisation, phase 8 (v2.1.16): the page chrome renders in the
/// current language — the eyebrow, title, intro, Before/After labels
/// and picker placeholders, the Compare button, the Result heading,
/// the DNA note, the no-changes line, the Added/Removed headings and
/// the footer note, plus the transient status lines and the DNA
/// state/hash lines — so the page follows the language the user
/// picked in Settings, with no restart. The DNA heading and the
/// unidentified-parts heading reuse the Report page's keys instead of
/// second copies. The data the page shows stays English: the
/// comparison summary sentence, the row cell values (component names,
/// health numbers, delta words, changed-spec lines), the DNA
/// changed/unidentified part lines, file paths and exception messages.
/// The page is cached by the shell, so the strings resolve from the
/// stored language code and re-render through
/// <see cref="ApplyLanguage"/>, the same in-place pattern the
/// Overview and History pages use.
/// </summary>
public sealed partial class ComparisonViewModel : ViewModelBase
{
    private readonly ISnapshotStore? _store;
    private readonly SystemSnapshot _current;
    private readonly IMachineDnaVerifier _dnaVerifier;

    private string _languageCode;

    /// <summary>Which transient status line is showing, so ApplyLanguage can re-resolve it instead of resetting it.</summary>
    private ComparisonStatusKind _statusKind = ComparisonStatusKind.Loading;

    /// <summary>The snapshot count of a snapshots-count status.</summary>
    private int _statusCount;

    /// <summary>The exception message of a failed status, or null when the current status carries none.</summary>
    private string? _statusError;

    /// <summary>The "before" scan date of a compared status, already formatted, or null when the current status carries none.</summary>
    private string? _comparedBefore;

    /// <summary>The "after" scan date of a compared status, already formatted, or null when the current status carries none.</summary>
    private string? _comparedAfter;

    /// <summary>Which DNA state line is showing, so ApplyLanguage can re-resolve it instead of resetting it.</summary>
    private ComparisonDnaKind _dnaKind = ComparisonDnaKind.NoComparison;

    /// <summary>The truncated DNA hash of the "before" snapshot, or null when it was not recorded.</summary>
    private string? _dnaHashBefore;

    /// <summary>The truncated DNA hash of the "after" snapshot, or null when it was not recorded.</summary>
    private string? _dnaHashAfter;

    /// <summary>The transient status lines the Comparison page can show. Stored so a language switch re-resolves the line instead of resetting it.</summary>
    private enum ComparisonStatusKind
    {
        Loading,
        HistoryUnavailable,
        NoSnapshots,
        OnlyOneSnapshot,
        SnapshotsCount,
        PickDifferent,
        Comparing,
        SnapshotUnreadable,
        DnaUnreadable,
        Compared,
        Failed,
        HistoryReadFailed,
    }

    /// <summary>The DNA state lines the Comparison page can show. Stored so a language switch re-resolves the line instead of resetting it.</summary>
    private enum ComparisonDnaKind
    {
        NoComparison,
        Identical,
        FingerprintMatches,
        Changed,
        Unverifiable,
        NotRecorded,
        Unreadable,
    }

    /// <param name="dnaVerifier">The DNA comparer. Defaults to the real verifier over the snapshots' stored DNA; a snapshot saved before DNA existed honestly reports "not recorded".</param>
    public ComparisonViewModel(ISnapshotStore? store, SystemSnapshot current, IMachineDnaVerifier? dnaVerifier = null, string? languageCode = null)
    {
        _store = store;
        _current = current;
        _dnaVerifier = dnaVerifier ?? MachineDnaVerifier.Instance;
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        SetStatus(ComparisonStatusKind.Loading);
        ResolveDnaTexts();
        _ = LoadHistoryAsync();
    }

    /// <summary>The page eyebrow, in the page language.</summary>
    public string ComparisonEyebrowText =>
        StringLocalizer.Get(AppStringKeys.ComparisonEyebrow, _languageCode);

    /// <summary>The page title, in the page language.</summary>
    public string ComparisonTitleText =>
        StringLocalizer.Get(AppStringKeys.ComparisonTitle, _languageCode);

    /// <summary>The page intro, in the page language.</summary>
    public string ComparisonIntroText =>
        StringLocalizer.Get(AppStringKeys.ComparisonIntro, _languageCode);

    /// <summary>The "Before" picker label, in the page language.</summary>
    public string ComparisonBeforeText =>
        StringLocalizer.Get(AppStringKeys.ComparisonBefore, _languageCode);

    /// <summary>The "After" picker label, in the page language.</summary>
    public string ComparisonAfterText =>
        StringLocalizer.Get(AppStringKeys.ComparisonAfter, _languageCode);

    /// <summary>The earlier-snapshot picker placeholder, in the page language.</summary>
    public string ComparisonPickEarlierText =>
        StringLocalizer.Get(AppStringKeys.ComparisonPickEarlier, _languageCode);

    /// <summary>The later-snapshot picker placeholder, in the page language.</summary>
    public string ComparisonPickLaterText =>
        StringLocalizer.Get(AppStringKeys.ComparisonPickLater, _languageCode);

    /// <summary>The Compare button label, in the page language.</summary>
    public string ComparisonCompareButtonText =>
        StringLocalizer.Get(AppStringKeys.ComparisonCompareButton, _languageCode);

    /// <summary>The "Result" heading, in the page language.</summary>
    public string ComparisonResultTitleText =>
        StringLocalizer.Get(AppStringKeys.ComparisonResultTitle, _languageCode);

    /// <summary>
    /// The "Machine DNA" heading, in the page language. Reuses the
    /// Report page's key instead of a second copy of the same words.
    /// </summary>
    public string ComparisonDnaTitleText =>
        StringLocalizer.Get(AppStringKeys.ReportDnaTitle, _languageCode);

    /// <summary>
    /// The "Not identified in one of the scans:" heading, in the page
    /// language. Reuses the Report page's key instead of a second copy
    /// of the same words.
    /// </summary>
    public string ComparisonDnaUnidentifiedTitleText =>
        StringLocalizer.Get(AppStringKeys.ReportDnaUnidentifiedTitle, _languageCode);

    /// <summary>The DNA explainer note, in the page language.</summary>
    public string ComparisonDnaNoteText =>
        StringLocalizer.Get(AppStringKeys.ComparisonDnaNote, _languageCode);

    /// <summary>The "no changes" line, in the page language.</summary>
    public string ComparisonNoChangesText =>
        StringLocalizer.Get(AppStringKeys.ComparisonNoChanges, _languageCode);

    /// <summary>The "Added components" heading, in the page language.</summary>
    public string ComparisonAddedTitleText =>
        StringLocalizer.Get(AppStringKeys.ComparisonAddedTitle, _languageCode);

    /// <summary>The "Removed components" heading, in the page language.</summary>
    public string ComparisonRemovedTitleText =>
        StringLocalizer.Get(AppStringKeys.ComparisonRemovedTitle, _languageCode);

    /// <summary>The footer note, in the page language.</summary>
    public string ComparisonFooterNoteText =>
        StringLocalizer.Get(AppStringKeys.ComparisonFooterNote, _languageCode);

    /// <summary>
    /// Re-renders this page in the given language, in place: every
    /// localized text is re-resolved from the stored state. Called by
    /// the shell when the user picks a language in Settings; a page
    /// created after the switch is already built in the new language
    /// by the constructor. An unknown code falls back to English
    /// through the localizer, never to a blank. Measured data (the
    /// comparison summary, the row cell values, the DNA changed and
    /// unidentified part lines, the dates inside the compared line)
    /// is untouched.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        OnPropertyChanged(nameof(ComparisonEyebrowText));
        OnPropertyChanged(nameof(ComparisonTitleText));
        OnPropertyChanged(nameof(ComparisonIntroText));
        OnPropertyChanged(nameof(ComparisonBeforeText));
        OnPropertyChanged(nameof(ComparisonAfterText));
        OnPropertyChanged(nameof(ComparisonPickEarlierText));
        OnPropertyChanged(nameof(ComparisonPickLaterText));
        OnPropertyChanged(nameof(ComparisonCompareButtonText));
        OnPropertyChanged(nameof(ComparisonResultTitleText));
        OnPropertyChanged(nameof(ComparisonDnaTitleText));
        OnPropertyChanged(nameof(ComparisonDnaUnidentifiedTitleText));
        OnPropertyChanged(nameof(ComparisonDnaNoteText));
        OnPropertyChanged(nameof(ComparisonNoChangesText));
        OnPropertyChanged(nameof(ComparisonAddedTitleText));
        OnPropertyChanged(nameof(ComparisonRemovedTitleText));
        OnPropertyChanged(nameof(ComparisonFooterNoteText));
        StatusMessage = ResolveStatusMessage();
        ResolveDnaTexts();
    }

    /// <summary>Records which status line is showing and renders it in the page language.</summary>
    private void SetStatus(ComparisonStatusKind kind, int count = 0, string? error = null, string? before = null, string? after = null)
    {
        _statusKind = kind;
        _statusCount = count;
        _statusError = error;
        _comparedBefore = before;
        _comparedAfter = after;
        StatusMessage = ResolveStatusMessage();
    }

    /// <summary>Resolves the stored status kind to its sentence in the page language.</summary>
    private string ResolveStatusMessage() => _statusKind switch
    {
        ComparisonStatusKind.Loading => StringLocalizer.Get(AppStringKeys.ComparisonLoading, _languageCode),
        ComparisonStatusKind.HistoryUnavailable => StringLocalizer.Get(AppStringKeys.ComparisonHistoryUnavailable, _languageCode),
        ComparisonStatusKind.NoSnapshots => StringLocalizer.Get(AppStringKeys.ComparisonNoSnapshots, _languageCode),
        ComparisonStatusKind.OnlyOneSnapshot => StringLocalizer.Get(AppStringKeys.ComparisonOnlyOneSnapshot, _languageCode),
        ComparisonStatusKind.SnapshotsCount => StringLocalizer.GetFormatted(
            AppStringKeys.ComparisonSnapshotsCount,
            _languageCode,
            _statusCount.ToString(CultureInfo.InvariantCulture)),
        ComparisonStatusKind.PickDifferent => StringLocalizer.Get(AppStringKeys.ComparisonPickDifferent, _languageCode),
        ComparisonStatusKind.Comparing => StringLocalizer.Get(AppStringKeys.ComparisonComparing, _languageCode),
        ComparisonStatusKind.SnapshotUnreadable => StringLocalizer.Get(AppStringKeys.ComparisonSnapshotUnreadable, _languageCode),
        ComparisonStatusKind.DnaUnreadable => StringLocalizer.Get(AppStringKeys.ComparisonDnaUnreadable, _languageCode),
        ComparisonStatusKind.Compared => StringLocalizer.GetFormatted(
            AppStringKeys.ComparisonCompared,
            _languageCode,
            _comparedBefore ?? string.Empty,
            _comparedAfter ?? string.Empty),
        ComparisonStatusKind.Failed => StringLocalizer.GetFormatted(AppStringKeys.ComparisonFailed, _languageCode, _statusError ?? string.Empty),
        ComparisonStatusKind.HistoryReadFailed => StringLocalizer.GetFormatted(AppStringKeys.ComparisonHistoryReadFailed, _languageCode, _statusError ?? string.Empty),
        _ => string.Empty,
    };

    /// <summary>Resolves the stored DNA state and hashes to their lines in the page language.</summary>
    private void ResolveDnaTexts()
    {
        ResolveDnaStateText();
        ResolveDnaHashTexts();
    }

    /// <summary>Resolves the stored DNA state to its line in the page language.</summary>
    private void ResolveDnaStateText()
    {
        DnaStateText = _dnaKind switch
        {
            ComparisonDnaKind.Identical => StringLocalizer.Get(AppStringKeys.ComparisonDnaIdentical, _languageCode),
            ComparisonDnaKind.FingerprintMatches => StringLocalizer.Get(AppStringKeys.ComparisonDnaFingerprintMatches, _languageCode),
            ComparisonDnaKind.Changed => StringLocalizer.Get(AppStringKeys.ComparisonDnaChanged, _languageCode),
            ComparisonDnaKind.Unverifiable => StringLocalizer.Get(AppStringKeys.ComparisonDnaUnverifiable, _languageCode),
            ComparisonDnaKind.NotRecorded => StringLocalizer.Get(AppStringKeys.ComparisonDnaNotRecordedState, _languageCode),
            ComparisonDnaKind.Unreadable => StringLocalizer.Get(AppStringKeys.ComparisonDnaUnreadable, _languageCode),
            _ => StringLocalizer.Get(AppStringKeys.ComparisonDnaNoComparison, _languageCode),
        };
    }

    /// <summary>Resolves the stored DNA hashes to their lines in the page language.</summary>
    private void ResolveDnaHashTexts()
    {
        DnaHashBeforeText = StringLocalizer.GetFormatted(
            AppStringKeys.ComparisonDnaHashBefore,
            _languageCode,
            _dnaHashBefore ?? StringLocalizer.Get(AppStringKeys.ComparisonDnaNotRecorded, _languageCode));
        DnaHashAfterText = StringLocalizer.GetFormatted(
            AppStringKeys.ComparisonDnaHashAfter,
            _languageCode,
            _dnaHashAfter ?? StringLocalizer.Get(AppStringKeys.ComparisonDnaNotRecorded, _languageCode));
    }

    /// <summary>Records the DNA state from a verification result and renders its lines in the page language.</summary>
    private void SetDnaState(DnaVerificationState state)
    {
        _dnaKind = state switch
        {
            DnaVerificationState.Identical => ComparisonDnaKind.Identical,
            DnaVerificationState.FingerprintMatches => ComparisonDnaKind.FingerprintMatches,
            DnaVerificationState.Changed => ComparisonDnaKind.Changed,
            DnaVerificationState.Unverifiable => ComparisonDnaKind.Unverifiable,
            _ => ComparisonDnaKind.NotRecorded,
        };
        ResolveDnaTexts();
    }

    public ObservableCollection<SnapshotSummary> History { get; } = new();

    public ObservableCollection<ComparisonRowViewModel> Rows { get; private set; } = new();

    public ObservableCollection<string> AddedComponents { get; private set; } = new();

    public ObservableCollection<string> RemovedComponents { get; private set; } = new();

    [ObservableProperty]
    public partial SnapshotSummary? SelectedBefore { get; set; }

    [ObservableProperty]
    public partial SnapshotSummary? SelectedAfter { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasComparison { get; set; }

    /// <summary>True when the compared snapshots differ anywhere; false shows a "no changes" line instead of an empty result card.</summary>
    [ObservableProperty]
    public partial bool HasChanges { get; set; }

    /// <summary>True while a comparison is running: the Compare button stays disabled so it cannot be double-run.</summary>
    [ObservableProperty]
    public partial bool IsComparing { get; set; }

    /// <summary>True once a comparison ran and the DNA row has something to say.</summary>
    [ObservableProperty]
    public partial bool HasDnaComparison { get; set; }

    /// <summary>One-line DNA verdict for the compared pair: identical, changed, unverifiable, or not recorded. Never a mismatch for a snapshot with no DNA.</summary>
    [ObservableProperty]
    public partial string DnaStateText { get; set; } = string.Empty;

    /// <summary>Truncated DNA hash of the "before" snapshot, or "DNA not recorded".</summary>
    [ObservableProperty]
    public partial string DnaHashBeforeText { get; set; } = string.Empty;

    /// <summary>Truncated DNA hash of the "after" snapshot, or "DNA not recorded".</summary>
    [ObservableProperty]
    public partial string DnaHashAfterText { get; set; } = string.Empty;

    /// <summary>Changed parts between the two snapshots, one line each; empty when identical or not recorded.</summary>
    public ObservableCollection<string> DnaChangedParts { get; private set; } = new();

    /// <summary>True when the last comparison named a part either scan could not identify.</summary>
    [ObservableProperty]
    public partial bool HasDnaUnidentifiedParts { get; set; }

    /// <summary>Parts either scan could not identify, one "Kind Label" line each; empty when every part was identified.</summary>
    public ObservableCollection<string> DnaUnidentifiedParts { get; private set; } = new();

    partial void OnSelectedBeforeChanged(SnapshotSummary? value) => CompareCommand.NotifyCanExecuteChanged();

    partial void OnSelectedAfterChanged(SnapshotSummary? value) => CompareCommand.NotifyCanExecuteChanged();

    partial void OnIsComparingChanged(bool value) => CompareCommand.NotifyCanExecuteChanged();

    private bool CanCompare => !IsComparing
        && SelectedBefore is not null
        && SelectedAfter is not null
        && SelectedBefore.Id != SelectedAfter.Id;

    [RelayCommand(CanExecute = nameof(CanCompare))]
    private async Task CompareAsync()
    {
        if (_store is null || SelectedBefore is null || SelectedAfter is null)
        {
            return;
        }

        if (SelectedBefore.Id == SelectedAfter.Id)
        {
            SetStatus(ComparisonStatusKind.PickDifferent);
            HasComparison = false;
            HasChanges = false;
            HasDnaComparison = false;
            HasDnaUnidentifiedParts = false;
            DnaUnidentifiedParts = new ObservableCollection<string>();
            OnPropertyChanged(nameof(DnaUnidentifiedParts));
            _dnaKind = ComparisonDnaKind.NoComparison;
            ResolveDnaStateText();
            return;
        }

        SetStatus(ComparisonStatusKind.Comparing);
        IsComparing = true;
        try
        {
            var before = await _store.GetSnapshotAsync(SelectedBefore.Id);
            var after = await _store.GetSnapshotAsync(SelectedAfter.Id);
            if (before is null || after is null)
            {
                SetStatus(ComparisonStatusKind.SnapshotUnreadable);
                HasComparison = false;
                HasChanges = false;
                HasDnaComparison = false;
                HasDnaUnidentifiedParts = false;
                DnaUnidentifiedParts = new ObservableCollection<string>();
                OnPropertyChanged(nameof(DnaUnidentifiedParts));
                _dnaKind = ComparisonDnaKind.Unreadable;
                ResolveDnaTexts();
                return;
            }

            var comparison = SnapshotComparer.Compare(before, after);
            SummaryText = comparison.Summary;
            Rows = new ObservableCollection<ComparisonRowViewModel>(
                comparison.ChangedComponents.Select(c => new ComparisonRowViewModel(c)));
            AddedComponents = new ObservableCollection<string>(comparison.AddedComponents);
            RemovedComponents = new ObservableCollection<string>(comparison.RemovedComponents);
            OnPropertyChanged(nameof(Rows));
            OnPropertyChanged(nameof(AddedComponents));
            OnPropertyChanged(nameof(RemovedComponents));
            HasComparison = true;
            HasChanges = Rows.Count > 0 || AddedComponents.Count > 0 || RemovedComponents.Count > 0;
            SetStatus(
                ComparisonStatusKind.Compared,
                before: before.CapturedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                after: after.CapturedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

            // DNA row (v2.0 U7): identity, not health. A snapshot with no
            // DNA reads as "not recorded", never as a mismatch, and the
            // row never feeds the score or the summary above.
            var dna = _dnaVerifier.Compare(before, after);
            _dnaHashBefore = _dnaVerifier.Describe(before).HashDisplay;
            _dnaHashAfter = _dnaVerifier.Describe(after).HashDisplay;
            SetDnaState(dna.State);
            DnaChangedParts = new ObservableCollection<string>(dna.ChangedParts.Select(p =>
                $"{p.Kind} {p.Label}: {p.Detail}"));
            OnPropertyChanged(nameof(DnaChangedParts));
            DnaUnidentifiedParts = new ObservableCollection<string>(dna.UnidentifiedParts);
            OnPropertyChanged(nameof(DnaUnidentifiedParts));
            HasDnaUnidentifiedParts = DnaUnidentifiedParts.Count > 0;
            HasDnaComparison = true;
        }
        catch (Exception exception)
        {
            SetStatus(ComparisonStatusKind.Failed, error: exception.Message);
            HasComparison = false;
            HasChanges = false;
            HasDnaComparison = false;
            _dnaKind = ComparisonDnaKind.Unreadable;
            ResolveDnaTexts();
        }
        finally
        {
            IsComparing = false;
        }
    }

    private async Task LoadHistoryAsync()
    {
        if (_store is null)
        {
            SetStatus(ComparisonStatusKind.HistoryUnavailable);
            return;
        }

        try
        {
            var history = await _store.GetHistoryAsync(50);
            History.Clear();
            foreach (var item in history)
            {
                History.Add(item);
            }

            if (History.Count == 0)
            {
                SetStatus(ComparisonStatusKind.NoSnapshots);
                return;
            }

            if (History.Count < 2)
            {
                SetStatus(ComparisonStatusKind.OnlyOneSnapshot);
                return;
            }

            SelectedBefore = History[1];
            SelectedAfter = History[0];
            SetStatus(ComparisonStatusKind.SnapshotsCount, count: History.Count);
            await CompareAsync();
        }
        catch (Exception exception)
        {
            SetStatus(ComparisonStatusKind.HistoryReadFailed, error: exception.Message);
        }
    }
}

public sealed class ComparisonRowViewModel
{
    public ComparisonRowViewModel(ComponentChange change)
    {
        ComponentName = change.ComponentName;
        HealthBefore = change.HealthBefore is null ? "Not measurable" : $"{change.HealthBefore:0.#}%";
        HealthAfter = change.HealthAfter is null ? "Not measurable" : $"{change.HealthAfter:0.#}%";
        DeltaText = change.HealthDelta is null
            ? "No health number"
            : change.HealthDelta.Value switch
            {
                > 0.05 => $"+{change.HealthDelta:0.#} pts",
                < -0.05 => $"{change.HealthDelta:0.#} pts",
                _ => "No change",
            };
        DeltaStatus = change.HealthDelta switch
        {
            > 0.05 => HealthStatus.Healthy,
            < -0.05 => HealthStatus.Problem,
            _ => HealthStatus.NotMeasurable,
        };
        ChangedSpecs = change.ChangedSpecs.Count == 0
            ? "Health only"
            : string.Join("; ", change.ChangedSpecs.Select(s =>
                s.Before is null ? $"{s.Label}: added ({s.After})"
                : s.After is null ? $"{s.Label}: removed (was {s.Before})"
                : $"{s.Label}: {s.Before} -> {s.After}"));
    }

    public string ComponentName { get; }

    public string HealthBefore { get; }

    public string HealthAfter { get; }

    public string DeltaText { get; }

    public HealthStatus DeltaStatus { get; }

    public string ChangedSpecs { get; }
}
