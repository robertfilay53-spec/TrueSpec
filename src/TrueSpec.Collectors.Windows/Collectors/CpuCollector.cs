namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;

/// <summary>
/// CPU identity from CIM and real temperature, fan, voltage and power readings
/// from LibreHardwareMonitor. No CPU life percentage is shown because no such
/// sensor exists.
/// </summary>
public sealed class CpuCollector : ICollector
{
    private readonly ISensorMonitor _sensors;

    public CpuCollector(ISensorMonitor sensors)
    {
        _sensors = sensors;
    }

    public string Id => "cpu";

    public string Name => "CPU";

    public string Category => "CPU";

    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(2);

    public bool RequiresAdministrator => true;

    public async Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var cpu = Wmi.Query("root\\cimv2", "SELECT Name, Manufacturer, ProcessorId, SocketDesignation, Architecture, CurrentClockSpeed, L2CacheSize, L3CacheSize, MaxClockSpeed, NumberOfCores, NumberOfLogicalProcessors, VirtualizationFirmwareEnabled FROM Win32_Processor").FirstOrDefault();
        if (cpu is null)
        {
            return ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "CIM did not return Win32_Processor.");
        }

        var sensors = await _sensors.ReadAsync(cancellationToken);
        var temperature = sensors.CpuTemperature();
        var load = sensors.CpuLoad();
        var power = sensors.Find(SensorKind.Power, "CPU", "Package", "CPU Package");
        var fan = sensors.Find(SensorKind.Fan, "CPU", "CPU Fan", "Fan");
        var voltage = sensors.Find(SensorKind.Voltage, "CPU", "CPU Core", "Vcore");
        var clock = sensors.Find(SensorKind.Clock, "CPU", "Core", "Bus Speed");

        var temperatureValue = temperature?.Value;
        var throttle = temperatureValue switch
        {
            >= 95d => "Yes",
            not null => "No",
            _ => CollectorSupport.NotReported,
        };

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.Model, cpu.String("Name"), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.Manufacturer, cpu.String("Manufacturer"), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.Cores, cpu.UInt32("NumberOfCores")?.ToString(CultureInfo.InvariantCulture), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.Threads, cpu.UInt32("NumberOfLogicalProcessors")?.ToString(CultureInfo.InvariantCulture), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.MaxClock, FormatMhz(cpu.UInt32("MaxClockSpeed")), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.CurrentClock, FormatMhz(cpu.UInt32("CurrentClockSpeed")), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.L2Cache, FormatKb(cpu.UInt32("L2CacheSize")), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.L3Cache, FormatKb(cpu.UInt32("L3CacheSize")), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.Socket, cpu.String("SocketDesignation"), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.Architecture, cpu.UInt16("Architecture") switch
            {
                0 => "x86",
                5 => "ARM",
                9 => "x64",
                12 => "ARM64",
                _ => CollectorSupport.NotReported,
            }, "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.Virtualization, cpu.Bool("VirtualizationFirmwareEnabled") switch
            {
                true => "Enabled",
                false => "Disabled",
                _ => CollectorSupport.NotReported,
            }, "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.ProcessorID, cpu.String("ProcessorId"), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.PackageTemperature, FormatSensor(temperature, "C", 1), "LibreHardwareMonitor"),
            CollectorSupport.Spec(SpecLabels.ThermalThrottleFlag, throttle, "LibreHardwareMonitor temperature threshold (95 C)"),
            CollectorSupport.Spec(SpecLabels.PackagePower, FormatSensor(power, "W", 1), "LibreHardwareMonitor"),
            CollectorSupport.Spec(SpecLabels.CPUFan, FormatSensor(fan, "RPM", 0), "LibreHardwareMonitor"),
            CollectorSupport.Spec(SpecLabels.CoreVoltage, FormatSensor(voltage, "V", 3), "LibreHardwareMonitor"),
            CollectorSupport.Spec(SpecLabels.LiveCoreClock, FormatSensor(clock, "MHz", 0), "LibreHardwareMonitor"),
            CollectorSupport.Spec(SpecLabels.CPULoad, FormatSensor(load, "%", 1), "LibreHardwareMonitor"),
            CollectorSupport.Spec(SpecLabels.LifeRemaining, "Not reported by hardware", "No CPU life sensor exists"),
        };

        var headline = cpu.String("Name") ?? "CPU";

        return ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            headline,
            specs,
            "CPU health is based on measured temperature and the thermal throttle flag, not on a life meter, because no CPU life sensor exists. The temperature comes from LibreHardwareMonitor and is only shown when the sensor is readable.");
    }

    private static string FormatMhz(uint? mhz) =>
        CollectorSupport.NonZero(mhz) is { } value ? string.Create(CultureInfo.InvariantCulture, $"{value} MHz") : CollectorSupport.NotReported;

    private static string FormatKb(uint? kb) =>
        CollectorSupport.NonZero(kb) is { } value ? string.Create(CultureInfo.InvariantCulture, $"{value} KB") : CollectorSupport.NotReported;

    private static string FormatSensor(SensorReading? reading, string unit, int decimals) =>
        reading is { HasValue: true }
            ? CollectorSupport.FormatNumber(reading.Value, unit, decimals)
            : CollectorSupport.NotReported;
}
