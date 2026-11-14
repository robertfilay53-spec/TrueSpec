using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Localization;
using TrueSpec.Core.Sensors;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// The "Load test" card on the Tests page (Blueprint 2): duration selector,
/// Start / Cancel, live temperature and clock readouts during the run, and a
/// result card with the throttle verdict and a temperature sparkline.
///
/// The warnings are shown before Start, never after the fact: the test loads
/// the CPU for the selected duration and stops automatically at the safety
/// limit, a short test is a functional check and not a burn-in certificate,
/// and a machine on battery may report power-limited results.
///
/// Localisation, phase 8 (v2.1.16): the card chrome renders in the
/// current language — the heading, intro, battery note, duration label,
/// Start/Cancel buttons, fan-health heading and note, temperature
/// caption, the two warning templates, the fixed status words and the
/// fixed live-line states ("Temperature: not running", "Clock:
/// measuring..."). The data the card shows stays English: the measured
/// result lines (peak temperature, average clock, rated max, held %),
/// the fan-health summary/lines/findings, the verdict text and the
/// live temperature/clock/elapsed values. The fixed status words are
/// re-derived from small stored fields (which status is showing, which
/// live-line state, the failure message), so a language switch
/// re-resolves them instead of silently resetting them. The card is
/// re-languaged in place through <see cref="ApplyLanguage"/> by the
/// Tests page, the same in-place pattern the Overview and History
/// pages use.
/// </summary>
public sealed partial class LoadTestViewModel : ViewModelBase
{
    private readonly ISensorMonitor _sensors;
    private readonly double? _ratedMaxMHz;
    private readonly Action<LoadTestResult>? _onCompleted;
    private readonly SynchronizationContext? _uiContext;

    private LoadTestRunner? _runner;
    private CancellationTokenSource? _cts;

    private string _languageCode;

    /// <summary>Which fixed status line is showing, so ApplyLanguage can re-resolve it instead of resetting it.</summary>
    private LoadTestStatusKind _statusKind = LoadTestStatusKind.NotRun;

    /// <summary>The exception message of a failed run, or null when the last run did not fail.</summary>
    private string? _failureMessage;

    /// <summary>The verdict text of the last completed run (measured data, shown verbatim), or null when no run completed.</summary>
    private string? _lastVerdictText;

    /// <summary>Which fixed state the live temperature line is in, so ApplyLanguage can re-resolve it. Measured keeps the English label and the invariant-formatted value.</summary>
    private LiveLineKind _liveTempKind = LiveLineKind.NotRunning;

    /// <summary>The invariant-formatted measured value for a Measured live temperature line (for example "72.3 C"), or null otherwise.</summary>
    private string? _liveTempMeasured;

    /// <summary>Which fixed state the live clock line is in, so ApplyLanguage can re-resolve it. Measured keeps the English label and the invariant-formatted value.</summary>
    private LiveLineKind _liveClockKind = LiveLineKind.NotRunning;

    /// <summary>The invariant-formatted measured value for a Measured live clock line (for example "4200 MHz"), or null otherwise.</summary>
    private string? _liveClockMeasured;

    /// <summary>The fixed status lines the load test can show. Stored so a language switch re-resolves the line instead of resetting it.</summary>
    private enum LoadTestStatusKind
    {
        NotRun,
        Running,
        StoppedTemperature,
        StoppedSensorLost,
        Cancelled,
        Complete,
        Failed,
    }

    /// <summary>The states of a live readout line. The four fixed states are chrome; Measured is a live value.</summary>
    private enum LiveLineKind
    {
        NotRunning,
        Measuring,
        Finished,
        NotReported,
        Measured,
    }

