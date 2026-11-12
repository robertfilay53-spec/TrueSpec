using System.Globalization;
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
            Crash(now),
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

    /// <summary>
    /// A crash component with a readable but empty log: the collector always
    /// writes the earliest-entry spec when it succeeds, even when no crash
    /// class was recorded, so this fixture mirrors that output.
    /// </summary>
    public static ComponentSnapshot Crash(DateTimeOffset now) => new(
        "crash",
        "Crash history",
        "Diagnostics",
        "crash",
        "No crashes recorded in the last 90 days",
        null,
        new[]
        {
            new SpecValue("Earliest event log entry", now.AddDays(-20).ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
        },
        "No blue screens, hardware errors, unexpected shutdowns or disk errors were recorded in the System event log in the last 90 days.",
        now);

    /// <summary>
    /// Snapshot shaped for history/trend tests: battery health, cycle count,
    /// storage wear and power-on hours live in spec values (the way the real
    /// collectors write them), with optional drivers and updates components.
    /// A null spec value leaves that spec out entirely; null driver/update
    /// values leave the whole component out. Mirrors the factory in
    /// HistoryTrendTests, extended with the drivers and updates specs the
    /// v1.2 trend and comparer read.
    /// </summary>
    public static SystemSnapshot HistorySnapshot(
        double score,
        string? batteryHealth,
        string? cycles,
        string? wear,
        string? powerOnHours,
        DateTimeOffset capturedAt,
        string machineName = "Test Laptop",
        string? oldestDriverDate = null,
        string? driversOlderThan5 = null,
        string? lastUpdateInstall = null,
        bool measurableScore = true)
    {
        var batterySpecs = new List<SpecValue>();
        if (batteryHealth is not null)
        {
            batterySpecs.Add(new SpecValue("Health", batteryHealth, "Full charge capacity / design capacity"));
        }

        if (cycles is not null)
        {
            batterySpecs.Add(new SpecValue("Cycle count", cycles, "CIM root\\WMI BatteryCycleCount"));
        }

        var storageSpecs = new List<SpecValue>();
        if (wear is not null)
        {
            storageSpecs.Add(new SpecValue("Wear level", wear, "Get-StorageReliabilityCounter.Wear"));
        }

        if (powerOnHours is not null)
        {
            storageSpecs.Add(new SpecValue("Power-on hours", powerOnHours, "Get-StorageReliabilityCounter.PowerOnHours"));
        }

        var wearValue = wear is null
            ? 0d
            : double.Parse(wear.TrimEnd('%'), CultureInfo.InvariantCulture);
        var components = new List<ComponentSnapshot>
        {
            new(
                "battery", "Battery", "Battery", "battery", "Test battery", null,
                batterySpecs,
                "Battery health compares full charge capacity with design capacity.",
                capturedAt),
            new(
                "storage", "Storage", "Storage", "storage", "Test drive", 100d - wearValue,
                storageSpecs,
                "Drive health comes from the manufacturer wear counter.",
                capturedAt),
        };

        if (oldestDriverDate is not null || driversOlderThan5 is not null)
        {
            var driverSpecs = new List<SpecValue>();
            if (driversOlderThan5 is not null)
            {
                driverSpecs.Add(new SpecValue("Drivers older than 5 years", driversOlderThan5, "CIM Win32_PnPSignedDriver.DriverDate"));
            }

            if (oldestDriverDate is not null)
            {
                driverSpecs.Add(new SpecValue("Oldest driver date", oldestDriverDate, "CIM Win32_PnPSignedDriver.DriverDate"));
            }

            components.Add(new ComponentSnapshot(
                "drivers", "Drivers", "Security", "drivers", "Test drivers", null,
                driverSpecs,
                "Drivers are the small programs that let Windows talk to hardware.",
                capturedAt));
        }

        if (lastUpdateInstall is not null)
        {
            components.Add(new ComponentSnapshot(
                "updates", "Windows Update", "Security", "updates", "Test updates", null,
                new[] { new SpecValue("Last successful update install", lastUpdateInstall, "Registry WindowsUpdate Auto Update Results Install LastSuccessTime") },
                "Windows Update status from read-only sources.",
                capturedAt));
        }

        var scoreResult = measurableScore
            ? new ScoreResult(
                score,
                Verdict.UsedHealthy,
                new[] { new ComponentScore("storage", 100d - wearValue, 10d, 10d, 10d) },
                10d)
            : ScoreResult.Empty;
        return new SystemSnapshot(
            Guid.NewGuid(),
            capturedAt,
            machineName,
            "Windows 11 Pro",
            components,
            scoreResult,
            new BuyerCheckEvaluator().Evaluate(components, null));
    }
}
