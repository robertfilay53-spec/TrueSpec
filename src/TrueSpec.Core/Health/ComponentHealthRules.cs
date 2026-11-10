namespace TrueSpec.Core.Health;

using TrueSpec.Core.Models;

/// <summary>
/// Turns measured specs into a component health percentage. Rules only run when
/// the needed readings are present. There is no RAM, CPU, motherboard or Wi-Fi
/// life sensor in this file, because none exists in real hardware.
/// </summary>
public static class ComponentHealthRules
{
    public static ComponentSnapshot Apply(ComponentSnapshot component, DateTimeOffset? now = null)
    {
        if (component.HealthPercent is not null)
        {
            return component;
        }

        var health = Evaluate(component, now);
        return health is null ? component : component with { HealthPercent = health };
    }

    public static double? Evaluate(ComponentSnapshot component, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(component);

        var id = component.Id.ToLowerInvariant();
        var category = component.Category.ToLowerInvariant();

        if (id == "drivers")
        {
            return Drivers(component.Specs);
        }

        return category switch
        {
            "storage" => Storage(component.Specs),
            "battery" => Battery(component.Specs),
            "cpu" => Thermal(component.Specs, "Package temperature", "Package temperature (C)"),
            "gpu" => Thermal(component.Specs, "GPU temperature", "GPU temperature (C)"),
            "ram" => Ram(component.Specs),
            "network" => Network(component.Specs, now ?? DateTimeOffset.UtcNow),
            _ => null,
        };
    }

    private static double? Storage(IReadOnlyList<SpecValue> specs)
    {
        var wear = SpecText.Number(specs, "Wear level");
        var health = HardwareMath.StorageHealthPercent(wear);
        if (health is null)
        {
            return null;
        }

        var result = health.Value;

        var flags = SpecText.Value(specs, "SMART warnings");
        if (string.Equals(flags, "Yes", StringComparison.OrdinalIgnoreCase))
        {
            result = Math.Min(result, 60d);
        }

        var healthWord = SpecText.Value(specs, "Health word");
        if (string.Equals(healthWord, "Warning", StringComparison.OrdinalIgnoreCase))
        {
            result = Math.Min(result, 65d);
        }
        else if (string.Equals(healthWord, "Unhealthy", StringComparison.OrdinalIgnoreCase))
        {
            result = Math.Min(result, 25d);
        }

        var uncorrected = SpecText.Number(specs, "Uncorrected errors");
        if (uncorrected is > 0d)
        {
            result = Math.Min(result, 40d);
        }

        return Math.Clamp(result, 0d, 100d);
    }

    private static double? Battery(IReadOnlyList<SpecValue> specs)
    {
        var design = SpecText.Number(specs, "Design capacity");
        var full = SpecText.Number(specs, "Full charge capacity");
        return HardwareMath.BatteryHealthPercent(design, full);
    }

    private static double? Thermal(IReadOnlyList<SpecValue> specs, params string[] temperatureLabels)
    {
        var temperature = SpecText.Number(specs, temperatureLabels);
        if (temperature is null)
        {
            return null;
        }

        var temp = temperature.Value;
        var health = temp switch
        {
            <= 60d => 100d,
            <= 80d => 100d - ((temp - 60d) * 1.0d),
            <= 90d => 80d - ((temp - 80d) * 2.0d),
            <= 100d => 60d - ((temp - 90d) * 3.0d),
            _ => 20d,
        };

        var throttled = SpecText.Value(specs, "Thermal throttle flag");
        if (string.Equals(throttled, "Yes", StringComparison.OrdinalIgnoreCase))
        {
            health -= 10d;
        }

        return Math.Clamp(health, 0d, 100d);
    }

    private static double? Ram(IReadOnlyList<SpecValue> specs)
    {
        var errors = SpecText.Number(specs, "Memory errors");
        if (errors is null)
        {
            return null;
        }

        return errors.Value <= 0d
            ? 100d
            : Math.Clamp(100d - (errors.Value * 10d), 0d, 100d);
    }

    private static double? Network(IReadOnlyList<SpecValue> specs, DateTimeOffset now)
    {
        var driverDate = SpecText.Date(specs, "Driver date");
        if (driverDate is null)
        {
            return null;
        }

        var ageDays = (now - driverDate.Value).TotalDays;
        return ageDays switch
        {
            <= 365d => 100d,
            <= 730d => 90d,
            <= 1095d => 75d,
            _ => 55d,
        };
    }

    private static double? Drivers(IReadOnlyList<SpecValue> specs)
    {
        var total = SpecText.Number(specs, "Installed drivers");
        var old = SpecText.Number(specs, "Drivers older than 5 years");
        if (total is null || old is null || total.Value <= 0d)
        {
            return null;
        }

        var ratio = Math.Clamp(old.Value / total.Value, 0d, 1d);
        return Math.Clamp(100d - (ratio * 100d), 0d, 100d);
    }
}
