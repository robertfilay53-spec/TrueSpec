using System.Globalization;
using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Collectors.Windows.Diagnostics;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: the collector reads the real System event log. On
/// Linux this file is excluded from the build (see TrueSpec.Tests.csproj),
/// like StorageCollectorTests. The date depends on the machine the test
/// runs on, so only its shape is asserted: a parseable yyyy-MM-dd date, or
/// SpecLabels.NotReportedByHardware when the log is empty or unreadable.
/// </summary>
public class SystemCollectorTests
{
    [Fact]
    public async Task SystemCollector_ReportsEarliestEventLogEntry()
    {
        var collector = new SystemCollector();

        Assert.Equal("system", collector.Id);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var collectTask = collector.CollectAsync(timeout.Token);
        var finished = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(60)));

        Assert.True(
            finished == collectTask,
            "SystemCollector did not finish within 60 seconds.");

        var snapshot = await collectTask;

        Assert.Equal("system", snapshot.Id);
        var spec = Assert.Single(
            snapshot.Specs,
            s => string.Equals(s.Label, "Earliest event log entry", StringComparison.Ordinal));
        Assert.Equal("Windows Event Log (System)", spec.Source);
        Assert.True(
            spec.Value == SpecLabels.NotReportedByHardware ||
            DateTimeOffset.TryParseExact(
                spec.Value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out _),
            $"Earliest event log entry was neither a yyyy-MM-dd date nor Not reported: '{spec.Value}'.");
    }

    [Fact]
    public void GroupTimeout_FitsInsidePerCollectorBudget()
    {
        // One slow read group must never eat the whole per-collector
        // budget: the groups run at the same time, so the collector
        // finishes in about the slowest group's time.
        Assert.True(
            SystemCollector.GroupTimeout < ScanTimeouts.Default.PerCollector,
            $"Group timeout {SystemCollector.GroupTimeout} must stay below the per-collector budget {ScanTimeouts.Default.PerCollector}.");
    }

    [Fact]
    public async Task RunWithTimeoutAsync_FastWork_ReturnsValue()
    {
        var result = await SystemCollector.RunWithTimeoutAsync(
            () => "done",
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.Equal("done", result);
    }

    [Fact]
    public async Task RunWithTimeoutAsync_SlowWork_ReturnsNullQuickly()
    {
        // The work sleeps 30 seconds on a pool thread; the helper must
        // give up after its 100 ms budget instead of waiting for it.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await SystemCollector.RunWithTimeoutAsync(
            () =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(30));
                return "never";
            },
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);
        stopwatch.Stop();

        Assert.Null(result);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Slow work was not cut off by its budget: took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task RunWithTimeoutAsync_FaultedWork_PropagatesFault()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SystemCollector.RunWithTimeoutAsync<string>(
                () => throw new InvalidOperationException("boom"),
                TimeSpan.FromSeconds(5),
                CancellationToken.None));
    }

    [Fact]
    public async Task RunWithTimeoutAsync_CancelledToken_ThrowsOperationCanceled()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            SystemCollector.RunWithTimeoutAsync(
                () => "never runs",
                TimeSpan.FromSeconds(5),
                cancelled.Token));
    }

    [Fact]
    public void BuildSnapshot_AllGroupsPresent_ReportsMeasuredValues()
    {
        var snapshot = SystemCollector.BuildSnapshot(
            FullCore(),
            new SystemCollector.TpmReading("2.0", "Yes"),
            Row(new Dictionary<string, object?> { ["VirtualizationBasedSecurityStatus"] = (uint)2 }),
            "On",
            "On",
            new CollectorSupport.DriverSignatureSummary(Total: 120, Unsigned: 2, Unknown: 0, Truncated: false),
            new WindowsEventLog.SystemLogSummary(
                CriticalErrors: 3,
                MemoryErrors: 1,
                EarliestEntry: new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal("Contoso ProBook 450", snapshot.Headline);
        Assert.Equal("Contoso", Spec(snapshot, SpecLabels.Manufacturer));
        Assert.Equal("ProBook 450", Spec(snapshot, SpecLabels.Model));
        Assert.Equal("2.0", Spec(snapshot, SpecLabels.TPMVersion));
        Assert.Equal("On", Spec(snapshot, SpecLabels.BitLocker));
        Assert.Equal("Running", Spec(snapshot, SpecLabels.VirtualizationBasedSecurity));
        Assert.Equal("120", Spec(snapshot, SpecLabels.InstalledDrivers));
        Assert.Equal("3", Spec(snapshot, SpecLabels.CriticalErrorsEventLog));
        Assert.Equal("2026-10-01", Spec(snapshot, SpecLabels.EarliestEventLogEntry));
    }

    [Fact]
    public void BuildSnapshot_MissingDriverGroup_LeavesDriverSpecsNotReported()
    {
        // A driver enumeration that timed out loses only the driver
        // specs: the identity, TPM and event log values stay measured.
        // This is the November 15, 2026 real-laptop finding: the slow
        // driver enumeration took the whole System page down with it.
        var snapshot = SystemCollector.BuildSnapshot(
            FullCore(),
            new SystemCollector.TpmReading("2.0", "Yes"),
            null,
            "On",
            "On",
            drivers: null,
            new WindowsEventLog.SystemLogSummary(
                CriticalErrors: 3,
                MemoryErrors: 1,
                EarliestEntry: new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal("Contoso ProBook 450", snapshot.Headline);
        Assert.Equal("Contoso", Spec(snapshot, SpecLabels.Manufacturer));
        Assert.Equal("2.0", Spec(snapshot, SpecLabels.TPMVersion));
        Assert.Equal("3", Spec(snapshot, SpecLabels.CriticalErrorsEventLog));
        Assert.Equal(SpecLabels.NotReportedByHardware, Spec(snapshot, SpecLabels.InstalledDrivers));
        Assert.Equal(SpecLabels.NotReportedByHardware, Spec(snapshot, SpecLabels.UnsignedDrivers));
        Assert.Equal(SpecLabels.NotReportedByHardware, Spec(snapshot, SpecLabels.SignedDriversOnly));
    }

    [Fact]
    public void BuildSnapshot_MissingEventLogGroup_LeavesEventLogSpecsNotReported()
    {
        // An event log walk that timed out loses only the three event
        // log specs: the identity, TPM, BitLocker and driver values
        // stay measured.
        var snapshot = SystemCollector.BuildSnapshot(
            FullCore(),
            new SystemCollector.TpmReading("2.0", "Yes"),
            Row(new Dictionary<string, object?> { ["VirtualizationBasedSecurityStatus"] = (uint)2 }),
            "On",
            "On",
            new CollectorSupport.DriverSignatureSummary(Total: 120, Unsigned: 2, Unknown: 0, Truncated: false),
            logSummary: null);

        Assert.Equal("Contoso ProBook 450", snapshot.Headline);
        Assert.Equal("Contoso", Spec(snapshot, SpecLabels.Manufacturer));
        Assert.Equal("2.0", Spec(snapshot, SpecLabels.TPMVersion));
        Assert.Equal("On", Spec(snapshot, SpecLabels.BitLocker));
        Assert.Equal("120", Spec(snapshot, SpecLabels.InstalledDrivers));
        Assert.Equal(SpecLabels.NotReportedByHardware, Spec(snapshot, SpecLabels.CriticalErrorsEventLog));
        Assert.Equal(SpecLabels.NotReportedByHardware, Spec(snapshot, SpecLabels.MemoryErrorsEventLog));
        Assert.Equal(SpecLabels.NotReportedByHardware, Spec(snapshot, SpecLabels.EarliestEventLogEntry));
    }

    [Fact]
    public void BuildSnapshot_MissingCoreGroup_ReportsNotAvailable()
    {
        var timedOut = SystemCollector.BuildSnapshot(
            core: null,
            tpm: null,
            deviceGuard: null,
            secureBoot: SpecLabels.NotReportedByHardware,
            bitLocker: null,
            drivers: null,
            logSummary: null);

        Assert.Equal("Not available", timedOut.Headline);
        var status = Assert.Single(timedOut.Specs);
        Assert.Equal(SpecLabels.Status, status.Label);
        Assert.Contains("Win32_ComputerSystem", status.Value, StringComparison.Ordinal);

        var emptyCore = SystemCollector.BuildSnapshot(
            new SystemCollector.CoreIdentity(null, null, null, null),
            tpm: null,
            deviceGuard: null,
            secureBoot: SpecLabels.NotReportedByHardware,
            bitLocker: null,
            drivers: null,
            logSummary: null);

        Assert.Equal("Not available", emptyCore.Headline);
    }

    private static SystemCollector.CoreIdentity FullCore() => new(
        Row(new Dictionary<string, object?>
        {
            ["CSName"] = "PROBOOK-450",
            ["Manufacturer"] = "Contoso",
            ["Model"] = "ProBook 450",
            ["SystemFamily"] = "ProBook",
            ["SystemSKUNumber"] = "SKU-1",
        }),
        Row(new Dictionary<string, object?>
        {
            ["SerialNumber"] = "SN-1",
            ["ReleaseDate"] = "20260110080000.000000+000",
            ["SMBIOSBIOSVersion"] = "1.2.3",
            ["SMBIOSMajorVersion"] = (uint)3,
            ["SMBIOSMinorVersion"] = (uint)2,
            ["Version"] = "1.2.3",
            ["Manufacturer"] = "Contoso",
        }),
        Row(new Dictionary<string, object?>
        {
            ["CSName"] = "PROBOOK-450",
            ["Caption"] = "Microsoft Windows 11 Pro",
            ["BuildNumber"] = "26100",
            ["InstallDate"] = "20260110080000.000000+000",
            ["LastBootUpTime"] = "20261114060000.000000+000",
            ["OSArchitecture"] = "64-bit",
            ["Version"] = "10.0.26100",
        }),
        Row(new Dictionary<string, object?>
        {
            ["ChassisTypes"] = new ushort[] { 10 },
            ["SerialNumber"] = "SN-1",
        }));

    private static WmiRow Row(Dictionary<string, object?> values) => new(values);

    private static string Spec(ComponentSnapshot snapshot, string label) =>
        Assert.Single(snapshot.Specs, s => string.Equals(s.Label, label, StringComparison.Ordinal)).Value;
}
