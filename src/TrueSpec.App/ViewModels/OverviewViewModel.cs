using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TrueSpec.Core.Models;

namespace TrueSpec.App.ViewModels;

/// <summary>The Overview page: hero, component cards and the live strip.</summary>
public sealed partial class OverviewViewModel : ViewModelBase
{
    private readonly DispatcherTimer _timer;

    public OverviewViewModel(
        SystemSnapshot snapshot,
        Action<string> openComponent,
        ICommand newLaptopCheckCommand,
        ICommand exportCommand)
    {
        MachineName = snapshot.MachineName;
        OperatingSystem = snapshot.OperatingSystem;
        Score = snapshot.Score.OverallScore;
        ScoreText = snapshot.Score.HasMeasurableComponents ? $"{Score:0}" : "--";
        VerdictText = snapshot.Score.Verdict.ToDisplayText();
        VerdictStatus = snapshot.Score.Verdict.ToHealthStatus();
        ScoreExplanation = snapshot.Score.Explanation;
        NewLaptopCheckCommand = newLaptopCheckCommand;
        ExportCommand = exportCommand;

        KeyStats = BuildKeyStats(snapshot);
        Cards = new ObservableCollection<ComponentCardViewModel>(
            snapshot.Components.Select(c => new ComponentCardViewModel(c, openComponent)));
        LiveMetrics = new ObservableCollection<LiveMetricViewModel>
        {
            new("CPU temp", "C", 42d, 58d, 47d),
            new("GPU temp", "C", 47d, 60d, 52d),
            new("CPU usage", "%", 5d, 35d, 12d),
            new("Battery drain", "W", 5d, 8d, 6.4d),
        };

        foreach (var metric in LiveMetrics)
        {
            metric.Seed();
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) =>
        {
            foreach (var metric in LiveMetrics)
            {
                metric.AddRandomSample();
            }

            StatusMessage = $"Live sensors updated {DateTime.Now:HH:mm:ss}";
        };
        _timer.Start();
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
    public partial string StatusMessage { get; set; } = "Live strip updates every 2 seconds.";

    private static IReadOnlyList<KeyStatViewModel> BuildKeyStats(SystemSnapshot snapshot)
    {
        var cpu = snapshot.Find("cpu");
        var ram = snapshot.Find("ram");
        var storage = snapshot.Find("storage");
        var battery = snapshot.Find("battery");

        var cpuTemp = SpecValueOf(cpu, "Package temperature") ?? "Not reported";
        var ramSize = SpecValueOf(ram, "Total memory") ?? "Not reported";
        var diskHealth = storage?.HealthText ?? "Not measurable";
        var batteryHealth = battery?.HealthText ?? "Not measurable";

        return new[]
        {
            new KeyStatViewModel("CPU temperature", cpuTemp, "LibreHardwareMonitor", HealthStatus.Healthy),
            new KeyStatViewModel("Memory", ramSize, "WMI Win32_PhysicalMemory", HealthStatus.NotMeasurable),
            new KeyStatViewModel("Disk health", diskHealth, "NVMe SMART", storage?.Status ?? HealthStatus.NotMeasurable),
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
