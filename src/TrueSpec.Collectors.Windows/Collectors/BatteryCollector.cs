namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Health;
using TrueSpec.Core.Models;

/// <summary>
/// Real battery capacities, cycles and health. Health is the full charge
/// capacity divided by the design capacity, exactly as the firmware reports it.
/// </summary>
public sealed class BatteryCollector : ICollector
{
    public string Id => "battery";

    public string Name => "Battery";

    public string Category => "Battery";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(5);

    public bool RequiresAdministrator => false;

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var battery = Wmi.Query("root\\cimv2", "SELECT DesignCapacity, FullChargeCapacity, EstimatedChargeRemaining, EstimatedRunTime, Chemistry FROM Win32_Battery").FirstOrDefault();
        var staticData = Wmi.Query("root\\wmi", "SELECT DesignedCapacity, Chemistry, ManufactureName, DeviceName, SerialNumber FROM BatteryStaticData").FirstOrDefault();
        var fullData = Wmi.Query("root\\wmi", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity").FirstOrDefault();
        var cycleData = Wmi.Query("root\\wmi", "SELECT CycleCount FROM BatteryCycleCount").FirstOrDefault();
        var status = Wmi.Query("root\\wmi", "SELECT Voltage, ChargeRate, DischargeRate FROM BatteryStatus").FirstOrDefault();

        if (battery is null && staticData is null)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "No battery was reported. This is normal on a desktop, or the battery firmware does not expose Win32_Battery."));
        }

        var design = staticData?.UInt32("DesignedCapacity") ?? battery?.UInt32("DesignCapacity");
        if (design is 0)
        {
            design = null;
        }

        var full = fullData?.UInt32("FullChargedCapacity") ?? battery?.UInt32("FullChargeCapacity");
        if (full is 0)
        {
            full = null;
        }

        var health = HardwareMath.BatteryHealthPercent(design, full);
        var wear = HardwareMath.BatteryWearPercent(design, full);
        var cycles = cycleData?.UInt32("CycleCount");
        var chemistryText = staticData?.String("Chemistry");
        var chemistry = string.IsNullOrWhiteSpace(chemistryText)
            ? CollectorSupport.BatteryChemistry(battery?.UInt32("Chemistry"))
            : CollectorSupport.BatteryChemistry(chemistryText);

        var currentCharge = battery?.UInt16("EstimatedChargeRemaining");
        var runtime = battery?.UInt32("EstimatedRunTime");
        // Win32_Battery reports 0x04444444 when the runtime is unknown.
        const uint UnknownRuntimeSentinel = 71582788;
        if (runtime is UnknownRuntimeSentinel)
        {
            runtime = null;
        }

        var headline = string.Join(" - ", new[]
        {
            // A missing design capacity is skipped, never printed as
            // "Not reported by hardware - 85.5% health".
            design is null ? null : FormatCapacity(design),
            health is null ? "Health not reported" : string.Create(CultureInfo.InvariantCulture, $"{health.Value:0.#}% health"),
        }.Where(v => !string.IsNullOrWhiteSpace(v)));

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.DesignCapacity, FormatCapacity(design), "CIM root\\WMI BatteryStaticData.DesignedCapacity"),
            CollectorSupport.Spec(SpecLabels.FullChargeCapacity, FormatCapacity(full), "CIM root\\WMI BatteryFullChargedCapacity.FullChargedCapacity"),
            CollectorSupport.Spec(SpecLabels.Health, health is null ? null : string.Create(CultureInfo.InvariantCulture, $"{health.Value:0.0}%"), "Full charge capacity / design capacity"),
            CollectorSupport.Spec(SpecLabels.Wear, wear is null ? null : string.Create(CultureInfo.InvariantCulture, $"{wear.Value:0.0}%"), "100 - health"),
            CollectorSupport.Spec(SpecLabels.HealthBand, HardwareMath.BatteryBandText(health), "Battery health bands (80/60)"),
            CollectorSupport.Spec(SpecLabels.CycleCount, cycles?.ToString(CultureInfo.InvariantCulture), "CIM root\\WMI BatteryCycleCount"),
            CollectorSupport.Spec(SpecLabels.Chemistry, chemistry, "CIM BatteryStaticData / Win32_Battery"),
            CollectorSupport.Spec(SpecLabels.Manufacturer, staticData?.String("ManufactureName"), "CIM root\\WMI BatteryStaticData"),
            CollectorSupport.Spec(SpecLabels.DeviceName, staticData?.String("DeviceName"), "CIM root\\WMI BatteryStaticData"),
            CollectorSupport.Spec(SpecLabels.SerialNumber, staticData?.String("SerialNumber"), "CIM root\\WMI BatteryStaticData"),
            CollectorSupport.Spec(SpecLabels.CurrentCharge, currentCharge is null
                ? null
                : string.Create(CultureInfo.InvariantCulture, $"{currentCharge.Value}%"), "CIM Win32_Battery.EstimatedChargeRemaining"),
            CollectorSupport.Spec(SpecLabels.Voltage, status?.UInt32("Voltage") is { } millivolts && millivolts > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{millivolts / 1000d:0.000} V")
                : null, "CIM root\\WMI BatteryStatus.Voltage"),
            CollectorSupport.Spec(SpecLabels.ChargeRate, status?.UInt32("ChargeRate") is { } chargeRate && chargeRate > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{chargeRate / 1000d:0.0} W")
                : null, "CIM root\\WMI BatteryStatus.ChargeRate"),
            CollectorSupport.Spec(SpecLabels.DischargeRate, status?.UInt32("DischargeRate") is { } dischargeRate && dischargeRate > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{dischargeRate / 1000d:0.0} W")
                : null, "CIM root\\WMI BatteryStatus.DischargeRate"),
            CollectorSupport.Spec(SpecLabels.EstimatedRuntime, runtime is null
                ? null
                : FormatRuntime(runtime.Value), "CIM Win32_Battery.EstimatedRunTime"),
        };

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            string.IsNullOrWhiteSpace(headline) ? "Battery" : headline,
            specs,
            "Battery health is the full charge capacity divided by the design capacity. 80% and above is normal, 60 to 79% is noticeable wear, and below 60% the battery should be replaced. Cycle count is shown only when the battery firmware exposes it."));
    }

    private static string FormatCapacity(uint? milliwattHours) =>
        milliwattHours is null
            ? CollectorSupport.NotReported
            : string.Create(CultureInfo.InvariantCulture, $"{milliwattHours.Value:N0} mWh");

    private static string FormatRuntime(uint minutes) =>
        string.Create(CultureInfo.InvariantCulture, $"{minutes / 60} hours {minutes % 60} minutes");
}
