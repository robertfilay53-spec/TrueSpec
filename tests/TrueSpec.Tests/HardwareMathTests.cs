using TrueSpec.Core.Health;

namespace TrueSpec.Tests;

public class HardwareMathTests
{
    [Theory]
    [InlineData(50000d, 45000d, 90d)]
    [InlineData(50000d, 50000d, 100d)]
    [InlineData(50000d, 25000d, 50d)]
    [InlineData(50000d, 60000d, 100d)]
    public void BatteryHealth_ComparesFullChargeWithDesignCapacity(
        double design,
        double full,
        double expected)
    {
        var health = HardwareMath.BatteryHealthPercent(design, full);

        Assert.NotNull(health);
        Assert.Equal(expected, health.Value, 3);
    }

    [Theory]
    [InlineData(null, 45000d)]
    [InlineData(50000d, null)]
    [InlineData(0d, 45000d)]
    [InlineData(-1d, 45000d)]
    [InlineData(50000d, -1d)]
    public void BatteryHealth_ReturnsNullWhenCapacityIsMissingOrInvalid(
        double? design,
        double? full)
    {
        Assert.Null(HardwareMath.BatteryHealthPercent(design, full));
    }

    [Theory]
    [InlineData(0d, 100d)]
    [InlineData(10d, 90d)]
    [InlineData(100d, 0d)]
    [InlineData(120d, 0d)]
    [InlineData(-5d, 100d)]
    public void StorageHealth_IsTheRemainderOfMeasuredWear(double wear, double expected)
    {
        var health = HardwareMath.StorageHealthPercent(wear);

        Assert.NotNull(health);
        Assert.Equal(expected, health.Value, 3);
    }

    [Theory]
    [InlineData(null, BatteryHealthBand.NotMeasurable)]
    [InlineData(100d, BatteryHealthBand.Normal)]
    [InlineData(80d, BatteryHealthBand.Normal)]
    [InlineData(79.9d, BatteryHealthBand.Worn)]
    [InlineData(60d, BatteryHealthBand.Worn)]
    [InlineData(59.9d, BatteryHealthBand.Replace)]
    public void BatteryBand_MatchesTheSpec(double? health, BatteryHealthBand expected)
    {
        Assert.Equal(expected, HardwareMath.BatteryBand(health));
    }
}
