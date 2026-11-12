using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

public class BuyerCheckEvaluatorTests
{
    [Fact]
    public void HealthyMachine_WithAdvertisedRam_PassesEveryBuyerCheck()
    {
        var snapshot = TestSnapshots.Healthy();

        Assert.Equal(BuyerCheckState.Pass, snapshot.BuyerChecks.OverallState);
        Assert.All(snapshot.BuyerChecks.Checks, check =>
            Assert.Equal(BuyerCheckState.Pass, check.State));
    }

    [Fact]
    public void MissingReadings_AreNotApplicableNotFailures()
    {
        var result = new BuyerCheckEvaluator().Evaluate(Array.Empty<ComponentSnapshot>());

        Assert.NotEmpty(result.Checks);
        Assert.All(result.Checks, check =>
            Assert.Equal(BuyerCheckState.NotApplicable, check.State));
        Assert.Equal(BuyerCheckState.NotApplicable, result.OverallState);
        Assert.Equal("Not enough data for a verdict", result.BannerText);
    }

    [Fact]
    public void OnePassedCheckAmongMissingReadings_IsAPassNotNotApplicable()
    {
        // Only the RAM check has data and it passes; the rest are missing.
        // One real pass is enough for an overall Pass.
        var result = new BuyerCheckEvaluator().Evaluate(
            new[] { TestSnapshots.Ram("16 GB") },
            new BuyerCheckContext("16 GB"));

        Assert.Equal(BuyerCheckState.Pass, result.Checks.Single(c => c.Id == "ram-size").State);
        Assert.Equal(BuyerCheckState.Pass, result.OverallState);
    }

    [Fact]
    public void UsedDriveAndBattery_FailTheNewLaptopChecks()
    {
        var components = new[]
        {
            TestSnapshots.Storage("9", "120"),
            TestSnapshots.Battery(40),
            TestSnapshots.Ram("16 GB"),
        };

        var result = new BuyerCheckEvaluator().Evaluate(components);

        Assert.Equal(BuyerCheckState.Fail, result.Checks.Single(c => c.Id == "ssd-wear").State);
        Assert.Equal(BuyerCheckState.Fail, result.Checks.Single(c => c.Id == "power-on-hours").State);
        Assert.Equal(BuyerCheckState.Fail, result.Checks.Single(c => c.Id == "battery-cycles").State);
    }

    [Theory]
    [InlineData("16 GB", "16 GB", BuyerCheckState.Pass)]
    [InlineData("16 GB", "32 GB", BuyerCheckState.Fail)]
    [InlineData("16 GB", "16GB", BuyerCheckState.Pass)]
    [InlineData("16.0 GB", "16 GB", BuyerCheckState.Pass)]
    [InlineData("16.0 GB", "16GB", BuyerCheckState.Pass)]
    [InlineData("16 GB", "16.0 GB", BuyerCheckState.Pass)]
    [InlineData("16 GiB", "16 GB", BuyerCheckState.Pass)]
    [InlineData("8 GB", "16 GB", BuyerCheckState.Fail)]
    [InlineData("16.0 GB", "15 GB", BuyerCheckState.Fail)]
    [InlineData("16.0 GB", "15.9 GB", BuyerCheckState.Pass)]
    public void AdvertisedRamComparison_FlagsMismatch(
        string actual,
        string advertised,
        BuyerCheckState expected)
    {
        var result = new BuyerCheckEvaluator().Evaluate(
            new[] { TestSnapshots.Ram(actual) },
            new BuyerCheckContext(advertised));

        var check = Assert.Single(result.Checks, c => c.Id == "ram-size");
        Assert.Equal(expected, check.State);
    }

    [Fact]
    public void DuplicateComponentIds_DoNotCrashTheScan()
    {
        var result = new BuyerCheckEvaluator().Evaluate(
            new[] { TestSnapshots.Ram("16 GB"), TestSnapshots.Ram("16 GB") },
            new BuyerCheckContext("16 GB"));

        Assert.Equal(BuyerCheckState.Pass, result.Checks.Single(c => c.Id == "ram-size").State);
    }

    [Fact]
    public void UnparseableAdvertisedRam_IsNotApplicableNotAFail()
    {
        var result = new BuyerCheckEvaluator().Evaluate(
            new[] { TestSnapshots.Ram("16.0 GB") },
            new BuyerCheckContext("lots"));

        Assert.Equal(BuyerCheckState.NotApplicable, result.Checks.Single(c => c.Id == "ram-size").State);
    }

    // ---- v1.3 integration: authenticity (U1) and crash (U3) buyer checks --

