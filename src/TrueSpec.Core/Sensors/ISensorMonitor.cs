namespace TrueSpec.Core.Sensors;

/// <summary>
/// One live sensor reading. The source always names where the value came from.
/// A null value means the sensor is not available, never zero.
/// </summary>
public sealed record SensorReading(
    string Hardware,
    string Name,
    SensorKind Kind,
    string Unit,
    double? Value,
    string Source)
{
    /// <summary>
    /// True when the sensor reported a usable value. A null, NaN or infinite
    /// value is never usable. A clock or temperature of zero (or below) is
    /// also not usable: a running CPU cannot be at 0 MHz or 0 C, so a zero
    /// from the sensor library is its "not reporting" sentinel, not a reading
    /// (LibreHardwareMonitor reports 0 on virtual machines whose sensors do
    /// not exist). A 0 RPM fan IS a real reading (a stopped fan) and a 0%
    /// load IS a real reading (an idle CPU), so Fan and Load keep zero.
    /// </summary>
    public bool HasValue
    {
        get
        {
            if (Value is null || double.IsNaN(Value.Value) || double.IsInfinity(Value.Value))
            {
                return false;
            }

            // A clock or temperature of zero is the sensor library's
            // "not reporting" sentinel, never a real reading.
            if (Kind is SensorKind.Clock or SensorKind.Temperature && Value.Value <= 0d)
            {
                return false;
            }

            return true;
        }
    }

    public string DisplayText => HasValue
        ? FormattableString.Invariant($"{Value:0.#} {Unit}")
        : "Not available";
}

public enum SensorKind
{
    Temperature,
    Fan,
    Voltage,
    Power,
    Load,
    Clock,
}

/// <summary>All live readings from one sensor refresh.</summary>
public sealed record SensorSnapshot(
    bool IsAvailable,
    string StatusText,
    DateTimeOffset CapturedAt,
    IReadOnlyList<SensorReading> Readings)
{
    public static SensorSnapshot Unavailable(string reason) => new(
        false,
        reason,
        DateTimeOffset.Now,
        Array.Empty<SensorReading>());

    /// <summary>
    /// The first reading of the kind whose hardware matches and that reported
    /// a usable value. A sensor with no usable value is not a match: it is
    /// "not reported", never a zero.
    /// </summary>
    public SensorReading? Find(SensorKind kind, string hardwareContains, params string[] preferredNames)
    {
        var candidates = Readings
            .Where(r => r.Kind == kind)
            .Where(r => r.HasValue)
            .Where(r => r.Hardware.Contains(hardwareContains, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var name in preferredNames)
        {
            var match = candidates.FirstOrDefault(r =>
                r.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return candidates.FirstOrDefault();
    }

    public SensorReading? CpuTemperature() => Find(SensorKind.Temperature, "CPU", "Package", "Core Max", "Tctl");

    public SensorReading? GpuTemperature() => Find(SensorKind.Temperature, "GPU", "GPU Core", "Core");

    public SensorReading? CpuLoad() => Find(SensorKind.Load, "CPU", "CPU Total", "Total");

    /// <summary>
    /// Every per-core clock reading with a value, for the load test's
    /// average/max clock. LibreHardwareMonitor also reports a "Bus Speed"
    /// clock (~100 MHz) on the CPU hardware: it is the bus, not a core,
    /// and averaging it in drags the average ~19% low on a 4-core machine
    /// at ~3500 MHz, enough to flip the load-test throttle verdict — so
    /// it is excluded here, as is any other non-core CPU clock. When no
    /// core clock reports, this is empty: "not reported", never a wrong
    /// number.
    /// </summary>
    public IReadOnlyList<SensorReading> CpuClocks() => Readings
        .Where(r => r.Kind == SensorKind.Clock && r.HasValue)
        .Where(r => r.Hardware.Contains("CPU", StringComparison.OrdinalIgnoreCase))
        .Where(r => r.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
        .ToList();

    /// <summary>Average of the per-core clocks, or null when no core clock reported.</summary>
    public double? CpuClockMHz()
    {
        var clocks = CpuClocks();
        return clocks.Count == 0 ? null : clocks.Average(r => r.Value!.Value);
    }

    /// <summary>Every fan reading with a value, for the load test's per-fan sampling (U5). A 0 RPM value is a real reading (a stopped fan); a fan sensor with no value is absent, never recorded as 0.</summary>
    public IReadOnlyList<SensorReading> FanReadings() => Readings
        .Where(r => r.Kind == SensorKind.Fan && r.HasValue)
        .ToList();

    /// <summary>CPU fan speed in RPM, or null when no CPU fan sensor reported.</summary>
    public double? CpuFanRpm() => Find(SensorKind.Fan, "CPU", "CPU Fan", "Fan")?.Value;

    /// <summary>CPU package power in watts, or null when no CPU power sensor reported.</summary>
    public double? CpuPowerW() => Find(SensorKind.Power, "CPU", "Package", "CPU Package")?.Value;

    public SensorReading? BatteryPower() => Find(SensorKind.Power, "Battery", "Battery Power", "Charge Rate", "Discharge Rate");
}

/// <summary>Live sensor source. Windows uses LibreHardwareMonitor.</summary>
public interface ISensorMonitor
{
    bool IsAvailable { get; }

    string StatusText { get; }

    Task<SensorSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Honest empty state for platforms without a hardware sensor library.</summary>
public sealed class UnavailableSensorMonitor : ISensorMonitor
{
    private readonly string _reason;

    public UnavailableSensorMonitor(string? reason = null)
    {
        _reason = reason ?? "Live sensors are only available on Windows with LibreHardwareMonitor.";
    }

    public bool IsAvailable => false;

    public string StatusText => _reason;

    public Task<SensorSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SensorSnapshot.Unavailable(_reason));
    }
}
