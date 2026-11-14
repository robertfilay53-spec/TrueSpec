using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.App.Services;
using TrueSpec.App.ViewModels;
using TrueSpec.Core;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Health;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Security;
using TrueSpec.Core.Sensors;
using TrueSpec.Core.Settings;
using TrueSpec.Core.Storage;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// Shell view model: owns navigation, the current page, theme and the status
/// bar. Pages are cached so switching back keeps their state. The scan is
/// saved to local history after every scan, and settings are loaded from
/// disk and saved whenever they change.
/// </summary>
public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly MachineScanService _scanService;
    private readonly ISensorMonitor _sensorMonitor;
    private readonly SensorLogWriter? _sensorLogWriter;
    private readonly ThresholdAlertEvaluator _alertEvaluator;
    private readonly IBuyerCheckEvaluator _buyerCheckEvaluator;
    private readonly AppSettingsStore _settingsStore;
    private readonly IExportFileOpener _fileOpener;
    private readonly ExportOrchestrator _exporter;

    // --- v2.0 U7 (UI wiring only) ---
    // The DNA verifier behind the Report page's "Verify this machine"
    // action and the Compare page's DNA row: the real comparer over the
    // snapshot's stored DNA (Core MachineDnaComparer). A snapshot saved
    // before DNA existed honestly reports "not recorded".
    private readonly IMachineDnaVerifier _dnaVerifier = MachineDnaVerifier.Instance;

    // The last verification result, carried into the rebuilt Report page
    // after a verification re-scan (ApplySnapshot clears the page cache).
    private MachineDnaVerification? _lastDnaVerification;
    // --- end v2.0 U7 ---

    private ISnapshotStore? _snapshotStore;
    private CancellationTokenSource? _scanCts;
    private bool _disposed;
    private readonly Dictionary<string, ViewModelBase> _pageCache =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _initialScanStarted;

    private SystemSnapshot _snapshot;
    private BuyerCheckContext _buyerContext = new();
    private AppSettings _settings;

    public MainViewModel()
        : this(
            CollectorRegistry.CreateCollectors(),
            CollectorRegistry.CreateSensorMonitor(),
            new BuyerCheckEvaluator(),
            CollectorRegistry.CreateMachineDnaProvider())
    {
    }

    public MainViewModel(
        IEnumerable<ICollector> collectors,
        ISensorMonitor sensorMonitor,
        IBuyerCheckEvaluator buyerCheckEvaluator)
        : this(collectors, sensorMonitor, buyerCheckEvaluator, null)
    {
    }

    /// <summary>
    /// The DNA provider is optional: production passes the Windows
    /// collector so every scan records the fingerprint; callers that pass
    /// null (tests, non-Windows) scan without DNA.
    /// </summary>
    public MainViewModel(
        IEnumerable<ICollector> collectors,
        ISensorMonitor sensorMonitor,
        IBuyerCheckEvaluator buyerCheckEvaluator,
        IMachineDnaProvider? dnaProvider)
        : this(MachineScanService.Create(collectors, dnaProvider), sensorMonitor, buyerCheckEvaluator)
    {
    }

    public MainViewModel(
        MachineScanService scanService,
        ISensorMonitor sensorMonitor,
        IBuyerCheckEvaluator buyerCheckEvaluator)
        : this(scanService, sensorMonitor, buyerCheckEvaluator, new AppSettingsStore(), new ShellExportFileOpener())
    {
    }

    /// <summary>
    /// Test seam: the same shell, but with the settings store and the file
    /// opener supplied by the test, so an export test can point the export
    /// folder at a temp directory and record "Open folder" / "Open file"
    /// without launching Explorer.
    /// </summary>
    internal MainViewModel(
        IEnumerable<ICollector> collectors,
        ISensorMonitor sensorMonitor,
        IBuyerCheckEvaluator buyerCheckEvaluator,
        AppSettingsStore settingsStore,
        IExportFileOpener fileOpener)
        : this(MachineScanService.Create(collectors), sensorMonitor, buyerCheckEvaluator, settingsStore, fileOpener)
    {
    }

    private MainViewModel(
        MachineScanService scanService,
        ISensorMonitor sensorMonitor,
        IBuyerCheckEvaluator buyerCheckEvaluator,
        AppSettingsStore settingsStore,
        IExportFileOpener fileOpener)
    {
        _scanService = scanService;
        _sensorMonitor = sensorMonitor;
        _buyerCheckEvaluator = buyerCheckEvaluator;

        _settingsStore = settingsStore;
        _settings = _settingsStore.Load();

        // The production opener enforces the export folder itself
        // (extension + containment inside the opener); a test opener
        // passed through the test seam is used as-is.
        _fileOpener = fileOpener is ShellExportFileOpener
            ? new ShellExportFileOpener(EffectiveExportFolder)
            : fileOpener;

        // The loading snapshot is created before the exporter: the
        // exporter holds a live Func over this field, and the compiler
        // must see the field assigned before the Func is built.
        _snapshot = CreateLoadingSnapshot();

        _exporter = new ExportOrchestrator(
            () => _snapshot,
            EffectiveExportFolder,
            _fileOpener,
            ShowExportConfirmation,
            message => StatusMessage = message);

        IsDarkTheme = _settings.IsDarkTheme;

        // The sensor log is a convenience, never a startup blocker: when
        // the log file cannot be created, logging stays off for the session
        // and the Overview says so.
        _sensorLogWriter = SensorLogWriter.CreateDefault();
        _alertEvaluator = new ThresholdAlertEvaluator(
            ThresholdAlertEvaluator.CpuTemperatureLabel,
            "C");
        _alertEvaluator.Configure(_settings.CpuTemperatureThresholdC, _settings.CpuTemperatureThresholdSeconds);

        // Loading state only: the constructor must never run a scan or block.
        // The window has to appear immediately. The real scan starts after
        // the window is shown (MainWindow calls BeginInitialScan on Opened)
        // and updates the Overview in place when it completes.
        // (The loading snapshot itself is created above, before the
        // ExportOrchestrator, so the exporter's snapshot Func is never null.)
        NavItems = new ObservableCollection<NavItemViewModel>(NavItemViewModel.CreateForLanguage(_settings.LanguageCode));
        Overview = CreateOverview(isScanning: true);

        LastScanText = "Scanning hardware...";
        SelectedNavItem = NavItems.FirstOrDefault();
        CurrentPage = Overview;
        StatusMessage = "Scanning hardware...";
    }

    /// <summary>
    /// Starts the first hardware scan. Called by MainWindow once the window is
    /// shown. Safe to call more than once; only the first call starts a scan.
    /// Never throws: every failure is reported through the status message.
    /// </summary>
    public void BeginInitialScan()
    {
        if (_initialScanStarted)
        {
            return;
        }

        _initialScanStarted = true;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        StartupLog.Append("MainViewModel: initial scan starting");
        IsScanRunning = true;
        try
        {
            _snapshotStore = await CreateSnapshotStoreAsync();
            var snapshot = await _scanService.ScanAsync(
                _buyerContext,
                NewScanToken(),
                ScanTimeouts.Default,
                StartupLog.Append);
            // The scan is done: clear the flag before ApplySnapshot, so a
            // page rebuilt from the new snapshot does not read "scanning".
            // (Same pattern as RefreshAsync: the save below is persistence,
            // not part of the scan, and must not keep the UI in "scanning"
            // state after the Overview already shows the finished score.)
            IsScanRunning = false;
            ApplySnapshot(snapshot, "Real scan complete. Live sensors use LibreHardwareMonitor when available.", resetNavigation: true);
            await SaveSnapshotAsync(snapshot);
            StartupLog.Append("MainViewModel: initial scan complete");
        }
        catch (OperationCanceledException)
        {
            StartupLog.Append("MainViewModel: initial scan cancelled");
            StatusMessage = "Scan cancelled.";
            // The hero must stop saying "Scanning...": the loading
            // snapshot is a finished (empty) scan, not a running one.
            Overview.UpdateSnapshot(_snapshot, isScanning: false);
            LastScanText = "No scan yet";
            OnPropertyChanged(nameof(LastScanText));
        }
        catch (Exception exception)
        {
            StartupLog.Append($"MainViewModel: initial scan failed: {exception.Message}");
            StatusMessage = $"Scan failed: {exception.Message}";
            // The hero must stop saying "Scanning...": the loading
            // snapshot is a finished (empty) scan, not a running one.
            Overview.UpdateSnapshot(_snapshot, isScanning: false);
            LastScanText = "No scan yet";
            OnPropertyChanged(nameof(LastScanText));
        }
        finally
        {
            IsScanRunning = false;
        }
    }

    /// <summary>
    /// A fresh token source for a scan. The previous source is disposed:
    /// a new scan can only start when no scan is running, so the old one
    /// is always finished by the time this is called.
    /// </summary>
    private CancellationToken NewScanToken()
    {
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        return _scanCts.Token;
    }

    /// <summary>
    /// Cancels the running scan. The scan reports "Scan cancelled." and
    /// keeps the previous snapshot: a cancelled scan is not a failed scan
    /// and must never blank the Overview.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsScanRunning))]
    private void CancelScan()
    {
        try
        {
            _scanCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The scan finished between the button press and the cancel.
        }
    }

    /// <summary>
    /// Applies a finished scan to the shell: updates the single live
    /// Overview in place and clears cached pages so every page reads the new
    /// snapshot. Shared by the initial load and by Refresh. The Overview is
    /// deliberately never rebuilt: the page the window already shows is the
    /// one that gets the new score, so Overview, Report and the exports can
    /// never disagree about the score or the verdict.
    /// </summary>
    private void ApplySnapshot(SystemSnapshot snapshot, string statusMessage, bool resetNavigation)
    {
        _snapshot = snapshot;
        // The buyer context is deliberately preserved: the advertised RAM the
        // user typed must survive a refresh, otherwise the RAM check silently
        // goes NotApplicable on the second refresh.
        foreach (var page in _pageCache.Values)
        {
            if (page is SettingsViewModel settings)
            {
                settings.Detach();
            }
        }

        _pageCache.Clear();

        Overview.UpdateSnapshot(snapshot, isScanning: false);

        LastScanText = $"Last scan {snapshot.CapturedAt:HH:mm:ss}";
        OnPropertyChanged(nameof(LastScanText));
        if (resetNavigation)
        {
            // The initial scan finished while the user may already have
            // navigated: resetting unconditionally yanks them back to
            // Overview and discards their navigation (the v2.0.1
            // Motherboard report: the click landed during the scan and
            // the completion reset the rail to Overview, so the item
            // read as broken while its routing was fine). Only reset
            // when they are still on the Overview they never left;
            // otherwise keep their page, rebuilt from the new snapshot
            // exactly like a refresh does below.
            if (CurrentPage is not OverviewViewModel && SelectedNavItem is { } navigated)
            {
                NavigateTo(navigated.Key);
            }
            else
            {
                SelectedNavItem = NavItems.FirstOrDefault();
                CurrentPage = Overview;
            }
        }
        else if (SelectedNavItem is { } selected)
        {
            // Stay on the same page, rebuilt from the new snapshot, instead
            // of yanking the user back to Overview on every refresh.
            NavigateTo(selected.Key);
        }
        else
        {
            // Transient page (buyer check / component detail): fall back to Overview.
            SelectedNavItem = NavItems.FirstOrDefault();
            CurrentPage = Overview;
        }

        StatusMessage = statusMessage;
    }

    /// <summary>
    /// An empty snapshot for the loading state: no components, no score, all
    /// buyer checks not applicable. The overview renders it as "Not reported
    /// by hardware" until the real scan replaces it.
    /// </summary>
    private SystemSnapshot CreateLoadingSnapshot() => new(
        Guid.NewGuid(),
        DateTimeOffset.Now,
        Environment.MachineName,
        "Not reported",
        Array.Empty<ComponentSnapshot>(),
        ScoreResult.Empty,
        _buyerCheckEvaluator.Evaluate(Array.Empty<ComponentSnapshot>(), null));

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    public OverviewViewModel Overview { get; private set; }

    public AppSettings Settings => _settings;

    /// <summary>True while a hardware scan is running: the Cancel button shows and Refresh stays guarded.</summary>
    [ObservableProperty]
    public partial bool IsScanRunning { get; set; }

    /// <summary>True while a "Verify this machine" re-scan is running: the Verify button stays disabled so it cannot be double-run.</summary>
    [ObservableProperty]
    public partial bool IsVerifyingMachine { get; set; }

    partial void OnIsScanRunningChanged(bool value)
    {
        CancelScanCommand.NotifyCanExecuteChanged();
        VerifyMachineCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsVerifyingMachineChanged(bool value) => VerifyMachineCommand.NotifyCanExecuteChanged();

    public bool HistoryAvailable => _snapshotStore is not null;

    public string ExportFolder => EffectiveExportFolder();

    /// <summary>
    /// The file the last successful export wrote, or null when no export
    /// has completed (or the confirmation was dismissed). The shell banner
    /// in MainWindow binds to this: a successful export used to be a
    /// status-bar line only, which the v1.4.0 laptop test proved the user
    /// never sees — he reported export as broken when the files were in
    /// fact written. The banner stays until dismissed and shows the exact
    /// path, with "Open folder" and "Open file" actions.
    /// </summary>
    [ObservableProperty]
    public partial string? ExportConfirmationPath { get; set; }

    /// <summary>Human-readable confirmation line, for example "Report exported to C:\Users\...\Downloads\TrueSpec-report-....pdf".</summary>
    [ObservableProperty]
    public partial string ExportConfirmationText { get; set; } = string.Empty;

    public bool HasExportConfirmation => ExportConfirmationPath is not null;

    partial void OnExportConfirmationPathChanged(string? value) => OnPropertyChanged(nameof(HasExportConfirmation));

    public string VersionText
    {
        get
        {
            // Read from the assembly, not a literal: the version is set once
            // in TrueSpec.App.csproj (<Version>) and stays correct on v1.0.1.
            var version = typeof(MainViewModel).Assembly.GetName().Version;
            return version is null
                ? "TrueSpec 1.0.0 (v1)"
                : $"TrueSpec {version.Major}.{version.Minor}.{version.Build} (v{version.Major})";
        }
    }

    public bool IsElevated => Environment.IsPrivilegedProcess;

    public string AdminText => IsElevated ? "Admin: elevated" : "Admin: standard user";

    public bool UpdateAvailable => false;

    public string UpdateText => UpdateAvailable ? "Update available" : "No update check";

    public string LastScanText { get; private set; }

    public string ThemeToggleText => IsDarkTheme ? "Switch to light" : "Switch to dark";

    public string RailToggleText => IsRailExpanded ? "Collapse rail" : "Expand rail";

    [ObservableProperty]
    public partial NavItemViewModel? SelectedNavItem { get; set; }

    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; set; }

    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; } = true;

    [ObservableProperty]
    public partial bool IsRailExpanded { get; set; } = true;

    [ObservableProperty]
    public partial double RailWidth { get; set; } = 200d;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Starting a real hardware scan.";

    partial void OnSelectedNavItemChanged(NavItemViewModel? value)
    {
        if (value is null || _suppressNavNavigation)
        {
            return;
        }

        NavigateTo(value.Key);
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        OnPropertyChanged(nameof(ThemeToggleText));
    }

    partial void OnIsRailExpandedChanged(bool value)
    {
        RailWidth = value ? 200d : 68d;
        OnPropertyChanged(nameof(RailToggleText));
    }

    /// <summary>
    /// Rebuilds the navigation rail for the current language, keeping
    /// the user on the same page: the selected item is re-selected by
    /// key (its label changes, its routing does not), and a transient
    /// page (no selected item) is left alone. Selection is cleared
    /// first so the rail never briefly points at a removed item.
    /// Re-selection never navigates: the page is already current, and
    /// navigating here would re-enter page creation while the Settings
    /// page itself is still being constructed (GetOrCreate caches a
    /// page only after its factory returns).
    /// </summary>
    private void ApplyLanguage()
    {
        var selectedKey = SelectedNavItem?.Key;
        _suppressNavNavigation = true;
        try
        {
            SelectedNavItem = null;
            NavItems.Clear();
            foreach (var item in NavItemViewModel.CreateForLanguage(_settings.LanguageCode))
            {
                NavItems.Add(item);
            }

            if (selectedKey is not null)
            {
                SelectedNavItem = NavItems.FirstOrDefault(i => i.Key == selectedKey);
            }
        }
        finally
        {
            _suppressNavNavigation = false;
        }
    }

    /// <summary>True while the rail is being rebuilt for a language change; re-selection then never navigates.</summary>
    private bool _suppressNavNavigation;

    /// <summary>
    /// Called by the settings page whenever the user changes a setting.
    /// Saves to disk, applies the theme and the live refresh interval.
    /// </summary>
    public void ApplySettings(AppSettings settings)
    {
        _settings = settings.Sanitized();
        _settingsStore.Save(_settings);
        IsDarkTheme = _settings.IsDarkTheme;
        ApplyLanguage();
        // The Overview is long-lived and updates in place: it
        // re-renders its chrome in the new language from the last scan
        // (ApplyLanguage re-runs its snapshot-derived rows), so the
        // landing page follows the switch with no restart.
        Overview.ApplyLanguage(_settings.LanguageCode);
        Overview.SetRefreshInterval(TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds));
        Overview.ApplySensorSettings(_settings);
        if (_pageCache.TryGetValue("history", out var page) && page is HistoryViewModel history)
        {
            history.ApplySensorSettings(
                SensorLogWriter.DefaultFilePath(),
                _settings.SensorLoggingEnabled,
                _settings.CpuTemperatureThresholdC);
            history.ApplyLanguage(_settings.LanguageCode);
        }

        // Cached Report and Tests pages re-render in the new language
        // in place, the same pattern as the History page above. A page
        // created after the switch is already built in the new language
        // by its constructor.
        if (_pageCache.TryGetValue("report", out var reportPage) && reportPage is ReportViewModel report)
        {
            report.ApplyLanguage(_settings.LanguageCode);
        }

        if (_pageCache.TryGetValue("tests", out var testsPage) && testsPage is TestsViewModel tests)
        {
            tests.ApplyLanguage(_settings.LanguageCode);
        }

        // Cached New Laptop Check and Crash History pages re-render
        // in the new language in place, the same pattern as the Report
        // and Tests pages above. A page created after the switch is
        // already built in the new language by its constructor.
        if (_pageCache.TryGetValue("buyer", out var buyerPage) && buyerPage is NewLaptopCheckViewModel buyer)
        {
            buyer.ApplyLanguage(_settings.LanguageCode);
        }

        if (_pageCache.TryGetValue("crash", out var crashPage) && crashPage is CrashHistoryViewModel crash)
        {
            crash.ApplyLanguage(_settings.LanguageCode);
        }

        // Cached Comparison and Fleet pages re-render in the new
        // language in place, the same pattern as the pages above. A
        // page created after the switch is already built in the new
        // language by its constructor.
        if (_pageCache.TryGetValue("compare", out var comparePage) && comparePage is ComparisonViewModel comparison)
        {
            comparison.ApplyLanguage(_settings.LanguageCode);
        }

        if (_pageCache.TryGetValue("fleet", out var fleetPage) && fleetPage is FleetViewModel fleet)
        {
            fleet.ApplyLanguage(_settings.LanguageCode);
        }

        // Cached component detail pages re-render in the new language in
        // place (their chrome resolves at construction time, so a page
        // built before the switch needs the same re-language call the
        // Settings page gives itself). A page created after the switch
        // is already built in the new language by its constructor.
        foreach (var cached in _pageCache.Values)
        {
            if (cached is ComponentDetailViewModel detail)
            {
                detail.ApplyLanguage(_settings.LanguageCode);
            }
        }

        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(ExportFolder));
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        _settings = _settings with { IsDarkTheme = IsDarkTheme };
        _settingsStore.Save(_settings);
    }

    [RelayCommand]
    private void ToggleRail() => IsRailExpanded = !IsRailExpanded;

    [RelayCommand]
    private void ShowBuyerCheck()
    {
        SelectedNavItem = null;
        CurrentPage = GetOrCreate(
            "buyer",
            () => new NewLaptopCheckViewModel(
                _snapshot,
                ExportJsonCommand,
                ExportChecklistCommand,
                _buyerCheckEvaluator,
                OnBuyerChecksChanged,
                _buyerContext.AdvertisedRam,
                _settings.LanguageCode));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsScanRunning)
        {
            StatusMessage = "A scan is already running.";
            return;
        }

        IsScanRunning = true;
        StatusMessage = "Scanning hardware...";
        try
        {
            var snapshot = await _scanService.ScanAsync(
                _buyerContext,
                NewScanToken(),
                ScanTimeouts.Default,
                StartupLog.Append);
            // A refresh re-scans the passive hardware, but the load test was
            // measured on this machine and stays valid: carry it forward
            // with its own timestamps, and re-evaluate the buyer checks
            // with it, so a throttle finding does not vanish on refresh.
            if (_snapshot.LoadTest is { } previousLoadTest)
            {
                var checks = _buyerCheckEvaluator.Evaluate(snapshot.Components, _buyerContext, previousLoadTest);
                snapshot = snapshot with
                {
                    LoadTest = previousLoadTest,
                    BuyerChecks = checks,
                    Score = snapshot.Score with { Verdict = ScoringEngine.CombineVerdict(snapshot.Score, checks) },
                };
            }
            // The scan is done: clear the flag before ApplySnapshot, so a
            // page rebuilt from the new snapshot does not read "scanning".
            IsScanRunning = false;
            ApplySnapshot(snapshot, "Hardware scan refreshed from real collectors.", resetNavigation: false);
            await SaveSnapshotAsync(snapshot);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Refresh failed: {exception.Message}";
        }
        finally
        {
            IsScanRunning = false;
        }
    }

    [RelayCommand]
    private Task ExportJsonAsync() => _exporter.ExportReportAsync(ReportFormat.Json);

    [RelayCommand]
    private Task ExportHtmlAsync() => _exporter.ExportReportAsync(ReportFormat.Html);

    [RelayCommand]
    private Task ExportCsvAsync() => _exporter.ExportReportAsync(ReportFormat.Csv);

    [RelayCommand]
    private Task ExportPdfAsync() => _exporter.ExportReportAsync(ReportFormat.Pdf);

    // --- v2.0 U7 (UI wiring only) ---
    /// <summary>
    /// Buyer certificate (one-page PDF) via
    /// <see cref="ExportOrchestrator.ExportBuyerCertificateAsync"/>: the
    /// banner, score, five key numbers, DNA hash and scan date, as a
    /// separate file from the full report. The button stays visible and
    /// the export reports honestly when the scan has no DNA.
    /// </summary>
    [RelayCommand]
    private Task ExportCertificateAsync() => _exporter.ExportBuyerCertificateAsync();

    /// <summary>
    /// "Verify this machine": re-scans the hardware and compares the new
    /// scan's DNA against the snapshot the Report page was showing, so a
    /// part swapped since that scan is named. The re-scan is a normal
    /// refresh (it saves to history and carries the load test forward);
    /// the verification result is stored here so it survives ApplySnapshot
    /// clearing the page cache, and the Report page is rebuilt from it.
    /// DNA is identity, not health: the result never touches the score.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanVerifyMachine))]
    private async Task VerifyMachineAsync()
    {
        if (IsScanRunning || IsVerifyingMachine)
        {
            return;
        }

        var before = _snapshot;
        IsVerifyingMachine = true;
        StatusMessage = "Verifying this machine, re-scanning hardware...";
        try
        {
            await RefreshAsync();
            _lastDnaVerification = _dnaVerifier.Compare(before, _snapshot);
            _pageCache.Remove("report");
            if (CurrentPage is ReportViewModel)
            {
                NavigateTo("report");
            }

            StatusMessage = _lastDnaVerification.SummaryText;
        }
        finally
        {
            IsVerifyingMachine = false;
        }
    }

    private bool CanVerifyMachine => !IsScanRunning && !IsVerifyingMachine;
    // --- end v2.0 U7 ---

    /// <summary>
    /// Exports the trend currently shown on the History page as CSV, to the
    /// configured export folder, exactly like the other exports: no dialog,
    /// no cloud. The trend lives on the History page, so this command reads
    /// it from the cached page; with no history there is nothing to export.
    /// </summary>
    [RelayCommand]
    private async Task ExportHistoryCsvAsync()
    {
        var history = _pageCache.TryGetValue("history", out var page) ? page as HistoryViewModel : null;
        await _exporter.ExportHistoryCsvAsync(history?.CurrentTrend);
        if (history is not null)
        {
            history.StatusMessage = StatusMessage;
        }
    }

    /// <summary>
    /// Exports the fleet table currently shown on the Fleet page as CSV,
    /// to the configured export folder, exactly like the other exports:
    /// no dialog, no cloud. The rows live on the Fleet page, so this
    /// command reads them from the cached page; with no fleet there is
    /// nothing to export.
    /// </summary>
    [RelayCommand]
    private async Task ExportFleetCsvAsync()
    {
        var fleet = _pageCache.TryGetValue("fleet", out var page) ? page as FleetViewModel : null;
        await _exporter.ExportFleetCsvAsync(fleet?.CurrentRows);
        if (fleet is not null)
        {
            fleet.StatusMessage = StatusMessage;
        }
    }

    /// <summary>
    /// Exports one fleet machine's latest scan as its own CSV, to the
    /// configured export folder, exactly like the other exports: no
    /// dialog, no cloud. The snapshot lives on the Fleet page (it loads
    /// one per machine), so the page calls this with the row and its
    /// snapshot. Mirrors the status line into the Fleet page afterwards,
    /// like the fleet-table export does.
    /// </summary>
    private async Task ExportMachineCsvAsync(FleetMachineRow row, SystemSnapshot snapshot)
    {
        await _exporter.ExportMachineCsvAsync(row, snapshot.Score);
        var fleet = _pageCache.TryGetValue("fleet", out var page) ? page as FleetViewModel : null;
        if (fleet is not null)
        {
            fleet.StatusMessage = StatusMessage;
        }
    }

    /// <summary>
    /// Exports every fleet machine as its own CSV, to the configured
    /// export folder. Raises the same unmissable confirmation as the
    /// other exports: the banner names the folder and the count, and
    /// "Open folder" opens the folder the files landed in (the
    /// confirmation path is the first file written, so it opens that
    /// file's folder). With nothing exported there is no confirmation,
    /// only the fleet page's status line, which names what was skipped.
    /// </summary>
    private async Task<ExportAllMachinesResult> ExportAllMachinesCsvAsync(
        IReadOnlyList<FleetMachineRow> rows,
        IReadOnlyDictionary<string, SystemSnapshot> snapshotsByMachine)
    {
        var result = await _exporter.ExportAllMachinesCsvAsync(rows, snapshotsByMachine);
        if (result.ExportedPaths.Count > 0)
        {
            ExportConfirmationPath = result.ExportedPaths[0];
            ExportConfirmationText = $"Exported {result.ExportedPaths.Count} machine CSV{(result.ExportedPaths.Count == 1 ? string.Empty : "s")} to {result.Folder}. These files contain hardware serial numbers, MAC addresses, IP addresses and Wi-Fi network names. Share them only with someone you trust.";
            StatusMessage = ExportConfirmationText;
        }

        return result;
    }

    [RelayCommand]
    private Task ExportChecklistAsync() => _exporter.ExportChecklistAsync();

    /// <summary>
    /// Raises the unmissable export confirmation: the banner in the shell
    /// (visible from every page, including Report and History) shows the
    /// exact saved path until the user dismisses it, and the status bar
    /// keeps the same line. Called after every successful export; a failed
    /// export raises no confirmation, only the failure status.
    /// </summary>
    private void ShowExportConfirmation(string path, string label)
    {
        ExportConfirmationPath = path;
        // One-line note (6g): an export contains hardware serial numbers,
        // MAC addresses, IP addresses and Wi-Fi network names. The buyer
        // should know before sharing the file with a seller.
        ExportConfirmationText = $"{label} exported to {path}. This file contains hardware serial numbers, MAC addresses, IP addresses and Wi-Fi network names. Share it only with someone you trust.";
        StatusMessage = ExportConfirmationText;
    }

    [RelayCommand]
    private void OpenExportFolder() => _exporter.OpenExportFolder(ExportConfirmationPath);

    [RelayCommand]
    private void OpenExportedFile() => _exporter.OpenExportedFile(ExportConfirmationPath);

    [RelayCommand]
    private void DismissExportConfirmation()
    {
        ExportConfirmationPath = null;
        ExportConfirmationText = string.Empty;
    }

    private string EffectiveExportFolder()
    {
        var folder = PathSafety.ValidateExportFolder(_settings.ExportFolder);
        if (folder is not null)
        {
            return folder;
        }

        return PathSafety.DefaultExportFolder();
    }

    private static async Task<ISnapshotStore?> CreateSnapshotStoreAsync()
    {
        try
        {
            var databasePath = AppDataRoot.Resolve().DatabasePath;
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();
            return store;
        }
        catch
        {
            // History is a convenience, never a startup blocker. When the
            // store cannot be created the compare page reports it plainly.
            return null;
        }
    }

    private async Task SaveSnapshotAsync(SystemSnapshot snapshot)
    {
        if (_snapshotStore is null)
        {
            return;
        }

        try
        {
            await _snapshotStore.SaveAsync(snapshot);
        }
        catch
        {
            // A failed history write must never surface as a scan error.
        }
    }

    /// <summary>
    /// Window-close cleanup (called from MainWindow.Closing): stops the
    /// Overview's 2 s sensor timer, cancels a running scan and disposes
    /// the sensor monitor. The monitor is the static LibreHardwareMonitor
    /// singleton, IDisposable and previously never disposed; the Overview
    /// timer had zero callers for its Dispose. Safe to call twice, and
    /// safe on a shell whose scan never started.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _scanCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The scan already finished.
        }

        Overview.Dispose();
        (_sensorMonitor as IDisposable)?.Dispose();
        _scanCts?.Dispose();
        GC.SuppressFinalize(this);
    }

    private OverviewViewModel CreateOverview(bool isScanning) => new(
        _snapshot,
        OpenComponent,
        ShowBuyerCheckCommand,
        ExportJsonCommand,
        _sensorMonitor,
        TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds),
        isScanning,
        _sensorLogWriter,
        _alertEvaluator,
        _settings,
        IsElevated,
        _settings.LanguageCode);

    /// <summary>
    /// The load test finished on the Tests page. The result is saved into
    /// the snapshot (so history keeps it) and the buyer checks are
    /// re-evaluated with it: a throttling machine keeps its passive health
    /// score, but the New Laptop Check banner shows the finding separately.
    /// </summary>
    private void OnLoadTestCompleted(LoadTestResult result)
    {
        var checks = _buyerCheckEvaluator.Evaluate(_snapshot.Components, _buyerContext, result);
        _snapshot = _snapshot with
        {
            LoadTest = result,
            BuyerChecks = checks,
            Score = _snapshot.Score with { Verdict = ScoringEngine.CombineVerdict(_snapshot.Score, checks) },
        };
        Overview.UpdateSnapshot(_snapshot, isScanning: false);
        _pageCache.Remove("report");
        _ = SaveSnapshotAsync(_snapshot);
        StatusMessage = "Load test complete. The result is saved in this scan.";
    }

    /// <summary>
    /// Rated max clock in MHz from the CPU collector's "Max clock" spec
    /// (Win32_Processor MaxClockSpeed), or null when it was not reported.
    /// A missing value is never guessed: the verdict then says throttling
    /// could not be judged.
    /// </summary>
    private static double? RatedMaxFromSnapshot(SystemSnapshot snapshot) =>
        SpecText.Number(snapshot.Find("cpu")?.Specs ?? Array.Empty<SpecValue>(), SpecLabels.MaxClock);

    /// <summary>
    /// True when the battery is discharging (a "Discharge rate" above zero
    /// was reported). A desktop, a machine on mains power, or a battery
    /// that does not report a discharge rate reads as not on battery.
    /// </summary>
    private static bool IsOnBattery(SystemSnapshot snapshot) =>
        SpecText.Number(snapshot.Find("battery")?.Specs ?? Array.Empty<SpecValue>(), SpecLabels.DischargeRate) is { } watts
        && watts > 0d;

    private void OnBuyerChecksChanged(BuyerCheckResult checks, string? advertisedRam)
    {
        // The verdict is a function of the score AND the buyer checks
        // (CombineVerdict caps it): re-derive it, or a check that flips
        // to Fail here would leave a stale "New" verdict on screen.
        _snapshot = _snapshot with
        {
            BuyerChecks = checks,
            Score = _snapshot.Score with { Verdict = ScoringEngine.CombineVerdict(_snapshot.Score, checks) },
        };
        _buyerContext = new BuyerCheckContext(
            string.IsNullOrWhiteSpace(advertisedRam) ? null : advertisedRam.Trim());
        Overview.UpdateSnapshot(_snapshot, isScanning: false);
        _pageCache.Remove("report");
    }

    private void NavigateTo(string key)
    {
        CurrentPage = key.ToLowerInvariant() switch
        {
            "overview" => Overview,
            "crash" => GetOrCreate("crash", () => new CrashHistoryViewModel(_snapshot, _settings.LanguageCode)),
            "tests" => GetOrCreate("tests", () => new TestsViewModel(
                _sensorMonitor,
                RatedMaxFromSnapshot(_snapshot),
                IsOnBattery(_snapshot),
                OnLoadTestCompleted,
                _settings.LanguageCode)),
            "report" => GetOrCreate("report", () => new ReportViewModel(_snapshot, ExportJsonCommand, ExportHtmlCommand, ExportCsvCommand, EffectiveExportFolder(), isScanning: IsScanRunning, exportPdfCommand: ExportPdfCommand, exportCertificateCommand: ExportCertificateCommand, verifyMachineCommand: VerifyMachineCommand, dnaVerification: _lastDnaVerification, isVerifyingMachine: IsVerifyingMachine, languageCode: _settings.LanguageCode)),
            "compare" => GetOrCreate("compare", () => new ComparisonViewModel(_snapshotStore, _snapshot, _dnaVerifier, _settings.LanguageCode)),
            "fleet" => GetOrCreate("fleet", () => new FleetViewModel(_snapshotStore, ExportFleetCsvCommand, ExportMachineCsvAsync, ExportAllMachinesCsvAsync, _settings.LanguageCode)),
            "history" => GetOrCreate("history", () => new HistoryViewModel(_snapshotStore, _snapshot, ExportHistoryCsvCommand, SensorLogWriter.DefaultFilePath(), _settings.SensorLoggingEnabled, _settings.CpuTemperatureThresholdC, languageCode: _settings.LanguageCode)),
            "settings" => GetOrCreate("settings", () => new SettingsViewModel(this)),
            _ => GetOrCreate($"component:{key}", () => new ComponentDetailViewModel(_snapshot, key, _settings.LanguageCode)),
        };
    }

    private void OpenComponent(string componentId)
    {
        var navItem = NavItems.FirstOrDefault(n =>
            string.Equals(n.Key, componentId, StringComparison.OrdinalIgnoreCase));

        if (navItem is not null)
        {
            SelectedNavItem = navItem;
            return;
        }

        SelectedNavItem = null;
        CurrentPage = GetOrCreate(
            $"component:{componentId}",
            () => new ComponentDetailViewModel(_snapshot, componentId, _settings.LanguageCode));
    }

    private ViewModelBase GetOrCreate(string key, Func<ViewModelBase> factory)
    {
        if (!_pageCache.TryGetValue(key, out var page))
        {
            page = factory();
            _pageCache[key] = page;
        }

        return page;
    }
}
