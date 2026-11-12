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
using TrueSpec.Core.Collectors;
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
public partial class MainViewModel : ViewModelBase
{
    private readonly MachineScanService _scanService;
    private readonly ISensorMonitor _sensorMonitor;
    private readonly IBuyerCheckEvaluator _buyerCheckEvaluator;
    private readonly AppSettingsStore _settingsStore;
    private ISnapshotStore? _snapshotStore;
    private readonly Dictionary<string, ViewModelBase> _pageCache =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _initialScanStarted;
    private bool _scanRunning;

    private SystemSnapshot _snapshot;
    private BuyerCheckContext _buyerContext = new();
    private AppSettings _settings;

    public MainViewModel()
        : this(
            CollectorRegistry.CreateCollectors(),
            CollectorRegistry.CreateSensorMonitor(),
            new BuyerCheckEvaluator())
    {
    }

    public MainViewModel(
        IEnumerable<ICollector> collectors,
        ISensorMonitor sensorMonitor,
        IBuyerCheckEvaluator buyerCheckEvaluator)
        : this(MachineScanService.Create(collectors), sensorMonitor, buyerCheckEvaluator)
    {
    }

    public MainViewModel(
        MachineScanService scanService,
        ISensorMonitor sensorMonitor,
        IBuyerCheckEvaluator buyerCheckEvaluator)
    {
        _scanService = scanService;
        _sensorMonitor = sensorMonitor;
        _buyerCheckEvaluator = buyerCheckEvaluator;

        _settingsStore = new AppSettingsStore();
        _settings = _settingsStore.Load();
        IsDarkTheme = _settings.IsDarkTheme;

        // Loading state only: the constructor must never run a scan or block.
        // The window has to appear immediately. The real scan starts after
        // the window is shown (MainWindow calls BeginInitialScan on Opened)
        // and updates the Overview in place when it completes.
        _snapshot = CreateLoadingSnapshot();

        NavItems = new ObservableCollection<NavItemViewModel>(NavItemViewModel.CreateDefault());
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
        _scanRunning = true;
        try
        {
            _snapshotStore = await CreateSnapshotStoreAsync();
            var snapshot = await _scanService.ScanAsync(
                _buyerContext,
                CancellationToken.None,
                ScanTimeouts.Default,
                StartupLog.Append);
            // The scan is done: clear the flag before ApplySnapshot, so a
            // page rebuilt from the new snapshot does not read "scanning".
            // (Same pattern as RefreshAsync: the save below is persistence,
            // not part of the scan, and must not keep the UI in "scanning"
            // state after the Overview already shows the finished score.)
            _scanRunning = false;
            ApplySnapshot(snapshot, "Real scan complete. Live sensors use LibreHardwareMonitor when available.", resetNavigation: true);
            await SaveSnapshotAsync(snapshot);
            StartupLog.Append("MainViewModel: initial scan complete");
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
            _scanRunning = false;
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
            SelectedNavItem = NavItems.FirstOrDefault();
            CurrentPage = Overview;
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

    public bool HistoryAvailable => _snapshotStore is not null;

    public string ExportFolder => EffectiveExportFolder();

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
        if (value is null)
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
    /// Called by the settings page whenever the user changes a setting.
    /// Saves to disk, applies the theme and the live refresh interval.
    /// </summary>
    public void ApplySettings(AppSettings settings)
    {
        _settings = settings.Sanitized();
        _settingsStore.Save(_settings);
        IsDarkTheme = _settings.IsDarkTheme;
        Overview.SetRefreshInterval(TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds));
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
                _buyerContext.AdvertisedRam));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_scanRunning)
        {
            StatusMessage = "A scan is already running.";
            return;
        }

        _scanRunning = true;
        StatusMessage = "Scanning hardware...";
        try
        {
            var snapshot = await _scanService.ScanAsync(
                _buyerContext,
                CancellationToken.None,
                ScanTimeouts.Default,
                StartupLog.Append);
            // The scan is done: clear the flag before ApplySnapshot, so a
            // page rebuilt from the new snapshot does not read "scanning".
            _scanRunning = false;
            ApplySnapshot(snapshot, "Hardware scan refreshed from real collectors.", resetNavigation: false);
            await SaveSnapshotAsync(snapshot);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Refresh failed: {exception.Message}";
        }
        finally
        {
            _scanRunning = false;
        }
    }

    [RelayCommand]
    private Task ExportJsonAsync() => ExportAsync(ReportFormat.Json);

    [RelayCommand]
    private Task ExportHtmlAsync() => ExportAsync(ReportFormat.Html);

    [RelayCommand]
    private Task ExportCsvAsync() => ExportAsync(ReportFormat.Csv);

    [RelayCommand]
    private Task ExportPdfAsync() => ExportAsync(ReportFormat.Pdf);

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
        var trend = history?.CurrentTrend;
        if (trend is null || trend.SnapshotCount == 0)
        {
            StatusMessage = "History export failed: there is no history to export yet.";
            if (history is not null)
            {
                history.StatusMessage = StatusMessage;
            }

            return;
        }

        try
        {
            var folder = EffectiveExportFolder();
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, PathSafety.SafeReportFileName("TrueSpec-history", "csv", DateTime.Now));
            await using var stream = File.Create(path);
            await new HistoryCsvExporter().ExportAsync(trend, stream);
            StatusMessage = $"History exported to {path}";
            if (history is not null)
            {
                history.StatusMessage = StatusMessage;
            }
        }
        catch (Exception exception)
        {
            StatusMessage = $"History export failed: {exception.Message}";
            if (history is not null)
            {
                history.StatusMessage = StatusMessage;
            }
        }
    }

    [RelayCommand]
    private async Task ExportChecklistAsync()
    {
        try
        {
            var folder = EffectiveExportFolder();
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, PathSafety.SafeReportFileName("TrueSpec-checklist", "html", DateTime.Now));
            await using var stream = File.Create(path);
            await new ChecklistHtmlExporter().ExportAsync(_snapshot, stream);
            StatusMessage = $"Buyer checklist exported to {path}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Checklist export failed: {exception.Message}";
        }
    }

    private async Task ExportAsync(ReportFormat format)
    {
        try
        {
            var folder = EffectiveExportFolder();
            Directory.CreateDirectory(folder);

            var (extension, exporter) = ReportExporterFactory.Resolve(format);

            var path = Path.Combine(folder, PathSafety.SafeReportFileName("TrueSpec-report", extension, DateTime.Now));
            await using var stream = File.Create(path);
            await exporter.ExportAsync(ReportModel.FromSnapshot(_snapshot), stream);

            StatusMessage = $"Report exported to {path}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Export failed: {exception.Message}";
        }
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
            var databasePath = Path.Combine(PathSafety.DefaultAppDataFolder(), "history.db");
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

    private OverviewViewModel CreateOverview(bool isScanning) => new(
        _snapshot,
        OpenComponent,
        ShowBuyerCheckCommand,
        ExportJsonCommand,
        _sensorMonitor,
        TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds),
        isScanning);

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
            "crash" => GetOrCreate("crash", () => new CrashHistoryViewModel(_snapshot)),
            "tests" => GetOrCreate("tests", () => new TestsViewModel()),
            "report" => GetOrCreate("report", () => new ReportViewModel(_snapshot, ExportJsonCommand, ExportHtmlCommand, ExportCsvCommand, EffectiveExportFolder(), isScanning: _scanRunning, exportPdfCommand: ExportPdfCommand)),
            "compare" => GetOrCreate("compare", () => new ComparisonViewModel(_snapshotStore, _snapshot)),
            "history" => GetOrCreate("history", () => new HistoryViewModel(_snapshotStore, _snapshot, ExportHistoryCsvCommand)),
            "settings" => GetOrCreate("settings", () => new SettingsViewModel(this)),
            _ => GetOrCreate($"component:{key}", () => new ComponentDetailViewModel(_snapshot, key)),
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
            () => new ComponentDetailViewModel(_snapshot, componentId));
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
