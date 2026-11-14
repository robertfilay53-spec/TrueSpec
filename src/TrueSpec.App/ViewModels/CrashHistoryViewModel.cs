using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Localization;
using TrueSpec.Core.Models;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// U3 Crash history page (Blueprint 3): one table row per failure class
/// (count in the last 90 days, first, last, plain meaning), an alert card
/// when a blue screen or fatal hardware error occurred in the last 30 days,
/// and a cleared-log warning when the log has a gap. Reads the crash
/// component the collector wrote; it never re-reads the event log and never
/// diagnoses beyond the fixed plain-meaning table.
///
/// Localisation, phase 8 (v2.1.16): the page chrome renders in the
/// current language — the title, the recent-crash heading, the three
/// fixed alert sentences, the "Failures by class" heading, the table
/// headers, the two fixed empty-state lines, the cleared-log warning
/// template and the footer note — so the page follows the language
/// the user picked in Settings, with no restart. The data the page
/// shows stays English: the component headline, the empty message
/// when it falls back to the component's own "What this means" text,
/// the class names, the counts and dates, the plain meanings and the
/// "Not reported" date fallback (a measured-data fallback, like the
/// History table's). The captured line reuses the detail page's
/// "Reading captured {0}" template instead of a second copy. The
/// page is cached by the shell, so the strings resolve from the
/// stored language code and re-render through
/// <see cref="ApplyLanguage"/>, the same in-place pattern the
/// Overview and History pages use.
/// </summary>
public sealed class CrashHistoryViewModel : ViewModelBase
{
    private string _languageCode;

    /// <summary>The component headline, or null when the component is missing: the headline line then falls back to the page title.</summary>
    private readonly string? _headline;

    /// <summary>The captured date, already formatted, or null when the component is missing: the captured line is then empty.</summary>
    private readonly string? _capturedAtText;

    /// <summary>Whether a blue screen occurred in the last 30 days, stored so a switch re-resolves the alert card instead of resetting it.</summary>
    private readonly bool _recentBugCheck;

    /// <summary>Whether a fatal hardware error occurred in the last 30 days, stored so a switch re-resolves the alert card instead of resetting it.</summary>
    private readonly bool _recentWheaFatal;

    /// <summary>The component's own "What this means" text, or null: the empty message falls back to it verbatim (data) when there is no history.</summary>
    private readonly string? _emptyWhatThisMeans;

    /// <summary>The earliest log entry date, already formatted, or null when the log does not look cleared.</summary>
    private readonly string? _earliestLogDateText;

