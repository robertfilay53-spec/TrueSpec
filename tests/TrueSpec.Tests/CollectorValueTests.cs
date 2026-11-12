using TrueSpec.Collectors.Windows.Wmi;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: the value decoders live in the Windows collectors
/// assembly (see TrueSpec.Tests.csproj, like StorageCollectorTests).
/// Covers the v1.4.1 collector-value fixes from Jawad's ThinkPad T480
/// laptop test (v1.4.0): battery chemistry "5269836", RAM form factor
/// "RIMM" on a SODIMM machine, storage "Operational status: 2", and the
/// missing BIOS vendor, plus the sentinel audit for the same collectors.
/// </summary>
public class CollectorValueTests
{
    [Theory]
    [InlineData(1u, "Other")]
    [InlineData(2u, "Unknown")]
    [InlineData(3u, "Lead acid")]
    [InlineData(4u, "Nickel cadmium")]
    [InlineData(5u, "Nickel metal hydride")]
    [InlineData(6u, "Lithium ion")]
    [InlineData(7u, "Zinc air")]
    [InlineData(8u, "Lithium polymer")]
    public void BatteryChemistry_DecodesDocumentedCodes(uint code, string expected)
    {
        Assert.Equal(expected, CollectorSupport.BatteryChemistry(code));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(9u)]
    [InlineData(5269836u)]
    [InlineData(uint.MaxValue)]
    public void BatteryChemistry_UnmappedCode_IsNotReported(uint code)
    {
        Assert.Equal(CollectorSupport.NotReported, CollectorSupport.BatteryChemistry(code));
    }

    [Fact]
    public void BatteryChemistry_NullCode_IsNotReported()
    {
        Assert.Equal(CollectorSupport.NotReported, CollectorSupport.BatteryChemistry((uint?)null));
    }