    private static ComponentSnapshot SystemWithDates(DateTimeOffset install, DateTimeOffset bios, string criticalErrors = "0") => new(
        "system",
        "System",
        "System",
        "system",
        "Test Laptop",
        null,
        new[]
        {
            new SpecValue("Operating system", "Windows 11 Pro", "CIM Win32_OperatingSystem"),
            new SpecValue("BIOS date", bios.ToString("yyyy-MM-dd"), "CIM Win32_BIOS"),
            new SpecValue("Windows install date", install.ToString("yyyy-MM-dd"), "CIM Win32_OperatingSystem"),
            new SpecValue("Critical errors (event log)", criticalErrors, "Windows Event Log"),
            new SpecValue("Signed drivers only", "Yes", "CIM Win32_PnPSignedDriver"),
        },
        "System information comes from Windows.",
        DateTimeOffset.UtcNow);

    private static ComponentSnapshot StorageWithWrites(string wear, string hours, string written) => new(
        "storage",
        "Storage",
        "Storage",
        "storage",
        $"{wear}% wear",
        100d,
        new[]
        {
            new SpecValue("Wear level", $"{wear}%", "SMART / NVMe reliability counters"),
            new SpecValue("SMART warnings", "No", "SMART / NVMe reliability counters"),
            new SpecValue("Power-on hours", hours, "SMART / NVMe reliability counters"),
            new SpecValue("Data written", written, "SATA SMART attribute 241 (total LBAs written)"),
        },
        "Drive health comes from the manufacturer wear counter.",
        DateTimeOffset.UtcNow);

