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
using TrueSpec.App.ViewModels;
using TrueSpec.Core;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// Shell view model: owns navigation, the current page, theme and the status
/// bar. Pages are cached so switching back keeps their state.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly SystemSnapshot _snapshot;
    private readonly Dictionary<string, ViewModelBase> _pageCache =
        new(StringComparer.OrdinalIgnoreCase);

    public MainViewModel()
        : this(MachineScanService.CreateDefaultStub().ScanAsync().GetAwaiter().GetResult())
    {
    }

    public MainViewModel(SystemSnapshot snapshot)
    {
        _snapshot = snapshot;
        NavItems = new ObservableCollection<NavItemViewModel>(NavItemViewModel.CreateDefault());
        Overview = new OverviewViewModel(snapshot, OpenComponent, ShowBuyerCheckCommand, ExportReportCommand);

        LastScanText = $"Last scan {snapshot.CapturedAt:HH:mm:ss}";
        SelectedNavItem = NavItems.FirstOrDefault();
        CurrentPage = Overview;
    }

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    public OverviewViewModel Overview { get; }

    public string VersionText => "TrueSpec 0.1.0 (P0)";

    public bool IsElevated => false;

    public string AdminText => IsElevated ? "Admin: elevated" : "Admin: standard user";

    public bool UpdateAvailable => false;

    public string UpdateText => UpdateAvailable ? "Update available" : "Up to date";

    public string LastScanText { get; }

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
    public partial string StatusMessage { get; set; } = "P0 foundation: sample data, no hardware access yet.";

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

    [RelayCommand]
    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;

    [RelayCommand]
    private void ToggleRail() => IsRailExpanded = !IsRailExpanded;

    [RelayCommand]
    private void ShowBuyerCheck()
    {
        SelectedNavItem = null;
        CurrentPage = GetOrCreate("buyer", () => new NewLaptopCheckViewModel(_snapshot, ExportReportCommand));
    }

    [RelayCommand]
    private void Refresh()
    {
        StatusMessage = $"Scan refreshed at {DateTime.Now:HH:mm:ss} (sample data).";
    }

    [RelayCommand]
    private async Task ExportReportAsync()
    {
        try
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents))
            {
                documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            var folder = Path.Combine(documents, "TrueSpec");
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, $"TrueSpec-report-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            await using var stream = File.Create(path);
            await new JsonReportExporter().ExportAsync(ReportModel.FromSnapshot(_snapshot), stream);

            StatusMessage = $"Report exported to {path}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Export failed: {exception.Message}";
        }
    }

    private void NavigateTo(string key)
    {
        CurrentPage = key.ToLowerInvariant() switch
        {
            "overview" => Overview,
            "tests" => GetOrCreate("tests", () => new TestsViewModel()),
            "report" => GetOrCreate("report", () => new ReportViewModel(_snapshot, ExportReportCommand)),
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
