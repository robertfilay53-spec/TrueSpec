namespace TrueSpec.Core.Models;

/// <summary>
/// The one contract for display labels: every label a collector emits and
/// every label a consumer reads (health rules, buyer checks, trends) is a
/// constant here. A renamed label in a collector used to silently break a
/// health rule or a buyer check that matched the old string; now both sides
/// compile against the same constant, and the agreement tests prove the
/// collectors emit every label their consumers expect. Labels built
/// dynamically (per-drive "Drive 2 - ...", per-volume, per-adapter) compose
/// the base label from these constants with a prefix, so the base label
/// itself is still the same constant. The typed-measurements channel that
/// replaces string matching entirely is a v1.3 item; this class is the
/// constants half of that contract.
/// </summary>
public static class SpecLabels
{
    public const string ActiveResolution = "Active resolution (Windows)";
    public const string Antivirus = "Antivirus";
    public const string Architecture = "Architecture";
    public const string BIOSDate = "BIOS date";
    public const string BIOSVendor = "BIOS vendor";
    public const string BIOSVersion = "BIOS version";
    public const string Band = "Band";
    public const string BitLocker = "BitLocker";
    public const string BluetoothDevices = "Bluetooth devices";
    public const string BluetoothVersion = "Bluetooth version";
    public const string CPUFan = "CPU fan";
    public const string CPULoad = "CPU load";
    public const string Capacity = "Capacity";
    public const string Channel = "Channel";
    public const string ChargeRate = "Charge rate";
    public const string ChassisSerial = "Chassis serial";
    public const string ChassisType = "Chassis type";
    public const string Chemistry = "Chemistry";
    public const string ColourDepth = "Colour depth";
    public const string ConfiguredSpeed = "Configured speed";
    public const string CoreVoltage = "Core voltage";
    public const string Cores = "Cores";
    public const string CrashBlueScreens = "Blue screens";
    public const string CrashCorrectedHardwareErrors = "Corrected hardware errors";
    public const string CrashCountSuffix = " (last 90 days)";
    public const string CrashDiskErrors = "Disk errors";
    public const string CrashEventLogGap = "Event log gap";
    public const string CrashFatalHardwareErrors = "Fatal hardware errors";
    public const string CrashFirstSuffix = " - first recorded";
    public const string CrashRecentSuffix = " - most recent";
    public const string CrashUnexpectedShutdowns = "Unexpected shutdowns";
    public const string CriticalErrorsEventLog = "Critical errors (event log)";
    public const string CurrentCharge = "Current charge";
    public const string CurrentClock = "Current clock";
    public const string CurrentMode = "Current mode";
    public const string CycleCount = "Cycle count";
    public const string DNSServers = "DNS servers";
    public const string DataRead = "Data read";
    public const string DataWritten = "Data written";
    public const string DaysSinceLastUpdateInstall = "Days since last update install";
    public const string DedicatedMemory = "Dedicated memory";
    public const string DesignCapacity = "Design capacity";
    public const string DeviceName = "Device name";
    public const string DirectXSupport = "DirectX support";
    public const string Disabled = "Disabled";
    public const string DischargeRate = "Discharge rate";
    public const string DriverDate = "Driver date";
    public const string DriverVersion = "Driver version";
    public const string DriversOlderThan5Years = "Drivers older than 5 years";
    public const string EarliestEventLogEntry = "Earliest event log entry";
    public const string Enabled = "Enabled";
    public const string EstimatedRuntime = "Estimated runtime";
    public const string EthernetDNSServers = "Ethernet DNS servers";
    public const string EthernetIPAddress = "Ethernet IP address";
    public const string EthernetMACAddress = "Ethernet MAC address";
    public const string EthernetAdapter = "Ethernet adapter";
    public const string EthernetDriverAge = "Ethernet driver age";
    public const string EthernetDriverDate = "Ethernet driver date";
    public const string EthernetDriverVersion = "Ethernet driver version";
    public const string EthernetGateway = "Ethernet gateway";
    public const string EthernetLinkSpeed = "Ethernet link speed (negotiated rate at scan time)";
    public const string EthernetStatus = "Ethernet status";
    public const string Filesystem = "Filesystem";
    public const string Firewall = "Firewall";
    public const string Firmware = "Firmware";
    public const string FormFactor = "Form factor";
    public const string FreeSpace = "Free space";
    public const string FreeSpacePercent = "Free space (%)";
    public const string FullChargeCapacity = "Full charge capacity";
    public const string GPUClock = "GPU clock";
    public const string GPUDriverAge = "GPU driver age";
    public const string GPUFan = "GPU fan";
    public const string GPULoad = "GPU load";
    public const string GPUPower = "GPU power";
    public const string GPUTemperature = "GPU temperature";
    public const string GPUTemperatureC = "GPU temperature (C)";
    public const string GPUVoltage = "GPU voltage";
    public const string Gateway = "Gateway";
    public const string HDR = "HDR";
    public const string HardwareManufactureDate = "Hardware manufacture date";
    public const string Health = "Health";
    public const string HealthBand = "Health band";
    public const string HealthWord = "Health word";
    public const string IPAddress = "IP address";
    public const string InstalledDrivers = "Installed drivers";
    public const string InstalledPrograms = "Installed programs";
    public const string InstalledRuntimes = "Installed runtimes";
    public const string InstalledUpdatesHotfixes = "Installed updates (hotfixes)";
    public const string Interface = "Interface";
    public const string KeysRead = "Keys read";
    public const string L2Cache = "L2 cache";
    public const string L3Cache = "L3 cache";
    public const string LastSuccessfulUpdateCheck = "Last successful update check";
    public const string LastSuccessfulUpdateInstall = "Last successful update install";
    public const string LicensedProduct = "Licensed product";
    public const string LicenseChannel = "License channel";
    public const string LifeRemaining = "Life remaining";
    public const string LinkSpeed = "Link speed (negotiated rate at scan time)";
    public const string LiveCoreClock = "Live core clock";
    public const string M2Slots = "M.2 slots";
    public const string MACAddress = "MAC address";
    public const string ManufactureDate = "Manufacture date";
    public const string Manufacturer = "Manufacturer";
    public const string MaxClock = "Max clock";
    public const string MemoryErrors = "Memory errors";
    public const string MemoryErrorsEventLog = "Memory errors (event log)";
    public const string MemoryTest = "Memory test";
    public const string Model = "Model";
    public const string MonitorsDetected = "Monitors detected";
    public const string NativeResolution = "Native resolution (panel)";
    public const string OldestDriverDate = "Oldest driver date";
    public const string OldestDriverDevice = "Oldest driver device";
    public const string OperatingSystem = "Operating system";
    public const string OperationalStatus = "Operational status";
    public const string PCIeSlots = "PCIe slots";
    public const string PackagePower = "Package power";
    public const string PackageTemperature = "Package temperature";
    public const string PackageTemperatureC = "Package temperature (C)";
    public const string PanelManufacturer = "Panel manufacturer";
    public const string PanelModel = "Panel model";
    public const string PanelName = "Panel name";
    public const string PanelSerial = "Panel serial";
    public const string PanelType = "Panel type";
    public const string PartNumber = "Part number";
    public const string PowerCycles = "Power cycles";
    public const string PowerOnHours = "Power-on hours";
    public const string ProcessorID = "Processor ID";
    public const string RAMSlots = "RAM slots";
    public const string RadioType = "Radio type";
    /// <summary>
    /// The rated boost clock from the bundled hardware database: a
    /// spec-sheet maximum (Intel ARK Max Turbo Frequency, AMD Max Boost
    /// Clock), never a measurement and never a replacement for the
    /// measured "Max clock". Shown only when the CPU model is in the
    /// dataset; a model that matches nothing gets no such spec.
    /// </summary>
    public const string RatedBoostClock = "Rated boost clock";
    /// <summary>
    /// The rated E-core boost clock from the bundled hardware database
    /// (Intel hybrid chips only): the Efficient-core Max Turbo
    /// Frequency. Null for every other chip, so no such spec is shown.
    /// </summary>
    public const string RatedECoreBoostClock = "Rated E-core boost clock";
    /// <summary>
    /// The rated memory from the bundled hardware database (GPU only):
    /// the card's VRAM size from its spec sheet, never a measurement and
    /// never a replacement for the measured "Dedicated memory". Shown
    /// only when the GPU model is in the dataset and its memory figure
    /// is unambiguous; a model whose device name cannot distinguish
    /// memory variants gets no such spec.
    /// </summary>
    public const string RatedMemory = "Rated memory";
    /// <summary>
    /// The rated board power from the bundled hardware database (GPU
    /// only): the manufacturer's Total Board Power / Total Graphics
    /// Power. For laptop GPUs it is the maximum of the published TGP
    /// range, so it is a ceiling, not the power this laptop draws.
    /// Shown only when the GPU model is in the dataset; never a
    /// replacement for the measured "GPU power".
    /// </summary>
    public const string RatedBoardPower = "Rated board power";
    public const string RatedEndurance = "Rated endurance";
    public const string ReadErrors = "Read errors";
    public const string ReceiveRate = "Receive rate";
    public const string RefreshRate = "Refresh rate";
    public const string Resolution = "Resolution";
    public const string SKU = "SKU";
    public const string SMARTWarnings = "SMART warnings";
    public const string SMBIOSVersion = "SMBIOS version";
    public const string SSID = "SSID";
    public const string SecureBoot = "Secure Boot";
    public const string SerialNumber = "Serial number";
    public const string Signal = "Signal";
    public const string SignedDriversOnly = "Signed drivers only";
    public const string Size = "Size";
    public const string Slot = "Slot";
    public const string SlotsUsed = "Slots used";
    public const string Socket = "Socket";
    public const string Speed = "Speed";
    public const string Standards = "Standards";
    public const string StartupPrograms = "Startup programs";
    public const string StateNotReported = "State not reported";

