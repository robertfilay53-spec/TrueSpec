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

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var sticks = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_PhysicalMemory");
        if (sticks.Count == 0)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "CIM did not return Win32_PhysicalMemory."));
        }

        var array = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_PhysicalMemoryArray").FirstOrDefault();
        var computer = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_ComputerSystem").FirstOrDefault();
        var memoryErrors = WindowsEventLog.CountMemoryErrors(TimeSpan.FromDays(30));

        var totalBytes = sticks.Sum(s => (decimal)(s.UInt64("Capacity") ?? 0UL));
        var first = sticks[0];
        var type = CollectorSupport.MemoryType(first.UInt16("SMBIOSMemoryType"));
        if (type == CollectorSupport.NotReported)
        {
            type = CollectorSupport.MemoryType(first.UInt32("MemoryType"));
        }

        var speed = CollectorSupport.NonZero(first.UInt32("ConfiguredClockSpeed"))
            ?? CollectorSupport.NonZero(first.UInt32("Speed"));
        var slots = array?.UInt32("MemoryDevices");
        var slotsText = slots is null
            ? CollectorSupport.NotReported
            : string.Create(CultureInfo.InvariantCulture, $"{sticks.Count} of {slots.Value}");

        var headline = string.Join(" ", new[]
        {
            CollectorSupport.FormatBytesBinary(totalBytes <= 0 ? null : (ulong)totalBytes),
            type == CollectorSupport.NotReported ? null : type,
            // "DDR5 5200 MT/s": never "-5200", which reads as a negative number.
            speed is null ? null : string.Create(CultureInfo.InvariantCulture, $"{speed.Value} MT/s"),
        }.Where(v => !string.IsNullOrWhiteSpace(v)));

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec("Total memory", CollectorSupport.FormatBytesBinary(totalBytes <= 0 ? null : (ulong)totalBytes), "CIM Win32_PhysicalMemory capacity sum"),
            CollectorSupport.Spec("Type", type, "CIM Win32_PhysicalMemory"),
            CollectorSupport.Spec("Speed", speed is null ? null : string.Create(CultureInfo.InvariantCulture, $"{speed.Value} MT/s"), "CIM Win32_PhysicalMemory"),
            CollectorSupport.Spec("Configured speed", CollectorSupport.NonZero(first.UInt32("ConfiguredClockSpeed")) is { } configured
                ? string.Create(CultureInfo.InvariantCulture, $"{configured} MT/s")
                : null, "CIM Win32_PhysicalMemory"),
            CollectorSupport.Spec("Form factor", CollectorSupport.FormFactor(first.UInt16("FormFactor")), "CIM Win32_PhysicalMemory"),
            CollectorSupport.Spec("Slots used", slotsText, "CIM Win32_PhysicalMemoryArray.MemoryDevices"),
            CollectorSupport.Spec("Memory errors", memoryErrors is null
                ? null
                : memoryErrors.Value.ToString(CultureInfo.InvariantCulture), "Windows Event Log (WHEA-Logger memory events, MemoryDiagnostic)"),
            CollectorSupport.Spec("Memory test", "Not run", "Windows Memory Diagnostic"),
            CollectorSupport.Spec("Life remaining", "Not reported by hardware", "No RAM life sensor exists"),
        };

        if (computer?.UInt64("TotalPhysicalMemory") is { } osTotal &&
            osTotal > 0 &&
            Math.Abs((decimal)osTotal - totalBytes) > 64UL * 1024UL * 1024UL)
        {
            specs.Add(CollectorSupport.Spec(
                "Total memory reported by Windows",
                CollectorSupport.FormatBytesBinary(osTotal),
                "CIM Win32_ComputerSystem.TotalPhysicalMemory"));
        }

        for (var i = 0; i < sticks.Count; i++)
        {
            var stick = sticks[i];
            var prefix = string.Create(CultureInfo.InvariantCulture, $"Stick {i + 1}");
            specs.Add(CollectorSupport.Spec($"{prefix} - Manufacturer", stick.String("Manufacturer"), "CIM Win32_PhysicalMemory"));
            specs.Add(CollectorSupport.Spec($"{prefix} - Part number", stick.String("PartNumber"), "CIM Win32_PhysicalMemory"));
            specs.Add(CollectorSupport.Spec($"{prefix} - Serial number", stick.String("SerialNumber"), "CIM Win32_PhysicalMemory"));
            specs.Add(CollectorSupport.Spec($"{prefix} - Capacity", CollectorSupport.FormatBytesBinary(CollectorSupport.NonZero(stick.UInt64("Capacity"))), "CIM Win32_PhysicalMemory"));
            specs.Add(CollectorSupport.Spec($"{prefix} - Slot", stick.String("DeviceLocator") ?? stick.String("BankLabel"), "CIM Win32_PhysicalMemory"));
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