    [Theory]
    // The T480 reported this raw BatteryStaticData value: a numeric
    // string is a raw enum code, never a name to print.
    [InlineData("5269836", CollectorSupport.NotReported)]
    [InlineData("6", "Lithium ion")]
    [InlineData("Li-ion", "Lithium ion")]
    [InlineData("Lithium-ion", "Lithium ion")]
    [InlineData("NiMH", "Nickel metal hydride")]
    [InlineData("Lithium Polymer", "Lithium polymer")]
    [InlineData("Unknown", "Unknown")]
    public void BatteryChemistry_StringDecodesNumbersAndNames(string input, string expected)
    {
        Assert.Equal(expected, CollectorSupport.BatteryChemistry(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BatteryChemistry_EmptyString_IsNotReported(string? input)
    {
        Assert.Equal(CollectorSupport.NotReported, CollectorSupport.BatteryChemistry(input));
    }

    [Theory]
    [InlineData(0u, "Unknown")]
    [InlineData(1u, "Other")]
    [InlineData(2u, "SIP")]
    [InlineData(3u, "DIP")]
    [InlineData(4u, "ZIP")]
    [InlineData(5u, "SOJ")]
    [InlineData(6u, "Proprietary")]
    [InlineData(7u, "SIMM")]
    [InlineData(8u, "DIMM")]
    [InlineData(9u, "TSOP")]
    [InlineData(10u, "PGA")]
    [InlineData(11u, "RIMM")]
    [InlineData(12u, "SODIMM")]
    [InlineData(13u, "SRIMM")]
    [InlineData(14u, "SMD")]
    [InlineData(15u, "SSMP")]
    [InlineData(16u, "QFP")]
    [InlineData(17u, "TQFP")]
    [InlineData(18u, "SOIC")]
    [InlineData(19u, "LCC")]
    [InlineData(20u, "PLCC")]
    [InlineData(21u, "BGA")]
    [InlineData(22u, "FPBGA")]
    [InlineData(23u, "LGA")]
    public void FormFactor_DecodesSmbiosCodes(uint code, string expected)
    {
        Assert.Equal(expected, CollectorSupport.FormFactor(code));
    }

    [Theory]
    [InlineData(24u)]
    [InlineData(255u)]
    [InlineData(uint.MaxValue)]
    public void FormFactor_UnmappedCode_IsNotReported(uint code)
    {
        Assert.Equal(CollectorSupport.NotReported, CollectorSupport.FormFactor(code));
    }

    [Fact]
    public void FormFactor_NullCode_IsNotReported()
    {
        Assert.Equal(CollectorSupport.NotReported, CollectorSupport.FormFactor((uint?)null));
    }

    [Theory]
    [InlineData(0u, "Unknown")]
    [InlineData(1u, "Other")]
    [InlineData(2u, "OK")]
    [InlineData(3u, "Degraded")]
    [InlineData(4u, "Stressed")]
    [InlineData(5u, "Predictive Failure")]
    [InlineData(6u, "Error")]
    [InlineData(7u, "Non-Recoverable Error")]
    [InlineData(8u, "Starting")]
    [InlineData(9u, "Stopping")]
    [InlineData(10u, "Stopped")]
    [InlineData(11u, "In Service")]
    [InlineData(12u, "No Contact")]
    [InlineData(13u, "Lost Communication")]
    [InlineData(14u, "Aborted")]
    [InlineData(15u, "Dormant")]
    [InlineData(16u, "Supporting Entity in Error")]
    [InlineData(17u, "Completed")]
    [InlineData(18u, "Power Mode")]
    public void OperationalStatus_DecodesDocumentedCodes(uint code, string expected)
    {
        Assert.Equal(expected, CollectorSupport.OperationalStatus(code));
    }

    [Theory]
    [InlineData(19u)]
    [InlineData(65535u)]
    [InlineData(uint.MaxValue)]
    public void OperationalStatus_UnmappedCode_IsNotReported(uint code)
    {
        Assert.Equal(CollectorSupport.NotReported, CollectorSupport.OperationalStatus(code));
    }

    [Fact]
    public void OperationalStatus_NullCode_IsNotReported()
    {
        Assert.Equal(CollectorSupport.NotReported, CollectorSupport.OperationalStatus((uint?)null));
    }

    [Theory]
    [InlineData(0u, null)]
    [InlineData(uint.MaxValue, null)]
    [InlineData(52000u, 52000u)]
    public void BatteryCapacity_SentinelsAreNotReported(uint value, uint? expected)
    {
        Assert.Equal(expected, CollectorSupport.BatteryCapacity(value));
    }

    [Theory]
    [InlineData((ushort)0, (ushort)0)]
    [InlineData((ushort)85, (ushort)85)]
    [InlineData((ushort)100, (ushort)100)]
    [InlineData((ushort)101, null)]
    [InlineData(ushort.MaxValue, null)]
    public void BatteryChargePercent_Above100IsNotReported(ushort value, ushort? expected)
    {
        Assert.Equal(expected, CollectorSupport.BatteryChargePercent(value));
    }

    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(60u, 60u)]
    [InlineData(71582788u, null)]
    [InlineData(uint.MaxValue, null)]
    public void BatteryRuntimeMinutes_SentinelsAreNotReported(uint value, uint? expected)
    {
        Assert.Equal(expected, CollectorSupport.BatteryRuntimeMinutes(value));
    }

    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(123u, 123u)]
    [InlineData(uint.MaxValue, null)]
    public void BatteryCycleCount_MaxValueIsNotReported(uint value, uint? expected)
    {
        Assert.Equal(expected, CollectorSupport.BatteryCycleCount(value));
    }

    [Theory]
    [InlineData(0u, null)]
    [InlineData(uint.MaxValue, null)]
    [InlineData(2400u, 2400u)]
    public void NonZeroNonMax_StripsZeroAndMaxValue(uint value, uint? expected)
    {
        Assert.Equal(expected, CollectorSupport.NonZeroNonMax(value));
    }

    [Theory]
    [InlineData(0ul, null)]
    [InlineData(ulong.MaxValue, null)]
    [InlineData(8589934592ul, 8589934592ul)]
    public void NonZeroNonMax64_StripsZeroAndMaxValue(ulong value, ulong? expected)
    {
        Assert.Equal(expected, CollectorSupport.NonZeroNonMax(value));
    }

    [Theory]
    [InlineData(0u, null)]
    [InlineData(45u, 45u)]
    [InlineData(150u, 150u)]
    [InlineData(151u, null)]
    [InlineData(uint.MaxValue, null)]
    public void DriveTemperatureCelsius_SentinelsAreNotReported(uint value, uint? expected)
    {
        Assert.Equal(expected, CollectorSupport.DriveTemperatureCelsius(value));
    }
}
