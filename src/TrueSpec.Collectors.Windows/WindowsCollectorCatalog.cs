namespace TrueSpec.Collectors.Windows;

using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Collectors.Windows.Sensors;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.HardwareDatabase;
using TrueSpec.Core.Sensors;
using TrueSpec.Core.Settings;

/// <summary>
/// The real Windows collector set. The same LibreHardwareMonitor instance feeds
/// both the one-shot scan and the live two second strip.
/// </summary>
public static class WindowsCollectorCatalog
{
    private static readonly LibreHardwareMonitorSensorMonitor SensorMonitor = new();

    public static IReadOnlyList<ICollector> CreateAll()
    {
        // The bundled hardware database is optional: when the setting is
        // off the CPU collector gets no database and shows no rated spec,
        // exactly like a machine whose model is not in the dataset. The
        // setting is read once here, at startup, because the collectors
        // are created once: toggling it takes effect after a restart,
        // like the portable-mode toggle.
        IHardwareDatabase? database = new AppSettingsStore().Load().HardwareDatabaseEnabled
            ? HardwareDatabase.Default
            : null;
        return new ICollector[]
        {
            new SystemCollector(),
            new CpuCollector(SensorMonitor, database),
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
    }

    public static ISensorMonitor CreateSensorMonitor() => SensorMonitor;

    /// <summary>The machine-DNA provider: the same serials the scan hashes into the snapshot.</summary>
    public static IMachineDnaProvider CreateMachineDnaProvider() => new MachineDnaCollector();
}
