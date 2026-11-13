using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.App.Services;
using TrueSpec.Core.Comparison;
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
/// </summary>
public sealed partial class ComparisonViewModel : ViewModelBase
{
    private readonly ISnapshotStore? _store;
    private readonly SystemSnapshot _current;
    private readonly IMachineDnaVerifier _dnaVerifier;

    /// <param name="dnaVerifier">The DNA comparer. Defaults to the real verifier over the snapshots' stored DNA; a snapshot saved before DNA existed honestly reports "not recorded".</param>
    public ComparisonViewModel(ISnapshotStore? store, SystemSnapshot current, IMachineDnaVerifier? dnaVerifier = null)
    {
        _store = store;
        _current = current;
        _dnaVerifier = dnaVerifier ?? MachineDnaVerifier.Instance;
        _ = LoadHistoryAsync();
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
    public partial string StatusMessage { get; set; } = "Loading snapshot history...";

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
    public partial string DnaStateText { get; set; } = "No DNA comparison yet. Pick two snapshots and compare.";

    /// <summary>Truncated DNA hash of the "before" snapshot, or "DNA not recorded".</summary>
    [ObservableProperty]
    public partial string DnaHashBeforeText { get; set; } = "DNA not recorded";

    /// <summary>Truncated DNA hash of the "after" snapshot, or "DNA not recorded".</summary>
    [ObservableProperty]
    public partial string DnaHashAfterText { get; set; } = "DNA not recorded";

    /// <summary>Changed parts between the two snapshots, one line each; empty when identical or not recorded.</summary>
    public ObservableCollection<string> DnaChangedParts { get; private set; } = new();

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
            StatusMessage = "Pick two different snapshots to compare.";
            HasComparison = false;
            HasChanges = false;
            HasDnaComparison = false;
            DnaStateText = "No DNA comparison yet. Pick two snapshots and compare.";
            return;
        }

        StatusMessage = "Comparing snapshots...";
        IsComparing = true;
        try
        {
            var before = await _store.GetSnapshotAsync(SelectedBefore.Id);
            var after = await _store.GetSnapshotAsync(SelectedAfter.Id);
            if (before is null || after is null)
            {
                StatusMessage = "One of the snapshots could not be read from history.";
                HasComparison = false;
                HasChanges = false;
                HasDnaComparison = false;
                DnaStateText = "DNA could not be compared: the snapshots could not be read.";
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
            StatusMessage = $"Compared {before.CapturedAt:yyyy-MM-dd HH:mm} with {after.CapturedAt:yyyy-MM-dd HH:mm}.";

            // DNA row (v2.0 U7): identity, not health. A snapshot with no
            // DNA reads as "not recorded", never as a mismatch, and the
            // row never feeds the score or the summary above.
            var dna = _dnaVerifier.Compare(before, after);
            DnaStateText = dna.State switch
            {
                DnaVerificationState.Identical => "Same machine, same parts.",
                DnaVerificationState.Changed => "Parts changed between these two scans.",
                DnaVerificationState.Unverifiable => "DNA could not be verified for these scans.",
                _ => "DNA not recorded for one or both snapshots.",
            };
            DnaHashBeforeText = "Before: " + (_dnaVerifier.Describe(before).HashDisplay ?? "DNA not recorded");
            DnaHashAfterText = "After: " + (_dnaVerifier.Describe(after).HashDisplay ?? "DNA not recorded");
            DnaChangedParts = new ObservableCollection<string>(dna.ChangedParts.Select(p =>
                $"{p.Kind} {p.Label}: {p.Detail}"));
            OnPropertyChanged(nameof(DnaChangedParts));
            HasDnaComparison = true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"Comparison failed: {exception.Message}";
            HasComparison = false;
            HasChanges = false;
            HasDnaComparison = false;
            DnaStateText = "DNA could not be compared: the snapshots could not be read.";
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
            StatusMessage = "Snapshot history is unavailable on this machine, so there is nothing to compare yet.";
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
                StatusMessage = "No snapshots are saved yet. Snapshots are saved after every scan, so refresh the scan and come back to compare.";
                return;
            }

            if (History.Count < 2)
            {
                StatusMessage = "Only one snapshot is saved so far. Run another scan (refresh) and come back to compare.";
                return;
            }

            SelectedBefore = History[1];
            SelectedAfter = History[0];
            StatusMessage = $"{History.Count} snapshots in history. Pick two and compare.";
            await CompareAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"History could not be read: {exception.Message}";
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
