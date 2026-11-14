using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Localization;
using TrueSpec.Core.Sensors;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// Tests page: real local benchmarks plus honest unavailable entries.
///
/// Localisation, phase 8 (v2.1.16): the page chrome renders in the
/// current language — the headings, the intro, the info note, the
/// benchmark scope note, the per-test status words, result lines and
/// Run button labels, and the load-test card (see
/// <see cref="LoadTestViewModel"/>) — so the page follows the language
/// the user picked in Settings, with no restart. The data the page
/// shows stays English: the per-test names and workload descriptions,
/// the measured result summaries and details, and the load-test
/// measured results, verdict text and live values. The page is cached
/// by the shell, so the strings resolve from the stored language code
/// and re-render through <see cref="ApplyLanguage"/>, the same
/// in-place pattern the Overview and History pages use.
/// </summary>
public sealed class TestsViewModel : ViewModelBase
{
    private string _languageCode;

    public TestsViewModel()
        : this(new UnavailableSensorMonitor(), null, false, null)
    {
    }

    public TestsViewModel(
        ISensorMonitor sensors,
        double? ratedMaxMHz,
        bool isOnBattery,
        Action<LoadTestResult>? onLoadTestCompleted,
        string? languageCode = null)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        LoadTest = new LoadTestViewModel(sensors, ratedMaxMHz, isOnBattery, onLoadTestCompleted, _languageCode);

        var benchmarks = new BenchmarkService();
        Tests = new[]
        {
            new TestItemViewModel(
                "CPU single-thread",
                "Runs a fixed single-thread workload and reports real measured operations per second.",
                benchmarks.RunCpuSingleThreadAsync,
                _languageCode),
            new TestItemViewModel(
                "CPU multi-thread",
                "Runs the same workload on all logical cores at once and reports real measured operations per second.",
                benchmarks.RunCpuMultiThreadAsync,
                _languageCode),
            new TestItemViewModel(
                "Disk read speed",
                "Reads a real 64 MB temporary file and reports measured sequential read speed.",
                benchmarks.RunDiskSequentialReadAsync,
                _languageCode),
            new TestItemViewModel(
                "Disk random 4K",
                "Random 4 KB reads and writes against a real 64 MB temporary file, queue depth 1, up to 2 seconds per phase. This is the number that decides how a machine feels.",
                benchmarks.RunDiskRandom4KAsync,
                _languageCode),
            new TestItemViewModel(
                "Memory copy",
                "Copies real 64 MB buffers and reports measured copy bandwidth in GB/s.",
                benchmarks.RunMemoryCopyAsync,
                _languageCode),
            new TestItemViewModel(
                "Disk latency under load",
                "Continuous random 4 KB reads keep the disk busy for up to 2 seconds; reports p50, p90 and p99 read latency in milliseconds.",
                benchmarks.RunDiskLatencyAsync,
                _languageCode),
            new TestItemViewModel(
                "Quick memory test",
                "Not in this build: the Windows Memory Diagnostic needs a reboot, so run it from Windows yourself for now.",
                null,
                _languageCode),
            new TestItemViewModel(
                "Battery runtime estimate",
                "Not in this build: no runtime estimate is shown until this test is implemented.",
                null,
                _languageCode),
            new TestItemViewModel(
                "Wi-Fi speed test",
                "Not in this build: an internet speed test would need the network, and this app is offline first.",
                null,
                _languageCode),
        };
    }

    /// <summary>The page eyebrow, in the page language.</summary>
    public string TestsEyebrowText =>
        StringLocalizer.Get(AppStringKeys.TestsEyebrow, _languageCode);

    /// <summary>The page title, in the page language.</summary>
    public string TestsTitleText =>
        StringLocalizer.Get(AppStringKeys.TestsTitle, _languageCode);

    /// <summary>The page introduction, in the page language.</summary>
    public string TestsIntroText =>
        StringLocalizer.Get(AppStringKeys.TestsIntro, _languageCode);

    /// <summary>The info note under the title, in the page language.</summary>
    public string TestsInfoNoteText =>
        StringLocalizer.Get(AppStringKeys.TestsInfoNote, _languageCode);

    /// <summary>
    /// U10 scope note (WORLD-CLASS-PLAN.md U10): these benchmarks measure
    /// this machine only. They do not replace Geekbench (cross-platform
    /// comparison) or 3DMark (GPU rendering). In the page language.
    /// </summary>
    public string BenchmarkScopeNote =>
        StringLocalizer.Get(AppStringKeys.TestsBenchmarkScopeNote, _languageCode);

    /// <summary>
    /// Re-renders this page in the given language, in place: every
    /// localized text is re-resolved, the load-test card and each test
    /// item re-render too. Called by the shell when the user picks a
    /// language in Settings; a page created after the switch is
    /// already built in the new language by the constructor. An unknown
    /// code falls back to English through the localizer, never to a
    /// blank. Measured data (test names, workload descriptions,
    /// measured results, load-test values) is untouched.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        LoadTest.ApplyLanguage(_languageCode);
        foreach (var test in Tests)
        {
            test.ApplyLanguage(_languageCode);
        }

        OnPropertyChanged(nameof(TestsEyebrowText));
        OnPropertyChanged(nameof(TestsTitleText));
        OnPropertyChanged(nameof(TestsIntroText));
        OnPropertyChanged(nameof(TestsInfoNoteText));
        OnPropertyChanged(nameof(BenchmarkScopeNote));
    }

    /// <summary>The load-test card: duration, Start/Cancel, live readouts and the result.</summary>
    public LoadTestViewModel LoadTest { get; }

    public IReadOnlyList<TestItemViewModel> Tests { get; }
}

