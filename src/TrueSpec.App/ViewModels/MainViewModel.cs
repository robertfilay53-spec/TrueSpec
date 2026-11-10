using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    private readonly ISnapshotStore? _snapshotStore;
    private readonly Dictionary<string, ViewModelBase> _pageCache =
        new(StringComparer.OrdinalIgnoreCase);

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

        _snapshotStore = CreateSnapshotStore();

        _snapshot = scanService.ScanAsync().GetAwaiter().GetResult();
        _ = SaveSnapshotAsync(_snapshot);

        NavItems = new ObservableCollection<NavItemViewModel>(NavItemViewModel.CreateDefault());
        Overview = CreateOverview();

        LastScanText = $"Last scan {_snapshot.CapturedAt:HH:mm:ss}";
        SelectedNavItem = NavItems.FirstOrDefault();
        CurrentPage = Overview;
        StatusMessage = "Real scan complete. Live sensors use LibreHardwareMonitor when available.";
    }

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    public OverviewViewModel Overview { get; private set; }

    public AppSettings Settings => _settings;

    public bool HistoryAvailable => _snapshotStore is not null;

    public string ExportFolder => EffectiveExportFolder();

    public string VersionText => "TrueSpec 1.0.0 (v1)";

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
    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;

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
                OnBuyerChecksChanged));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        StatusMessage = "Scanning hardware...";
        try
        {
            var snapshot = await _scanService.ScanAsync(_buyerContext);
            _snapshot = snapshot;
            _buyerContext = new BuyerCheckContext();
            _pageCache.Clear();
            _ = SaveSnapshotAsync(snapshot);

            var previous = Overview;
            Overview = CreateOverview();
            previous.Dispose();

            LastScanText = $"Last scan {snapshot.CapturedAt:HH:mm:ss}";
            OnPropertyChanged(nameof(LastScanText));
            SelectedNavItem = NavItems.FirstOrDefault();
            CurrentPage = Overview;
            StatusMessage = "Hardware scan refreshed from real collectors.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Refresh failed: {exception.Message}";
        }
    }

    [RelayCommand]
    private Task ExportJsonAsync() => ExportAsync(ReportFormat.Json);

    [RelayCommand]
    private Task ExportHtmlAsync() => ExportAsync(ReportFormat.Html);

    [RelayCommand]
    private Task ExportCsvAsync() => ExportAsync(ReportFormat.Csv);

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

            var (extension, exporter) = format switch
            {
                ReportFormat.Html => ("html", (IReportExporter)new HtmlReportExporter()),
                ReportFormat.Csv => ("csv", new CsvReportExporter()),
                _ => ("json", new JsonReportExporter()),
            };

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

    private static ISnapshotStore? CreateSnapshotStore()
    {
        try
        {
            var databasePath = Path.Combine(PathSafety.DefaultAppDataFolder(), "history.db");
            var store = new SqliteSnapshotStore(databasePath);
            store.InitializeAsync().GetAwaiter().GetResult();
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

    private OverviewViewModel CreateOverview() => new(
        _snapshot,
        OpenComponent,
        ShowBuyerCheckCommand,
        ExportJsonCommand,
        _sensorMonitor,
        TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds));

    private void OnBuyerChecksChanged(BuyerCheckResult checks, string? advertisedRam)
    {
        _snapshot = _snapshot with { BuyerChecks = checks };
        _buyerContext = new BuyerCheckContext(
            string.IsNullOrWhiteSpace(advertisedRam) ? null : advertisedRam.Trim());
        _pageCache.Remove("report");
    }

    private void NavigateTo(string key)
    {
        CurrentPage = key.ToLowerInvariant() switch
        {
            "overview" => Overview,
            "tests" => GetOrCreate("tests", () => new TestsViewModel()),
            "report" => GetOrCreate("report", () => new ReportViewModel(_snapshot, ExportJsonCommand, ExportHtmlCommand, ExportCsvCommand, EffectiveExportFolder())),
            "compare" => GetOrCreate("compare", () => new ComparisonViewModel(_snapshotStore, _snapshot)),
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
