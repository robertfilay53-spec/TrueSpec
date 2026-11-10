namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;

/// <summary>
/// Graphics adapter identity and driver from CIM, with real temperature, load,
/// fan, power and clock sensors from LibreHardwareMonitor.
/// </summary>
public sealed class GpuCollector : ICollector
{
    private readonly ISensorMonitor _sensors;

    public GpuCollector(ISensorMonitor sensors)
    {
        _sensors = sensors;
    }

    public string Id => "gpu";

    public string Name => "GPU";

    public string Category => "GPU";

    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(2);

    public bool RequiresAdministrator => true;

    public async Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var adapters = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_VideoController");
        if (adapters.Count == 0)
        {
            return ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "CIM did not return Win32_VideoController.");
        }

        var adapter = adapters[0];
        var sensors = await _sensors.ReadAsync(cancellationToken);
        var temperature = sensors.GpuTemperature();
        var load = sensors.Find(SensorKind.Load, "GPU", "GPU Core", "Core");
        var power = sensors.Find(SensorKind.Power, "GPU", "GPU Package", "GPU Power");
        var fan = sensors.Find(SensorKind.Fan, "GPU", "GPU Fan", "Fan");
        var voltage = sensors.Find(SensorKind.Voltage, "GPU", "GPU Core", "Core");
        var clock = sensors.Find(SensorKind.Clock, "GPU", "GPU Core", "Core");

        var temperatureValue = temperature?.Value;
        var throttle = temperatureValue switch
        {
            >= 90d => "Yes",
            not null => "No",
            _ => CollectorSupport.NotReported,
        };

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec("Model", adapter.String("Name"), "CIM Win32_VideoController"),
            CollectorSupport.Spec("Vendor", adapter.String("AdapterCompatibility"), "CIM Win32_VideoController"),
            CollectorSupport.Spec("Dedicated memory", CollectorSupport.FormatBytes(adapter.UInt64("AdapterRAM")), "CIM Win32_VideoController.AdapterRAM"),
            CollectorSupport.Spec("Driver version", adapter.String("DriverVersion"), "CIM Win32_VideoController"),
            CollectorSupport.Spec("Driver date", adapter.DateText("DriverDate"), "CIM Win32_VideoController.DriverDate"),
            CollectorSupport.Spec("Video processor", adapter.String("VideoProcessor"), "CIM Win32_VideoController"),
            CollectorSupport.Spec("Current mode", adapter.String("VideoModeDescription"), "CIM Win32_VideoController"),
            CollectorSupport.Spec("DirectX support", "Not reported by hardware", "WMI does not expose the DirectX feature level"),
            CollectorSupport.Spec("GPU temperature", FormatSensor(temperature, "C", 1), "LibreHardwareMonitor"),
            CollectorSupport.Spec("Thermal throttle flag", throttle, "LibreHardwareMonitor temperature threshold (90 C)"),
            CollectorSupport.Spec("GPU load", FormatSensor(load, "%", 1), "LibreHardwareMonitor"),
            CollectorSupport.Spec("GPU power", FormatSensor(power, "W", 1), "LibreHardwareMonitor"),
            CollectorSupport.Spec("GPU fan", FormatSensor(fan, "RPM", 0), "LibreHardwareMonitor"),
            CollectorSupport.Spec("GPU voltage", FormatSensor(voltage, "V", 3), "LibreHardwareMonitor"),
            CollectorSupport.Spec("GPU clock", FormatSensor(clock, "MHz", 0), "LibreHardwareMonitor"),
        };

        for (var i = 1; i < adapters.Count; i++)
        {
            var extra = adapters[i];
            var prefix = string.Create(CultureInfo.InvariantCulture, $"Adapter {i + 1}");
            specs.Add(CollectorSupport.Spec($"{prefix} - Model", extra.String("Name"), "CIM Win32_VideoController"));
            specs.Add(CollectorSupport.Spec($"{prefix} - Driver version", extra.String("DriverVersion"), "CIM Win32_VideoController"));
        }

        return ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            adapter.String("Name") ?? "GPU",
            specs,
            "GPU health is judged from measured temperature and throttling only. A graphics card does not expose a health percentage. Hot spots, fan noise under light work or a very old driver are the warning signs worth checking.");
    }

    private static string FormatSensor(SensorReading? reading, string unit, int decimals) =>
        reading is { HasValue: true }
            ? CollectorSupport.FormatNumber(reading.Value, unit, decimals)
            : CollectorSupport.NotReported;
}