public sealed partial class TestItemViewModel : ViewModelBase
{
    private readonly Func<CancellationToken, Task<BenchmarkResult>>? _run;
    private string _languageCode;

    /// <summary>Which fixed status/result line is showing, so ApplyLanguage can re-resolve it instead of resetting it.</summary>
    private TestItemOutcome _outcome = TestItemOutcome.NotRun;

    /// <summary>The exception message of a failed run, or null when the last run did not fail.</summary>
    private string? _failureMessage;

    /// <summary>The measured summary of a completed run (shown verbatim), or null when no run completed.</summary>
    private string? _successStatus;

    /// <summary>The measured result line of a completed run (shown verbatim), or null when no run completed.</summary>
    private string? _successResult;

    /// <summary>The states a test item can be in. Stored so a language switch re-resolves the status and result lines instead of resetting them.</summary>
    private enum TestItemOutcome
    {
        NotRun,
        Running,
        Failed,
        Succeeded,
    }

    public TestItemViewModel(
        string name,
        string description,
        Func<CancellationToken, Task<BenchmarkResult>>? run,
        string? languageCode = null)
    {
        Name = name;
        Description = description;
        _run = run;
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        Status = ResolveStatusText();
        ResultText = ResolveResultText();
    }

    public string Name { get; }

    public string Description { get; }

    public bool CanRun => _run is not null;

    /// <summary>The Run button enables only for an implemented test that is not already running.</summary>
    public bool CanRunNow => CanRun && !IsRunning;

    /// <summary>Button label follows the state: unavailable, running, or ready. In the page language.</summary>
    public string RunButtonText => !CanRun
        ? StringLocalizer.Get(AppStringKeys.TestsItemNotAvailableButton, _languageCode)
        : IsRunning
            ? StringLocalizer.Get(AppStringKeys.TestsItemRunningButton, _languageCode)
            : StringLocalizer.Get(AppStringKeys.TestsItemRun, _languageCode);

    /// <summary>
    /// Re-renders this item in the given language, in place: the
    /// status, result and button texts are re-resolved from the stored
    /// outcome. A completed run keeps its measured summary and result
    /// verbatim. Called by the Tests page when the user picks a
    /// language in Settings.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        Status = ResolveStatusText();
        ResultText = ResolveResultText();
        OnPropertyChanged(nameof(RunButtonText));
    }

    /// <summary>The current status line, resolved in the page language from the stored outcome.</summary>
    private string ResolveStatusText()
    {
        if (!CanRun)
        {
            return StringLocalizer.Get(AppStringKeys.TestsItemNotAvailable, _languageCode);
        }

        return _outcome switch
        {
            TestItemOutcome.NotRun => StringLocalizer.Get(AppStringKeys.TestsItemReady, _languageCode),
            TestItemOutcome.Running => StringLocalizer.Get(AppStringKeys.TestsItemRunning, _languageCode),
            TestItemOutcome.Failed => StringLocalizer.Get(AppStringKeys.TestsItemFailed, _languageCode),
            TestItemOutcome.Succeeded => _successStatus ?? StringLocalizer.Get(AppStringKeys.TestsItemReady, _languageCode),
            _ => StringLocalizer.Get(AppStringKeys.TestsItemReady, _languageCode),
        };
    }

    /// <summary>The current result line, resolved in the page language from the stored outcome.</summary>
    private string ResolveResultText() => _outcome switch
    {
        TestItemOutcome.NotRun => !CanRun
            ? StringLocalizer.Get(AppStringKeys.TestsItemNotImplementedResult, _languageCode)
            : StringLocalizer.Get(AppStringKeys.TestsItemNotRunYet, _languageCode),
        TestItemOutcome.Running => StringLocalizer.Get(AppStringKeys.TestsItemMeasuring, _languageCode),
        TestItemOutcome.Failed => StringLocalizer.GetFormatted(AppStringKeys.TestsItemNoResult, _languageCode, _failureMessage ?? string.Empty),
        TestItemOutcome.Succeeded => _successResult ?? StringLocalizer.Get(AppStringKeys.TestsItemNotRunYet, _languageCode),
        _ => StringLocalizer.Get(AppStringKeys.TestsItemNotRunYet, _languageCode),
    };

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial string ResultText { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunNow));
        OnPropertyChanged(nameof(RunButtonText));
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (_run is null || IsRunning)
        {
            return;
        }

        IsRunning = true;
        _outcome = TestItemOutcome.Running;
        Status = ResolveStatusText();
        ResultText = ResolveResultText();
        try
        {
            var result = await _run(CancellationToken.None);
            _outcome = TestItemOutcome.Succeeded;
            _successStatus = result.Summary;
            _successResult = $"{result.Detail} Source: {result.Source}.";
            Status = _successStatus;
            ResultText = _successResult;
        }
        catch (Exception exception)
        {
            _outcome = TestItemOutcome.Failed;
            _failureMessage = exception.Message;
            Status = ResolveStatusText();
            ResultText = ResolveResultText();
        }
        finally
        {
            IsRunning = false;
        }
    }
}
