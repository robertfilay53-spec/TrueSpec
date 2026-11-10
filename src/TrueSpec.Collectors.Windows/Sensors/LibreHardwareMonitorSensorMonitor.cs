namespace TrueSpec.Collectors.Windows.Sensors;

using LibreHardwareMonitor.Hardware;
using TrueSpec.Core.Sensors;

/// <summary>
/// Live temperatures, fans, voltages, power, load and clocks from
/// LibreHardwareMonitor. Every value is a real sensor reading; when the library
/// cannot open a sensor the snapshot is marked unavailable.
/// </summary>
public sealed class LibreHardwareMonitorSensorMonitor : ISensorMonitor, IDisposable
{
    private readonly object _gate = new();
    private Computer? _computer;
    private bool _initialized;
    private bool _available;
    private string _status = "LibreHardwareMonitor has not been initialized yet.";

    public bool IsAvailable
    {
        get
        {
            lock (_gate)
            {
                return _available;
            }
        }
    }

    public string StatusText
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public Task<SensorSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => ReadCore(cancellationToken), cancellationToken);

    private SensorSnapshot ReadCore(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_initialized)
            {
                Initialize();
            }

            if (_computer is null)
            {
                return SensorSnapshot.Unavailable(_status);
            }

            try
            {
                var readings = new List<SensorReading>();
                foreach (var hardware in _computer.Hardware)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReadHardware(hardware, GroupFor(hardware), readings);
                }

                _available = readings.Count > 0;
                _status = _available
                    ? "LibreHardwareMonitor live sensors"
                    : "LibreHardwareMonitor opened, but this machine exposes no readable sensors. Administrator rights may be required.";

                return new SensorSnapshot(_available, _status, DateTimeOffset.Now, readings);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _available = false;
                _status = $"LibreHardwareMonitor read failed: {exception.Message}";
                return SensorSnapshot.Unavailable(_status);
            }
        }
    }

    private void Initialize()
    {
        _initialized = true;
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsStorageEnabled = true,
                IsBatteryEnabled = true,
                IsPsuEnabled = true,
                IsNetworkEnabled = true,
            };
            _computer.Open();
            _status = "LibreHardwareMonitor opened.";
        }
        catch (Exception exception)
        {
            _computer = null;
            _available = false;
            _status = $"LibreHardwareMonitor could not open: {exception.Message}";
        }
    }

    private static void ReadHardware(
        IHardware hardware,
        string group,
        List<SensorReading> readings)
    {
        try
        {
            hardware.Update();
        }
        catch
        {
            // A single unavailable device must not hide the other real sensors.
        }

        foreach (var sensor in hardware.Sensors)
        {
            var kind = KindFor(sensor.SensorType);
            if (kind is null)
            {
                continue;
            }

            var value = sensor.Value;
            readings.Add(new SensorReading(
                group,
                sensor.Name,
                kind.Value,
                UnitFor(kind.Value),
                value is null || float.IsNaN(value.Value) ? null : value.Value,
                "LibreHardwareMonitor"));
        }

        foreach (var subHardware in hardware.SubHardware)
        {
            ReadHardware(subHardware, group, readings);
        }
    }

    private static string GroupFor(IHardware hardware) => hardware.HardwareType switch
    {
        HardwareType.Cpu => "CPU",
        HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => "GPU",
        HardwareType.Motherboard => "Motherboard",
        HardwareType.SuperIO or HardwareType.EmbeddedController => "Motherboard",
        HardwareType.Storage => "Storage",
        HardwareType.Battery => "Battery",
        HardwareType.Memory => "RAM",
        _ => hardware.HardwareType.ToString(),
    };

    private static SensorKind? KindFor(SensorType type) => type switch
    {
        SensorType.Temperature => SensorKind.Temperature,
        SensorType.Fan => SensorKind.Fan,
        SensorType.Voltage => SensorKind.Voltage,
        SensorType.Power => SensorKind.Power,
        SensorType.Load => SensorKind.Load,
        SensorType.Clock => SensorKind.Clock,
        _ => null,
    };

    private static string UnitFor(SensorKind kind) => kind switch
    {
        SensorKind.Temperature => "C",
        SensorKind.Fan => "RPM",
        SensorKind.Voltage => "V",
        SensorKind.Power => "W",
        SensorKind.Load => "%",
        _ => "MHz",
    };

    public void Dispose()
    {
        lock (_gate)
        {
            try
            {
                _computer?.Close();
            }
            catch
            {
                // Closing a sensor library must never crash shutdown.
            }

            _computer = null;
            _available = false;
        }
    }
}
