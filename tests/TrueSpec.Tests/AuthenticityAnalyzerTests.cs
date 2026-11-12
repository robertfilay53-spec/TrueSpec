using System.Globalization;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

/// <summary>
/// U1 Authenticity engine (Blueprint 1): pure rules R1-R6 over a snapshot.
/// "Now" is the snapshot's CapturedAt, fixed at midnight so day boundaries
/// are exact. Every rule has a passing and a firing case, tolerance
/// boundaries, and missing-signal cases that must skip the check, never
/// produce a false positive.
/// </summary>
public class AuthenticityAnalyzerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);

    private static string Day(int daysAgo) =>
        Now.AddDays(-daysAgo).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static SystemSnapshot Snapshot(
        string? installDate = null,
        string? biosDate = null,
        string? earliestLogEntry = null,
        string? powerOnHours = null,
        string? dataWritten = null,
        string? cycleCount = null,
        string[]? hotfixDates = null)
    {
        var components = new List<ComponentSnapshot>();

        var systemSpecs = new List<SpecValue>();
        if (installDate is not null)
        {
            systemSpecs.Add(new SpecValue("Windows install date", installDate, "CIM Win32_OperatingSystem.InstallDate"));
        }

        if (biosDate is not null)
        {
            systemSpecs.Add(new SpecValue("BIOS date", biosDate, "CIM Win32_BIOS.ReleaseDate"));
        }

        if (earliestLogEntry is not null)
        {
            systemSpecs.Add(new SpecValue("Earliest event log entry", earliestLogEntry, "Windows Event Log (System)"));
        }

        if (systemSpecs.Count > 0)
        {
            components.Add(new ComponentSnapshot(
                "system", "System", "System", "system", "Test Laptop", null,
                systemSpecs, "System information comes from Windows.", Now));
        }

        var storageSpecs = new List<SpecValue>();
        if (powerOnHours is not null)
        {
            storageSpecs.Add(new SpecValue("Power-on hours", powerOnHours, "Get-StorageReliabilityCounter.PowerOnHours"));
        }

        if (dataWritten is not null)
        {
            storageSpecs.Add(new SpecValue("Data written", dataWritten, "SATA SMART attribute 241 (total LBAs written)"));
        }

        if (storageSpecs.Count > 0)
        {
            components.Add(new ComponentSnapshot(
                "storage", "Storage", "Storage", "storage", "Test drive", null,
                storageSpecs, "Drive health comes from the manufacturer wear counter.", Now));
        }

        if (cycleCount is not null)
        {
            components.Add(new ComponentSnapshot(
                "battery", "Battery", "Battery", "battery", "Test battery", null,
                new[] { new SpecValue("Cycle count", cycleCount, @"CIM root\WMI BatteryCycleCount") },
                "Battery health compares full charge capacity with design capacity.", Now));
        }

        if (hotfixDates is not null)
        {
            var specs = hotfixDates
                .Select((date, i) => new SpecValue(
                    $"Update - KB1000{i}",
                    date,
                    "CIM Win32_QuickFixEngineering"))
                .ToArray<SpecValue>();
            components.Add(new ComponentSnapshot(
                "updates", "Windows Update", "Security", "updates", "Test updates", null,
                specs, "Windows Update status from read-only sources.", Now));
        }

        return new SystemSnapshot(
            Guid.NewGuid(),
            Now,
            "Test Laptop",
            "Windows 11 Pro",
            components,
            ScoreResult.Empty,
            new BuyerCheckEvaluator().Evaluate(components, null));
    }

    private static AuthenticityCheck? FindCheck(AuthenticityResult result, string title) =>
        result.Checks.FirstOrDefault(c =>
            string.Equals(c.Title, title, StringComparison.Ordinal));

    private static void AssertSkipped(AuthenticityResult result, string title) =>
        Assert.Contains(result.SkippedChecks, s => s.StartsWith(title, StringComparison.Ordinal));

    private static void AssertNotSkipped(AuthenticityResult result, string title) =>
        Assert.DoesNotContain(result.SkippedChecks, s => s.StartsWith(title, StringComparison.Ordinal));

    // v1.3 integration: the component-list overload (used by the buyer-check
    // evaluator, which runs before the snapshot is built) is the same
    // analysis as the snapshot overload, not a second implementation.

    [Fact]
    public void Analyze_ComponentListOverload_MatchesSnapshotOverload()
    {
        var snapshot = Snapshot(
            installDate: Day(3),
            biosDate: Day(20),
            earliestLogEntry: Day(30),
            powerOnHours: "4120",
            dataWritten: "650.0 GB",
            cycleCount: "80",
            hotfixDates: new[] { Day(40) });

        var fromSnapshot = AuthenticityAnalyzer.Analyze(snapshot);
        var fromComponents = AuthenticityAnalyzer.Analyze(snapshot.Components, snapshot.CapturedAt);

        Assert.Equal(fromSnapshot.Verdict, fromComponents.Verdict);
        Assert.Equal(fromSnapshot.Summary, fromComponents.Summary);
        Assert.Equal(fromSnapshot.Checks.Count, fromComponents.Checks.Count);
        Assert.Equal(fromSnapshot.SkippedChecks.Count, fromComponents.SkippedChecks.Count);
        Assert.Equal(fromSnapshot.Signals.Count, fromComponents.Signals.Count);
    }

    // R1: SSD power-on hours vs Windows age (Strong, 72h tolerance).

    [Fact]
    public void R1_Passes_WhenHoursFitInsideWindowsAgePlusTolerance()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(20), powerOnHours: "100"));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R1Title));
        AssertNotSkipped(result, AuthenticityAnalyzer.R1Title);
    }

    [Fact]
    public void R1_Fires_WhenDriveIsFarOlderThanWindowsInstall()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(3), powerOnHours: "4120"));

        var check = FindCheck(result, AuthenticityAnalyzer.R1Title);
        Assert.NotNull(check);
        Assert.Equal(AuthenticitySeverity.Strong, check.Severity);
        Assert.Contains("4,120", check.EvidenceLine, StringComparison.Ordinal);
        Assert.Equal(AuthenticityResult.SignsOfPriorUse, result.Verdict);
    }

    [Fact]
    public void R1_Boundary_ExactlyAtTolerance_Passes()
    {
        // Install 3 days ago = 72h window + 72h tolerance = 144h allowed.
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(3), powerOnHours: "144"));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R1Title));
    }

    [Fact]
    public void R1_Boundary_OneHourOverTolerance_Fires()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(3), powerOnHours: "145"));

        Assert.NotNull(FindCheck(result, AuthenticityAnalyzer.R1Title));
    }

    [Fact]
    public void R1_Skipped_WhenNoPowerOnHoursSignal()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(3)));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R1Title));
        AssertSkipped(result, AuthenticityAnalyzer.R1Title);
        Assert.Equal(AuthenticityResult.NoSignsOfPriorUse, result.Verdict);
    }

    [Fact]
    public void R1_Skipped_WhenPowerOnHoursNotReported()
    {
        var result = AuthenticityAnalyzer.Analyze(
            Snapshot(installDate: Day(3), powerOnHours: SpecLabels.NotReportedByHardware));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R1Title));
        AssertSkipped(result, AuthenticityAnalyzer.R1Title);
    }

    // R2: total host writes vs Windows age (Watch, > 500 GB, install < 30 days).

    [Fact]
    public void R2_Fires_WhenFreshInstallHasHundredsOfGigabytesWritten()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(10), dataWritten: "650.0 GB"));

        var check = FindCheck(result, AuthenticityAnalyzer.R2Title);
        Assert.NotNull(check);
        Assert.Equal(AuthenticitySeverity.Watch, check.Severity);
        Assert.Equal(AuthenticityResult.SignsOfPriorUse, result.Verdict);
    }

    [Fact]
    public void R2_Passes_WhenWritesBelowThreshold()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(10), dataWritten: "400.0 GB"));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R2Title));
    }

    [Fact]
    public void R2_Boundary_Exactly500Gb_Passes()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(10), dataWritten: "500.0 GB"));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R2Title));
    }

    [Fact]
    public void R2_Passes_WhenInstallIsNotFresh()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(40), dataWritten: "800.0 GB"));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R2Title));
    }

    [Fact]
    public void R2_Boundary_InstallExactly30Days_Passes()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(30), dataWritten: "800.0 GB"));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R2Title));
    }

    [Fact]
    public void R2_Skipped_WhenNoDataWrittenSignal()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(10)));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R2Title));
        AssertSkipped(result, AuthenticityAnalyzer.R2Title);
    }

    [Fact]
    public void R2_Skipped_WhenDataWrittenNotReported()
    {
        // NVMe drives have no total-writes spec today: the check is skipped,
        // never guessed from a missing signal.
        var result = AuthenticityAnalyzer.Analyze(
            Snapshot(installDate: Day(10), dataWritten: SpecLabels.NotReportedByHardware));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R2Title));
        AssertSkipped(result, AuthenticityAnalyzer.R2Title);
    }

    // R3: battery cycles vs Windows age (Watch, > 50 cycles, install < 30 days).

    [Fact]
    public void R3_Fires_WhenFreshInstallHasManyCycles()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(10), cycleCount: "120"));

        var check = FindCheck(result, AuthenticityAnalyzer.R3Title);
        Assert.NotNull(check);
        Assert.Equal(AuthenticitySeverity.Watch, check.Severity);
        Assert.Equal(AuthenticityResult.SignsOfPriorUse, result.Verdict);
    }

    [Fact]
    public void R3_Passes_WhenCyclesBelowThreshold()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(10), cycleCount: "40"));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R3Title));
    }

    [Fact]
    public void R3_Boundary_Exactly50Cycles_Passes()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(10), cycleCount: "50"));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R3Title));
    }

    [Fact]
    public void R3_Boundary_51Cycles_Fires()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(10), cycleCount: "51"));

        Assert.NotNull(FindCheck(result, AuthenticityAnalyzer.R3Title));
    }

    [Fact]
    public void R3_Passes_WhenInstallIsNotFresh()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(40), cycleCount: "200"));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R3Title));
    }

    [Fact]
    public void R3_Skipped_WhenNoBatterySignal()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(10)));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R3Title));
        AssertSkipped(result, AuthenticityAnalyzer.R3Title);
    }

    // R4: BIOS date vs Windows age (Strong, impossible data).

    [Fact]
    public void R4_Fires_WhenBiosDateIsAfterInstallDate()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(20), biosDate: Day(5)));

        var check = FindCheck(result, AuthenticityAnalyzer.R4Title);
        Assert.NotNull(check);
        Assert.Equal(AuthenticitySeverity.Strong, check.Severity);
        Assert.Equal(AuthenticityResult.SignsOfPriorUse, result.Verdict);
    }

    [Fact]
    public void R4_Fires_MarksBiosSignalUnreliable()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(20), biosDate: Day(5)));

        var bios = result.Signals.FirstOrDefault(s =>
            string.Equals(s.Name, "BIOS date", StringComparison.Ordinal));
        Assert.NotNull(bios);
        Assert.False(bios.IsReliable);
    }

    [Fact]
    public void R4_Passes_WhenBiosDateIsBeforeInstallDate()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(20), biosDate: Day(400)));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R4Title));
        var bios = result.Signals.FirstOrDefault(s =>
            string.Equals(s.Name, "BIOS date", StringComparison.Ordinal));
        Assert.NotNull(bios);
        Assert.True(bios.IsReliable);
    }

    [Fact]
    public void R4_Skipped_WhenNoBiosDateSignal()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(20)));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R4Title));
        AssertSkipped(result, AuthenticityAnalyzer.R4Title);
    }

    // R5: earliest hotfix vs install date (Info).

    [Fact]
    public void R5_Fires_WhenEarliestHotfixPredatesInstall()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(
            installDate: Day(20),
            hotfixDates: new[] { Day(30), Day(10) }));

        var check = FindCheck(result, AuthenticityAnalyzer.R5Title);
        Assert.NotNull(check);
        Assert.Equal(AuthenticitySeverity.Info, check.Severity);
    }

    [Fact]
    public void R5_Passes_WhenAllHotfixesPostdateInstall()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(
            installDate: Day(20),
            hotfixDates: new[] { Day(10), Day(5) }));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R5Title));
    }

    [Fact]
    public void R5_Skipped_WhenNoHotfixSignal()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(20)));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R5Title));
        AssertSkipped(result, AuthenticityAnalyzer.R5Title);
    }

    // R6: event-log gap (Watch, earliest entry > 7 days after install).

    [Fact]
    public void R6_Fires_WhenEarliestLogEntryIsWeeksAfterInstall()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(20), earliestLogEntry: Day(5)));

        var check = FindCheck(result, AuthenticityAnalyzer.R6Title);
        Assert.NotNull(check);
        Assert.Equal(AuthenticitySeverity.Watch, check.Severity);
        Assert.Equal(AuthenticityResult.SignsOfPriorUse, result.Verdict);
    }

    [Fact]
    public void R6_Passes_WhenLogStartsSoonAfterInstall()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(20), earliestLogEntry: Day(15)));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R6Title));
    }

    [Fact]
    public void R6_Boundary_GapExactly7Days_Passes()
    {
        Assert.Equal(7, AuthenticityAnalyzer.LogGapDays);
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(20), earliestLogEntry: Day(13)));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R6Title));
    }

    [Fact]
    public void R6_Boundary_Gap8Days_Fires()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(20), earliestLogEntry: Day(12)));

        Assert.NotNull(FindCheck(result, AuthenticityAnalyzer.R6Title));
    }

    [Fact]
    public void R6_Skipped_WhenNoLogEntrySignal()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(20)));

        Assert.Null(FindCheck(result, AuthenticityAnalyzer.R6Title));
        AssertSkipped(result, AuthenticityAnalyzer.R6Title);
    }

    // Verdict and missing-signal behaviour.

    [Fact]
    public void Verdict_NoSigns_WhenEverySignalIsFreshAndPresent()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(
            installDate: Day(20),
            biosDate: Day(400),
            earliestLogEntry: Day(19),
            powerOnHours: "100",
            dataWritten: "40.0 GB",
            cycleCount: "5",
            hotfixDates: new[] { Day(10) }));

        Assert.Equal(AuthenticityResult.NoSignsOfPriorUse, result.Verdict);
        Assert.Empty(result.Checks);
        Assert.Empty(result.SkippedChecks);
    }

    [Fact]
    public void Verdict_NoSigns_WhenOnlyInfoCheckFires()
    {
        // R5 alone is evidence the install is not the machine's first, but
        // an Info finding does not change the verdict: a re-image is not
        // proof of prior use by a previous owner.
        var result = AuthenticityAnalyzer.Analyze(Snapshot(
            installDate: Day(20),
            hotfixDates: new[] { Day(30) }));

        Assert.NotNull(FindCheck(result, AuthenticityAnalyzer.R5Title));
        Assert.Equal(AuthenticityResult.NoSignsOfPriorUse, result.Verdict);
    }

    [Fact]
    public void Verdict_NeverFalsePositive_WhenNoSignalIsAvailable()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot());

        Assert.Empty(result.Checks);
        Assert.Equal(AuthenticityResult.NoSignsOfPriorUse, result.Verdict);
        Assert.Equal(6, result.SkippedChecks.Count);
    }

    [Fact]
    public void MissingInstallDate_SkipsEveryRule()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(
            biosDate: Day(400),
            powerOnHours: "4120",
            dataWritten: "900.0 GB",
            cycleCount: "300",
            hotfixDates: new[] { Day(30) },
            earliestLogEntry: Day(5)));

        Assert.Empty(result.Checks);
        Assert.Equal(AuthenticityResult.NoSignsOfPriorUse, result.Verdict);
        Assert.Equal(6, result.SkippedChecks.Count);
    }

    [Fact]
    public void Signals_CarryNameValueAndSource()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(
            installDate: Day(20),
            biosDate: Day(400),
            powerOnHours: "100"));

        var install = result.Signals.FirstOrDefault(s =>
            string.Equals(s.Name, "Windows install date", StringComparison.Ordinal));
        Assert.NotNull(install);
        Assert.Equal(Day(20), install.Value);
        Assert.Contains("Win32_OperatingSystem", install.Source, StringComparison.Ordinal);
        Assert.Equal(Now, install.CollectedAt);
    }

    [Fact]
    public void EvidenceLine_NeverAccusesTheSeller()
    {
        var result = AuthenticityAnalyzer.Analyze(Snapshot(installDate: Day(3), powerOnHours: "4120"));

        foreach (var check in result.Checks)
        {
            Assert.DoesNotContain("fraud", check.EvidenceLine, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lied", check.EvidenceLine, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("refurbished", check.EvidenceLine, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("fraud", result.Verdict, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lied", result.Verdict, StringComparison.OrdinalIgnoreCase);
    }
}
