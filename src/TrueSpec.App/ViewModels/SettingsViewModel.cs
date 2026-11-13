using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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

    /// <summary>Test seams for the portable-mode status: pass an explicit exe folder and env value instead of the real ones.</summary>
    private readonly string? _exeDirectory;
    private readonly string? _environmentValue;
    private PortableModeStatus _portableStatus;

    public SettingsViewModel(MainViewModel main, string? exeDirectory = null, string? environmentValue = null)
    {
        _main = main;
        _settings = main.Settings;
        _exeDirectory = exeDirectory;
        _environmentValue = environmentValue;
        _portableStatus = PortableMode.GetStatus(_exeDirectory, _environmentValue);
        RefreshIntervalSeconds = _settings.RefreshIntervalSeconds;
        ExportFolder = _settings.ExportFolder;
        SensorLoggingEnabled = _settings.SensorLoggingEnabled;
        CpuTemperatureAlertEnabled = _settings.CpuTemperatureThresholdC is not null;
        CpuTemperatureThresholdText = _settings.CpuTemperatureThresholdC?.ToString("0.#", CultureInfo.InvariantCulture) ?? "90";
        CpuTemperatureThresholdSeconds = _settings.CpuTemperatureThresholdSeconds;
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

    /// <summary>Empty means the default Downloads folder (falling back to Documents/TrueSpec when Downloads does not exist).</summary>
    [ObservableProperty]
    public partial string ExportFolder { get; set; } = string.Empty;

    /// <summary>Explains a rejected folder pick; empty when nothing went wrong.</summary>
    [ObservableProperty]
    public partial string FolderMessage { get; set; } = string.Empty;

    /// <summary>Whether live sensor readings are recorded to the local CSV log.</summary>
    [ObservableProperty]
    public partial bool SensorLoggingEnabled { get; set; } = true;

    partial void OnSensorLoggingEnabledChanged(bool value)
    {
        Save();
    }

    /// <summary>Whether the CPU temperature alert is on. Off is the shipped default: no threshold implies a diagnosis.</summary>
    [ObservableProperty]
    public partial bool CpuTemperatureAlertEnabled { get; set; }

    partial void OnCpuTemperatureAlertEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(AlertSummaryText));
        Save();
    }

    /// <summary>The alert threshold in C, as typed. Only saved when it parses inside 30-120 C.</summary>
    [ObservableProperty]
    public partial string CpuTemperatureThresholdText { get; set; } = "90";

    partial void OnCpuTemperatureThresholdTextChanged(string value)
    {
        OnPropertyChanged(nameof(AlertSummaryText));
        if (TryParseThreshold(value, out _))
        {
            ThresholdMessage = string.Empty;
            Save();
        }
        else
        {
            ThresholdMessage = "Enter a temperature between 30 and 120 C.";
        }
    }

    /// <summary>How long the temperature must stay at or above the threshold before alerting.</summary>
    [ObservableProperty]
    public partial int CpuTemperatureThresholdSeconds { get; set; } = AppSettings.DefaultCpuTemperatureThresholdSeconds;

    partial void OnCpuTemperatureThresholdSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(AlertSummaryText));
        Save();
    }

    public IReadOnlyList<int> ThresholdDurationOptions { get; } = new[]
    {
        15, 30, 60, 120,
    };

    /// <summary>Explains a rejected threshold value; empty when nothing went wrong.</summary>
    [ObservableProperty]
    public partial string ThresholdMessage { get; set; } = string.Empty;

    public string AlertSummaryText => CpuTemperatureAlertEnabled
        ? $"Alert when the CPU temperature reaches {CpuTemperatureThresholdText} C for {CpuTemperatureThresholdSeconds} seconds."
        : "No temperature alert set.";

    private static bool TryParseThreshold(string text, out double threshold)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out threshold) &&
            threshold >= AppSettings.MinCpuTemperatureThresholdC &&
            threshold <= AppSettings.MaxCpuTemperatureThresholdC)
        {
            return true;
        }

        threshold = 0;
        return false;
    }

    public string ExportFolderText => string.IsNullOrWhiteSpace(ExportFolder)
        ? PathSafety.DefaultExportFolder() + " (default)"
        : ExportFolder;

    public string LanguageText => "English (locked in v1)";

    /// <summary>The folder this run keeps its data in (settings, history, sensor log).</summary>
    public string DataFolderText => _portableStatus.DataFolder;

    /// <summary>
    /// One sentence on the portable state: whether data is kept next to
    /// the app or in the default folder, and which signal asked for it.
    /// A marker in a protected folder reads as off with the reason named:
    /// the marker is present but portable mode never activates there.
    /// </summary>
    public string PortableModeSummaryText => _portableStatus switch
    {
        { IsPortable: true, MarkerPresent: true, EnvRequested: true } =>
            "Portable mode is on (marker file and environment variable). Data is kept next to the app.",
        { IsPortable: true, MarkerPresent: true } =>
            "Portable mode is on. Data is kept next to the app.",
        { IsPortable: true } =>
            "Portable mode is on through the TRUESPEC_PORTABLE environment variable. Data is kept next to the app.",
        { MarkerPresent: true } =>
            "The portable marker file is present, but this app folder is a system or protected folder, so portable mode is not active and data stays in the default folder.",
        _ => "Portable mode is off. Data is kept in the default folder.",
    };

    /// <summary>The toggle acts on the marker file, never on the environment variable.</summary>
    public string PortableModeActionText => _portableStatus.MarkerPresent
        ? "Turn portable mode off"
        : "Turn portable mode on";

    /// <summary>Outcome of the last portable-mode change; empty when nothing was changed yet.</summary>
    [ObservableProperty]
    public partial string PortableModeMessage { get; set; } = string.Empty;

    [RelayCommand]
    private void TogglePortableMode()
    {
        var result = PortableMode.SetEnabled(
            _exeDirectory ?? AppContext.BaseDirectory,
            !_portableStatus.MarkerPresent,
            _environmentValue);
        _portableStatus = PortableMode.GetStatus(_exeDirectory, _environmentValue);
        PortableModeMessage = result.Message;
        OnPropertyChanged(nameof(DataFolderText));
        OnPropertyChanged(nameof(PortableModeSummaryText));
        OnPropertyChanged(nameof(PortableModeActionText));
    }

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
            SensorLoggingEnabled = SensorLoggingEnabled,
            CpuTemperatureThresholdC = CpuTemperatureAlertEnabled && TryParseThreshold(CpuTemperatureThresholdText, out var threshold)
                ? threshold
                : null,
            CpuTemperatureThresholdSeconds = CpuTemperatureThresholdSeconds,
        }.Sanitized();

        RefreshIntervalSeconds = _settings.RefreshIntervalSeconds;
        ExportFolder = _settings.ExportFolder;
        SensorLoggingEnabled = _settings.SensorLoggingEnabled;
        CpuTemperatureAlertEnabled = _settings.CpuTemperatureThresholdC is not null;
        if (_settings.CpuTemperatureThresholdC is { } savedThreshold)
        {
            CpuTemperatureThresholdText = savedThreshold.ToString("0.#", CultureInfo.InvariantCulture);
        }

        CpuTemperatureThresholdSeconds = _settings.CpuTemperatureThresholdSeconds;
        OnPropertyChanged(nameof(AlertSummaryText));
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
