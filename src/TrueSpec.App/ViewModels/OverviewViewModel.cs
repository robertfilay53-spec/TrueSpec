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

namespace TrueSpec.App.ViewModels;

/// <summary>The Overview page: hero, component cards and the live strip.</summary>
public sealed partial class OverviewViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly ISensorMonitor _sensorMonitor;
    private readonly IReadOnlyList<SpecValue> _cpuSpecs;
    private bool _refreshing;

    public OverviewViewModel(
        SystemSnapshot snapshot,
        Action<string> openComponent,
        ICommand newLaptopCheckCommand,
        ICommand exportCommand,
        ISensorMonitor sensorMonitor)
    {
        _sensorMonitor = sensorMonitor;
        MachineName = snapshot.MachineName;
        OperatingSystem = snapshot.OperatingSystem;
        Score = snapshot.Score.OverallScore;
        ScoreText = snapshot.Score.HasMeasurableComponents ? $"{Score:0}" : "--";
        VerdictText = snapshot.Score.Verdict.ToDisplayText();
        VerdictStatus = snapshot.Score.Verdict.ToHealthStatus();
        ScoreExplanation = snapshot.Score.Explanation;
        NewLaptopCheckCommand = newLaptopCheckCommand;
        ExportCommand = exportCommand;

        _cpuSpecs = snapshot.Find("cpu")?.Specs ?? Array.Empty<SpecValue>();
        KeyStats = BuildKeyStats(snapshot);
        Cards = new ObservableCollection<ComponentCardViewModel>(
            snapshot.Components.Select(c => new ComponentCardViewModel(c, openComponent)));
        LiveMetrics = new ObservableCollection<LiveMetricViewModel>
        {
            new("CPU temp", "C"),
            new("GPU temp", "C"),
            new("CPU usage", "%"),
            new("Battery drain", "W"),
        };
        StatusMessage = sensorMonitor.StatusText;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshSensorsAsync();
        _timer.Start();
        _ = RefreshSensorsAsync();
    }

    public string MachineName { get; }

    public string OperatingSystem { get; }

    public double Score { get; }

    public string ScoreText { get; }

    public string VerdictText { get; }

    public HealthStatus VerdictStatus { get; }

    public string ScoreExplanation { get; }

    public IReadOnlyList<KeyStatViewModel> KeyStats { get; }

    public ObservableCollection<ComponentCardViewModel> Cards { get; }

    public ObservableCollection<LiveMetricViewModel> LiveMetrics { get; }

    public ICommand NewLaptopCheckCommand { get; }

    public ICommand ExportCommand { get; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

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

            UpdateMetric("CPU temp", snapshot.CpuTemperature()?.Value);
            UpdateMetric("GPU temp", snapshot.GpuTemperature()?.Value);
            UpdateMetric("CPU usage", snapshot.CpuLoad()?.Value);
            UpdateMetric("Battery drain", snapshot.BatteryPower()?.Value);
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
        var cpuTemp = cpuTempSpec?.Value ?? "Not reported by hardware";
        var cpuTempStatus = cpuTempSpec is not null && SpecText.TryParseLeadingNumber(cpuTemp, out var temp)
            ? temp switch
            {
                < 80d => HealthStatus.Healthy,
                < 90d => HealthStatus.Watch,
                _ => HealthStatus.Problem,
            }
            : HealthStatus.NotMeasurable;

        var ramSize = SpecValueOf(ram, "Total memory") ?? "Not reported by hardware";
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
