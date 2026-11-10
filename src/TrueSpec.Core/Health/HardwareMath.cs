namespace TrueSpec.Core.Health;

/// <summary>
/// The only two hardware health percentages that physically exist on a normal
/// PC: battery health from the capacity pair and SSD health from measured wear.
/// Both return null when the hardware does not expose the needed values.
/// </summary>
public static class HardwareMath
{
    public static double? BatteryHealthPercent(double? designCapacity, double? fullChargeCapacity)
    {
        if (designCapacity is null || fullChargeCapacity is null)
        {
            return null;
        }

        var design = designCapacity.Value;
        var full = fullChargeCapacity.Value;

        if (double.IsNaN(design) || double.IsNaN(full) || double.IsInfinity(design) || double.IsInfinity(full))
        {
            return null;
        }

        if (design <= 0d || full < 0d)
        {
            return null;
        }

        return Math.Clamp(full / design * 100d, 0d, 100d);
    }

    public static double? BatteryWearPercent(double? designCapacity, double? fullChargeCapacity)
    {
        var health = BatteryHealthPercent(designCapacity, fullChargeCapacity);
        return health is null ? null : Math.Clamp(100d - health.Value, 0d, 100d);
    }

    public static BatteryHealthBand BatteryBand(double? healthPercent)
    {
        if (healthPercent is null || double.IsNaN(healthPercent.Value))
        {
            return BatteryHealthBand.NotMeasurable;
        }

        return healthPercent.Value switch
        {
            >= 80d => BatteryHealthBand.Normal,
            >= 60d => BatteryHealthBand.Worn,
            _ => BatteryHealthBand.Replace,
        };
    }

    public static string BatteryBandText(double? healthPercent) => BatteryBand(healthPercent) switch
    {
        BatteryHealthBand.Normal => "Normal (80-100%)",
        BatteryHealthBand.Worn => "Noticeable wear (60-79%)",
        BatteryHealthBand.Replace => "Replace (below 60%)",
        _ => "Not measurable",
    };

    /// <summary>
    /// SSD health from the manufacturer wear counter. Wear is "percentage used",
    /// so health is the remainder. Counters above 100% used are clamped to 0.
    /// </summary>
    public static double? StorageHealthPercent(double? wearPercent)
    {
        if (wearPercent is null || double.IsNaN(wearPercent.Value) || double.IsInfinity(wearPercent.Value))
        {
            return null;
        }

        var wear = Math.Max(0d, wearPercent.Value);
        return Math.Clamp(100d - wear, 0d, 100d);
    }
}

public enum BatteryHealthBand
{
    NotMeasurable,
    Normal,
    Worn,
    Replace,
}