    public LoadTestViewModel(
        ISensorMonitor sensors,
        double? ratedMaxMHz,
        bool isOnBattery,
        Action<LoadTestResult>? onCompleted,
        string? languageCode = null)
    {
        _sensors = sensors;
        _ratedMaxMHz = ratedMaxMHz;
        _onCompleted = onCompleted;
        _uiContext = SynchronizationContext.Current;
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        IsOnBattery = isOnBattery;
        StatusText = ResolveStatusText();
        LiveTemperatureText = ResolveLiveTempText();
        LiveClockText = ResolveLiveClockText();
    }

    public bool IsOnBattery { get; }

    public IReadOnlyList<int> Durations { get; } = new[] { 30, 60, 120 };

    [ObservableProperty]
    public partial int SelectedDurationSeconds { get; set; } = 60;

    /// <summary>The card heading, in the card language.</summary>
    public string LoadTestTitleText =>
        StringLocalizer.Get(AppStringKeys.LoadTestTitle, _languageCode);

    /// <summary>The card introduction, in the card language.</summary>
    public string LoadTestIntroText =>
        StringLocalizer.Get(AppStringKeys.LoadTestIntro, _languageCode);

    /// <summary>The battery note, shown when the machine runs on battery, in the card language.</summary>
    public string LoadTestBatteryNoteText =>
        StringLocalizer.Get(AppStringKeys.LoadTestBatteryNote, _languageCode);

    /// <summary>The duration label, in the card language.</summary>
    public string LoadTestDurationLabelText =>
        StringLocalizer.Get(AppStringKeys.LoadTestDurationLabel, _languageCode);

    /// <summary>The Start button, in the card language.</summary>
    public string LoadTestStartText =>
        StringLocalizer.Get(AppStringKeys.LoadTestStart, _languageCode);

    /// <summary>The Cancel button, in the card language.</summary>
    public string LoadTestCancelText =>
        StringLocalizer.Get(AppStringKeys.LoadTestCancel, _languageCode);

    /// <summary>The fan-health heading, in the card language.</summary>
    public string LoadTestFanHealthTitleText =>
        StringLocalizer.Get(AppStringKeys.LoadTestFanHealthTitle, _languageCode);

    /// <summary>The fixed fan-health note, in the card language.</summary>
    public string LoadTestFanHealthNoteText =>
        StringLocalizer.Get(AppStringKeys.LoadTestFanHealthNote, _languageCode);

    /// <summary>The temperature chart caption, in the card language.</summary>
    public string LoadTestTemperatureCaptionText =>
        StringLocalizer.Get(AppStringKeys.LoadTestTemperatureCaption, _languageCode);

    /// <summary>
    /// Re-renders this card in the given language, in place: every
    /// localized text is re-resolved from the stored state. Called by
    /// the Tests page when the user picks a language in Settings; a
    /// card created after the switch is already built in the new
    /// language by the constructor. An unknown code falls back to
    /// English through the localizer, never to a blank. Measured data
    /// (result lines, fan health, verdict text, live values) is
    /// untouched.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        StatusText = ResolveStatusText();
        LiveTemperatureText = ResolveLiveTempText();
        LiveClockText = ResolveLiveClockText();
        if (_statusKind == LoadTestStatusKind.Failed)
        {
            VerdictText = StringLocalizer.GetFormatted(AppStringKeys.TestsItemNoResult, _languageCode, _failureMessage ?? string.Empty);
        }
        else if (_lastVerdictText is not null)
        {
            // A completed run keeps its measured verdict verbatim.
            VerdictText = _lastVerdictText;
        }

