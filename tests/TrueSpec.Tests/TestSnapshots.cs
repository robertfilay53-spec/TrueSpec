using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

internal static class TestSnapshots
{
    public static SystemSnapshot Healthy()
    {
        var now = DateTimeOffset.UtcNow;
        var components = new[]
        {
            System(now),
            Cpu(),
            Ram("16 GB"),
            Storage("0", "0"),
            Battery(2),
            Gpu(),
            Network(now.AddDays(-20).ToString("yyyy-MM-dd")),
        };

        var score = new ScoringEngine().Score(components);
        var checks = new BuyerCheckEvaluator().Evaluate(
            components,
            new BuyerCheckContext("16 GB"));

        return new SystemSnapshot(
            Guid.NewGuid(),
            now,
            "Test Laptop",
            "Windows 11 Pro",
            components,
            score,
            checks);
    }

    public static ComponentSnapshot System(DateTimeOffset now) => new(
        "system",
        "System",
        "System",
        "system",
        "Test Laptop",
        null,
        new[]
        {
            new SpecValue("Operating system", "Windows 11 Pro", "CIM Win32_OperatingSystem"),
            new SpecValue("BIOS date", now.AddDays(-20).ToString("yyyy-MM-dd"), "CIM Win32_BIOS"),
            new SpecValue("Windows install date", now.AddDays(-20).ToString("yyyy-MM-dd"), "CIM Win32_OperatingSystem"),
            new SpecValue("Critical errors (event log)", "0", "Windows Event Log"),
            new SpecValue("Signed drivers only", "Yes", "CIM Win32_PnPSignedDriver"),
        },
        "System information comes from Windows.",
        now);

    public static ComponentSnapshot Cpu() => new(
        "cpu",
        "CPU",
        "CPU",
        "cpu",
        "Test CPU",
        95d,
        new[] { new SpecValue("Package temperature", "55 C", "LibreHardwareMonitor") },
        "CPU health uses live temperature.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Ram(string actual) => new(
        "ram",
        "RAM",
        "RAM",
        "ram",
        actual,
        100d,
        new[] { new SpecValue("Total memory", actual, "CIM Win32_PhysicalMemory") },
        "RAM health is not measurable from a PC sensor.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Storage(string wear, string hours, double? health = 100d) => new(
        "storage",
        "Storage",
        "Storage",
        "storage",
        $"{wear}% wear",
        health,
        new[]
        {
            new SpecValue("Wear level", $"{wear}%", "SMART / NVMe reliability counters"),
            new SpecValue("SMART warnings", "No", "SMART / NVMe reliability counters"),
            new SpecValue("Health word", "Good", "SMART / NVMe reliability counters"),
            new SpecValue("Uncorrected errors", "0", "SMART / NVMe reliability counters"),
            new SpecValue("Power-on hours", hours, "SMART / NVMe reliability counters"),
        },
        "Drive health comes from the manufacturer wear counter.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Battery(int cycles, double? health = 90d) => new(
        "battery",
        "Battery",
        "Battery",
        "battery",
        "Test battery",
        health,
        new[]
        {
            new SpecValue("Design capacity", "50000 mWh", "Battery firmware"),
            new SpecValue("Full charge capacity", "45000 mWh", "Battery firmware"),
            new SpecValue("Cycle count", cycles.ToString(), "Battery firmware"),
        },
        "Battery health compares full charge capacity with design capacity.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Gpu() => new(
        "gpu",
        "GPU",
        "GPU",
        "gpu",
        "Test GPU",
        100d,
        new[] { new SpecValue("GPU temperature", "60 C", "LibreHardwareMonitor") },
        "GPU health uses live temperature.",
        DateTimeOffset.Now);

    public static ComponentSnapshot Network(string driverDate) => new(
        "network",
        "Network",
        "Network",
        "network",
        "Test adapter",
        100d,
        new[] { new SpecValue("Driver date", driverDate, "CIM Win32_PnPSignedDriver") },
        "Network health uses the adapter driver date.",
        DateTimeOffset.Now);
}