    public CrashHistoryViewModel(SystemSnapshot snapshot, string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;

        var component = snapshot.Find("crash");
        _headline = component?.Headline;
        _capturedAtText = component is null
            ? null
            : component.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var history = CrashHistoryParser.FromComponent(component);
        if (history is null)
        {
            HasHistory = false;
            _emptyWhatThisMeans = component?.WhatThisMeans;
            Rows = new ObservableCollection<CrashClassRowViewModel>();
            HasRows = false;
            _recentBugCheck = false;
            _recentWheaFatal = false;
            _earliestLogDateText = null;
            return;
        }

        HasHistory = true;
        _emptyWhatThisMeans = null;
        Rows = new ObservableCollection<CrashClassRowViewModel>(
            history.Summaries.Select(s => new CrashClassRowViewModel(s)));
        HasRows = Rows.Count > 0;

        var now = DateTimeOffset.UtcNow;
        _recentBugCheck = CrashHistoryParser.HasRecentEvent(history, CrashClass.BugCheck, now);
        _recentWheaFatal = CrashHistoryParser.HasRecentEvent(history, CrashClass.WheaFatal, now);

        _earliestLogDateText = history is { LogClearedSuspected: true, EarliestLogEntry: { } earliest }
            ? earliest.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>The page title, in the page language.</summary>
    public string CrashTitleText =>
        StringLocalizer.Get(AppStringKeys.CrashTitle, _languageCode);

    /// <summary>
    /// The component headline (data, stays English), or the page title
    /// when the component is missing: the headline line then reads the
    /// same words the title above it already shows, translated.
    /// </summary>
    public string Headline => _headline ?? CrashTitleText;

    /// <summary>
    /// The captured line, in the page language. Reuses the detail
    /// page's "Reading captured {0}" template; the date inside keeps
    /// the app's existing formatting. Empty when no component was read.
    /// </summary>
    public string CapturedText => _capturedAtText is null
        ? string.Empty
        : StringLocalizer.GetFormatted(AppStringKeys.DetailCaptured, _languageCode, _capturedAtText);

    /// <summary>The "Recent crash" alert card heading, in the page language.</summary>
    public string RecentCrashTitleText =>
        StringLocalizer.Get(AppStringKeys.CrashRecentTitle, _languageCode);

    /// <summary>The "Failures by class" table heading, in the page language.</summary>
    public string FailuresByClassText =>
        StringLocalizer.Get(AppStringKeys.CrashFailuresByClass, _languageCode);

    /// <summary>The "Class" table header, in the page language.</summary>
    public string HeaderClassText =>
        StringLocalizer.Get(AppStringKeys.CrashHeaderClass, _languageCode);

    /// <summary>The "Count (90 days)" table header, in the page language.</summary>
    public string HeaderCountText =>
        StringLocalizer.Get(AppStringKeys.CrashHeaderCount, _languageCode);

    /// <summary>The "First" table header, in the page language.</summary>
    public string HeaderFirstText =>
        StringLocalizer.Get(AppStringKeys.CrashHeaderFirst, _languageCode);

    /// <summary>The "Last" table header, in the page language.</summary>
    public string HeaderLastText =>
        StringLocalizer.Get(AppStringKeys.CrashHeaderLast, _languageCode);

    /// <summary>The "Meaning" table header, in the page language.</summary>
    public string HeaderMeaningText =>
        StringLocalizer.Get(AppStringKeys.CrashHeaderMeaning, _languageCode);

    /// <summary>The footer note under the table, in the page language.</summary>
    public string FooterNoteText =>
        StringLocalizer.Get(AppStringKeys.CrashFooterNote, _languageCode);

    /// <summary>
    /// Re-renders this page in the given language, in place: every
    /// localized text is re-resolved from the stored state. Called by
    /// the shell when the user picks a language in Settings; a page
    /// created after the switch is already built in the new language
    /// by the constructor. An unknown code falls back to English
    /// through the localizer, never to a blank. Measured data (the
    /// headline, the "What this means" fallback, class names, counts,
    /// dates, plain meanings, the date inside the captured line and
    /// the cleared-log warning) is untouched.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        OnPropertyChanged(nameof(CrashTitleText));
        OnPropertyChanged(nameof(Headline));
        OnPropertyChanged(nameof(CapturedText));
        OnPropertyChanged(nameof(RecentCrashTitleText));
        OnPropertyChanged(nameof(FailuresByClassText));
        OnPropertyChanged(nameof(HeaderClassText));
        OnPropertyChanged(nameof(HeaderCountText));
        OnPropertyChanged(nameof(HeaderFirstText));
        OnPropertyChanged(nameof(HeaderLastText));
        OnPropertyChanged(nameof(HeaderMeaningText));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(AlertText));
        OnPropertyChanged(nameof(LogClearedText));
        OnPropertyChanged(nameof(FooterNoteText));
    }

    /// <summary>False when the crash component is missing or unreadable: the view shows the empty message instead of a table.</summary>
    public bool HasHistory { get; }

    /// <summary>
    /// Explains the empty table: no history, or no crashes recorded.
    /// The two fixed lines are in the page language; when there is no
    /// history and the component wrote its own "What this means", that
    /// data text shows verbatim instead.
    /// </summary>
    public string EmptyMessage
    {
        get
        {
            if (HasHistory)
            {
                return HasRows
                    ? string.Empty
                    : StringLocalizer.Get(AppStringKeys.CrashEmptyNoCrashes, _languageCode);
            }

            return _emptyWhatThisMeans
                ?? StringLocalizer.Get(AppStringKeys.CrashEmptyNotCollected, _languageCode);
        }
    }

    public ObservableCollection<CrashClassRowViewModel> Rows { get; }

    /// <summary>False when no failure class was recorded: the view shows the "no crashes" line instead of an empty table.</summary>
    public bool HasRows { get; }

    /// <summary>True when a blue screen or fatal hardware error occurred in the last 30 days.</summary>
    public bool HasRecentAlert => _recentBugCheck || _recentWheaFatal;

    /// <summary>
    /// One plain sentence for the alert card, or empty when there is
    /// no alert. One of three fixed sentences, in the page language,
    /// re-resolved from the stored recent-event flags.
    /// </summary>
    public string AlertText => (_recentBugCheck, _recentWheaFatal) switch
    {
        (true, true) => StringLocalizer.Get(AppStringKeys.CrashAlertBoth, _languageCode),
        (true, false) => StringLocalizer.Get(AppStringKeys.CrashAlertBugCheck, _languageCode),
        (false, true) => StringLocalizer.Get(AppStringKeys.CrashAlertWheaFatal, _languageCode),
        _ => string.Empty,
    };

    /// <summary>
    /// Warns that the log may have been cleared, or empty when the log
    /// looks complete. The sentence is in the page language; the date
    /// inside keeps the app's existing formatting.
    /// </summary>
    public string LogClearedText => _earliestLogDateText is null
        ? string.Empty
        : StringLocalizer.GetFormatted(AppStringKeys.CrashLogCleared, _languageCode, _earliestLogDateText);

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
