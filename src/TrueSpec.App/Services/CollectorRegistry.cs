namespace TrueSpec.App.Services;

using System.Collections.Generic;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Sensors;
#if TRUESPEC_WINDOWS
using TrueSpec.Collectors.Windows;
#endif

/// <summary>
/// Chooses real Windows collectors on Windows and honest unavailable collectors
/// everywhere else. The app never substitutes sample data.
/// </summary>
public static class CollectorRegistry
{
    public static IReadOnlyList<ICollector> CreateCollectors()
    {
#if TRUESPEC_WINDOWS
        return WindowsCollectorCatalog.CreateAll();
#else
        return UnavailableCollectorCatalog.CreateAll();
#endif
    }

    public static ISensorMonitor CreateSensorMonitor()
    {
#if TRUESPEC_WINDOWS
        return WindowsCollectorCatalog.CreateSensorMonitor();
#else
        return new UnavailableSensorMonitor();
#endif
    }

    /// <summary>
    /// The machine-DNA provider on Windows; null everywhere else, so a
    /// scan without one simply records no DNA instead of inventing it.
    /// </summary>
    public static IMachineDnaProvider? CreateMachineDnaProvider()
    {
#if TRUESPEC_WINDOWS
        return WindowsCollectorCatalog.CreateMachineDnaProvider();
#else
        return null;
#endif
    }
}
