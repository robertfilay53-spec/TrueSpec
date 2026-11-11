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
    public bool HasValue => Value is not null && !double.IsNaN(Value.Value) && !double.IsInfinity(Value.Value);

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

    public SensorReading? Find(SensorKind kind, string hardwareContains, params string[] preferredNames)
    {
        var candidates = Readings
            .Where(r => r.Kind == kind)
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
