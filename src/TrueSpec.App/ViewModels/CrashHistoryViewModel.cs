using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// U3 Crash history page (Blueprint 3): one table row per failure class
/// (count in the last 90 days, first, last, plain meaning), an alert card
/// when a blue screen or fatal hardware error occurred in the last 30 days,
/// and a cleared-log warning when the log has a gap. Reads the crash
/// component the collector wrote; it never re-reads the event log and never
/// diagnoses beyond the fixed plain-meaning table.
/// </summary>
public sealed class CrashHistoryViewModel : ViewModelBase
{
    public CrashHistoryViewModel(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var component = snapshot.Find("crash");
        Headline = component?.Headline ?? "Crash history";
        CapturedText = component is null
            ? string.Empty
            : $"Reading captured {component.CapturedAt:yyyy-MM-dd HH:mm:ss}";

        var history = CrashHistoryParser.FromComponent(component);
        if (history is null)
        {
            HasHistory = false;
            EmptyMessage = component?.WhatThisMeans
                ?? "Crash history has not been collected yet.";
            Rows = new ObservableCollection<CrashClassRowViewModel>();
            HasRows = false;
            HasRecentAlert = false;
            AlertText = string.Empty;
            LogClearedText = string.Empty;
            return;
        }

        HasHistory = true;
        Rows = new ObservableCollection<CrashClassRowViewModel>(
            history.Summaries.Select(s => new CrashClassRowViewModel(s)));
        HasRows = Rows.Count > 0;
        EmptyMessage = HasRows
            ? string.Empty
            : "No blue screens, hardware errors, unexpected shutdowns or disk errors were recorded in the last 90 days.";

        var now = DateTimeOffset.UtcNow;
        var recentBugCheck = CrashHistoryParser.HasRecentEvent(history, CrashClass.BugCheck, now);
        var recentWheaFatal = CrashHistoryParser.HasRecentEvent(history, CrashClass.WheaFatal, now);
        HasRecentAlert = recentBugCheck || recentWheaFatal;
        AlertText = (recentBugCheck, recentWheaFatal) switch
        {
            (true, true) => "A blue screen and a fatal hardware error occurred in the last 30 days.",
            (true, false) => "A blue screen occurred in the last 30 days.",
            (false, true) => "A fatal hardware error occurred in the last 30 days.",
            _ => string.Empty,
        };

        LogClearedText = history is { LogClearedSuspected: true, EarliestLogEntry: { } earliest }
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"The event log has no entries before {earliest:yyyy-MM-dd}, so this history may be incomplete. A cleared log reads as a gap, never as a clean record.")
            : string.Empty;
    }

    public string Headline { get; }

    public string CapturedText { get; }

    /// <summary>False when the crash component is missing or unreadable: the view shows the empty message instead of a table.</summary>
    public bool HasHistory { get; }

    /// <summary>Explains the empty table: no history, or no crashes recorded.</summary>
    public string EmptyMessage { get; }

    public ObservableCollection<CrashClassRowViewModel> Rows { get; }

    /// <summary>False when no failure class was recorded: the view shows the "no crashes" line instead of an empty table.</summary>
    public bool HasRows { get; }

    /// <summary>True when a blue screen or fatal hardware error occurred in the last 30 days.</summary>
    public bool HasRecentAlert { get; }

    /// <summary>One plain sentence for the alert card, or empty when there is no alert.</summary>
    public string AlertText { get; }

    /// <summary>Warns that the log may have been cleared, or empty when the log looks complete.</summary>
    public string LogClearedText { get; }

    /// <summary>True when the cleared-log warning should show.</summary>
    public bool HasLogClearedText => LogClearedText.Length > 0;
}

/// <summary>One row of the crash table: class, 90-day count, first, last, fixed plain meaning.</summary>
public sealed class CrashClassRowViewModel
{
    public CrashClassRowViewModel(CrashClassSummary summary)
    {
        ClassName = CrashHistoryParser.DisplayName(summary.Class);
        Count90d = summary.Count90d;
        FirstText = FormatDate(summary.FirstAt);
        LastText = FormatDate(summary.LastAt);
        Meaning = Capitalize(summary.PlainMeaning);
    }

    public string ClassName { get; }

    public int Count90d { get; }

    public string FirstText { get; }

    public string LastText { get; }

    public string Meaning { get; }

    private static string FormatDate(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        ?? "Not reported";

    private static string Capitalize(string text) =>
        text.Length == 0
            ? text
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{char.ToUpper(text[0], CultureInfo.InvariantCulture)}{text[1..]}");
}
