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

        var board = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_BaseBoard").FirstOrDefault();
        var bios = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_BIOS").FirstOrDefault();
        var enclosure = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_SystemEnclosure").FirstOrDefault();
        var computer = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_ComputerSystem").FirstOrDefault();

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
            : CollectorSupport.NotReported;

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec("Manufacturer", manufacturer, "CIM Win32_BaseBoard"),
            CollectorSupport.Spec("Model", product, "CIM Win32_BaseBoard"),
            CollectorSupport.Spec("Version", board?.String("Version"), "CIM Win32_BaseBoard"),
            CollectorSupport.Spec("Serial number", board?.String("SerialNumber"), "CIM Win32_BaseBoard"),
            CollectorSupport.Spec("Chassis type", chassis, "CIM Win32_SystemEnclosure"),
            CollectorSupport.Spec("Chassis serial", enclosure?.String("SerialNumber"), "CIM Win32_SystemEnclosure"),
            CollectorSupport.Spec("System family", computer?.String("SystemFamily"), "CIM Win32_ComputerSystem"),
            CollectorSupport.Spec("BIOS vendor", bios?.String("Manufacturer"), "CIM Win32_BIOS"),
            CollectorSupport.Spec("BIOS version", bios?.String("SMBIOSBIOSVersion") ?? bios?.String("Version"), "CIM Win32_BIOS"),
            CollectorSupport.Spec("BIOS date", bios?.DateText("ReleaseDate"), "CIM Win32_BIOS.ReleaseDate"),
            CollectorSupport.Spec("PCIe slots", "Not reported by hardware", "Laptop and many desktop boards do not expose slot count through CIM"),
            CollectorSupport.Spec("M.2 slots", "Not reported by hardware", "M.2 slot count is not exposed by CIM"),
            CollectorSupport.Spec("RAM slots", "Not reported by hardware", "See the RAM page for the real slots-used reading"),
            CollectorSupport.Spec("Life remaining", "Not reported by hardware", "No motherboard life sensor exists"),
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