    private static ComponentSnapshot CrashWithBugCheck(DateTimeOffset mostRecent, int count90d) => new(
        "crash",
        "Crash history",
        "Diagnostics",
        "crash",
        $"{count90d} blue screen(s) in the last 90 days",
        null,
        new[]
        {
            new SpecValue("Blue screens (last 90 days)", count90d.ToString(), "Windows Event Log (System)"),
            new SpecValue("Blue screens - first recorded", mostRecent.AddDays(-40).ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
            new SpecValue("Blue screens - most recent", mostRecent.ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
            new SpecValue("Earliest event log entry", mostRecent.AddDays(-100).ToString("yyyy-MM-dd"), "Windows Event Log (System)"),
        },
        "Test component.",
        DateTimeOffset.UtcNow);

    /// <summary>Every ordinary check passes, with an empty crash log.</summary>
    private static ComponentSnapshot[] AllPassBase(DateTimeOffset now) => new[]
    {
        SystemWithDates(now.AddDays(-20), now.AddDays(-20)),
        TestSnapshots.Storage("0", "0"),
        TestSnapshots.Battery(2),
        TestSnapshots.Ram("16 GB"),
        TestSnapshots.Crash(now),
    };

    [Fact]
    public void HealthyMachine_AuthenticityAndCrashChecksPassToo()
    {
        var result = TestSnapshots.Healthy().BuyerChecks;

        Assert.Equal(BuyerCheckState.Pass, result.Checks.Single(c => c.Id == "authenticity").State);
        Assert.Equal(BuyerCheckState.Pass, result.Checks.Single(c => c.Id == "recent-crashes").State);
    }

    [Fact]
    public void Authenticity_StrongCheck_FailsTheRowAndDropsTheBannerOneLevel()
    {
        var now = DateTimeOffset.UtcNow;
        var components = AllPassBase(now).ToList();
        // R4: BIOS date one day after the Windows install date.
        components[0] = SystemWithDates(now.AddDays(-20), now.AddDays(-19));

        var result = new BuyerCheckEvaluator().Evaluate(components, new BuyerCheckContext("16 GB"));

        var check = result.Checks.Single(c => c.Id == "authenticity");
        Assert.Equal(BuyerCheckState.Fail, check.State);
        Assert.Contains("BIOS date vs Windows install date", check.Title, StringComparison.Ordinal);
        Assert.Equal(BuyerCheckState.Warning, result.OverallState);
        Assert.Equal("WARNING - Signs of prior use (BIOS date vs Windows install date)", result.BannerText);
    }

    [Fact]
    public void Authenticity_WatchOnly_WarnsTheRowButDoesNotDropTheBanner()
    {
        var now = DateTimeOffset.UtcNow;
        var components = new[]
        {
            SystemWithDates(now.AddDays(-10), now.AddDays(-10)),
            // R2: 650 GB written on a 10-day-old install.
            StorageWithWrites("0", "0", "650.0 GB"),
            TestSnapshots.Battery(2),
            TestSnapshots.Ram("16 GB"),
            TestSnapshots.Crash(now),
        };

        var result = new BuyerCheckEvaluator().Evaluate(components, new BuyerCheckContext("16 GB"));

        Assert.Equal(BuyerCheckState.Warning, result.Checks.Single(c => c.Id == "authenticity").State);
        Assert.Equal(BuyerCheckState.Pass, result.OverallState);
        Assert.Equal("PASS - this machine matches a new laptop", result.BannerText);
    }

    [Fact]
    public void Authenticity_AllRulesSkipped_IsNotApplicableNotAPass()
    {
        var result = new BuyerCheckEvaluator().Evaluate(Array.Empty<ComponentSnapshot>());

        Assert.Equal(BuyerCheckState.NotApplicable, result.Checks.Single(c => c.Id == "authenticity").State);
        Assert.Equal(BuyerCheckState.NotApplicable, result.Checks.Single(c => c.Id == "recent-crashes").State);
    }

    [Fact]
    public void RecentBugCheck_FailsTheRowAndDropsTheBannerOneLevel()
    {
        var now = DateTimeOffset.UtcNow;
        var components = AllPassBase(now).ToList();
        components[^1] = CrashWithBugCheck(now.AddDays(-5), 2);

        var result = new BuyerCheckEvaluator().Evaluate(components, new BuyerCheckContext("16 GB"));

        var check = result.Checks.Single(c => c.Id == "recent-crashes");
        Assert.Equal(BuyerCheckState.Fail, check.State);
        Assert.Equal("Recent blue screens recorded", check.Title);
        Assert.Equal(BuyerCheckState.Warning, result.OverallState);
        Assert.Equal("WARNING - Recent blue screens recorded", result.BannerText);
    }

    [Fact]
    public void OlderBugCheck_WarnsTheRowButDoesNotDropTheBanner()
    {
        var now = DateTimeOffset.UtcNow;
        var components = AllPassBase(now).ToList();
        components[^1] = CrashWithBugCheck(now.AddDays(-40), 1);

        var result = new BuyerCheckEvaluator().Evaluate(components, new BuyerCheckContext("16 GB"));

        Assert.Equal(BuyerCheckState.Warning, result.Checks.Single(c => c.Id == "recent-crashes").State);
        Assert.Equal(BuyerCheckState.Pass, result.OverallState);
    }

    [Fact]
    public void BothFindings_DropTheBannerTwoLevelsToFail_AndNameAuthenticity()
    {
        var now = DateTimeOffset.UtcNow;
        var components = AllPassBase(now).ToList();
        components[0] = SystemWithDates(now.AddDays(-20), now.AddDays(-19));
        components[^1] = CrashWithBugCheck(now.AddDays(-5), 1);

        var result = new BuyerCheckEvaluator().Evaluate(components, new BuyerCheckContext("16 GB"));

        Assert.Equal(BuyerCheckState.Fail, result.OverallState);
        Assert.Equal("FAIL - Signs of prior use (BIOS date vs Windows install date)", result.BannerText);
    }

    [Fact]
    public void WarningBase_PlusStrongAuthenticity_DropsToFail()
    {
        var now = DateTimeOffset.UtcNow;
        var components = new[]
        {
            // R4 Strong (BIOS after install) plus a Warning base: critical errors.
            SystemWithDates(now.AddDays(-20), now.AddDays(-19), criticalErrors: "3"),
            TestSnapshots.Storage("0", "0"),
            TestSnapshots.Battery(2),
            TestSnapshots.Ram("16 GB"),
            TestSnapshots.Crash(now),
        };

        var result = new BuyerCheckEvaluator().Evaluate(components, new BuyerCheckContext("16 GB"));

        Assert.Equal(BuyerCheckState.Warning, result.Checks.Single(c => c.Id == "critical-errors").State);
        Assert.Equal(BuyerCheckState.Fail, result.OverallState);
    }

    [Fact]
    public void FailBase_PlusStrongAuthenticity_StaysFail()
    {
        var now = DateTimeOffset.UtcNow;
        var components = new[]
        {
            SystemWithDates(now.AddDays(-20), now.AddDays(-19)),
            TestSnapshots.Storage("9", "120"),
            TestSnapshots.Battery(40),
            TestSnapshots.Ram("16 GB"),
        };

        var result = new BuyerCheckEvaluator().Evaluate(components);

        Assert.Equal(BuyerCheckState.Fail, result.OverallState);
        Assert.Equal("FAIL - Signs of prior use (BIOS date vs Windows install date)", result.BannerText);
    }

    [Fact]
    public void FindingWithNoUsableBaseReadings_LandsOnWarningNotNotApplicable()
    {
        // Only the authenticity entry has data (R4 Strong); every ordinary
        // check is NotApplicable. The finding itself is data, so the banner
        // is a verdict, not "not enough data".
        var checks = new[]
        {
            new BuyerCheck("ssd-wear", "SSD wear is 0%", "No reading.", BuyerCheckState.NotApplicable, "Ask."),
            new BuyerCheck("authenticity", "Signs of prior use (BIOS date vs Windows install date)", "BIOS date is after the install date.", BuyerCheckState.Fail, "Ask."),
        };

        var result = new BuyerCheckResult(checks);

        Assert.Equal(BuyerCheckState.Warning, result.OverallState);
        Assert.Equal("WARNING - Signs of prior use (BIOS date vs Windows install date)", result.BannerText);
    }

    [Fact]
    public void CrashHistory_DoesNotChangeTheHealthScore()
    {
        var now = DateTimeOffset.UtcNow;
        var clean = AllPassBase(now);
        var withCrashes = AllPassBase(now).ToList();
        withCrashes[^1] = CrashWithBugCheck(now.AddDays(-5), 3);

        var cleanScore = new ScoringEngine().Score(clean);
        var crashScore = new ScoringEngine().Score(withCrashes);

        Assert.Equal(cleanScore.OverallScore, crashScore.OverallScore);
    }

    private static LoadTestResult GetLoadTestResult(double clockMHz, double tempC, string? abortReason = null)
    {
        var baseTime = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        var samples = Enumerable.Range(0, 35).Select(i => new LoadTestSample(
            baseTime.AddSeconds(i),
            tempC,
            clockMHz,
            clockMHz,
            2400d,
            45d,
            55d,
            false)).ToList();
        return LoadTestAnalyzer.Analyze(samples, 4200d, abortReason, 60);
    }

    [Fact]
    public void NoLoadTest_NoLoadTestRowIsShown()
    {
        var result = new BuyerCheckEvaluator().Evaluate(AllPassBase(DateTimeOffset.UtcNow), new BuyerCheckContext("16 GB"));

        Assert.DoesNotContain(result.Checks, c => c.Id == BuyerCheckResult.LoadTestCheckId);
    }

    [Fact]
    public void LoadTestClean_PassesTheRowAndKeepsTheBannerPass()
    {
        var loadTest = GetLoadTestResult(3900d, 70d);

        var result = new BuyerCheckEvaluator().Evaluate(AllPassBase(DateTimeOffset.UtcNow), new BuyerCheckContext("16 GB"), loadTest);

        var check = result.Checks.Single(c => c.Id == BuyerCheckResult.LoadTestCheckId);
        Assert.Equal(BuyerCheckState.Pass, check.State);
        Assert.Equal("CPU held rated clock under load", check.Title);
        Assert.Equal(BuyerCheckState.Pass, result.OverallState);
    }

    [Fact]
    public void LoadTestThrottled_FailsTheRowAndDropsTheBannerOneLevel_NotToFail()
    {
        var loadTest = GetLoadTestResult(1800d, 97d);

        var result = new BuyerCheckEvaluator().Evaluate(AllPassBase(DateTimeOffset.UtcNow), new BuyerCheckContext("16 GB"), loadTest);

        var check = result.Checks.Single(c => c.Id == BuyerCheckResult.LoadTestCheckId);
        Assert.Equal(BuyerCheckState.Fail, check.State);
        Assert.Equal("CPU throttled under load", check.Title);
        // One-grade drop, like the other findings: a throttling machine can
        // still score 90 on passive health, and the banner names the finding.
        Assert.Equal(BuyerCheckState.Warning, result.OverallState);
        Assert.Equal("WARNING - CPU throttled under load", result.BannerText);
    }

    [Fact]
    public void LoadTestPowerLimited_WarnsTheRowButDoesNotFailIt()
    {
        var loadTest = GetLoadTestResult(1800d, 65d);

        var result = new BuyerCheckEvaluator().Evaluate(AllPassBase(DateTimeOffset.UtcNow), new BuyerCheckContext("16 GB"), loadTest);

        Assert.Equal(BuyerCheckState.Warning, result.Checks.Single(c => c.Id == BuyerCheckResult.LoadTestCheckId).State);
        // A Warning finding does not drop the banner (same as an older
        // bug check that only warns): the banner stays Pass.
        Assert.Equal(BuyerCheckState.Pass, result.OverallState);
    }

    [Fact]
    public void LoadTestTemperatureAbort_FailsTheRow()
    {
        var loadTest = GetLoadTestResult(1800d, 96d, LoadTestResult.AbortTemperature);

        var result = new BuyerCheckEvaluator().Evaluate(AllPassBase(DateTimeOffset.UtcNow), new BuyerCheckContext("16 GB"), loadTest);

        Assert.Equal(BuyerCheckState.Fail, result.Checks.Single(c => c.Id == BuyerCheckResult.LoadTestCheckId).State);
    }

    [Fact]
    public void LoadTestCancelled_IsNotApplicableNotAFailure()
    {
        var loadTest = GetLoadTestResult(3900d, 70d, LoadTestResult.AbortCancelled);

        var result = new BuyerCheckEvaluator().Evaluate(AllPassBase(DateTimeOffset.UtcNow), new BuyerCheckContext("16 GB"), loadTest);

        Assert.Equal(BuyerCheckState.NotApplicable, result.Checks.Single(c => c.Id == BuyerCheckResult.LoadTestCheckId).State);
        Assert.Equal(BuyerCheckState.Pass, result.OverallState);
    }

    // ---- v1.4.1 (F8): honest check-count split, BIOS date from motherboard ----

    [Fact]
    public void LaptopShape_OneFailedNineNotApplicable_ReportsTheRealSplitNotZeroOfTen()
    {
        // The v1.4.0 laptop test: battery cycles 42 failed, every other
        // reading was missing, and the headline said "0 of 10 checks
        // passed", hiding that almost nothing was measured at all.
        var result = new BuyerCheckEvaluator().Evaluate(
            new[] { TestSnapshots.Battery(42) });

        Assert.Equal(10, result.Checks.Count);
        Assert.Equal(BuyerCheckState.Fail, result.Checks.Single(c => c.Id == "battery-cycles").State);
        Assert.Equal(9, result.Checks.Count(c => c.State == BuyerCheckState.NotApplicable));
        Assert.Equal(0, result.Checks.Count(c => c.State == BuyerCheckState.Pass));
        Assert.Equal("1 failed, 9 not applicable, 0 passed", result.SummaryText);
    }

    [Fact]
    public void HealthyMachine_SummaryTextIsAllPassed()
    {
        var snapshot = TestSnapshots.Healthy();

        Assert.Equal("10 passed", snapshot.BuyerChecks.SummaryText);
    }

    [Fact]
    public void BiosDate_OnMotherboardOnly_UsesTheCollectedDateInsteadOfNotAvailable()
    {
        // v1.4.0 laptop: the system collector timed out, but the
        // motherboard collector had already read BIOS date 2025-09-06.
        // The check must use that date, not report "not available".
        var motherboard = MotherboardWithBiosDate("2025-09-06");

        var result = new BuyerCheckEvaluator().Evaluate(new[] { motherboard });

        var check = result.Checks.Single(c => c.Id == "bios-date");
        Assert.NotEqual(BuyerCheckState.NotApplicable, check.State);
        Assert.Contains("2025-09-06", check.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void BiosDate_SystemValueNotReported_FallsBackToMotherboard()
    {
        // Same failure mode, one step closer to the hardware: the system
        // component exists but its BIOS date spec is the literal
        // "Not reported by hardware", which is not a parseable date.
        var system = new ComponentSnapshot(
            "system",
            "System",
            "System",
            "system",
            "Test Laptop",
            null,
            new[]
            {
                new SpecValue("BIOS date", "Not reported by hardware", "CIM Win32_BIOS"),
            },
            "System information comes from Windows.",
            DateTimeOffset.UtcNow);

        var result = new BuyerCheckEvaluator().Evaluate(new[] { system, MotherboardWithBiosDate("2025-09-06") });

        var check = result.Checks.Single(c => c.Id == "bios-date");
        Assert.NotEqual(BuyerCheckState.NotApplicable, check.State);
        Assert.Contains("2025-09-06", check.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void BiosDate_PrefersTheSystemValueWhenBothComponentsReportOne()
    {
        var now = DateTimeOffset.UtcNow;
        var system = TestSnapshots.System(now);
        var systemBiosDate = now.AddDays(-20).ToString("yyyy-MM-dd");

        var result = new BuyerCheckEvaluator().Evaluate(new[] { system, MotherboardWithBiosDate("2025-09-06") });

        var check = result.Checks.Single(c => c.Id == "bios-date");
        Assert.Equal(BuyerCheckState.Pass, check.State);
        Assert.Contains(systemBiosDate, check.Detail, StringComparison.Ordinal);
    }

    private static ComponentSnapshot MotherboardWithBiosDate(string biosDate) => new(
        "motherboard",
        "Motherboard",
        "Motherboard",
        "motherboard",
        "Test board",
        null,
        new[] { new SpecValue("BIOS date", biosDate, "CIM Win32_BIOS") },
        "Board and BIOS identity.",
        DateTimeOffset.UtcNow);
}
