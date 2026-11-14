using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TrueSpec.Core.Health;
using TrueSpec.Core.Localization;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;
using TrueSpec.Core.Settings;

namespace TrueSpec.App.ViewModels;

/// <summary>
/// The Overview page: hero, component cards and the live strip.
///
/// One instance lives for the whole app lifetime and is updated in place
/// whenever a scan finishes (see <see cref="UpdateSnapshot"/>). The page the
/// window already shows can therefore never be a stale copy of an older
/// snapshot: Overview, Report and every export all read the same, current
/// snapshot. While a scan is running the hero says "Scanning..." — it never
/// claims "Not measurable", because "no score yet" is not the same as
/// "no measurable health value".
///
/// Localisation, phase 5 (v2.1.11): the page chrome renders in the
/// current language — the hero labels, the section headings, the buttons,
/// the score note, the key-stat labels, the live-strip labels and the
/// sensor-log and alert-setting sentences — so the landing page follows
/// the language the user picked in Settings, with no restart. The data
/// the page shows stays English: component names, headlines, health and
/// status texts, spec labels, values and sources, the score explanation
/// and the verdict text are measured data produced by the collectors and
/// the scoring engine; the admin notice and the threshold-alert texts
/// are diagnostic strings produced by Core with no language parameter
/// yet. The shell re-languages this long-lived page through
/// <see cref="ApplyLanguage"/> when the user picks a language in
/// Settings, the same in-place pattern the detail pages use.
/// </summary>
public sealed partial class OverviewViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly ISensorMonitor _sensorMonitor;
    private readonly SensorLogWriter? _sensorLogWriter;
    private readonly ThresholdAlertEvaluator? _alertEvaluator;
    private readonly Action<string> _openComponent;
    private readonly bool _isElevated;
    private IReadOnlyList<SpecValue> _cpuSpecs = Array.Empty<SpecValue>();
    private bool _refreshing;

    private string _languageCode;
    // Set by the constructor through UpdateSnapshot, before anything
    // reads it; ApplyLanguage re-runs UpdateSnapshot from it.
    private SystemSnapshot _lastSnapshot = null!;
    private bool _lastIsScanning;
    private AppSettings? _storedSettings;

    public OverviewViewModel(
        SystemSnapshot snapshot,
        Action<string> openComponent,
        ICommand newLaptopCheckCommand,
        ICommand exportCommand,
        ISensorMonitor sensorMonitor,
        TimeSpan? refreshInterval = null,
        bool isScanning = false,
        SensorLogWriter? sensorLogWriter = null,
        ThresholdAlertEvaluator? alertEvaluator = null,
        AppSettings? settings = null,
        bool? isElevated = null,
        string? languageCode = null)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        _sensorMonitor = sensorMonitor;
        _sensorLogWriter = sensorLogWriter;
        _alertEvaluator = alertEvaluator;
        _openComponent = openComponent;
        // Reuses the shell's elevation check (Environment.IsPrivilegedProcess):
        // injectable so tests can drive the notice without elevation.
        _isElevated = isElevated ?? Environment.IsPrivilegedProcess;
        NewLaptopCheckCommand = newLaptopCheckCommand;
        ExportCommand = exportCommand;
        Cards = new ObservableCollection<ComponentCardViewModel>();
        SensorAlerts = new ObservableCollection<string>();
        LiveMetrics = new ObservableCollection<LiveMetricViewModel>
        {
            // Built from stable keys: the display name resolves in the
            // page language and re-resolves on a switch, while the key
            // the strip updates by never changes.
            new(LiveMetricViewModel.CpuTempKey, "C", _languageCode),
            new(LiveMetricViewModel.GpuTempKey, "C", _languageCode),
            new(LiveMetricViewModel.CpuUsageKey, "%", _languageCode),
            // "Battery power", not "Battery drain": LibreHardwareMonitor
            // reports a signed power flow, negative while the battery is
            // charging, so "drain" would be wrong half the time.
            new(LiveMetricViewModel.BatteryPowerKey, "W", _languageCode),
        };

        UpdateSnapshot(snapshot, isScanning);

        StatusMessage = sensorMonitor.StatusText;
        ApplySensorSettings(settings ?? AppSettings.Default);

        _timer = new DispatcherTimer { Interval = refreshInterval ?? TimeSpan.FromSeconds(2) };
        RefreshText = BuildRefreshText(_timer.Interval);
        _timer.Tick += async (_, _) => await RefreshSensorsAsync();
        _timer.Start();
        _ = RefreshSensorsAsync();
    }

    /// <summary>
    /// Refreshes every snapshot-derived value in place from the latest scan.
    /// The shell calls this after every scan instead of building a new page,
    /// so the Overview on screen always shows the current score and verdict.
    /// While <paramref name="isScanning"/> is true the hero reports the scan
    /// in progress; a finished scan with genuinely no measurable component
    /// reports "Not measurable" instead.
    /// </summary>
    public void UpdateSnapshot(SystemSnapshot snapshot, bool isScanning)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Kept so ApplyLanguage can rebuild the snapshot-derived rows
        // (score breakdown, key stats, cards) in the new language.
        _lastSnapshot = snapshot;
        _lastIsScanning = isScanning;

        MachineName = snapshot.MachineName;
        OperatingSystem = snapshot.OperatingSystem;

        if (isScanning)
        {
            Score = 0d;
            ScoreText = "…";
            VerdictText = StringLocalizer.Get(AppStringKeys.OverviewScanning, _languageCode);
            VerdictStatus = HealthStatus.NotMeasurable;
            ScoreExplanation = StringLocalizer.Get(AppStringKeys.OverviewScanningNote, _languageCode);
            ScoreBreakdown = Array.Empty<ScoreBreakdownRow>();
        }
        else
        {
            Score = snapshot.Score.OverallScore;
            ScoreText = snapshot.Score.HasMeasurableComponents ? $"{Score:0.#}" : "--";
            VerdictText = snapshot.Score.Verdict.ToDisplayText();
            VerdictStatus = snapshot.Score.Verdict.ToHealthStatus();
            ScoreExplanation = snapshot.Score.Explanation;
            ScoreBreakdown = snapshot.Score.Components
                .Select(c => new ScoreBreakdownRow(
                    c.Component,
                    $"{c.HealthPercent:0.#}%",
                    StringLocalizer.GetFormatted(
                        AppStringKeys.OverviewWeight,
                        _languageCode,
                        c.Weight.ToString("0.#", CultureInfo.InvariantCulture)),
                    StringLocalizer.GetFormatted(
                        AppStringKeys.OverviewContribution,
                        _languageCode,
                        c.Contribution.ToString("0.#", CultureInfo.InvariantCulture)),
                    HealthStatusExtensions.FromHealth(c.HealthPercent)))
                .ToList();
        }

        _cpuSpecs = snapshot.Find("cpu")?.Specs ?? Array.Empty<SpecValue>();
        KeyStats = BuildKeyStats(snapshot);

        Cards.Clear();
        foreach (var card in snapshot.Components.Select(c => new ComponentCardViewModel(c, _openComponent, _languageCode)))
        {
            Cards.Add(card);
        }

        HasComponents = Cards.Count > 0;

        // The admin notice is recomputed from every finished scan: a
        // standard-user scan with silent admin-gated readings says so
        // plainly instead of leaving blank cards unexplained. While a scan
        // is running there is no snapshot to judge, so no banner.
        AdminNoticeText = isScanning
            ? string.Empty
            : AdminElevationNotice.ForSnapshot(snapshot, _isElevated) ?? string.Empty;
        HasAdminNotice = AdminNoticeText.Length > 0;
    }

    /// <summary>
    /// Called by the shell when the user changes a setting. The timer is updated live; the sensor read itself already
    /// runs off the UI thread (Task.Run inside the monitor).
    /// </summary>
    public void SetRefreshInterval(TimeSpan interval)
    {
        var clamped = TimeSpan.FromSeconds(Math.Clamp(interval.TotalSeconds, 1, 30));
        _timer.Interval = clamped;
        RefreshText = BuildRefreshText(clamped);
        OnPropertyChanged(nameof(RefreshText));
    }

    /// <summary>
    /// Applies the sensor settings live: logging on/off and the CPU
    /// temperature alert threshold. Called by the shell whenever settings
    /// change, and once from the constructor with the loaded settings.
    /// Reconfiguring the threshold resets any crossing in progress: the
    /// old threshold is no longer being watched.
    /// </summary>
    public void ApplySensorSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Kept so ApplyLanguage can re-resolve these sentences in the
        // new language without the shell re-passing the settings.
        _storedSettings = settings;

        if (_sensorLogWriter is not null)
        {
            _sensorLogWriter.Enabled = settings.SensorLoggingEnabled;
        }

        _alertEvaluator?.Configure(settings.CpuTemperatureThresholdC, settings.CpuTemperatureThresholdSeconds);

        SensorLogText = _sensorLogWriter is null
            ? StringLocalizer.Get(AppStringKeys.OverviewSensorLogUnavailable, _languageCode)
            : settings.SensorLoggingEnabled
                ? StringLocalizer.GetFormatted(
                    AppStringKeys.OverviewSensorLogOn, _languageCode, _sensorLogWriter.FilePath)
                : StringLocalizer.Get(AppStringKeys.OverviewSensorLogOff, _languageCode);
        AlertSettingsText = settings.CpuTemperatureThresholdC is { } threshold
            ? StringLocalizer.GetFormatted(
                AppStringKeys.OverviewAlertSet,
                _languageCode,
                threshold.ToString("0.#", CultureInfo.InvariantCulture),
                settings.CpuTemperatureThresholdSeconds)
            : StringLocalizer.Get(AppStringKeys.OverviewAlertNotSet, _languageCode);
        OnPropertyChanged(nameof(SensorLogText));
        OnPropertyChanged(nameof(AlertSettingsText));
    }

    /// <summary>The admin-notice eyebrow, in the page language.</summary>
    public string AdminEyebrowText =>
        StringLocalizer.Get(AppStringKeys.OverviewAdminEyebrow, _languageCode);

    /// <summary>"out of 100" under the score ring.</summary>
    public string OutOf100Text =>
        StringLocalizer.Get(AppStringKeys.OverviewOutOf100, _languageCode);

    /// <summary>The badge next to the verdict.</summary>
    public string OverallHealthText =>
        StringLocalizer.Get(AppStringKeys.OverviewOverallHealth, _languageCode);

    /// <summary>The note under the score breakdown.</summary>
    public string ScoreNoteText =>
        StringLocalizer.Get(AppStringKeys.OverviewScoreNote, _languageCode);

    /// <summary>The hero's New Laptop Check button.</summary>
    public string NewLaptopCheckText =>
        StringLocalizer.Get(AppStringKeys.OverviewNewLaptopCheck, _languageCode);

    /// <summary>The hero's Export Report button.</summary>
    public string ExportReportText =>
        StringLocalizer.Get(AppStringKeys.OverviewExportReport, _languageCode);

    /// <summary>The heading above the component cards.</summary>
    public string ComponentHealthText =>
        StringLocalizer.Get(AppStringKeys.OverviewComponentHealth, _languageCode);

    /// <summary>The empty state when no component was collected.</summary>
    public string NoComponentsText =>
        StringLocalizer.Get(AppStringKeys.OverviewNoComponents, _languageCode);

    /// <summary>The heading above the live strip.</summary>
    public string LiveSensorsText =>
        StringLocalizer.Get(AppStringKeys.OverviewLiveSensors, _languageCode);

    /// <summary>The temperature-alert card's eyebrow.</summary>
    public string AlertEyebrowText =>
        StringLocalizer.Get(AppStringKeys.OverviewAlertEyebrow, _languageCode);

    /// <summary>The note under the temperature-alert list.</summary>
    public string AlertNoteText =>
        StringLocalizer.Get(AppStringKeys.OverviewAlertNote, _languageCode);

    /// <summary>The footer line at the bottom of the page.</summary>
    public string FooterText =>
        StringLocalizer.Get(AppStringKeys.OverviewFooter, _languageCode);

    /// <summary>
    /// Re-renders this page in the given language, in place: every
    /// localized text is re-resolved, the snapshot-derived rows are
    /// rebuilt from the last scan, the live-strip names re-resolve, and
    /// the sensor sentences are rebuilt from the stored settings.
    /// Called by the shell when the user picks a language in Settings;
    /// a page created after the switch is already built in the new
    /// language by the constructor. An unknown code falls back to
    /// English through the localizer, never to a blank. Measured data
    /// (verdict, explanation, component names, values) is untouched:
    /// it is rebuilt from the same snapshot, so it keeps its values.
    /// </summary>
    public void ApplyLanguage(string? languageCode)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        UpdateSnapshot(_lastSnapshot, _lastIsScanning);
        if (_storedSettings is not null)
        {
            ApplySensorSettings(_storedSettings);
        }

        SetRefreshInterval(_timer.Interval);
        foreach (var metric in LiveMetrics)
        {
            metric.ApplyLanguage(_languageCode);
        }

        OnPropertyChanged(nameof(AdminEyebrowText));
        OnPropertyChanged(nameof(OutOf100Text));
        OnPropertyChanged(nameof(OverallHealthText));
        OnPropertyChanged(nameof(ScoreNoteText));
        OnPropertyChanged(nameof(NewLaptopCheckText));
        OnPropertyChanged(nameof(ExportReportText));
        OnPropertyChanged(nameof(ComponentHealthText));
        OnPropertyChanged(nameof(NoComponentsText));
        OnPropertyChanged(nameof(LiveSensorsText));
        OnPropertyChanged(nameof(AlertEyebrowText));
        OnPropertyChanged(nameof(AlertNoteText));
        OnPropertyChanged(nameof(FooterText));
    }

    private string BuildRefreshText(TimeSpan interval)
    {
        var seconds = interval.TotalSeconds;
        // Reuses the Settings seconds keys: "1 second" / "{0} seconds"
        // resolve in the page language, and the number inside keeps the
        // app's invariant formatting.
        var label = seconds == 1
            ? StringLocalizer.Get(AppStringKeys.SecondsOne, _languageCode)
            : StringLocalizer.GetFormatted(
                AppStringKeys.SecondsMany,
                _languageCode,
                seconds.ToString("0", CultureInfo.InvariantCulture));
        return StringLocalizer.GetFormatted(AppStringKeys.OverviewRefreshText, _languageCode, label);
    }

    [ObservableProperty]
    private string _machineName = string.Empty;

    [ObservableProperty]
    private string _operatingSystem = string.Empty;

    /// <summary>Numeric score for the ring. 0 while scanning or unmeasurable.</summary>
    [ObservableProperty]
    private double _score;

    /// <summary>"…" while scanning, "--" when a finished scan has no measurable component, otherwise the score.</summary>
    [ObservableProperty]
    private string _scoreText = "--";

    [ObservableProperty]
    private string _verdictText = string.Empty;

    [ObservableProperty]
    private HealthStatus _verdictStatus = HealthStatus.NotMeasurable;

    [ObservableProperty]
    private string _scoreExplanation = string.Empty;

    /// <summary>Per-component score breakdown: health, weight and points contributed.</summary>
    [ObservableProperty]
    private IReadOnlyList<ScoreBreakdownRow> _scoreBreakdown = Array.Empty<ScoreBreakdownRow>();

    [ObservableProperty]
    private IReadOnlyList<KeyStatViewModel> _keyStats = Array.Empty<KeyStatViewModel>();

    public ObservableCollection<ComponentCardViewModel> Cards { get; }

    /// <summary>False when the scan collected no components: the view shows an empty state instead of a blank section.</summary>
    [ObservableProperty]
    public partial bool HasComponents { get; set; }

    /// <summary>
    /// The "Run as administrator for full results" banner text, empty when
    /// no notice applies (elevated process, or no admin-gated gaps).
    /// </summary>
    [ObservableProperty]
    public partial string AdminNoticeText { get; set; } = string.Empty;

    /// <summary>True when the admin notice banner should be shown.</summary>
    [ObservableProperty]
    public partial bool HasAdminNotice { get; set; }

    public ObservableCollection<LiveMetricViewModel> LiveMetrics { get; }

    /// <summary>Threshold alerts fired this session, newest first, capped at 10.</summary>
    public ObservableCollection<string> SensorAlerts { get; }

    /// <summary>The latest threshold alert, if any. Empty when no alert has fired.</summary>
    [ObservableProperty]
    public partial string LatestSensorAlertText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSensorAlert { get; set; }

    /// <summary>Where the sensor log lives, or why there is none.</summary>
    [ObservableProperty]
    public partial string SensorLogText { get; set; } = string.Empty;

    /// <summary>What the temperature alert is set to, or that none is set.</summary>
    [ObservableProperty]
    public partial string AlertSettingsText { get; set; } = string.Empty;

    public ICommand NewLaptopCheckCommand { get; }

    public ICommand ExportCommand { get; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial string RefreshText { get; set; } = string.Empty;

    public void Dispose()
    {
        _timer.Stop();
    }

    private async Task RefreshSensorsAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var snapshot = await _sensorMonitor.ReadAsync();
            StatusMessage = snapshot.IsAvailable
                ? StringLocalizer.GetFormatted(
                    AppStringKeys.OverviewLiveUpdated,
                    _languageCode,
                    DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture))
                : snapshot.StatusText;

            ProcessSensorSnapshot(snapshot);
        }
        catch (Exception exception)
        {
            StatusMessage = StringLocalizer.GetFormatted(
                AppStringKeys.OverviewLiveReadFailed, _languageCode, exception.Message);
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// Handles one live sensor snapshot: updates the strip, appends the
    /// sample to the sensor log, and evaluates the temperature threshold.
    /// Public so tests can drive it deterministically without waiting on
    /// the refresh timer. Never throws: logging and alerting must never
    /// break the live strip.
    /// </summary>
    public void ProcessSensorSnapshot(SensorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        UpdateMetric(LiveMetricViewModel.CpuTempKey, snapshot.CpuTemperature()?.Value);
        UpdateMetric(LiveMetricViewModel.GpuTempKey, snapshot.GpuTemperature()?.Value);
        UpdateMetric(LiveMetricViewModel.CpuUsageKey, snapshot.CpuLoad()?.Value);
        UpdateMetric(LiveMetricViewModel.BatteryPowerKey, snapshot.BatteryPower()?.Value);

        _sensorLogWriter?.Append(snapshot);

        if (_alertEvaluator is not null)
        {
            var alert = _alertEvaluator.Evaluate(snapshot.CpuTemperature()?.Value, snapshot.CapturedAt);
            if (alert is not null)
            {
                SensorAlerts.Insert(0, alert.Text);
                while (SensorAlerts.Count > 10)
                {
                    SensorAlerts.RemoveAt(SensorAlerts.Count - 1);
                }

                LatestSensorAlertText = alert.Text;
                HasSensorAlert = true;
            }
        }
    }

    private void UpdateMetric(string key, double? value)
    {
        // Looked up by stable key, never by display name: the name is
        // the translated label and changes with the language, the key
        // does not. A legacy metric (no key) can never match here, so a
        // metric built without a key is never updated by mistake.
        var metric = LiveMetrics.FirstOrDefault(m =>
            string.Equals(m.MetricKey, key, StringComparison.Ordinal));
        metric?.AddSample(value);
    }

    private IReadOnlyList<KeyStatViewModel> BuildKeyStats(SystemSnapshot snapshot)
    {
        var ram = snapshot.Find("ram");
        var storage = snapshot.Find("storage");
        var battery = snapshot.Find("battery");

        var cpuTempSpec = _cpuSpecs.FirstOrDefault(s =>
            string.Equals(s.Label, "Package temperature", StringComparison.OrdinalIgnoreCase));
        var cpuTemp = cpuTempSpec?.Value ?? SpecLabels.NotReportedByHardware;
        var cpuTempStatus = cpuTempSpec is not null && SpecText.TryParseLeadingNumber(cpuTemp, out var temp)
            ? temp switch
            {
                < 80d => HealthStatus.Healthy,
                < 90d => HealthStatus.Watch,
                _ => HealthStatus.Problem,
            }
            : HealthStatus.NotMeasurable;

        var ramSize = SpecValueOf(ram, "Total memory") ?? SpecLabels.NotReportedByHardware;
        var diskHealth = storage?.HealthText ?? "Not measurable";
        var batteryHealth = battery?.HealthText ?? "Not measurable";

        return new[]
        {
            // The labels are fixed page chrome, so they translate; the
            // values and sources are measured data and stay as reported.
            new KeyStatViewModel(StringLocalizer.Get(AppStringKeys.OverviewKeyCpuTemp, _languageCode), cpuTemp, cpuTempSpec?.Source ?? "LibreHardwareMonitor", cpuTempStatus),
            new KeyStatViewModel(StringLocalizer.Get(AppStringKeys.OverviewKeyMemory, _languageCode), ramSize, "CIM Win32_PhysicalMemory", HealthStatus.NotMeasurable),
            new KeyStatViewModel(StringLocalizer.Get(AppStringKeys.OverviewKeyDiskHealth, _languageCode), diskHealth, "SMART / NVMe reliability counters", storage?.Status ?? HealthStatus.NotMeasurable),
            new KeyStatViewModel(StringLocalizer.Get(AppStringKeys.OverviewKeyBatteryHealth, _languageCode), batteryHealth, "Battery firmware", battery?.Status ?? HealthStatus.NotMeasurable),
        };
    }

    private static string? SpecValueOf(ComponentSnapshot? component, string label) =>
        component?.Specs.FirstOrDefault(s =>
            string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase))?.Value;
}