        OnPropertyChanged(nameof(WarningText));
        OnPropertyChanged(nameof(FunctionalCheckText));
        OnPropertyChanged(nameof(LoadTestTitleText));
        OnPropertyChanged(nameof(LoadTestIntroText));
        OnPropertyChanged(nameof(LoadTestBatteryNoteText));
        OnPropertyChanged(nameof(LoadTestDurationLabelText));
        OnPropertyChanged(nameof(LoadTestStartText));
        OnPropertyChanged(nameof(LoadTestCancelText));
        OnPropertyChanged(nameof(LoadTestFanHealthTitleText));
        OnPropertyChanged(nameof(LoadTestFanHealthNoteText));
        OnPropertyChanged(nameof(LoadTestTemperatureCaptionText));
    }

    /// <summary>The current status line, resolved in the card language from the stored status kind.</summary>
    private string ResolveStatusText() => _statusKind switch
    {
        LoadTestStatusKind.NotRun => StringLocalizer.Get(AppStringKeys.TestsItemNotRunYet, _languageCode),
        LoadTestStatusKind.Running => StringLocalizer.Get(AppStringKeys.TestsItemRunning, _languageCode),
        LoadTestStatusKind.StoppedTemperature => StringLocalizer.Get(AppStringKeys.LoadTestStoppedTemperature, _languageCode),
        LoadTestStatusKind.StoppedSensorLost => StringLocalizer.Get(AppStringKeys.LoadTestStoppedSensorLost, _languageCode),
        LoadTestStatusKind.Cancelled => StringLocalizer.Get(AppStringKeys.LoadTestCancelled, _languageCode),
        LoadTestStatusKind.Complete => StringLocalizer.Get(AppStringKeys.LoadTestComplete, _languageCode),
        LoadTestStatusKind.Failed => StringLocalizer.Get(AppStringKeys.TestsItemFailed, _languageCode),
        _ => StringLocalizer.Get(AppStringKeys.TestsItemNotRunYet, _languageCode),
    };

    /// <summary>The live temperature line, resolved in the card language. A measured value keeps the English label and the invariant-formatted value: it is a live value, not chrome.</summary>
    private string ResolveLiveTempText() => _liveTempKind switch
    {
        LiveLineKind.NotRunning => StringLocalizer.Get(AppStringKeys.LoadTestTempNotRunning, _languageCode),
        LiveLineKind.Measuring => StringLocalizer.Get(AppStringKeys.LoadTestTempMeasuring, _languageCode),
        LiveLineKind.Finished => StringLocalizer.Get(AppStringKeys.LoadTestTempFinished, _languageCode),
        LiveLineKind.NotReported => StringLocalizer.Get(AppStringKeys.LoadTestTempNotReported, _languageCode),
        LiveLineKind.Measured => $"Temperature: {_liveTempMeasured}",
        _ => StringLocalizer.Get(AppStringKeys.LoadTestTempNotRunning, _languageCode),
    };

    /// <summary>The live clock line, resolved in the card language. A measured value keeps the English label and the invariant-formatted value: it is a live value, not chrome.</summary>
    private string ResolveLiveClockText() => _liveClockKind switch
    {
        LiveLineKind.NotRunning => StringLocalizer.Get(AppStringKeys.LoadTestClockNotRunning, _languageCode),
        LiveLineKind.Measuring => StringLocalizer.Get(AppStringKeys.LoadTestClockMeasuring, _languageCode),
        LiveLineKind.Finished => StringLocalizer.Get(AppStringKeys.LoadTestClockFinished, _languageCode),
        LiveLineKind.NotReported => StringLocalizer.Get(AppStringKeys.LoadTestClockNotReported, _languageCode),
        LiveLineKind.Measured => $"Clock: {_liveClockMeasured}",
        _ => StringLocalizer.Get(AppStringKeys.LoadTestClockNotRunning, _languageCode),
    };

    /// <summary>
    /// The warning shown before Start; it names the selected duration and the
    /// safety limit from the one shared constant
    /// (<see cref="LoadTestConfig.DefaultAbortTemperatureC"/>), so the text
    /// can never disagree with the temperature at which the test aborts.
    /// The numbers keep the app's invariant formatting in every language.
    /// </summary>
    public string WarningText => StringLocalizer.GetFormatted(
        AppStringKeys.LoadTestWarning,
        _languageCode,
        SelectedDurationSeconds.ToString(CultureInfo.InvariantCulture),
        LoadTestConfig.DefaultAbortTemperatureC.ToString("0.#", CultureInfo.InvariantCulture));

    public string FunctionalCheckText => StringLocalizer.GetFormatted(
        AppStringKeys.LoadTestFunctionalCheck,
        _languageCode,
        SelectedDurationSeconds.ToString(CultureInfo.InvariantCulture));

    public bool CanStart => !IsRunning;

    public bool CanCancel => IsRunning;

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LiveTemperatureText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LiveClockText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial string VerdictText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultPeakText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultClockText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultPercentText { get; set; } = string.Empty;

    /// <summary>
    /// The rated maximum clock the test was judged against, shown on its
    /// own line: it is a known WMI value even when no clock was measured
    /// under load, so it must never be hidden behind the held-% line.
    /// </summary>
    [ObservableProperty]
    public partial string ResultRatedMaxText { get; set; } = string.Empty;

    /// <summary>U5: one line per reported fan ("CPU Fan #1: idle 1,200 RPM, under load 2,400 RPM (peak 82 C)"). Empty when no fan sensor reported.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> FanHealthLines { get; set; } = Array.Empty<string>();

    /// <summary>U5: one line per measured fan fault, or empty when no fault fired.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> FanHealthFindings { get; set; } = Array.Empty<string>();

    /// <summary>U5: the fan-health verdict line; the no-sensor empty state is part of this text.</summary>
    [ObservableProperty]
    public partial string FanHealthSummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<double> TemperatureValues { get; set; } = Array.Empty<double>();

    partial void OnSelectedDurationSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(WarningText));
        OnPropertyChanged(nameof(FunctionalCheckText));
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanCancel));
    }

    /// <summary>
    /// The rated-max line of the result card, on its own: the rated max is
    /// a known value from WMI even when the test measured no clock, so it
    /// is shown whenever it is known and never folded into the held-% line.
    /// </summary>
    public static string RatedMaxText(LoadTestResult result) =>
        result.RatedMaxMHz is { } rated
            ? string.Create(CultureInfo.InvariantCulture, $"Rated max clock: {rated:0} MHz")
            : "Rated max clock: not reported by hardware";

    /// <summary>
    /// The held-% line of the result card: only the measured percentage.
    /// When no clock was measured the line says so on its own, while the
    /// rated max above it still shows the known value.
    /// </summary>
    public static string HeldPercentText(LoadTestResult result) =>
        result.ClockHeldPercent is { } percent
            ? string.Create(CultureInfo.InvariantCulture, $"Held {percent:0}% of rated clock")
            : "Held % of rated clock: not reported by hardware";

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _runner = new LoadTestRunner(_sensors);
        IsRunning = true;
        HasResult = false;
        TemperatureValues = Array.Empty<double>();
        FanHealthLines = Array.Empty<string>();
        FanHealthFindings = Array.Empty<string>();
        FanHealthSummaryText = string.Empty;
        _statusKind = LoadTestStatusKind.Running;
        StatusText = ResolveStatusText();
        ElapsedText = "0 s";
        _liveTempKind = LiveLineKind.Measuring;
        LiveTemperatureText = ResolveLiveTempText();
        _liveClockKind = LiveLineKind.Measuring;
        LiveClockText = ResolveLiveClockText();
        VerdictText = string.Empty;

        try
        {
            var config = new LoadTestConfig(SelectedDurationSeconds, RatedMaxMHz: _ratedMaxMHz);
            var progress = new Progress<LoadTestProgress>(OnProgress);
            var result = await _runner.RunAsync(config, progress, _cts.Token);

            _statusKind = result.AbortReason switch
            {
                LoadTestResult.AbortTemperature => LoadTestStatusKind.StoppedTemperature,
                LoadTestResult.AbortSensorLost => LoadTestStatusKind.StoppedSensorLost,
                LoadTestResult.AbortCancelled => LoadTestStatusKind.Cancelled,
                _ => LoadTestStatusKind.Complete,
            };
            StatusText = ResolveStatusText();
            HasResult = true;
            _lastVerdictText = result.VerdictText;
            VerdictText = result.VerdictText;
            ResultPeakText = result.PeakTempC is { } peak
                ? string.Create(CultureInfo.InvariantCulture, $"Peak temperature: {peak:0.#} C")
                : "Peak temperature: not reported by hardware";
            ResultClockText = result.AvgClockMHz is { } clock
                ? string.Create(CultureInfo.InvariantCulture, $"Average clock (last {LoadTestAnalyzer.VerdictWindowSeconds(result.Samples)} s): {clock:0} MHz")
                : "Average clock: not reported by hardware";
            ResultRatedMaxText = RatedMaxText(result);
            ResultPercentText = HeldPercentText(result);
            // U5 fan health, measured from the same samples: the rows are
            // the per-fan idle/load speeds, the findings are the measured
            // faults (if any), and the summary is the honest empty state
            // when this machine exposes no fan sensors.
            var fanHealth = result.FanHealth;
            FanHealthLines = fanHealth is null
                ? Array.Empty<string>()
                : fanHealth.Fans.Select(f => f.DisplayText).ToList();
            FanHealthFindings = fanHealth is null
                ? Array.Empty<string>()
                : fanHealth.Findings.Select(f => f.Message).ToList();
            FanHealthSummaryText = fanHealth?.SummaryText
                ?? "No fan sensors were reported by hardware, so fan health could not be checked.";
            TemperatureValues = result.Samples
                .Where(s => s.CpuTempC.HasValue)
                .Select(s => s.CpuTempC!.Value)
                .ToList();
            ElapsedText = string.Create(
                CultureInfo.InvariantCulture,
                $"Finished in {(result.CompletedAt - result.StartedAt).TotalSeconds:0} s.");
            _liveTempKind = LiveLineKind.Finished;
            LiveTemperatureText = ResolveLiveTempText();
            _liveClockKind = LiveLineKind.Finished;
            LiveClockText = ResolveLiveClockText();

            _onCompleted?.Invoke(result);
        }
        catch (Exception exception)
        {
            _statusKind = LoadTestStatusKind.Failed;
            StatusText = ResolveStatusText();
            _failureMessage = exception.Message;
            VerdictText = StringLocalizer.GetFormatted(AppStringKeys.TestsItemNoResult, _languageCode, exception.Message);
        }
        finally
        {
            IsRunning = false;
            _runner = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private void OnProgress(LoadTestProgress progress)
    {
        // Progress<T> captures the UI synchronization context, so this
        // already runs on the UI thread in the app. The explicit post is
        // belt and braces for a caller without a captured context.
        if (_uiContext is not null && SynchronizationContext.Current != _uiContext)
        {
            _uiContext.Post(_ => ApplyProgress(progress), null);
            return;
        }

        ApplyProgress(progress);
    }

    private void ApplyProgress(LoadTestProgress progress)
    {
        ElapsedText = string.Create(CultureInfo.InvariantCulture, $"{progress.ElapsedSeconds} s");
        if (progress.CpuTempC is { } temp)
        {
            _liveTempKind = LiveLineKind.Measured;
            _liveTempMeasured = string.Create(CultureInfo.InvariantCulture, $"{temp:0.#} C");
        }
        else
        {
            _liveTempKind = LiveLineKind.NotReported;
            _liveTempMeasured = null;
        }

        LiveTemperatureText = ResolveLiveTempText();
        if (progress.AvgClockMHz is { } clock)
        {
            _liveClockKind = LiveLineKind.Measured;
            _liveClockMeasured = string.Create(CultureInfo.InvariantCulture, $"{clock:0} MHz");
        }
        else
        {
            _liveClockKind = LiveLineKind.NotReported;
            _liveClockMeasured = null;
        }

        LiveClockText = ResolveLiveClockText();
    }
}