    /// <summary>
    /// The one honest "no reading" value in the whole app: anything the
    /// hardware did not report says exactly this, never a guessed number.
    /// Every collector, analyzer, exporter and ViewModel that needs the
    /// text uses this constant; the literal appears nowhere else in the
    /// tree (enforced by HonestyChokePointTests), so a wording change is
    /// one edit and a comparison can never silently miss a copy.
    /// </summary>
    public const string NotReportedByHardware = "Not reported by hardware";
    public const string Status = "Status";
    public const string SystemFamily = "System family";
    public const string TPMActivated = "TPM activated";
    public const string TPMEnabled = "TPM enabled";
    public const string TPMOwned = "TPM owned";
    public const string TPMVersion = "TPM version";
    public const string TRIM = "TRIM";
    public const string Temperature = "Temperature";
    public const string ThermalThrottleFlag = "Thermal throttle flag";
    public const string Threads = "Threads";
    public const string TotalMemory = "Total memory";
    /// <summary>
    /// The memory Windows currently sees (Win32_ComputerSystem.
    /// TotalPhysicalMemory). Deliberately not called a "total": on a
    /// virtual machine with dynamic memory this reading moves between
    /// scans of the same machine, while the installed total (the
    /// Win32_PhysicalMemory stick sum) does not. A label that says
    /// "total" must never change between scans.
    /// </summary>
    public const string TotalMemoryReportedByWindows = "Memory visible to Windows";
    public const string TotalSpace = "Total space";
    public const string TransmitRate = "Transmit rate";
    public const string Type = "Type";
    public const string UncorrectedErrors = "Uncorrected errors";
    public const string UnreadableEntries = "Unreadable entries";
    public const string UnknownDriverSignature = "Unknown driver signature";
    public const string UnsignedDrivers = "Unsigned drivers";
    public const string UpdateManagement = "Update management";
    public const string Uptime = "Uptime";
    public const string UserAccountControl = "User Account Control";
    public const string Vendor = "Vendor";
    public const string Version = "Version";
    public const string VeryOldDriverFlag = "Very old driver flag";
    public const string VideoProcessor = "Video processor";
    public const string Virtualization = "Virtualization";
    public const string VirtualizationBasedSecurity = "Virtualization-based security";
    public const string Voltage = "Voltage";
    public const string Volumes = "Volumes";
    public const string Wear = "Wear";
    public const string WearLevel = "Wear level";
    public const string WiFiAdapter = "Wi-Fi adapter";
    public const string WifiDriverAge = "Wi-Fi driver age";
    public const string WifiStandardNegotiated = "Wi-Fi standard (negotiated)";
    public const string WiFiVendor = "Wi-Fi vendor";
    public const string DotNetFramework = ".NET Framework";
    public const string WindowsActivation = "Windows activation";
    public const string WindowsBuild = "Windows build";
    public const string WindowsEdition = "Windows edition";
    public const string WindowsInstallDate = "Windows install date";
    public const string WriteErrors = "Write errors";
}
