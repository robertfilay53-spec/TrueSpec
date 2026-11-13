namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Benchmarks;
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

    // WMI query strings are hoisted so CollectorWmiSelectionTests can pin
    // the SELECT list to the properties this collector reads (hardening
    // item 7, F9 bug class).
    internal const string ProcessorQuery = "SELECT Name, Manufacturer, ProcessorId, SocketDesignation, Architecture, CurrentClockSpeed, L2CacheSize, L3CacheSize, MaxClockSpeed, NumberOfCores, NumberOfLogicalProcessors, VirtualizationFirmwareEnabled FROM Win32_Processor";

    public async Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var cpu = Wmi.Query("root\\cimv2", ProcessorQuery).FirstOrDefault();
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
        // Live core clock is the first per-core clock only. The sensor
        // library also reports a ~100 MHz "Bus Speed" clock on the CPU
        // hardware, and showing that as the core clock when no core clock
        // reports would be a wrong number, not a reading: CpuClocks()
        // excludes it, so a machine with no core clock honestly reports
        // SpecLabels.NotReportedByHardware instead of 100 MHz.
        var clock = sensors.CpuClocks().FirstOrDefault();

        var temperatureValue = temperature?.Value;
        // The throttle flag shares the one safety-limit constant with the
        // load test (LoadTestConfig.DefaultAbortTemperatureC): a second
        // hardcoded 95 here would silently drift if the default ever
        // changes, and the flag must agree with the temperature at which
        // the load test aborts.
        var throttle = ThrottleFlag(temperatureValue);

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.Model, cpu.String("Name"), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.Manufacturer, cpu.String("Manufacturer"), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.Cores, cpu.UInt32("NumberOfCores")?.ToString(CultureInfo.InvariantCulture), "CIM Win32_Processor", numericValue: (double?)cpu.UInt32("NumberOfCores"), numericUnit: "cores"),
            CollectorSupport.Spec(SpecLabels.Threads, cpu.UInt32("NumberOfLogicalProcessors")?.ToString(CultureInfo.InvariantCulture), "CIM Win32_Processor", numericValue: (double?)cpu.UInt32("NumberOfLogicalProcessors"), numericUnit: "threads"),
            CollectorSupport.Spec(SpecLabels.MaxClock, FormatMhz(cpu.UInt32("MaxClockSpeed")), "CIM Win32_Processor", numericValue: (double?)CollectorSupport.NonZero(cpu.UInt32("MaxClockSpeed")), numericUnit: "MHz"),
            CollectorSupport.Spec(SpecLabels.CurrentClock, FormatMhz(cpu.UInt32("CurrentClockSpeed")), "CIM Win32_Processor", numericValue: (double?)CollectorSupport.NonZero(cpu.UInt32("CurrentClockSpeed")), numericUnit: "MHz"),
            CollectorSupport.Spec(SpecLabels.L2Cache, FormatKb(cpu.UInt32("L2CacheSize")), "CIM Win32_Processor", numericValue: (double?)CollectorSupport.NonZero(cpu.UInt32("L2CacheSize")), numericUnit: "KB"),
            CollectorSupport.Spec(SpecLabels.L3Cache, FormatKb(cpu.UInt32("L3CacheSize")), "CIM Win32_Processor", numericValue: (double?)CollectorSupport.NonZero(cpu.UInt32("L3CacheSize")), numericUnit: "KB"),
            CollectorSupport.Spec(SpecLabels.Socket, cpu.String("SocketDesignation"), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.Architecture, cpu.UInt16("Architecture") switch
            {
                0 => "x86",
                5 => "ARM",
                9 => "x64",
                12 => "ARM64",
                _ => SpecLabels.NotReportedByHardware,
            }, "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.Virtualization, cpu.Bool("VirtualizationFirmwareEnabled") switch
            {
                true => "Enabled",
                false => "Disabled",
                _ => SpecLabels.NotReportedByHardware,
            }, "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.ProcessorID, cpu.String("ProcessorId"), "CIM Win32_Processor"),
            CollectorSupport.Spec(SpecLabels.PackageTemperature, FormatSensor(temperature, "C", 1), "LibreHardwareMonitor", numericValue: temperature is { HasValue: true } ? temperature.Value : null, numericUnit: "C"),
            CollectorSupport.Spec(SpecLabels.ThermalThrottleFlag, throttle, string.Create(CultureInfo.InvariantCulture, $"LibreHardwareMonitor temperature threshold ({LoadTestConfig.DefaultAbortTemperatureC:0.#} C)")),
            CollectorSupport.Spec(SpecLabels.PackagePower, FormatSensor(power, "W", 1), "LibreHardwareMonitor", numericValue: power is { HasValue: true } ? power.Value : null, numericUnit: "W"),
            CollectorSupport.Spec(SpecLabels.CPUFan, FormatSensor(fan, "RPM", 0), "LibreHardwareMonitor", numericValue: fan is { HasValue: true } ? fan.Value : null, numericUnit: "RPM"),
            CollectorSupport.Spec(SpecLabels.CoreVoltage, FormatSensor(voltage, "V", 3), "LibreHardwareMonitor", numericValue: voltage is { HasValue: true } ? voltage.Value : null, numericUnit: "V"),
            CollectorSupport.Spec(SpecLabels.LiveCoreClock, FormatSensor(clock, "MHz", 0), "LibreHardwareMonitor", numericValue: clock is { HasValue: true } ? clock.Value : null, numericUnit: "MHz"),
            CollectorSupport.Spec(SpecLabels.CPULoad, FormatSensor(load, "%", 1), "LibreHardwareMonitor", numericValue: load is { HasValue: true } ? load.Value : null, numericUnit: "%"),
            CollectorSupport.Spec(SpecLabels.LifeRemaining, SpecLabels.NotReportedByHardware, "No CPU life sensor exists"),
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

    /// <summary>
    /// The thermal throttle flag from a package temperature: at or above
    /// the shared safety limit (<see cref="LoadTestConfig.DefaultAbortTemperatureC"/>)
    /// the CPU is throttling; below it, it is not; no reading means no
    /// judgement, never a guessed "No".
    /// </summary>
    internal static string ThrottleFlag(double? temperatureC) => temperatureC switch
    {
        >= LoadTestConfig.DefaultAbortTemperatureC => "Yes",
        not null => "No",
        _ => SpecLabels.NotReportedByHardware,
    };

    private static string FormatMhz(uint? mhz) =>
        CollectorSupport.NonZero(mhz) is { } value ? string.Create(CultureInfo.InvariantCulture, $"{value} MHz") : SpecLabels.NotReportedByHardware;

    private static string FormatKb(uint? kb) =>
        CollectorSupport.NonZero(kb) is { } value ? string.Create(CultureInfo.InvariantCulture, $"{value} KB") : SpecLabels.NotReportedByHardware;

    private static string FormatSensor(SensorReading? reading, string unit, int decimals) =>
        reading is { HasValue: true }
            ? CollectorSupport.FormatNumber(reading.Value, unit, decimals)
            : SpecLabels.NotReportedByHardware;
}
