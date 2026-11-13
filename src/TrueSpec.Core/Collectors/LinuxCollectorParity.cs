namespace TrueSpec.Core.Collectors;

/// <summary>
/// How far a Linux data source covers one TrueSpec collector (plan: Beyond
/// v2.0, item 3 — Linux collector parity study). Full means the same readings
/// from a Linux source; Partial means the core readings map but some need a
/// tool that is not installed by default, need elevation, or have no
/// equivalent; NotFeasible means the readings are Windows products with no
/// Linux counterpart, so the collector would need a redesign, not a port.
/// No collector reaches Full in this study; the value exists so a future
/// port can claim it.
/// </summary>
public enum LinuxParity
{
    Full,
    Partial,
    NotFeasible,
}

/// <summary>
/// One row of the Linux parity study: one collector, the Linux source that
/// would feed it, how far that source covers it, and the plain-English
/// reason. The source is a path or a tool name a reader can check on any
/// Ubuntu machine; a source that is not installed by default says so.
/// </summary>
public sealed record LinuxCollectorParityEntry(
    string CollectorId,
    string LinuxSource,
    LinuxParity Feasibility,
    string Notes);

/// <summary>
/// The Linux collector parity study (plan: Beyond v2.0, item 3), as data.
/// Every collector in the app has exactly one row, so the study cannot
/// silently drop a collector: the tests pin the catalogue against the
/// collector list. The study was done on Ubuntu 24.04 (the same OS the CI
/// runners use): /proc and /sys were read directly, and the tools a Linux
/// build would need were checked for presence. smartctl, upower,
/// dmidecode, lspci, lm-sensors and xrandr were all absent on a stock
/// install; journalctl, apt and dpkg were present. Hardware serials are
/// root-only on Linux (/sys/devices/virtual/dmi/id/product_serial is mode
/// 0400) and were unreadable even to root on this VM, so serials are not a
/// standard-user fact on Linux the way they are on Windows.
/// Verdict: no collector ports as-is. 14 of 15 map partially; Security has
/// no Linux equivalent. Windows stays the shipping target; a Linux build
/// starts only if users ask for it.
/// </summary>
public static class LinuxCollectorParity
{
    /// <summary>
    /// The study's one-sentence verdict. Windows stays the shipping target;
    /// a Linux build starts only if users ask for it.
    /// </summary>
    public const string StudyVerdict =
        "No collector ports as-is: 14 of 15 map partially to a Linux source, Security has no Linux equivalent, " +
        "and the tools a Linux build would need are not installed by default on Ubuntu. " +
        "Windows stays the shipping target; a Linux build starts only if users ask for it.";

