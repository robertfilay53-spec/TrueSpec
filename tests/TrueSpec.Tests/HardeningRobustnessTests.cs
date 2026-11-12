using TrueSpec.Core;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Health;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Settings;

#if WINDOWS
using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Collectors.Windows.Sensors;
using TrueSpec.Collectors.Windows.Wmi;
#endif

namespace TrueSpec.Tests;

/// <summary>
/// Regression tests for the Nov 12, 2026 hardening-robustness pass:
/// the label contract, the thread-pool scan offload, the finite-number
/// guards, the health clamp, the scoring row name, and the small
/// bounded-input items. Tests use factories and [Theory] tables per the
/// testing-patterns skill, and assert behaviour, not implementation.
/// </summary>
public class SpecLabelAgreementTests
{
    // Factory: one component with specs built ONLY from SpecLabels
    // constants, the way the collectors now build them.
    private static ComponentSnapshot MakeComponent(
        string id, string category, double? health, params (string Label, string Value)[] specs) => new(
        id, id, category, id, "test", health,
        specs.Select(s => new SpecValue(s.Label, s.Value, "test")).ToArray(),
        "test", DateTimeOffset.Now);

    [Fact]
    public void SpecLabels_MatchTheExactStringsConsumersExpect()
    {
        // A renamed constant must fail loudly here instead of silently
        // breaking a health rule, a buyer check or a trend that matched
        // the old string. Each pair is (constant, the string the consumer
        // historically read). HistoryTrends.cs still holds its own literals
        // (a v1.2 file, not edited here); its labels are in this table too,
        // so a drift between the constants and the trends file is caught.
        var expected = new (string Constant, string Literal)[]
        {
            (SpecLabels.WearLevel, "Wear level"),
            (SpecLabels.SMARTWarnings, "SMART warnings"),
            (SpecLabels.HealthWord, "Health word"),
            (SpecLabels.UncorrectedErrors, "Uncorrected errors"),
            (SpecLabels.DesignCapacity, "Design capacity"),
            (SpecLabels.FullChargeCapacity, "Full charge capacity"),
            (SpecLabels.PackageTemperature, "Package temperature"),
            (SpecLabels.PackageTemperatureC, "Package temperature (C)"),
            (SpecLabels.GPUTemperature, "GPU temperature"),
            (SpecLabels.GPUTemperatureC, "GPU temperature (C)"),
            (SpecLabels.ThermalThrottleFlag, "Thermal throttle flag"),
            (SpecLabels.MemoryErrors, "Memory errors"),
            (SpecLabels.DriverDate, "Driver date"),
            (SpecLabels.InstalledDrivers, "Installed drivers"),
            (SpecLabels.DriversOlderThan5Years, "Drivers older than 5 years"),
            (SpecLabels.CycleCount, "Cycle count"),
            (SpecLabels.PowerOnHours, "Power-on hours"),
            (SpecLabels.TotalMemory, "Total memory"),
            (SpecLabels.BIOSDate, "BIOS date"),
            (SpecLabels.WindowsInstallDate, "Windows install date"),
            (SpecLabels.CriticalErrorsEventLog, "Critical errors (event log)"),
            (SpecLabels.SignedDriversOnly, "Signed drivers only"),
            (SpecLabels.Health, "Health"),
            (SpecLabels.OperatingSystem, "Operating system"),
            (SpecLabels.WindowsEdition, "Windows edition"),
            (SpecLabels.Status, "Status"),
        };

        Assert.All(expected, pair => Assert.Equal(pair.Literal, pair.Constant));
    }

