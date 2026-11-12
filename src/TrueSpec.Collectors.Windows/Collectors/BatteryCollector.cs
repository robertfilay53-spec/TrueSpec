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

    // WMI query strings are hoisted so CollectorWmiSelectionTests can pin
    // the SELECT list to the properties this collector reads (hardening
    // item 7, F9 bug class).
    internal const string BatteryQuery = "SELECT DesignCapacity, FullChargeCapacity, EstimatedChargeRemaining, EstimatedRunTime, Chemistry FROM Win32_Battery";
    internal const string StaticDataQuery = "SELECT DesignedCapacity, Chemistry, ManufactureName, DeviceName, SerialNumber FROM BatteryStaticData";
    internal const string FullChargedCapacityQuery = "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity";
    internal const string CycleCountQuery = "SELECT CycleCount FROM BatteryCycleCount";
    internal const string StatusQuery = "SELECT Voltage, ChargeRate, DischargeRate FROM BatteryStatus";

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var battery = Wmi.Query("root\\cimv2", BatteryQuery).FirstOrDefault();
        var staticData = Wmi.Query("root\\wmi", StaticDataQuery).FirstOrDefault();
        var fullData = Wmi.Query("root\\wmi", FullChargedCapacityQuery).FirstOrDefault();
        var cycleData = Wmi.Query("root\\wmi", CycleCountQuery).FirstOrDefault();
        var status = Wmi.Query("root\\wmi", StatusQuery).FirstOrDefault();

        if (battery is null && staticData is null)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "No battery was reported. This is normal on a desktop, or the battery firmware does not expose Win32_Battery."));
        }

        var design = CollectorSupport.BatteryCapacity(staticData?.UInt32("DesignedCapacity") ?? battery?.UInt32("DesignCapacity"));
        var full = CollectorSupport.BatteryCapacity(fullData?.UInt32("FullChargedCapacity") ?? battery?.UInt32("FullChargeCapacity"));

        var health = HardwareMath.BatteryHealthPercent(design, full);
        var wear = HardwareMath.BatteryWearPercent(design, full);
        var cycles = CollectorSupport.BatteryCycleCount(cycleData?.UInt32("CycleCount"));

        // BatteryStaticData reports Chemistry as a raw number; when it is
        // an unmapped vendor value the decoder returns "Not reported by
        // hardware", so fall back to the Win32_Battery code before giving
        // up: on a ThinkPad the static value is garbage while the
        // Win32_Battery code is the real chemistry.
        var chemistry = CollectorSupport.BatteryChemistry(staticData?.String("Chemistry"));
        if (chemistry == SpecLabels.NotReportedByHardware)
        {
            chemistry = CollectorSupport.BatteryChemistry(battery?.UInt32("Chemistry"));
        }

        var currentCharge = CollectorSupport.BatteryChargePercent(battery?.UInt16("EstimatedChargeRemaining"));
        var runtime = CollectorSupport.BatteryRuntimeMinutes(battery?.UInt32("EstimatedRunTime"));

        var headline = string.Join(" - ", new[]
        {
            // A missing design capacity is skipped, never printed as
            // "<not reported> - 85.5% health".
            design is null ? null : FormatCapacity(design),
            health is null ? "Health not reported" : string.Create(CultureInfo.InvariantCulture, $"{health.Value:0.#}% health"),
        }.Where(v => !string.IsNullOrWhiteSpace(v)));

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.DesignCapacity, FormatCapacity(design), "CIM root\\WMI BatteryStaticData.DesignedCapacity", numericValue: (double?)design, numericUnit: "mWh"),
            CollectorSupport.Spec(SpecLabels.FullChargeCapacity, FormatCapacity(full), "CIM root\\WMI BatteryFullChargedCapacity.FullChargedCapacity", numericValue: (double?)full, numericUnit: "mWh"),
            CollectorSupport.Spec(SpecLabels.Health, health is null ? null : string.Create(CultureInfo.InvariantCulture, $"{health.Value:0.0}%"), "Full charge capacity / design capacity", numericValue: health, numericUnit: "%"),
            CollectorSupport.Spec(SpecLabels.Wear, wear is null ? null : string.Create(CultureInfo.InvariantCulture, $"{wear.Value:0.0}%"), "100 - health", numericValue: wear, numericUnit: "%"),
            CollectorSupport.Spec(SpecLabels.HealthBand, HardwareMath.BatteryBandText(health), "Battery health bands (80/60)"),
            CollectorSupport.Spec(SpecLabels.CycleCount, cycles?.ToString(CultureInfo.InvariantCulture), "CIM root\\WMI BatteryCycleCount", numericValue: (double?)cycles, numericUnit: "cycles"),
            CollectorSupport.Spec(SpecLabels.Chemistry, chemistry, "CIM BatteryStaticData / Win32_Battery"),
            CollectorSupport.Spec(SpecLabels.Manufacturer, staticData?.String("ManufactureName"), "CIM root\\WMI BatteryStaticData"),
            CollectorSupport.Spec(SpecLabels.DeviceName, staticData?.String("DeviceName"), "CIM root\\WMI BatteryStaticData"),
            CollectorSupport.Spec(SpecLabels.SerialNumber, staticData?.String("SerialNumber"), "CIM root\\WMI BatteryStaticData"),
            CollectorSupport.Spec(SpecLabels.CurrentCharge, currentCharge is null
                ? null
                : string.Create(CultureInfo.InvariantCulture, $"{currentCharge.Value}%"), "CIM Win32_Battery.EstimatedChargeRemaining", numericValue: (double?)currentCharge, numericUnit: "%"),
            CollectorSupport.Spec(SpecLabels.Voltage, CollectorSupport.NonZeroNonMax(status?.UInt32("Voltage")) is { } millivolts
                ? string.Create(CultureInfo.InvariantCulture, $"{millivolts / 1000d:0.000} V")
                : null, "CIM root\\WMI BatteryStatus.Voltage", numericValue: CollectorSupport.NonZeroNonMax(status?.UInt32("Voltage")) is { } mv ? mv / 1000d : null, numericUnit: "V"),
            CollectorSupport.Spec(SpecLabels.ChargeRate, CollectorSupport.NonZeroNonMax(status?.UInt32("ChargeRate")) is { } chargeRate
                ? string.Create(CultureInfo.InvariantCulture, $"{chargeRate / 1000d:0.0} W")
                : null, "CIM root\\WMI BatteryStatus.ChargeRate", numericValue: CollectorSupport.NonZeroNonMax(status?.UInt32("ChargeRate")) is { } cr ? cr / 1000d : null, numericUnit: "W"),
            CollectorSupport.Spec(SpecLabels.DischargeRate, CollectorSupport.NonZeroNonMax(status?.UInt32("DischargeRate")) is { } dischargeRate
                ? string.Create(CultureInfo.InvariantCulture, $"{dischargeRate / 1000d:0.0} W")
                : null, "CIM root\\WMI BatteryStatus.DischargeRate", numericValue: CollectorSupport.NonZeroNonMax(status?.UInt32("DischargeRate")) is { } dr ? dr / 1000d : null, numericUnit: "W"),
            CollectorSupport.Spec(SpecLabels.EstimatedRuntime, runtime is null
                ? null
                : FormatRuntime(runtime.Value), "CIM Win32_Battery.EstimatedRunTime", numericValue: (double?)runtime, numericUnit: "minutes"),
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
            ? SpecLabels.NotReportedByHardware
            : string.Create(CultureInfo.InvariantCulture, $"{milliwattHours.Value:N0} mWh");

    private static string FormatRuntime(uint minutes) =>
        string.Create(CultureInfo.InvariantCulture, $"{minutes / 60} hours {minutes % 60} minutes");
}
