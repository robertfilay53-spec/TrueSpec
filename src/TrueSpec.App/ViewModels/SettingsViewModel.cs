using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueSpec.Core.HardwareDatabase;
using TrueSpec.Core.Localization;
using TrueSpec.Core.Plugins;
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

    /// <summary>Why the last plug-in discovery pass could not list plug-ins, if it could not.</summary>
    private string? _pluginDiscoveryError;

    /// <summary>The last portable-mode toggle result, kept so its
    /// message can be re-resolved when the language changes (the result
    /// carries its message as a key, not a formatted string).</summary>
    private PortableModeToggleResult? _lastToggleResult;

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
        HardwareDatabaseEnabled = _settings.HardwareDatabaseEnabled;
        CpuTemperatureAlertEnabled = _settings.CpuTemperatureThresholdC is not null;
        CpuTemperatureThresholdText = _settings.CpuTemperatureThresholdC?.ToString("0.#", CultureInfo.InvariantCulture) ?? "90";
        CpuTemperatureThresholdSeconds = _settings.CpuTemperatureThresholdSeconds;
        LanguageCode = _settings.LanguageCode;
        RefreshPluginDiscovery();
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

    public string ThemeText => IsDarkTheme
        ? StringLocalizer.Get(AppStringKeys.ThemeDarkDefault, LanguageCode)
        : StringLocalizer.Get(AppStringKeys.ThemeLight, LanguageCode);

    /// <summary>One refresh-rate / duration option: the seconds value and its localized text ("5 seconds" / "5 Sekunden").</summary>
    public sealed record RefreshOption(int Value, string Text);

    /// <summary>The page header and the Appearance card, in the current language.</summary>
    public string SettingsEyebrowText => StringLocalizer.Get(AppStringKeys.SettingsEyebrow, LanguageCode);
    public string SettingsTitleText => StringLocalizer.Get(AppStringKeys.SettingsTitle, LanguageCode);
    public string SettingsIntroText => StringLocalizer.Get(AppStringKeys.SettingsIntro, LanguageCode);
    public string AppearanceTitleText => StringLocalizer.Get(AppStringKeys.AppearanceTitle, LanguageCode);
    public string AppearanceNoteText => StringLocalizer.Get(AppStringKeys.AppearanceNote, LanguageCode);

    /// <summary>ToggleSwitch labels for the theme switch.</summary>
    public string ThemeDarkText => StringLocalizer.Get(AppStringKeys.ThemeDark, LanguageCode);
    public string ThemeLightText => StringLocalizer.Get(AppStringKeys.ThemeLight, LanguageCode);

    /// <summary>The refresh-rate card, in the current language.</summary>
    public string RefreshTitleText => StringLocalizer.Get(AppStringKeys.RefreshTitle, LanguageCode);
    public string RefreshNoteText => StringLocalizer.Get(AppStringKeys.RefreshNote, LanguageCode);

    /// <summary>
    /// The refresh-rate options with their localized texts, rebuilt for
    /// the current language. The ComboBox binds SelectedValue to the
    /// seconds value, so picking an option still saves the number, not
    /// the text. The old RefreshSecondsToTextConverter is gone: it had
    /// no language to read, and a converter cannot see the view model.
    /// </summary>
    public IReadOnlyList<RefreshOption> RefreshRateOptions => BuildSecondOptions(new[] { 1, 2, 5, 10, 30 });

    private IReadOnlyList<RefreshOption> BuildSecondOptions(int[] seconds) =>
        seconds.Select(s => new RefreshOption(s, s == 1
            ? StringLocalizer.Get(AppStringKeys.SecondsOne, LanguageCode)
            : StringLocalizer.GetFormatted(AppStringKeys.SecondsMany, LanguageCode, s))).ToList();

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

    /// <summary>
    /// Whether the bundled hardware database is used to show a CPU's
    /// rated boost clock. Takes effect after a restart: the collectors
    /// are created once at startup.
    /// </summary>
    [ObservableProperty]
    public partial bool HardwareDatabaseEnabled { get; set; } = true;

    partial void OnHardwareDatabaseEnabledChanged(bool value)
    {
        Save();
    }

    /// <summary>The hardware-database card, in the current language.</summary>
    public string HardwareDatabaseTitleText => StringLocalizer.Get(AppStringKeys.HardwareDatabaseTitle, LanguageCode);
    public string HardwareDatabaseNoteText => StringLocalizer.Get(AppStringKeys.HardwareDatabaseNote, LanguageCode);
    public string HardwareDatabaseVersionText => StringLocalizer.GetFormatted(
        AppStringKeys.HardwareDatabaseVersion,
        LanguageCode,
        HardwareDatabase.Default.Version,
        HardwareDatabase.Default.CpuCount,
        HardwareDatabase.Default.GpuCount,
        HardwareDatabase.Default.Updated);

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
            ThresholdMessage = StringLocalizer.Get(AppStringKeys.ThresholdMessage, LanguageCode);
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

    public IReadOnlyList<RefreshOption> ThresholdDurationOptions => BuildSecondOptions(new[] { 15, 30, 60, 120 });

    /// <summary>Explains a rejected threshold value; empty when nothing went wrong.</summary>
    [ObservableProperty]
    public partial string ThresholdMessage { get; set; } = string.Empty;

    /// <summary>The sensor-logging card and the alert card, in the current language.</summary>
    public string LoggingTitleText => StringLocalizer.Get(AppStringKeys.LoggingTitle, LanguageCode);
    public string LoggingIntroText => StringLocalizer.Get(AppStringKeys.LoggingIntro, LanguageCode);
    public string RecordTitleText => StringLocalizer.Get(AppStringKeys.RecordTitle, LanguageCode);
    public string RecordNoteText => StringLocalizer.Get(AppStringKeys.RecordNote, LanguageCode);
    public string ToggleOnText => StringLocalizer.Get(AppStringKeys.ToggleOn, LanguageCode);
    public string ToggleOffText => StringLocalizer.Get(AppStringKeys.ToggleOff, LanguageCode);
    public string AlertTitleText => StringLocalizer.Get(AppStringKeys.AlertTitle, LanguageCode);
    public string AlertNoteText => StringLocalizer.Get(AppStringKeys.AlertNote, LanguageCode);
    public string AlertAboveLabelText => StringLocalizer.Get(AppStringKeys.AlertAboveLabel, LanguageCode);
    public string AlertDurationLabelText => StringLocalizer.Get(AppStringKeys.AlertDurationLabel, LanguageCode);

    public string AlertSummaryText => CpuTemperatureAlertEnabled
        ? StringLocalizer.GetFormatted(AppStringKeys.AlertSummaryOn, LanguageCode, CpuTemperatureThresholdText, CpuTemperatureThresholdSeconds)
        : StringLocalizer.Get(AppStringKeys.AlertSummaryOff, LanguageCode);

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
        ? PathSafety.DefaultExportFolder() + " " + StringLocalizer.Get(AppStringKeys.ExportDefaultSuffix, LanguageCode)
        : ExportFolder;

    /// <summary>The export-folder card, in the current language.</summary>
    public string ExportTitleText => StringLocalizer.Get(AppStringKeys.ExportTitle, LanguageCode);
    public string ExportChooseText => StringLocalizer.Get(AppStringKeys.ExportChoose, LanguageCode);
    public string ExportResetText => StringLocalizer.Get(AppStringKeys.ExportReset, LanguageCode);
    public string ExportNoteText => StringLocalizer.Get(AppStringKeys.ExportNote, LanguageCode);

    /// <summary>The language codes the picker offers, in pack order (English first).</summary>
    public IReadOnlyList<string> LanguageOptions { get; } =
        LanguagePacks.All.Select(p => p.Code).ToList();

    /// <summary>
    /// The UI language. Picking one saves immediately, rebuilds the
    /// navigation rail right away (through <see cref="Save"/> and the
    /// main view model's settings apply), and re-renders this page in
    /// the new language (v2.1.8 translates the whole Settings page).
    /// Component detail pages re-render too (v2.1.10); the remaining
    /// pages follow in later versions.
    /// </summary>
    [ObservableProperty]
    public partial string LanguageCode { get; set; } = LanguagePacks.EnglishCode;

    partial void OnLanguageCodeChanged(string value)
    {
        RaiseLocalizedTextsChanged();
        Save();
    }

    /// <summary>
    /// Re-resolves every localized text on this page for the current
    /// language. Called when the language changes; the two message
    /// properties (threshold, folder) are re-resolved too when they
    /// currently hold a message, so a rejection shown before the switch
    /// does not stay in the old language. The plug-in list is
    /// re-discovered in the new language (its status and reason are
    /// Core diagnostic strings, resolved at discovery time), and the
    /// portable-toggle message is re-resolved from its key when a
    /// toggle has happened.
    /// </summary>
    private void RaiseLocalizedTextsChanged()
    {
        OnPropertyChanged(nameof(SettingsEyebrowText));
        OnPropertyChanged(nameof(SettingsTitleText));
        OnPropertyChanged(nameof(SettingsIntroText));
        OnPropertyChanged(nameof(AppearanceTitleText));
        OnPropertyChanged(nameof(AppearanceNoteText));
        OnPropertyChanged(nameof(ThemeText));
        OnPropertyChanged(nameof(ThemeDarkText));
        OnPropertyChanged(nameof(ThemeLightText));
        OnPropertyChanged(nameof(RefreshTitleText));
        OnPropertyChanged(nameof(RefreshNoteText));
        OnPropertyChanged(nameof(RefreshRateOptions));
        OnPropertyChanged(nameof(LoggingTitleText));
        OnPropertyChanged(nameof(LoggingIntroText));
        OnPropertyChanged(nameof(RecordTitleText));
        OnPropertyChanged(nameof(RecordNoteText));
        OnPropertyChanged(nameof(ToggleOnText));
        OnPropertyChanged(nameof(ToggleOffText));
        OnPropertyChanged(nameof(AlertTitleText));
        OnPropertyChanged(nameof(AlertSummaryText));
        OnPropertyChanged(nameof(AlertNoteText));
        OnPropertyChanged(nameof(AlertAboveLabelText));
        OnPropertyChanged(nameof(AlertDurationLabelText));
        OnPropertyChanged(nameof(HardwareDatabaseTitleText));
        OnPropertyChanged(nameof(HardwareDatabaseNoteText));
        OnPropertyChanged(nameof(HardwareDatabaseVersionText));
        OnPropertyChanged(nameof(ThresholdDurationOptions));
        OnPropertyChanged(nameof(ExportTitleText));
        OnPropertyChanged(nameof(ExportFolderText));
        OnPropertyChanged(nameof(ExportChooseText));
        OnPropertyChanged(nameof(ExportResetText));
        OnPropertyChanged(nameof(ExportNoteText));
        OnPropertyChanged(nameof(DataLocationTitleText));
        OnPropertyChanged(nameof(PortableModeSummaryText));
        OnPropertyChanged(nameof(PortableModeActionText));
        OnPropertyChanged(nameof(DataLocationNoteText));
        OnPropertyChanged(nameof(PluginsTitleText));
        OnPropertyChanged(nameof(PluginsIntroText));
        OnPropertyChanged(nameof(PluginsSummaryText));
        OnPropertyChanged(nameof(PluginsRefreshText));
        OnPropertyChanged(nameof(PluginsNotLoadedNoteText));
        OnPropertyChanged(nameof(PrivacyTitleText));
        OnPropertyChanged(nameof(PrivacyText));
        OnPropertyChanged(nameof(LanguageText));
        OnPropertyChanged(nameof(LanguageTitleText));
        OnPropertyChanged(nameof(LanguageDescriptionText));
        OnPropertyChanged(nameof(LanguageNoteText));
        if (!string.IsNullOrEmpty(ThresholdMessage))
        {
            ThresholdMessage = StringLocalizer.Get(AppStringKeys.ThresholdMessage, LanguageCode);
        }

        if (!string.IsNullOrEmpty(FolderMessage))
        {
            FolderMessage = StringLocalizer.Get(AppStringKeys.ExportFolderRejected, LanguageCode);
        }

        // The plug-in list's status/reason and the discovery error are
        // resolved at discovery time, so the pass re-runs in the new
        // language; the list is small (at most 100 folders) and the
        // pass never throws. The constructor also calls
        // RefreshPluginDiscovery explicitly, so a shell seeded with a
        // non-default language discovers twice at startup — harmless.
        RefreshPluginDiscovery();
        if (_lastToggleResult is not null)
        {
            PortableModeMessage = (_lastToggleResult with { LanguageCode = LanguageCode }).Message;
        }
    }

    /// <summary>The current language's own display name ("English", "Deutsch", "Français", "Español").</summary>
    public string LanguageText => LanguagePacks.Get(LanguageCode).DisplayName;

    /// <summary>The language card's own strings, in the current language.</summary>
    public string LanguageTitleText => StringLocalizer.Get(AppStringKeys.LanguageTitle, LanguageCode);

    /// <summary>The language card's own strings, in the current language.</summary>
    public string LanguageDescriptionText => StringLocalizer.Get(AppStringKeys.LanguageDescription, LanguageCode);

    /// <summary>The language card's own strings, in the current language.</summary>
    public string LanguageNoteText => StringLocalizer.Get(AppStringKeys.LanguageNote, LanguageCode);

    /// <summary>The folder this run keeps its data in (settings, history, sensor log).</summary>
    public string DataFolderText => _portableStatus.DataFolder;

    /// <summary>
    /// One sentence on the portable state: whether data is kept next to
    /// the app or in the default folder, and which signal asked for it.
    /// A marker in a protected folder reads as off with the reason named:
    /// the marker is present but portable mode never activates there.
    /// </summary>
    /// <summary>The data-location card, in the current language.</summary>
    public string DataLocationTitleText => StringLocalizer.Get(AppStringKeys.DataLocationTitle, LanguageCode);
    public string DataLocationNoteText => StringLocalizer.Get(AppStringKeys.DataLocationNote, LanguageCode);

    public string PortableModeSummaryText => _portableStatus switch
    {
        { IsPortable: true, MarkerPresent: true, EnvRequested: true } =>
            StringLocalizer.Get(AppStringKeys.PortableSummaryOnMarkerEnv, LanguageCode),
        { IsPortable: true, MarkerPresent: true } =>
            StringLocalizer.Get(AppStringKeys.PortableSummaryOnMarker, LanguageCode),
        { IsPortable: true } =>
            StringLocalizer.Get(AppStringKeys.PortableSummaryOnEnv, LanguageCode),
        { MarkerPresent: true } =>
            StringLocalizer.Get(AppStringKeys.PortableSummaryMarkerBlocked, LanguageCode),
        _ => StringLocalizer.Get(AppStringKeys.PortableSummaryOff, LanguageCode),
    };

    /// <summary>The toggle acts on the marker file, never on the environment variable.</summary>
    public string PortableModeActionText => _portableStatus.MarkerPresent
        ? StringLocalizer.Get(AppStringKeys.PortableActionOff, LanguageCode)
        : StringLocalizer.Get(AppStringKeys.PortableActionOn, LanguageCode);

    /// <summary>Outcome of the last portable-mode change; empty when nothing was changed yet.</summary>
    [ObservableProperty]
    public partial string PortableModeMessage { get; set; } = string.Empty;

    /// <summary>
    /// Collector plug-ins, phase 1 (plan Beyond v2.0 item 2): the folder
    /// plug-ins live in, one subfolder per plug-in, under this run's data
    /// folder. Installed mode reads %LocalAppData%\TrueSpec\Plugins;
    /// portable mode reads the Plugins folder next to the exe, so the
    /// stick carries its plug-ins.
    /// </summary>
    public string PluginFolderText => Path.Combine(_portableStatus.DataFolder, "Plugins");

    /// <summary>The plug-ins the last discovery pass found, in folder-name order.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<DiscoveredPlugin> DiscoveredPlugins { get; set; } = Array.Empty<DiscoveredPlugin>();

    /// <summary>
    /// One line on what discovery found: how many plug-ins, or why none
    /// could be listed. A missing plug-ins folder reads as "no plug-ins",
    /// never as an error. The count lines and the discovery error are
    /// localized in the current language.
    /// </summary>
    public string PluginsSummaryText => _pluginDiscoveryError is not null
        ? _pluginDiscoveryError
        : DiscoveredPlugins.Count == 0
            ? StringLocalizer.Get(AppStringKeys.PluginsSummaryNone, LanguageCode)
            : DiscoveredPlugins.Count == 1
                ? StringLocalizer.Get(AppStringKeys.PluginsSummaryOne, LanguageCode)
                : StringLocalizer.GetFormatted(AppStringKeys.PluginsSummaryMany, LanguageCode, DiscoveredPlugins.Count);

    /// <summary>The plug-ins card and the Privacy card, in the current language.</summary>
    public string PluginsTitleText => StringLocalizer.Get(AppStringKeys.PluginsTitle, LanguageCode);
    public string PluginsIntroText => StringLocalizer.Get(AppStringKeys.PluginsIntro, LanguageCode);
    public string PluginsRefreshText => StringLocalizer.Get(AppStringKeys.PluginsRefresh, LanguageCode);
    public string PluginsNotLoadedNoteText => StringLocalizer.Get(AppStringKeys.PluginsNotLoadedNote, LanguageCode);
    public string PrivacyTitleText => StringLocalizer.Get(AppStringKeys.PrivacyTitle, LanguageCode);
    public string PrivacyText => StringLocalizer.Get(AppStringKeys.PrivacyText, LanguageCode);

    /// <summary>
    /// Re-runs discovery over the plug-ins folder and replaces the list.
    /// Runs in the constructor, when the user refreshes, and when the
    /// portable-mode toggle moves the data folder (and the plug-ins
    /// folder with it).
    /// </summary>
    private void RefreshPluginDiscovery()
    {
        var result = PluginDiscovery.Discover(PluginFolderText, LanguageCode);
        DiscoveredPlugins = result.Plugins;
        _pluginDiscoveryError = result.Error;
        OnPropertyChanged(nameof(PluginsSummaryText));
    }

    [RelayCommand]
    private void RefreshPlugins() => RefreshPluginDiscovery();

    [RelayCommand]
    private void TogglePortableMode()
    {
        var result = PortableMode.SetEnabled(
            _exeDirectory ?? AppContext.BaseDirectory,
            !_portableStatus.MarkerPresent,
            _environmentValue,
            LanguageCode);
        _lastToggleResult = result;
        _portableStatus = PortableMode.GetStatus(_exeDirectory, _environmentValue);
        PortableModeMessage = result.Message;
        OnPropertyChanged(nameof(DataFolderText));
        OnPropertyChanged(nameof(PortableModeSummaryText));
        OnPropertyChanged(nameof(PortableModeActionText));
        OnPropertyChanged(nameof(PluginFolderText));
        RefreshPluginDiscovery();
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
            FolderMessage = StringLocalizer.Get(AppStringKeys.ExportFolderRejected, LanguageCode);
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
            HardwareDatabaseEnabled = HardwareDatabaseEnabled,
            CpuTemperatureThresholdC = CpuTemperatureAlertEnabled && TryParseThreshold(CpuTemperatureThresholdText, out var threshold)
                ? threshold
                : null,
            CpuTemperatureThresholdSeconds = CpuTemperatureThresholdSeconds,
            LanguageCode = LanguageCode,
        }.Sanitized();

        RefreshIntervalSeconds = _settings.RefreshIntervalSeconds;
        ExportFolder = _settings.ExportFolder;
        SensorLoggingEnabled = _settings.SensorLoggingEnabled;
        HardwareDatabaseEnabled = _settings.HardwareDatabaseEnabled;
        CpuTemperatureAlertEnabled = _settings.CpuTemperatureThresholdC is not null;
        if (_settings.CpuTemperatureThresholdC is { } savedThreshold)
        {
            CpuTemperatureThresholdText = savedThreshold.ToString("0.#", CultureInfo.InvariantCulture);
        }

        CpuTemperatureThresholdSeconds = _settings.CpuTemperatureThresholdSeconds;
        LanguageCode = _settings.LanguageCode;
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
