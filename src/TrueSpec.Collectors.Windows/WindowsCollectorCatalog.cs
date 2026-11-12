namespace TrueSpec.Collectors.Windows;

using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Collectors.Windows.Sensors;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Sensors;

/// <summary>
/// The real Windows collector set. The same LibreHardwareMonitor instance feeds
/// both the one-shot scan and the live two second strip.
/// </summary>
public static class WindowsCollectorCatalog
{
    private static readonly LibreHardwareMonitorSensorMonitor SensorMonitor = new();

    public static IReadOnlyList<ICollector> CreateAll() => new ICollector[]
    {
        new SystemCollector(),
        new CpuCollector(SensorMonitor),
        new RamCollector(),
        new StorageCollector(),
        new BatteryCollector(),
        new GpuCollector(SensorMonitor),
        new DisplayCollector(),
        new NetworkCollector(),
        new MotherboardCollector(),
        new SecurityCollector(),
        new DriversCollector(),
        new UpdatesCollector(),
        new StartupCollector(),
        new SoftwareCollector(),
        new CrashCollector(),
    };

    public static ISensorMonitor CreateSensorMonitor() => SensorMonitor;
}
