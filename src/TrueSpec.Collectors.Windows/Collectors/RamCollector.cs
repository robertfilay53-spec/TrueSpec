namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using TrueSpec.Collectors.Windows.Diagnostics;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// Per-stick SPD data from Win32_PhysicalMemory. There is no RAM life sensor on
/// any consumer stick, so life is explicitly reported as not measurable.
/// </summary>
public sealed class RamCollector : ICollector
{
    public string Id => "ram";

    public string Name => "RAM";

    public string Category => "RAM";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(10);

    public bool RequiresAdministrator => false;

    // WMI query strings are hoisted so CollectorWmiSelectionTests can pin
    // the SELECT list to the properties this collector reads (hardening
    // item 7, F9 bug class).
    internal const string PhysicalMemoryQuery = "SELECT Capacity, Manufacturer, PartNumber, SerialNumber, BankLabel, DeviceLocator, FormFactor, SMBIOSMemoryType, MemoryType, ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory";
    internal const string PhysicalMemoryArrayQuery = "SELECT MemoryDevices FROM Win32_PhysicalMemoryArray";
    internal const string ComputerSystemQuery = "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem";

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var sticks = Wmi.Query("root\\cimv2", PhysicalMemoryQuery);
        if (sticks.Count == 0)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "CIM did not return Win32_PhysicalMemory."));
        }

        var array = Wmi.Query("root\\cimv2", PhysicalMemoryArrayQuery).FirstOrDefault();
        var computer = Wmi.Query("root\\cimv2", ComputerSystemQuery).FirstOrDefault();
        var memoryErrors = WindowsEventLog.CountMemoryErrors(TimeSpan.FromDays(30));

        var totalBytes = sticks.Sum(s => (decimal)(CollectorSupport.NonZeroNonMax(s.UInt64("Capacity")) ?? 0UL));
        var first = sticks[0];
        var type = CollectorSupport.MemoryType(first.UInt16("SMBIOSMemoryType"));
        if (type == SpecLabels.NotReportedByHardware)
        {
            type = CollectorSupport.MemoryType(first.UInt32("MemoryType"));
        }

        var speed = CollectorSupport.NonZeroNonMax(first.UInt32("ConfiguredClockSpeed"))
            ?? CollectorSupport.NonZeroNonMax(first.UInt32("Speed"));
        var slots = CollectorSupport.NonZeroNonMax(array?.UInt32("MemoryDevices"));
        var slotsText = slots is null
            ? SpecLabels.NotReportedByHardware
            : string.Create(CultureInfo.InvariantCulture, $"{sticks.Count} of {slots.Value}");

        var totalText = totalBytes <= 0 ? null : CollectorSupport.FormatBytesBinary((ulong)totalBytes);
        var headline = string.Join(" ", new[]
        {
            totalText,
            type == SpecLabels.NotReportedByHardware ? null : type,
            // "DDR5 5200 MT/s": never "-5200", which reads as a negative number.
            speed is null ? null : string.Create(CultureInfo.InvariantCulture, $"{speed.Value} MT/s"),
        }.Where(v => !string.IsNullOrWhiteSpace(v)));

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.TotalMemory, CollectorSupport.FormatBytesBinary(totalBytes <= 0 ? null : (ulong)totalBytes), "CIM Win32_PhysicalMemory capacity sum", numericValue: totalBytes <= 0 ? null : (double?)totalBytes, numericUnit: "bytes"),
            CollectorSupport.Spec(SpecLabels.Type, type, "CIM Win32_PhysicalMemory"),
            CollectorSupport.Spec(SpecLabels.Speed, speed is null ? null : string.Create(CultureInfo.InvariantCulture, $"{speed.Value} MT/s"), "CIM Win32_PhysicalMemory", numericValue: (double?)speed, numericUnit: "MT/s"),
            CollectorSupport.Spec(SpecLabels.ConfiguredSpeed, CollectorSupport.NonZeroNonMax(first.UInt32("ConfiguredClockSpeed")) is { } configured
                ? string.Create(CultureInfo.InvariantCulture, $"{configured} MT/s")
                : null, "CIM Win32_PhysicalMemory", numericValue: (double?)CollectorSupport.NonZeroNonMax(first.UInt32("ConfiguredClockSpeed")), numericUnit: "MT/s"),
            CollectorSupport.Spec(SpecLabels.FormFactor, CollectorSupport.FormFactor(first.UInt16("FormFactor")), "CIM Win32_PhysicalMemory"),
            CollectorSupport.Spec(SpecLabels.SlotsUsed, slotsText, "CIM Win32_PhysicalMemoryArray.MemoryDevices"),
            CollectorSupport.Spec(SpecLabels.MemoryErrors, memoryErrors is null
                ? null
                : memoryErrors.Value.ToString(CultureInfo.InvariantCulture), "Windows Event Log (WHEA-Logger memory events, MemoryDiagnostic)", numericValue: (double?)memoryErrors, numericUnit: "errors"),
            CollectorSupport.Spec(SpecLabels.MemoryTest, "Not run", "Windows Memory Diagnostic"),
            CollectorSupport.Spec(SpecLabels.LifeRemaining, SpecLabels.NotReportedByHardware, "No RAM life sensor exists"),
        };

        if (computer?.UInt64("TotalPhysicalMemory") is { } osTotal &&
            osTotal > 0 &&
            Math.Abs((decimal)osTotal - totalBytes) > 64UL * 1024UL * 1024UL)
        {
            specs.Add(CollectorSupport.Spec(
                SpecLabels.TotalMemoryReportedByWindows,
                CollectorSupport.FormatBytesBinary(osTotal),
                "CIM Win32_ComputerSystem.TotalPhysicalMemory",
                numericValue: (double?)osTotal,
                numericUnit: "bytes"));
        }

        for (var i = 0; i < sticks.Count; i++)
        {
            var stick = sticks[i];
            var prefix = string.Create(CultureInfo.InvariantCulture, $"Stick {i + 1}");
            specs.Add(CollectorSupport.Spec($"{prefix} - {SpecLabels.Manufacturer}", stick.String("Manufacturer"), "CIM Win32_PhysicalMemory"));
            specs.Add(CollectorSupport.Spec($"{prefix} - {SpecLabels.PartNumber}", stick.String("PartNumber"), "CIM Win32_PhysicalMemory"));
            specs.Add(CollectorSupport.Spec($"{prefix} - {SpecLabels.SerialNumber}", stick.String("SerialNumber"), "CIM Win32_PhysicalMemory"));
            specs.Add(CollectorSupport.Spec($"{prefix} - {SpecLabels.Capacity}", CollectorSupport.FormatBytesBinary(CollectorSupport.NonZeroNonMax(stick.UInt64("Capacity"))), "CIM Win32_PhysicalMemory", numericValue: (double?)CollectorSupport.NonZeroNonMax(stick.UInt64("Capacity")), numericUnit: "bytes"));
            specs.Add(CollectorSupport.Spec($"{prefix} - {SpecLabels.Slot}", stick.String("DeviceLocator") ?? stick.String("BankLabel"), "CIM Win32_PhysicalMemory"));
        }

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            headline,
            specs,
            "No RAM stick on any laptop exposes a life percentage, so TrueSpec does not invent one. It shows the real per-stick SPD data, the slot map and any memory errors from the Windows event log. Compare the installed size with the seller listing."));
    }
}