    [Fact]
    public void HealthRules_ReadSpecsBuiltFromSpecLabels()
    {
        var storage = MakeComponent("storage", "Storage", null,
            (SpecLabels.WearLevel, "0%"),
            (SpecLabels.SMARTWarnings, "No"),
            (SpecLabels.HealthWord, "Healthy"),
            (SpecLabels.UncorrectedErrors, "0"));
        var battery = MakeComponent("battery", "Battery", null,
            (SpecLabels.DesignCapacity, "50000 mWh"),
            (SpecLabels.FullChargeCapacity, "45000 mWh"));
        var network = MakeComponent("network", "Network", null,
            (SpecLabels.DriverDate, DateTimeOffset.UtcNow.AddDays(-20).ToString("yyyy-MM-dd")));
        var drivers = MakeComponent("drivers", "Security", null,
            (SpecLabels.InstalledDrivers, "100"),
            (SpecLabels.DriversOlderThan5Years, "0"));

        Assert.Equal(100d, ComponentHealthRules.Evaluate(storage));
        Assert.Equal(90d, ComponentHealthRules.Evaluate(battery));
        Assert.Equal(100d, ComponentHealthRules.Evaluate(network));
        Assert.Equal(100d, ComponentHealthRules.Evaluate(drivers));
    }

    [Fact]
    public void BuyerChecks_ReadSpecsBuiltFromSpecLabels()
    {
        var now = DateTimeOffset.UtcNow;
        var components = new[]
        {
            MakeComponent("storage", "Storage", 100d,
                (SpecLabels.WearLevel, "0%"),
                (SpecLabels.SMARTWarnings, "No"),
                (SpecLabels.PowerOnHours, "0")),
            MakeComponent("battery", "Battery", 90d,
                (SpecLabels.CycleCount, "2")),
            MakeComponent("ram", "RAM", null,
                (SpecLabels.TotalMemory, "16.0 GB")),
            MakeComponent("system", "System", null,
                (SpecLabels.BIOSDate, now.AddDays(-20).ToString("yyyy-MM-dd")),
                (SpecLabels.WindowsInstallDate, now.AddDays(-20).ToString("yyyy-MM-dd")),
                (SpecLabels.CriticalErrorsEventLog, "0"),
                (SpecLabels.SignedDriversOnly, "Yes")),
            // An empty crash log: the collector always writes the
            // earliest-entry spec when it succeeds, even with no crashes.
            MakeComponent("crash", "Crash history", null,
                (SpecLabels.EarliestEventLogEntry, now.AddDays(-20).ToString("yyyy-MM-dd"))),
        };

        var result = new BuyerCheckEvaluator().Evaluate(components, new BuyerCheckContext("16 GB"));

        Assert.All(result.Checks, check => Assert.Equal(BuyerCheckState.Pass, check.State));
        Assert.Equal(BuyerCheckState.Pass, result.OverallState);
    }

    [Fact]
    public void Trends_ReadSpecsBuiltFromSpecLabels()
    {
        var before = SnapshotWithLabels(90d, "90.0%", "2", "2%", "100", DateTimeOffset.UtcNow.AddDays(-2));
        var after = SnapshotWithLabels(85d, "85.5%", "3", "5%", "250", DateTimeOffset.UtcNow.AddDays(-1));

        var trend = HistoryTrendAnalyzer.Analyze(new[] { after, before });

        Assert.Equal(2, trend.SnapshotCount);
        Assert.Equal(-5d, trend.ScoreDelta);
        Assert.Equal(-4.5d, trend.BatteryHealthDelta);
        Assert.Equal(3d, trend.StorageWearDelta);
    }

    // Same shape as HistoryTrendTests.SnapshotWith, but every label comes
    // from SpecLabels: the trend reader and the label constants must agree.
    private static SystemSnapshot SnapshotWithLabels(
        double score, string batteryHealth, string cycles, string wear, string hours, DateTimeOffset capturedAt)
    {
        var components = new[]
        {
            new ComponentSnapshot("battery", "Battery", "Battery", "battery", "Test battery", null,
                new[]
                {
                    new SpecValue(SpecLabels.Health, batteryHealth, "Battery firmware"),
                    new SpecValue(SpecLabels.CycleCount, cycles, "Battery firmware"),
                },
                "Battery health compares full charge capacity with design capacity.", capturedAt),
            new ComponentSnapshot("storage", "Storage", "Storage", "storage", "Test drive", null,
                new[]
                {
                    new SpecValue(SpecLabels.WearLevel, wear, "SMART / NVMe reliability counters"),
                    new SpecValue(SpecLabels.PowerOnHours, hours, "SMART / NVMe reliability counters"),
                },
                "Drive health comes from the manufacturer wear counter.", capturedAt),
        };

        return new SystemSnapshot(
            Guid.NewGuid(), capturedAt, "Test Laptop", "Windows 11 Pro",
            components,
            new ScoreResult(score, Verdict.LikeNew, new[] { new ComponentScore("Storage", score, 100d, 1d, score) }, 100d),
            new BuyerCheckResult(Array.Empty<BuyerCheck>()));
    }
}

