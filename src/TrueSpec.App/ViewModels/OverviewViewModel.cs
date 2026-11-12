using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TrueSpec.Core.Health;
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
        bool? isElevated = null)
    {
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
            new("CPU temp", "C"),
            new("GPU temp", "C"),
            new("CPU usage", "%"),
            // "Battery power", not "Battery drain": LibreHardwareMonitor
            // reports a signed power flow, negative while the battery is
            // charging, so "drain" would be wrong half the time.
            new("Battery power", "W"),
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

        MachineName = snapshot.MachineName;
        OperatingSystem = snapshot.OperatingSystem;

        if (isScanning)
        {
            Score = 0d;
            ScoreText = "…";
            VerdictText = "Scanning…";
            VerdictStatus = HealthStatus.NotMeasurable;
            ScoreExplanation = "The hardware scan is still running. The score appears when the scan finishes.";
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
                    $"Weight {c.Weight:0.#}",
                    $"+{c.Contribution:0.#} pts",
                    HealthStatusExtensions.FromHealth(c.HealthPercent)))
                .ToList();
        }

        _cpuSpecs = snapshot.Find("cpu")?.Specs ?? Array.Empty<SpecValue>();
        KeyStats = BuildKeyStats(snapshot);

        Cards.Clear();
        foreach (var card in snapshot.Components.Select(c => new ComponentCardViewModel(c, _openComponent)))
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

        if (_sensorLogWriter is not null)
        {
            _sensorLogWriter.Enabled = settings.SensorLoggingEnabled;
        }

        _alertEvaluator?.Configure(settings.CpuTemperatureThresholdC, settings.CpuTemperatureThresholdSeconds);

        SensorLogText = _sensorLogWriter is null
            ? "Sensor logging is unavailable on this machine."
            : settings.SensorLoggingEnabled
                ? $"Sensor readings are recorded to {_sensorLogWriter.FilePath}. The chart is on the History page."
                : "Sensor logging is off. Turn it on in Settings to record readings.";
        AlertSettingsText = settings.CpuTemperatureThresholdC is { } threshold
            ? $"Alert when the CPU temperature reaches {threshold.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} C for {settings.CpuTemperatureThresholdSeconds} seconds. Set it in Settings."
            : "No temperature alert set. TrueSpec ships with no threshold; set one in Settings if you want an alert.";
        OnPropertyChanged(nameof(SensorLogText));
        OnPropertyChanged(nameof(AlertSettingsText));
    }

    private static string BuildRefreshText(TimeSpan interval)
    {
        var seconds = interval.TotalSeconds;
        var label = seconds == 1 ? "1 second" : $"{seconds:0} seconds";
        return $"Live sensors refresh every {label}. Values appear only when LibreHardwareMonitor reports them.";
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
                ? $"Live sensors updated {DateTime.Now:HH:mm:ss}"
                : snapshot.StatusText;

            ProcessSensorSnapshot(snapshot);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Live sensor read failed: {exception.Message}";
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

        UpdateMetric("CPU temp", snapshot.CpuTemperature()?.Value);
        UpdateMetric("GPU temp", snapshot.GpuTemperature()?.Value);
        UpdateMetric("CPU usage", snapshot.CpuLoad()?.Value);
        UpdateMetric("Battery power", snapshot.BatteryPower()?.Value);

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

    private void UpdateMetric(string name, double? value)
    {
        var metric = LiveMetrics.FirstOrDefault(m =>
            string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
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
            new KeyStatViewModel("CPU temperature", cpuTemp, cpuTempSpec?.Source ?? "LibreHardwareMonitor", cpuTempStatus),
            new KeyStatViewModel("Memory", ramSize, "CIM Win32_PhysicalMemory", HealthStatus.NotMeasurable),
            new KeyStatViewModel("Disk health", diskHealth, "SMART / NVMe reliability counters", storage?.Status ?? HealthStatus.NotMeasurable),
            new KeyStatViewModel("Battery health", batteryHealth, "Battery firmware", battery?.Status ?? HealthStatus.NotMeasurable),
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
    public ComponentCardViewModel(ComponentSnapshot component, Action<string> openComponent)
    {
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