public sealed record KeyStatViewModel(string Label, string Value, string Source, HealthStatus Status);

public sealed record ScoreBreakdownRow(
    string Component,
    string HealthText,
    string WeightText,
    string ContributionText,
    HealthStatus Status);

public sealed class ComponentCardViewModel : ViewModelBase
{
    private readonly string _languageCode;

    public ComponentCardViewModel(ComponentSnapshot component, Action<string> openComponent, string? languageCode = null)
    {
        _languageCode = languageCode ?? LanguagePacks.EnglishCode;
        Id = component.Id;
        Name = component.Name;
        IconData = IconLibrary.Get(component.IconKey);
        Headline = component.Headline;
        HealthPercent = component.HealthPercent;
        HealthValue = component.HealthPercent ?? 0d;
        HealthText = component.HealthText;
        Status = component.Status;
        HasHealth = component.HealthPercent is not null;
        DetailsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => openComponent(Id));
    }

    /// <summary>The "Health" label above the card's health bar, in the card's language.</summary>
    public string HealthLabelText =>
        StringLocalizer.Get(AppStringKeys.OverviewHealthLabel, _languageCode);

    /// <summary>The card's "Details" button, in the card's language.</summary>
    public string DetailsText =>
        StringLocalizer.Get(AppStringKeys.OverviewDetails, _languageCode);

    public string Id { get; }

    public string Name { get; }

    public string IconData { get; }

    public string Headline { get; }

    public double? HealthPercent { get; }

    public double HealthValue { get; }

    public string HealthText { get; }

    public HealthStatus Status { get; }

    public bool HasHealth { get; }

    public ICommand DetailsCommand { get; }
}