public class ScanThreadPoolTests
{
    /// <summary>
    /// Blocks the calling thread for 3 seconds before returning a task,
    /// like a synchronous WMI call that never yields. Before the offload
    /// this blocked the scan itself and the per-collector timeout could
    /// never fire; with Task.Run the timeout race is real.
    /// </summary>
    private sealed class BlockingCollector : ICollector
    {
        public string Id => "blocking";
        public string Name => "Blocking";
        public string Category => "Test";
        public string IconKey => "system";
        public TimeSpan RefreshInterval => TimeSpan.FromSeconds(2);
        public bool RequiresAdministrator => false;

        public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
        {
            Thread.Sleep(TimeSpan.FromSeconds(3));
            return Task.FromResult(TestSnapshots.System(DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public async Task BlockingCollector_TimesOutInsteadOfBlockingTheScan()
    {
        var service = MachineScanService.Create(new ICollector[] { new BlockingCollector() });
        var timeouts = new ScanTimeouts(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(30));

        var started = DateTimeOffset.UtcNow;
        var snapshot = await service.ScanAsync(null, CancellationToken.None, timeouts, null);
        var elapsed = DateTimeOffset.UtcNow - started;

        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"Scan took {elapsed}; the blocking collector defeated the timeout.");
        var component = snapshot.Find("blocking")!;
        Assert.Equal("Not available", component.Headline);
        Assert.Null(component.HealthPercent);
    }

    [Fact]
    public async Task Scan_KeepsSequentialOrderAfterOffload()
    {
        var order = new List<string>();
        var collectors = new[] { "one", "two", "three" }.Select(id => new OrderCollector(id, order)).ToArray();
        var service = MachineScanService.Create(collectors);

        await service.ScanAsync();

        Assert.Equal(new[] { "one", "two", "three" }, order);
    }

    private sealed class OrderCollector : ICollector
    {
        private readonly List<string> _order;
        public OrderCollector(string id, List<string> order) { Id = id; _order = order; }
        public string Id { get; }
        public string Name => Id;
        public string Category => "Test";
        public string IconKey => "system";
        public TimeSpan RefreshInterval => TimeSpan.FromSeconds(2);
        public bool RequiresAdministrator => false;

        public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
        {
            _order.Add(Id);
            return Task.FromResult(TestSnapshots.System(DateTimeOffset.UtcNow));
        }
    }
}

public class SpecTextFiniteTests
{
    [Theory]
    [InlineData("Infinity")]
    [InlineData("+Infinity")]
    [InlineData("-Infinity")]
    [InlineData("Infinity%")]
    [InlineData("NaN")]
    [InlineData("NaN%")]
    public void TryParseLeadingNumber_RejectsNonFiniteValues(string text)
    {
        Assert.False(SpecText.TryParseLeadingNumber(text, out _));
    }

    [Theory]
    [InlineData("85.5%", 85.5d)]
    [InlineData("100", 100d)]
    [InlineData("0", 0d)]
    [InlineData("-12.5 C", -12.5d)]
    public void TryParseLeadingNumber_StillParsesFiniteValues(string text, double expected)
    {
        Assert.True(SpecText.TryParseLeadingNumber(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("Infinity GB")]
    [InlineData("NaN GB")]
    [InlineData("-Infinity TB")]
    public void TryParseMemorySizeBytes_RejectsNonFiniteValues(string text)
    {
        Assert.False(SpecText.TryParseMemorySizeBytes(text, out _));
    }

    [Fact]
    public void FromHealth_MapsInfinityToNotMeasurable()
    {
        Assert.Equal(HealthStatus.NotMeasurable, HealthStatusExtensions.FromHealth(double.PositiveInfinity));
        Assert.Equal(HealthStatus.NotMeasurable, HealthStatusExtensions.FromHealth(double.NegativeInfinity));
        Assert.Equal(HealthStatus.NotMeasurable, HealthStatusExtensions.FromHealth(double.NaN));
        Assert.Equal(HealthStatus.Healthy, HealthStatusExtensions.FromHealth(100d));
    }
}

public class ComponentHealthClampTests
{
    private static ComponentSnapshot MakeComponent(double? health) => new(
        "test", "Test", "Test", "test", "test", health,
        Array.Empty<SpecValue>(), "test", DateTimeOffset.Now);

    [Theory]
    [InlineData(double.PositiveInfinity, null)]
    [InlineData(double.NegativeInfinity, null)]
    [InlineData(double.NaN, null)]
    public void NonFiniteHealth_BecomesNoMeasurement(double input, double? expected)
    {
        var component = MakeComponent(input);

        Assert.Equal(expected, component.HealthPercent);
        Assert.Equal(HealthStatus.NotMeasurable, component.Status);
    }

    [Theory]
    [InlineData(150d, 100d)]
    [InlineData(-25d, 0d)]
    [InlineData(100d, 100d)]
    [InlineData(0d, 0d)]
    [InlineData(55.5d, 55.5d)]
    public void OutOfRangeHealth_IsClamped(double input, double expected)
    {
        Assert.Equal(expected, MakeComponent(input).HealthPercent);
    }
}

public class ScoringDuplicateLabelTests
{
    private static ComponentSnapshot MakeComponent(string id, string name, string category, double? health) => new(
        id, name, category, id, "test", health,
        Array.Empty<SpecValue>(), "test", DateTimeOffset.Now);

    [Fact]
    public void DuplicateCategoryRow_IsNamedAfterTheWorstComponent()
    {
        var engine = new ScoringEngine();
        var healthy = MakeComponent("storage-a", "Healthy Drive", "Storage", 100d);
        var dying = MakeComponent("storage-b", "Dying Drive", "Storage", 20d);

        var result = engine.Score(new[] { healthy, dying });

        var row = Assert.Single(result.Components);
        Assert.Equal("Dying Drive", row.Component);
        Assert.Equal(20d, row.HealthPercent);
    }

    [Fact]
    public void DuplicateCategoryRow_NamesWorstRegardlessOfOrder()
    {
        var engine = new ScoringEngine();
        var dying = MakeComponent("storage-b", "Dying Drive", "Storage", 20d);
        var healthy = MakeComponent("storage-a", "Healthy Drive", "Storage", 100d);

        var result = engine.Score(new[] { dying, healthy });

        var row = Assert.Single(result.Components);
        Assert.Equal("Dying Drive", row.Component);
        Assert.Equal(20d, row.HealthPercent);
    }
}

public class SettingsSizeCapTests
{
    private static string TempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact]
    public void Load_OversizedFile_FallsBackToDefaults()
    {
        var directory = TempDirectory();
        var filePath = Path.Combine(directory, "settings.json");

        try
        {
            // A valid JSON body padded far past the 64 KB cap.
            var json = "{\"isDarkTheme\":false,\"refreshIntervalSeconds\":5,"
                + "\"exportFolder\":\"" + new string('x', 70_000) + "\"}";
            File.WriteAllText(filePath, json);

            var loaded = new AppSettingsStore(filePath).Load();

            Assert.Equal(AppSettings.Default, loaded);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_FileAtCap_LoadsNormally()
    {
        var directory = TempDirectory();
        var filePath = Path.Combine(directory, "settings.json");

        try
        {
            var store = new AppSettingsStore(filePath);
            Assert.True(store.Save(new AppSettings { IsDarkTheme = false, RefreshIntervalSeconds = 5 }));
            Assert.True(new FileInfo(filePath).Length <= AppSettingsStore.MaxFileBytes);
            Assert.Equal(5, store.Load().RefreshIntervalSeconds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_WritesSchemaVersion_AndLoadNormalizesUnknownVersions()
    {
        var directory = TempDirectory();
        var filePath = Path.Combine(directory, "settings.json");

        try
        {
            var store = new AppSettingsStore(filePath);
            Assert.True(store.Save(new AppSettings { RefreshIntervalSeconds = 5 }));

            var saved = File.ReadAllText(filePath);
            Assert.Contains("\"schemaVersion\": 1", saved);

            // A file from a newer schema still loads its known fields.
            File.WriteAllText(filePath, "{\"isDarkTheme\":false,\"refreshIntervalSeconds\":7,\"schemaVersion\":99}");
            var loaded = store.Load();
            Assert.Equal(7, loaded.RefreshIntervalSeconds);
            Assert.Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void OldFileWithoutSchemaVersion_LoadsAsVersionOne()
    {
        var directory = TempDirectory();
        var filePath = Path.Combine(directory, "settings.json");

        try
        {
            File.WriteAllText(filePath, "{\"isDarkTheme\":false,\"refreshIntervalSeconds\":3}");
            var loaded = new AppSettingsStore(filePath).Load();

            Assert.Equal(3, loaded.RefreshIntervalSeconds);
            Assert.Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

public class BenchmarkFreeSpaceTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(1024L)]
    [InlineData(64L * 1024L * 1024L - 1L)]
    public void FreeSpaceFailure_BelowTestSize_ExplainsTheShortfall(long available)
    {
        var message = BenchmarkService.FreeSpaceFailure(available);

        Assert.NotNull(message);
        Assert.Contains("64 MB", message, StringComparison.Ordinal);
        Assert.Contains("free some space", message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(64L * 1024L * 1024L)]
    [InlineData(64L * 1024L * 1024L + 1L)]
    [InlineData(long.MaxValue)]
    public void FreeSpaceFailure_AtOrAboveTestSize_AllowsTheRun(long available)
    {
        Assert.Null(BenchmarkService.FreeSpaceFailure(available));
    }
}

public class StartupCommandCapTests
{
    [Fact]
    public void CleanCommand_CapsLongCommandsWithVisibleEllipsis()
    {
        var command = @"C:\Tools\" + new string('a', 600) + ".exe";

        var cleaned = StartupProgramParser.CleanCommand(command);

        Assert.NotNull(cleaned);
        Assert.Equal(StartupProgramParser.MaxCommandLength, cleaned!.Length);
        Assert.EndsWith("...", cleaned, StringComparison.Ordinal);
        Assert.StartsWith(@"C:\Tools\", cleaned, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(511)]
    [InlineData(512)]
    public void CleanCommand_AtOrUnderCap_IsUntouched(int length)
    {
        var command = new string('x', length);

        Assert.Equal(command, StartupProgramParser.CleanCommand(command));
    }

    [Fact]
    public void CleanCommand_ShortCommand_IsUntouched()
    {
        Assert.Equal(@"C:\Tools\app.exe", StartupProgramParser.CleanCommand(@"C:\Tools\app.exe"));
    }
}

#if WINDOWS
/// <summary>
/// Windows-leg only: the collectors, WMI and the sensor monitor only exist
/// on the net10.0-windows target. Guarded with #if WINDOWS so the net10.0
/// leg still compiles (the test project excludes no files for this class).
/// </summary>
public class WindowsHardeningTests
{
    [Fact]
    public void CollectorSupportSpec_CapsLongValuesWithVisibleEllipsis()
    {
        var value = new string('a', 600);

        var spec = CollectorSupport.Spec(SpecLabels.Model, value, "test");

        Assert.Equal(CollectorSupport.MaxSpecValueLength, spec.Value.Length);
        Assert.EndsWith("...", spec.Value, StringComparison.Ordinal);
        Assert.StartsWith(new string('a', 10), spec.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(511)]
    [InlineData(512)]
    public void CollectorSupportSpec_AtOrUnderCap_IsUntouched(int length)
    {
        var value = new string('b', length);

        Assert.Equal(value, CollectorSupport.Spec(SpecLabels.Model, value, "test").Value);
    }

    [Fact]
    public void WmiQuery_RowCapBoundsResults()
    {
        // Win32_Process is the chattiest everyday class: the query must
        // return bounded rows and never hang, whatever the machine runs.
        var rows = Wmi.Query("root\\cimv2", "SELECT Name FROM Win32_Process", out var error);

        Assert.True(rows.Count <= Wmi.MaxRows, $"Query returned {rows.Count} rows, over the {Wmi.MaxRows} cap.");
        Assert.Equal(1000, Wmi.MaxRows);
        Assert.True(error is null || error == Wmi.TruncatedError, $"Unexpected error: {error}");
    }

    [Fact]
    public void NetworkCollectorBuildSpecs_EmitsContractLabels()
    {
        var reading = new NetworkCollector.NetworkReading(
            WifiName: "Test Wi-Fi",
            WifiVendor: "Test Vendor",
            WifiDriverVersion: "1.2.3",
            WifiDriverDate: "2024-01-15",
            WifiSpeedBitsPerSecond: 866_000_000L,
            WifiMac: "AA:BB:CC:DD:EE:FF",
            WifiIp: "192.168.1.10",
            WifiGateway: "192.168.1.1",
            WifiDns: "8.8.8.8",
            Standards: "802.11ax (Wi-Fi 6/6E)",
            Signal: "80%",
            Band: "5 GHz",
            Channel: "36",
            RadioType: "802.11ax",
            ReceiveRate: "866",
            TransmitRate: "866",
            Ssid: "TestNet",
            EthernetName: "Test Ethernet",
            EthernetSpeedBitsPerSecond: 1_000_000_000L,
            EthernetMac: "11:22:33:44:55:66",
            EthernetDriverDate: "2023-06-01",
            EthernetIp: "192.168.1.11",
            BluetoothCount: 2);

        var specs = NetworkCollector.BuildSpecs(reading, Array.Empty<NetworkCollector.InterfaceData>());

        var labels = specs.Select(s => s.Label).ToList();
        foreach (var expected in new[]
        {
            SpecLabels.WiFiAdapter, SpecLabels.WiFiVendor, SpecLabels.Standards,
            SpecLabels.DriverVersion, SpecLabels.DriverDate, SpecLabels.LinkSpeed,
            SpecLabels.Signal, SpecLabels.Band, SpecLabels.Channel, SpecLabels.RadioType,
            SpecLabels.ReceiveRate, SpecLabels.TransmitRate, SpecLabels.SSID,
            SpecLabels.MACAddress, SpecLabels.IPAddress, SpecLabels.Gateway,
            SpecLabels.DNSServers, SpecLabels.HardwareManufactureDate,
            SpecLabels.EthernetAdapter, SpecLabels.EthernetLinkSpeed, SpecLabels.EthernetStatus,
            SpecLabels.EthernetMACAddress, SpecLabels.EthernetDriverDate,
            SpecLabels.EthernetIPAddress, SpecLabels.EthernetGateway, SpecLabels.EthernetDNSServers,
            SpecLabels.BluetoothDevices, SpecLabels.BluetoothVersion,
        })
        {
            Assert.Contains(expected, labels);
        }

        Assert.Equal("Test Wi-Fi", SpecText.Value(specs, SpecLabels.WiFiAdapter));
        Assert.Equal("2024-01-15", SpecText.Value(specs, SpecLabels.DriverDate));
    }

    [Fact]
    public async Task SensorMonitor_ReadTimeoutIsBounded_AndDisposedMonitorAnswersFast()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), LibreHardwareMonitorSensorMonitor.ReadTimeout);

        var monitor = new LibreHardwareMonitorSensorMonitor();
        monitor.Dispose();

        var started = DateTimeOffset.UtcNow;
        var snapshot = await monitor.ReadAsync();
        var elapsed = DateTimeOffset.UtcNow - started;

        Assert.False(snapshot.IsAvailable);
        Assert.True(elapsed < TimeSpan.FromSeconds(5), $"Disposed read took {elapsed}.");
    }
}
#endif
