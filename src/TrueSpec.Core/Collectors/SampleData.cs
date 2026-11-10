namespace TrueSpec.Core.Collectors;

using TrueSpec.Core.Models;

/// <summary>
/// Realistic sample readings used by the P0 stub collectors so the full UI is
/// viewable before any real hardware access exists. Every value here is fake
/// demo data and every collector that returns it is clearly a stub.
/// </summary>
public static class SampleData
{
    public const string MachineName = "THINKPAD-X1-DEMO";
    public const string OperatingSystem = "Windows 11 Pro 24H2 (build 26100)";

    public static ComponentSnapshot System() => new(
        "system",
        "System",
        "System",
        "system",
        "ThinkPad X1 Carbon Gen 12",
        null,
        new[]
        {
            Spec("Manufacturer", "Lenovo", "WMI Win32_ComputerSystem"),
            Spec("Model", "ThinkPad X1 Carbon Gen 12", "WMI Win32_ComputerSystem"),
            Spec("Serial number", "PF4ABCDE", "WMI Win32_BIOS"),
            Spec("SKU", "21KC005LUS", "WMI Win32_ComputerSystem"),
            Spec("Chassis type", "Notebook", "WMI Win32_SystemEnclosure"),
            Spec("BIOS vendor", "Lenovo", "WMI Win32_BIOS"),
            Spec("BIOS version", "N1XET59W (1.44)", "WMI Win32_BIOS"),
            Spec("BIOS date", "2025-08-12", "WMI Win32_BIOS"),
            Spec("SMBIOS version", "3.5", "WMI Win32_BIOS"),
            Spec("Windows edition", "Windows 11 Pro", "WMI Win32_OperatingSystem"),
            Spec("Windows build", "26100.5600", "WMI Win32_OperatingSystem"),
            Spec("Windows install date", "2026-09-21", "WMI Win32_OperatingSystem"),
            Spec("Uptime", "2 days, 6 hours", "WMI Win32_OperatingSystem"),
            Spec("TPM version", "2.0", "WMI Win32_TPM"),
            Spec("Secure Boot", "On", "WMI Win32_DeviceGuard"),
            Spec("BitLocker", "On", "WMI Win32_EncryptableVolume"),
            Spec("Critical errors (event log)", "0", "Windows Event Log"),
            Spec("Signed drivers only", "Yes", "WMI Win32_PnPSignedDriver"),
        },
        "This is the machine identity block. Check the model and serial number against the listing the seller showed you. A recent BIOS date and a recent Windows install date both support the idea that the machine was set up recently.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Cpu() => new(
        "cpu",
        "CPU",
        "CPU",
        "cpu",
        "Intel Core Ultra 7 155H",
        86d,
        new[]
        {
            Spec("Model", "Intel Core Ultra 7 155H", "WMI Win32_Processor"),
            Spec("Cores", "16 (6 performance + 8 efficiency + 2 low power)", "WMI Win32_Processor"),
            Spec("Threads", "22", "WMI Win32_Processor"),
            Spec("Base clock", "1.40 GHz", "WMI Win32_Processor"),
            Spec("Boost clock", "4.80 GHz", "WMI Win32_Processor"),
            Spec("L3 cache", "24 MB", "WMI Win32_Processor"),
            Spec("Package temperature", "47 C", "LibreHardwareMonitor (stub)", true),
            Spec("Peak temperature", "71 C", "LibreHardwareMonitor (stub)", true),
            Spec("Throttling", "No", "LibreHardwareMonitor (stub)"),
            Spec("Virtualization", "Enabled", "WMI Win32_Processor"),
        },
        "CPU health is based on temperatures and throttling, not on a life meter, because no such sensor exists. Anything under 90 C is normal for a laptop under load. Repeated throttling would lower this score.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Ram() => new(
        "ram",
        "RAM",
        "RAM",
        "ram",
        "32 GB LPDDR5x-7467",
        null,
        new[]
        {
            Spec("Total memory", "32 GB", "WMI Win32_ComputerSystem"),
            Spec("Advertised memory", "32 GB", "Seller listing (entered by buyer)"),
            Spec("Type", "LPDDR5x", "WMI Win32_PhysicalMemory"),
            Spec("Speed", "7467 MT/s", "WMI Win32_PhysicalMemory"),
            Spec("Form factor", "Soldered", "WMI Win32_PhysicalMemory"),
            Spec("Slots used", "2 of 2", "WMI Win32_PhysicalMemory"),
            Spec("Manufacturer", "Samsung", "WMI Win32_PhysicalMemory"),
            Spec("Part number", "K3LKBKB0BM-MGCP", "WMI Win32_PhysicalMemory"),
            Spec("Memory errors", "0", "Windows Event Log"),
            Spec("Memory test", "Not run", "Windows Memory Diagnostic"),
            Spec("Life remaining", "Not reported by hardware", "No memory life sensor exists"),
        },
        "No RAM stick on any laptop exposes a life percentage, so this tool does not invent one. What it does report is the real size, type and speed, plus any memory errors and the result of the built-in memory test. Compare the size here with what the seller advertised.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Storage() => new(
        "storage",
        "Storage",
        "Storage",
        "storage",
        "1 TB NVMe SSD - 97% health",
        97d,
        new[]
        {
            Spec("Model", "Samsung PM9A1 1 TB", "WMI Win32_DiskDrive"),
            Spec("Serial number", "S4EWNX0T912345", "WMI Win32_DiskDrive"),
            Spec("Firmware", "GXA7A0Q", "WMI Win32_DiskDrive"),
            Spec("Interface", "NVMe", "WMI Win32_DiskDrive"),
            Spec("Capacity", "1 TB (953 GB usable)", "WMI Win32_DiskDrive"),
            Spec("Wear level", "3%", "NVMe SMART percentage used", true),
            Spec("Power-on hours", "412", "NVMe SMART"),
            Spec("Power cycles", "97", "NVMe SMART"),
            Spec("Data written", "12.4 TB", "NVMe SMART data units written"),
            Spec("Rated endurance", "600 TBW", "Manufacturer spec"),
            Spec("Temperature", "38 C", "NVMe SMART"),
            Spec("Health word", "Good", "Get-StorageReliabilityCounter"),
            Spec("TRIM", "Enabled", "Windows storage stack"),
        },
        "SSD wear is a real manufacturer estimate, not a failure date. 3% used is a healthy drive but it is not a brand new one. A truly new laptop shows 0% wear and close to zero power-on hours.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Battery() => new(
        "battery",
        "Battery",
        "Battery",
        "battery",
        "57 Wh - 97% health",
        97d,
        new[]
        {
            Spec("Design capacity", "57.0 Wh", "WMI Win32_Battery"),
            Spec("Full charge capacity", "55.2 Wh", "powercfg /batteryreport"),
            Spec("Health", "96.8%", "Full charge / design capacity"),
            Spec("Wear", "3.2%", "Full charge / design capacity"),
            Spec("Cycle count", "12", "Battery firmware via WMI"),
            Spec("Chemistry", "Lithium ion", "WMI Win32_Battery"),
            Spec("Manufacturer", "SMP", "WMI Win32_Battery"),
            Spec("Current charge", "78%", "WMI Win32_Battery"),
            Spec("Estimated runtime", "5 hours 40 minutes", "powercfg /batteryreport", true),
        },
        "Battery health is the full charge capacity divided by the design capacity. 80% and up is normal. 60 to 79% is noticeable wear. Below 60% the battery should be replaced. Cycle count shows how many charge cycles the pack has been through.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Gpu() => new(
        "gpu",
        "GPU",
        "GPU",
        "gpu",
        "Intel Arc Graphics",
        90d,
        new[]
        {
            Spec("Model", "Intel Arc Graphics (integrated)", "WMI Win32_VideoController"),
            Spec("Vendor", "Intel", "WMI Win32_VideoController"),
            Spec("Dedicated memory", "Shared system memory", "WMI Win32_VideoController"),
            Spec("Driver version", "32.0.101.5768", "WMI Win32_VideoController"),
            Spec("Driver date", "2026-03-14", "WMI Win32_VideoController"),
            Spec("Temperature", "52 C", "LibreHardwareMonitor (stub)", true),
            Spec("Utilization", "8%", "LibreHardwareMonitor (stub)", true),
            Spec("DirectX support", "12 Ultimate", "WMI Win32_VideoController"),
            Spec("Outputs", "1 (internal OLED)", "WMI Win32_VideoController"),
        },
        "GPU health is estimated from temperature and load, because the card itself has no health percentage. A GPU that stays cool under load is fine. Hot spots or fan noise under light work are warning signs.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Display() => new(
        "display",
        "Display",
        "Display",
        "display",
        "14.0\" OLED 2880 x 1800 - 120 Hz",
        null,
        new[]
        {
            Spec("Panel manufacturer", "Samsung", "EDID WmiMonitorID"),
            Spec("Panel model", "ATNA40CU05", "EDID WmiMonitorID"),
            Spec("Panel serial", "0x4A2C91", "EDID WmiMonitorID"),
            Spec("Manufacture date", "2025 week 18", "EDID manufacture week/year"),
            Spec("Size", "14.0 inches", "EDID"),
            Spec("Resolution", "2880 x 1800", "WMI Win32_VideoController"),
            Spec("Refresh rate", "120 Hz", "WMI Win32_VideoController"),
            Spec("Panel type", "OLED", "EDID"),
            Spec("HDR", "Yes (DisplayHDR True Black 500)", "EDID extended block"),
            Spec("Color depth", "10-bit", "EDID"),
        },
        "The panel block shows exactly what screen is fitted. The manufacture date comes from the panel itself. A panel made long before the laptop was sold can be a sign of a replaced screen.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Network() => new(
        "network",
        "Network",
        "Network",
        "network",
        "Intel Wi-Fi 6E AX211",
        92d,
        new[]
        {
            Spec("Wi-Fi adapter", "Intel Wi-Fi 6E AX211", "WMI Win32_NetworkAdapter"),
            Spec("Standards", "802.11ax (Wi-Fi 6E)", "netsh wlan show drivers"),
            Spec("Driver version", "23.60.1.4", "WMI Win32_PnPSignedDriver"),
            Spec("Driver date", "2026-03-14", "WMI Win32_PnPSignedDriver"),
            Spec("Link speed", "2402 Mbps", "Get-NetAdapter"),
            Spec("Signal", "88%", "netsh wlan show interfaces"),
            Spec("Band", "6 GHz", "netsh wlan show interfaces"),
            Spec("Channel", "37", "netsh wlan show interfaces"),
            Spec("MAC address", "AC-74-B1-2C-9F-10", "Get-NetAdapter"),
            Spec("Ethernet", "Not present", "Get-NetAdapter"),
            Spec("Bluetooth", "5.3", "Bluetooth adapter"),
            Spec("Hardware manufacture date", "Not reported by hardware", "Not exposed by any Wi-Fi card"),
        },
        "Network health is based on the age of the driver and the capability of the adapter. The card itself never reports a manufacture date, so this tool shows the driver date and says the hardware date is not reported instead of guessing.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Motherboard() => new(
        "motherboard",
        "Motherboard",
        "Motherboard",
        "motherboard",
        "Lenovo 21KC",
        null,
        new[]
        {
            Spec("Manufacturer", "Lenovo", "WMI Win32_BaseBoard"),
            Spec("Model", "21KC", "WMI Win32_BaseBoard"),
            Spec("Serial number", "PF4ABCDE", "WMI Win32_BaseBoard"),
            Spec("Chipset", "Intel Meteor Lake SoC", "WMI Win32_BaseBoard"),
            Spec("BIOS version", "N1XET59W (1.44)", "WMI Win32_BIOS"),
            Spec("BIOS date", "2025-08-12", "WMI Win32_BIOS"),
            Spec("PCIe slots", "Not reported by hardware", "Laptop board"),
            Spec("M.2 slots", "1 (occupied)", "WMI Win32_DiskDrive"),
            Spec("RAM slots", "Soldered memory", "WMI Win32_PhysicalMemory"),
        },
        "The motherboard block confirms the board and BIOS fitted to this machine. There is no motherboard life sensor. Health here is judged from BIOS age and the condition of the components connected to it.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Security() => new(
        "security",
        "Security",
        "Security",
        "security",
        "TPM 2.0 - Secure Boot on",
        100d,
        new[]
        {
            Spec("TPM version", "2.0", "WMI Win32_TPM"),
            Spec("Secure Boot", "On", "WMI Win32_DeviceGuard"),
            Spec("BitLocker", "On (C: encrypted)", "WMI Win32_EncryptableVolume"),
            Spec("Virtualization-based security", "On", "WMI Win32_DeviceGuard"),
            Spec("User Account Control", "On", "Windows registry"),
            Spec("Defender antivirus", "On", "Windows Security"),
            Spec("Firewall", "On", "Windows Security"),
            Spec("Unsigned drivers", "0", "WMI Win32_PnPSignedDriver"),
        },
        "This block shows the security features that protect the machine. A used laptop with BitLocker already on and Secure Boot enabled has been set up properly. If BitLocker is off on a machine sold as new, ask why.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Drivers() => new(
        "drivers",
        "Drivers",
        "Security",
        "drivers",
        "187 drivers - all signed",
        100d,
        new[]
        {
            Spec("Installed drivers", "187", "WMI Win32_PnPSignedDriver"),
            Spec("Unsigned drivers", "0", "WMI Win32_PnPSignedDriver"),
            Spec("Drivers older than 5 years", "3", "WMI Win32_PnPSignedDriver"),
            Spec("Oldest driver date", "2019-06-11", "WMI Win32_PnPSignedDriver"),
        },
        "Drivers are the small programs that let Windows talk to hardware. All signed drivers is a good sign. A handful of very old drivers is normal, but many old drivers can cause instability.",
        DateTimeOffset.Now);

    public static IReadOnlyList<ComponentSnapshot> All() => new[]
    {
        System(), Cpu(), Ram(), Storage(), Battery(), Gpu(),
        Display(), Network(), Motherboard(), Security(), Drivers(),
    };

    private static SpecValue Spec(string label, string value, string source, bool estimate = false) =>
        new(label, value, source, estimate);
}
