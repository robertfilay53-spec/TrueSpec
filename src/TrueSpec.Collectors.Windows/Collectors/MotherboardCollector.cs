namespace TrueSpec.Collectors.Windows.Collectors;

using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// Board and BIOS identity from CIM. There is no motherboard life sensor, so
/// the detail page reports BIOS age and connected component condition instead.
/// </summary>
public sealed class MotherboardCollector : ICollector
{
    public string Id => "motherboard";

    public string Name => "Motherboard";

    public string Category => "Motherboard";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(10);

    public bool RequiresAdministrator => false;

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var board = Wmi.Query("root\\cimv2", "SELECT Manufacturer, Product, Version, SerialNumber FROM Win32_BaseBoard").FirstOrDefault();
        // Manufacturer and Version must be in the SELECT: WmiRow only
        // carries the properties the query asked for, so reading them
        // without selecting them always returned null and the BIOS vendor
        // showed SpecLabels.NotReportedByHardware even though the same
        // Win32_BIOS read had the version and date (a ThinkPad reports
        // "LENOVO" here).
        var bios = Wmi.Query("root\\cimv2", "SELECT Manufacturer, ReleaseDate, SMBIOSBIOSVersion, Version, SerialNumber FROM Win32_BIOS").FirstOrDefault();
        var enclosure = Wmi.Query("root\\cimv2", "SELECT ChassisTypes, SerialNumber FROM Win32_SystemEnclosure").FirstOrDefault();
        var computer = Wmi.Query("root\\cimv2", "SELECT SystemFamily FROM Win32_ComputerSystem").FirstOrDefault();

        if (board is null && bios is null)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "CIM did not return Win32_BaseBoard or Win32_BIOS."));
        }

        var manufacturer = board?.String("Manufacturer");
        var product = board?.String("Product");
        var headline = string.Join(" ", new[] { manufacturer, product }
            .Where(v => !string.IsNullOrWhiteSpace(v)));

        var chassisTypes = enclosure?.UInt16Array("ChassisTypes");
        var chassis = chassisTypes is { Length: > 0 }
            ? CollectorSupport.ChassisType(chassisTypes[0])
            : SpecLabels.NotReportedByHardware;

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.Manufacturer, manufacturer, "CIM Win32_BaseBoard"),
            CollectorSupport.Spec(SpecLabels.Model, product, "CIM Win32_BaseBoard"),
            CollectorSupport.Spec(SpecLabels.Version, board?.String("Version"), "CIM Win32_BaseBoard"),
            CollectorSupport.Spec(SpecLabels.SerialNumber, board?.String("SerialNumber"), "CIM Win32_BaseBoard"),
            CollectorSupport.Spec(SpecLabels.ChassisType, chassis, "CIM Win32_SystemEnclosure"),
            CollectorSupport.Spec(SpecLabels.ChassisSerial, enclosure?.String("SerialNumber"), "CIM Win32_SystemEnclosure"),
            CollectorSupport.Spec(SpecLabels.SystemFamily, computer?.String("SystemFamily"), "CIM Win32_ComputerSystem"),
            CollectorSupport.Spec(SpecLabels.BIOSVendor, bios?.String("Manufacturer"), "CIM Win32_BIOS"),
            CollectorSupport.Spec(SpecLabels.BIOSVersion, bios?.String("SMBIOSBIOSVersion") ?? bios?.String("Version"), "CIM Win32_BIOS"),
            CollectorSupport.Spec(SpecLabels.BIOSDate, bios?.DateText("ReleaseDate"), "CIM Win32_BIOS.ReleaseDate"),
            CollectorSupport.Spec(SpecLabels.PCIeSlots, SpecLabels.NotReportedByHardware, "Laptop and many desktop boards do not expose slot count through CIM"),
            CollectorSupport.Spec(SpecLabels.M2Slots, SpecLabels.NotReportedByHardware, "M.2 slot count is not exposed by CIM"),
            CollectorSupport.Spec(SpecLabels.RAMSlots, SpecLabels.NotReportedByHardware, "See the RAM page for the real slots-used reading"),
            CollectorSupport.Spec(SpecLabels.LifeRemaining, SpecLabels.NotReportedByHardware, "No motherboard life sensor exists"),
        };

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            string.IsNullOrWhiteSpace(headline) ? "Motherboard" : headline,
            specs,
            "The motherboard block confirms the board and BIOS fitted to this machine. There is no motherboard life sensor. Health here is judged from BIOS age and the condition of the components connected to it."));
    }
}
