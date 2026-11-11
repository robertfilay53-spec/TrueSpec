using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.Core.Security;
using TrueSpec.Core.Settings;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// Settings page. Every control works and persists: theme applies
/// immediately, the refresh interval retunes the live sensor timer, and the
/// export folder is where reports actually land. There is no update check
/// and no startup registration: an offline app does not promise network
/// features, and nothing is installed without consent.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private AppSettings _settings;

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        _settings = main.Settings;
        RefreshIntervalSeconds = _settings.RefreshIntervalSeconds;
        ExportFolder = _settings.ExportFolder;
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
            Save();
        }
    }

    public string ThemeText => IsDarkTheme ? "Dark (default)" : "Light";

    public IReadOnlyList<int> RefreshRateOptions { get; } = new[]
    {
        1, 2, 5, 10, 30,
    };

    [ObservableProperty]
    public partial int RefreshIntervalSeconds { get; set; } = AppSettings.DefaultRefreshSeconds;

    partial void OnRefreshIntervalSecondsChanged(int value)
    {
        Save();
    }

    /// <summary>Empty means the default Documents/TrueSpec folder.</summary>
    [ObservableProperty]
    public partial string ExportFolder { get; set; } = string.Empty;

    /// <summary>Explains a rejected folder pick; empty when nothing went wrong.</summary>
    [ObservableProperty]
    public partial string FolderMessage { get; set; } = string.Empty;

    public string ExportFolderText => string.IsNullOrWhiteSpace(ExportFolder)
        ? PathSafety.DefaultExportFolder() + " (default)"
        : ExportFolder;

    public string LanguageText => "English (locked in v1)";

    /// <summary>
    /// Set by the view: opens the native folder picker and returns the
    /// chosen path, or null when the user cancels. The view model never
    /// touches the windowing system itself.
    /// </summary>
    public Func<Task<string?>>? FolderPicker { get; set; }

    [RelayCommand]
    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;

    [RelayCommand]
    private async Task PickExportFolderAsync()
    {
        if (FolderPicker is null)
        {
            return;
        }

        var picked = await FolderPicker();
        if (picked is null)
        {
            return;
        }

        var validated = PathSafety.ValidateExportFolder(picked);
        if (validated is null)
        {
            // A picked folder the validator rejects (a drive root or a
            // system folder) used to do nothing at all, with no message.
            FolderMessage = "That folder can't be used for exports (drive root or system folder). Pick another folder.";
            return;
        }

        ExportFolder = validated;
        FolderMessage = string.Empty;
        OnPropertyChanged(nameof(ExportFolderText));
        Save();
    }

    [RelayCommand]
    private void ResetExportFolder()
    {
        ExportFolder = string.Empty;
        FolderMessage = string.Empty;
        OnPropertyChanged(nameof(ExportFolderText));
        Save();
    }

    /// <summary>
    /// Unsubscribes from the shell. The shell calls this when it drops a
    /// cached settings page (for example on refresh): without it, every
    /// dropped page stays alive through the event subscription.
    /// </summary>
    public void Detach()
    {
        _main.PropertyChanged -= OnMainPropertyChanged;
    }

    private void Save()
    {
        _settings = new AppSettings
        {
            IsDarkTheme = IsDarkTheme,
            RefreshIntervalSeconds = RefreshIntervalSeconds,
            ExportFolder = ExportFolder,
        }.Sanitized();

        RefreshIntervalSeconds = _settings.RefreshIntervalSeconds;
        ExportFolder = _settings.ExportFolder;
        _main.ApplySettings(_settings);
    }

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
        {
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(ThemeText));
        }
    }
}
