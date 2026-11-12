namespace TrueSpec.Collectors.Windows.Sensors;

using LibreHardwareMonitor.Hardware;
using TrueSpec.Core.Sensors;

/// <summary>
/// Live temperatures, fans, voltages, power, load and clocks from
/// LibreHardwareMonitor. Every value is a real sensor reading; when the library
/// cannot open a sensor the snapshot is marked unavailable.
///
/// Ownership: this monitor owns the single shared <see cref="Computer"/>
/// instance for the process. It is created lazily on the first read, never
/// shared with any other owner, and closed only by <see cref="Dispose"/>.
/// One sweep runs at a time (see <see cref="_sweepGate"/>): the library's
/// hardware objects are not thread-safe, so a second read while a sweep is
/// running returns "unavailable" immediately instead of queueing behind it.
/// </summary>
public sealed class LibreHardwareMonitorSensorMonitor : ISensorMonitor, IDisposable
{
    // A sweep that hangs (a stuck driver behind hardware.Update()) used to
    // hold the state lock for the whole read, so the live strip stalled
    // indefinitely and Dispose blocked with it. The sweep now runs without
    // the lock and is abandoned after this long: the caller gets an
    // "unavailable" snapshot and the UI stays live. The abandoned sweep
    // still owns the gate until it finishes, so a later sweep can never run
    // concurrently with it on the same Computer instance.
    public static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly SemaphoreSlim _sweepGate = new(1, 1);
    private Computer? _computer;
    private bool _initialized;
    private bool _available;
    private bool _disposed;
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

    public async Task<SensorSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return SensorSnapshot.Unavailable("LibreHardwareMonitor has been disposed.");
            }
        }

        // One sweep at a time, without waiting: a caller that arrives while
        // a sweep is running (or while a hung sweep is being abandoned)
        // gets "unavailable" now instead of queueing behind a stuck read.
        bool gateTaken;
        try
        {
            gateTaken = await _sweepGate.WaitAsync(0, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        if (!gateTaken)
        {
            return SensorSnapshot.Unavailable("A previous sensor sweep is still running; live sensors are temporarily unavailable.");
        }

        var releaseGate = true;
        try
        {
            var sweep = Task.Run(() => Sweep(cancellationToken), CancellationToken.None);
            var timeout = Task.Delay(ReadTimeout, cancellationToken);
            var done = await Task.WhenAny(sweep, timeout);
            if (done == sweep)
            {
                return await sweep;
            }

            // The sweep is still running past the timeout, or the caller
            // cancelled while waiting. Either way the sweep is abandoned:
            // the gate is released when it eventually finishes, never now,
            // so no later sweep can run concurrently with it. The release
            // tolerates a concurrent Dispose: shutting down while a sweep
            // is abandoned must not turn into a second crash.
            releaseGate = false;
            _ = sweep.ContinueWith(
                task =>
                {
                    _ = task.Exception;
                    ReleaseGate();
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);

            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                _available = false;
                _status = $"LibreHardwareMonitor read timed out after {ReadTimeout.TotalSeconds:0} seconds; live sensors are temporarily unavailable.";
            }

            return SensorSnapshot.Unavailable(_status);
        }
        finally
        {
            if (releaseGate)
            {
                ReleaseGate();
            }
        }
    }

    private void ReleaseGate()
    {
        try
        {
            _sweepGate.Release();
        }
        catch (ObjectDisposedException)
        {
            // Dispose ran first; the gate is already gone.
        }
        catch (SemaphoreFullException)
        {
            // Defensive: the gate must never be released twice.
        }
    }

    /// <summary>
    /// One sweep over the shared <see cref="Computer"/>. The state lock is
    /// held only to grab the instance and to publish the result: the
    /// hardware walk itself runs unlocked, so a hung
    /// <c>hardware.Update()</c> can never block <see cref="Dispose"/> or
    /// the status readers. A sweep abandoned by the timeout still publishes
    /// when it eventually finishes; its readings are real, only late.
    /// </summary>
    private SensorSnapshot Sweep(CancellationToken cancellationToken)
    {
        Computer? computer;
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_disposed)
            {
                return SensorSnapshot.Unavailable("LibreHardwareMonitor has been disposed.");
            }

            if (!_initialized)
            {
                Initialize();
            }

            computer = _computer;
            if (computer is null)
            {
                return SensorSnapshot.Unavailable(_status);
            }
        }

        try
        {
            var readings = new List<SensorReading>();
            foreach (var hardware in computer.Hardware)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadHardware(hardware, GroupFor(hardware), readings);
            }

            string status;
            bool available;
            lock (_gate)
            {
                _available = readings.Count > 0;
                _status = _available
                    ? "LibreHardwareMonitor live sensors"
                    : "LibreHardwareMonitor opened, but this machine exposes no readable sensors. Administrator rights may be required.";
                available = _available;
                status = _status;
            }

            return new SensorSnapshot(available, status, DateTimeOffset.Now, readings);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                _available = false;
                _status = $"LibreHardwareMonitor read failed: {exception.Message}";
            }

            return SensorSnapshot.Unavailable(_status);
        }
    }

    private void Initialize()
    {
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
            _initialized = true;
            _status = "LibreHardwareMonitor opened.";
        }
        catch (Exception exception)
        {
            // Stay uninitialized so the next read retries the open: a
            // transient failure at startup (driver busy, first-run race)
            // used to leave live sensors dead for the whole session.
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
                value is null || float.IsNaN(value.Value) || float.IsInfinity(value.Value) ? null : value.Value,
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
        // TryEnter, never a blocking lock: a sweep no longer holds this
        // lock while walking hardware, but a slow Initialize still could.
        // Dispose must never hang shutdown waiting for it; if the lock
        // cannot be taken quickly the instance is simply marked disposed
        // and the close is skipped (the process is shutting down anyway).
        var taken = false;
        try
        {
            taken = Monitor.TryEnter(_gate, TimeSpan.FromSeconds(2));
            if (!taken)
            {
                _disposed = true;
                return;
            }

            _disposed = true;
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
        finally
        {
            if (taken)
            {
                Monitor.Exit(_gate);
            }

            try
            {
                _sweepGate.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Dispose is idempotent for callers; the gate already went.
            }
        }
    }
}