    /// <summary>
    /// One row per collector, in the same order as the collector catalogue
    /// (system, cpu, ram, storage, battery, gpu, display, network,
    /// motherboard, security, drivers, updates, startup, software, crash).
    /// </summary>
    public static IReadOnlyList<LinuxCollectorParityEntry> All { get; } = new[]
    {
        new LinuxCollectorParityEntry(
            "system",
            "/sys/devices/virtual/dmi/id + /proc/version",
            LinuxParity.Partial,
            "BIOS date, vendor and version, product name and OS version read as a standard user (verified on Ubuntu 24.04). " +
            "Windows install date has no Linux equivalent, and hardware serials are root-only."),
        new LinuxCollectorParityEntry(
            "cpu",
            "/proc/cpuinfo + /sys/devices/system/cpu/cpufreq",
            LinuxParity.Partial,
            "Model, core count, current MHz and cache come from cpuinfo (verified); the rated maximum comes from cpufreq on real hardware " +
            "but is absent in a VM. Live temperatures need hwmon or lm-sensors, which are not installed by default."),
        new LinuxCollectorParityEntry(
            "ram",
            "/proc/meminfo",
            LinuxParity.Partial,
            "Total, free and available memory read as a standard user (verified). Per-module manufacturer, serial, speed and slot need " +
            "dmidecode, which needs root and is not installed by default. The Windows memory-diagnostic result has no Linux equivalent."),
        new LinuxCollectorParityEntry(
            "storage",
            "/sys/block + smartctl",
            LinuxParity.Partial,
            "Devices, sizes and filesystems come from /sys/block (verified). SMART health, wear and power-on hours need smartctl, which is " +
            "not installed by default and needs root for most operations; NVMe counters need nvme-cli, also not installed by default."),
        new LinuxCollectorParityEntry(
            "battery",
            "/sys/class/power_supply/BAT0",
            LinuxParity.Partial,
            "On a laptop this covers design and full capacity, cycle count, chemistry, manufacturer, serial and charge rate with no extra tool " +
            "and no elevation. The directory is empty on this VM (no battery). The firmware estimated-runtime reading has no Linux equivalent."),
        new LinuxCollectorParityEntry(
            "gpu",
            "lspci + /sys/class/drm",
            LinuxParity.Partial,
            "lspci names the GPU model but is not installed by default; live clocks, temperatures and VRAM need hwmon or vendor tools. " +
            "This headless VM exposes no DRM device, so the display path could not be verified here."),
        new LinuxCollectorParityEntry(
            "display",
            "/sys/class/drm/*/edid",
            LinuxParity.Partial,
            "The EDID (panel manufacturer, model, serial, manufacture week and year) is the same panel truth as on Windows, when a display " +
            "is attached. This VM has no DRM connectors; xrandr is not installed by default."),
        new LinuxCollectorParityEntry(
            "network",
            "/sys/class/net + iw",
            LinuxParity.Partial,
            "Adapters, MAC addresses and link state come from /sys/class/net (verified). Wi-Fi standard and link speed need iw, which is not " +
            "installed by default; driver dates need modinfo or ethtool."),
        new LinuxCollectorParityEntry(
            "motherboard",
            "/sys/devices/virtual/dmi/id",
            LinuxParity.Partial,
            "Board vendor and product, BIOS vendor, version and date read as a standard user (verified). Serials are mode 0400 root-only and " +
            "were unreadable even to root on this VM."),
        new LinuxCollectorParityEntry(
            "security",
            "No Linux equivalent",
            LinuxParity.NotFeasible,
            "Device Guard, BitLocker, Windows Defender and TPM attestation are Windows products. Linux has LUKS, AppArmor/SELinux and " +
            "firewalls, but those are different facts, not the same collector ported. A Linux security view would need a redesign, not a port."),
        new LinuxCollectorParityEntry(
            "drivers",
            "/sys/bus + modinfo",
            LinuxParity.Partial,
            "Loaded kernel modules and their versions come from /sys/bus and modinfo. Driver age maps poorly (modules carry no install date the " +
            "way Windows drivers do), and there is no signed-driver inventory equivalent."),
        new LinuxCollectorParityEntry(
            "updates",
            "apt/dpkg history",
            LinuxParity.Partial,
            "dpkg lists installed packages with name, version and install date (verified). There is no single last-update-check or last-install " +
            "equivalent; unattended-upgrades logs vary by setup."),
        new LinuxCollectorParityEntry(
            "startup",
            "systemd user units + ~/.config/autostart + /etc/xdg/autostart",
            LinuxParity.Partial,
            "The standard autostart locations cover what launches at sign-in. There is no StartupApproved enabled/disabled state equivalent, and " +
            "the directories do not exist on a minimal install (verified absent here)."),
        new LinuxCollectorParityEntry(
            "software",
            "dpkg/rpm",
            LinuxParity.Partial,
            "Installed programs with name, version and install date come from dpkg (verified) or rpm. Windows activation state has no Linux " +
            "equivalent and would simply not apply."),
        new LinuxCollectorParityEntry(
            "crash",
            "journalctl (system journal)",
            LinuxParity.Partial,
            "Kernel oops, panics, service failures and unexpected shutdowns are in the journal (verified journalctl present with a boot list). " +
            "BugCheck and WHEA event IDs are Windows-only, so the crash classes would need new definitions, not a port of the same ones."),
    };

    /// <summary>
    /// The parity row for one collector id, or null when the id is not a
    /// collector. The comparison is exact and case-sensitive: collector ids
    /// are lowercase by contract.
    /// </summary>
    public static LinuxCollectorParityEntry? Get(string collectorId)
    {
        ArgumentNullException.ThrowIfNull(collectorId);
        return All.FirstOrDefault(
            entry => string.Equals(entry.CollectorId, collectorId, StringComparison.Ordinal));
    }
}
