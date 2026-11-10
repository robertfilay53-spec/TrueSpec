using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TrueSpec.App.ViewModels;

/// <summary>Settings page. Only the theme toggle and status text are wired in this build.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += OnMainPropertyChanged;
    }

    public bool IsDarkTheme
    {
        get => _main.IsDarkTheme;
        set
        {
            if (_main.IsDarkTheme == value)
            {
                return;
            }

            _main.IsDarkTheme = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeText));
        }
    }

    public string ThemeText => IsDarkTheme ? "Dark (default)" : "Light";

    public IReadOnlyList<string> RefreshRateOptions { get; } = new[]
    {
        "1 second",
        "2 seconds",
        "5 seconds",
        "10 seconds",
    };

    public IReadOnlyList<string> UpdateChannelOptions { get; } = new[]
    {
        "Stable",
        "Preview",
    };

    [ObservableProperty]
    public partial string RefreshRate { get; set; } = "2 seconds";

    [ObservableProperty]
    public partial string UpdateChannel { get; set; } = "Stable";

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial bool CheckForUpdatesOnStartup { get; set; }

    public string LanguageText => "English (locked in v1)";

    [RelayCommand]
    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
        {
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(ThemeText));
        }
    }
}
