namespace TrueSpec.Core.Scoring;

using System.Globalization;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Health;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;

public interface IBuyerCheckEvaluator
{
    BuyerCheckResult Evaluate(IEnumerable<ComponentSnapshot> components, BuyerCheckContext? context = null, LoadTestResult? loadTest = null);
}

/// <summary>
/// Buyer-mode rules. Each rule reads a measured spec value and returns pass,
/// warning or fail with a plain English explanation. Missing readings become
/// "Not applicable" so the report never claims a fake pass or fail.
/// </summary>
public sealed class BuyerCheckEvaluator : IBuyerCheckEvaluator
{
    public BuyerCheckResult Evaluate(IEnumerable<ComponentSnapshot> components, BuyerCheckContext? context = null, LoadTestResult? loadTest = null)
    {
        ArgumentNullException.ThrowIfNull(components);

        var componentList = components.ToList();
        var byId = componentList
            // A duplicate collector id must never crash the whole scan:
            // keep the first component for an id and carry on.
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var checks = new List<BuyerCheck>
        {
            CheckSsdWear(byId),
            CheckBatteryCycles(byId),
            CheckPowerOnHours(byId),
            CheckAdvertisedRam(byId, context),
            CheckBiosDate(byId),
            CheckWindowsInstallDate(byId),
            CheckCriticalErrors(byId),
            CheckSignedDrivers(byId),
            CheckAuthenticity(componentList),
            CheckRecentCrashes(byId),
        };

        // The load-test check only exists once a load test has actually run:
        // a machine that never ran the test is "not tested", not a pass and
        // not a failure, so no row is shown for it.
        if (loadTest is not null)
        {
            checks.Add(CheckLoadTest(loadTest));
        }

        return new BuyerCheckResult(checks);
    }

    /// <summary>
    /// The load test is NotMeasurable for the health score (it is a
    /// user-initiated test, not a passive measurement), but it is a buyer
    /// finding: a throttling machine can score 90 on passive health while
    /// the New Laptop Check banner shows this finding separately (one-grade
    /// drop, same as the authenticity and crash findings).
    /// </summary>
    private static BuyerCheck CheckLoadTest(LoadTestResult loadTest)
    {
        const string title = "CPU held rated clock under load";

        if (loadTest.AbortReason == LoadTestResult.AbortCancelled)
        {
            return new BuyerCheck(BuyerCheckResult.LoadTestCheckId, title,
                "The load test was cancelled before it finished, so it proves nothing about throttling.",
                BuyerCheckState.NotApplicable,
                "Run the 60-second load test from the Tests page and let it finish.");
        }

        if (loadTest.AbortReason == LoadTestResult.AbortSensorLost)
        {
            return new BuyerCheck(BuyerCheckResult.LoadTestCheckId, title,
                "The sensors stopped reporting during the load test, so throttling could not be judged.",
                BuyerCheckState.NotApplicable,
                "Ask the seller whether the sensors work, and run the load test again.");
        }

        if (loadTest.AbortReason == LoadTestResult.AbortTemperature)
        {
            return new BuyerCheck(BuyerCheckResult.LoadTestCheckId, "CPU throttled under load",
                FormattableString.Invariant(
                    $"The load test stopped automatically at {loadTest.PeakTempC:0.#} C, above the 95 C safety limit. The cooling could not keep up with a full load."),
                BuyerCheckState.Fail,
                "Ask why the machine overheats under load, and whether the cooling has been serviced.");
        }

        if (loadTest.Throttled)
        {
            return new BuyerCheck(BuyerCheckResult.LoadTestCheckId, "CPU throttled under load",
                loadTest.VerdictText,
                BuyerCheckState.Fail,
                "Ask why the machine slows down under load, and whether the cooling has been serviced.");
        }

        if (loadTest.PowerLimited)
        {
            return new BuyerCheck(BuyerCheckResult.LoadTestCheckId, title,
                loadTest.VerdictText,
                BuyerCheckState.Warning,
                "Ask whether the machine was on battery or in a power-saving mode during the test, and run it again on mains power.");
        }

        if (loadTest.AvgClockMHz is null || loadTest.ClockHeldPercent is null)
        {
            return new BuyerCheck(BuyerCheckResult.LoadTestCheckId, title,
                loadTest.VerdictText,
                BuyerCheckState.NotApplicable,
                "Run the load test again: no clock reading was recorded, so throttling could not be judged.");
        }

        return new BuyerCheck(BuyerCheckResult.LoadTestCheckId, title,
            loadTest.VerdictText,
            BuyerCheckState.Pass,
            "Nothing to ask.");
    }

    private static BuyerCheck CheckSsdWear(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "ssd-wear";
        const string title = "SSD wear is 0%";

        if (!TryGetSpec(byId, "storage", out var component, SpecLabels.WearLevel) ||
            !SpecText.TryParseLeadingNumber(component, out var wear))
        {
            return new BuyerCheck(id, title,
                "No SSD wear reading was available.",
                BuyerCheckState.NotApplicable,
                "Ask the seller for a screenshot of the drive SMART data.");
        }

        var smartWarning = SpecValue(byId, "storage", SpecLabels.SMARTWarnings);
        var extra = string.Equals(smartWarning, "Yes", StringComparison.OrdinalIgnoreCase)
            ? " The drive also reports SMART warnings."
            : string.Empty;

        return wear <= 0d && extra.Length == 0
            ? new BuyerCheck(id, title,
                "The drive reports 0% wear and no SMART warnings.",
                BuyerCheckState.Pass,
                "Nothing to ask.")
            : new BuyerCheck(id, title,
                FormattableString.Invariant($"The drive reports {wear:0.#}% wear.{extra}"),
                BuyerCheckState.Fail,
                "Ask why the drive has wear if the laptop is being sold as new.");
    }

    private static BuyerCheck CheckBatteryCycles(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "battery-cycles";
        const string title = "Battery cycles 5 or fewer";

        if (!TryGetSpec(byId, "battery", out var value, SpecLabels.CycleCount) ||
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles))
        {
            return new BuyerCheck(id, title,
                "The battery did not report a cycle count.",
                BuyerCheckState.NotApplicable,
                "Ask the seller to run a battery report and show the cycle count.");
        }

        return cycles <= 5
            ? new BuyerCheck(id, title,
                $"The battery reports {cycles} charge cycles.",
                BuyerCheckState.Pass,
                "Nothing to ask.")
            : new BuyerCheck(id, title,
                $"The battery reports {cycles} charge cycles, which means it has been charged many times.",
                BuyerCheckState.Fail,
                "Ask how the laptop was used before it was sold.");
    }

    private static BuyerCheck CheckPowerOnHours(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "power-on-hours";
        const string title = "Drive power-on hours are 0";

        if (!TryGetSpec(byId, "storage", out var value, SpecLabels.PowerOnHours) ||
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours))
        {
            return new BuyerCheck(id, title,
                "No drive power-on hour count was available.",
                BuyerCheckState.NotApplicable,
                "Ask the seller for a screenshot of the drive SMART data.");
        }

        return hours == 0
            ? new BuyerCheck(id, title,
                "The drive reports 0 power-on hours.",
                BuyerCheckState.Pass,
                "Nothing to ask.")
            : new BuyerCheck(id, title,
                $"The drive has been powered on for {hours} hours.",
                BuyerCheckState.Fail,
                "Ask how a new laptop already has powered-on hours on its drive.");
    }

    private static BuyerCheck CheckAdvertisedRam(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        BuyerCheckContext? context)
    {
        const string id = "ram-size";
        const string title = "RAM size matches the advertisement";

        if (!TryGetSpec(byId, "ram", out var actual, SpecLabels.TotalMemory))
        {
            return new BuyerCheck(id, title,
                "The installed RAM size was not available.",
                BuyerCheckState.NotApplicable,
                "Ask the seller for the exact RAM size.");
        }

        var advertised = context?.AdvertisedRam;
        if (string.IsNullOrWhiteSpace(advertised))
        {
            return new BuyerCheck(id, title,
                $"Installed memory is {actual}. No advertised size was entered.",
                BuyerCheckState.NotApplicable,
                "Type the RAM size from the seller listing to enable this check.");
        }

        // Compare numbers, not strings: a 16 GiB machine shows "16.0 GB"
        // and a listing that says "16 GB" is the same machine, while the
        // old string compare failed it ("16.0GB" != "16GB"). A 1% tolerance
        // absorbs rounding ("15.9 GB" still matches 16 GiB); a real
        // mismatch still fails.
        if (!SpecText.TryParseMemorySizeBytes(actual, out var actualBytes) ||
            !SpecText.TryParseMemorySizeBytes(advertised, out var advertisedBytes))
        {
            return new BuyerCheck(id, title,
                $"Installed memory is {actual} but the listing size ({advertised}) could not be understood.",
                BuyerCheckState.NotApplicable,
                "Type the RAM size from the seller listing as a number and a unit, for example \"16 GB\".");
        }

        var difference = Math.Abs((double)actualBytes - advertisedBytes);
        return difference <= 0.01d * actualBytes
            ? new BuyerCheck(id, title,
                $"Installed memory is {actual} and the listing says {advertised}.",
                BuyerCheckState.Pass,
                "Nothing to ask.")
            : new BuyerCheck(id, title,
                $"Installed memory is {actual} but the listing says {advertised}.",
                BuyerCheckState.Fail,
                "Ask the seller to explain the difference before you buy.");
    }

    private static BuyerCheck CheckBiosDate(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "bios-date";
        const string title = "BIOS date is recent";

        // The system collector and the motherboard collector both read
        // Win32_BIOS.ReleaseDate, but they run independently: on a slow
        // machine the system collector can time out while the motherboard
        // collector already delivered the same date. Read the system
        // value first and fall back to the motherboard value, so the
        // check uses the date already collected instead of reporting
        // "not available" for a date the Motherboard page shows.
        if (!TryReadDate(byId, "system", SpecLabels.BIOSDate, out var biosDate) &&
            !TryReadDate(byId, "motherboard", SpecLabels.BIOSDate, out biosDate))
        {
            return new BuyerCheck(id, title,
                "The BIOS date was not available.",
                BuyerCheckState.NotApplicable,
                "Ask the seller for the BIOS version and date.");
        }

        var age = DateTimeOffset.UtcNow - biosDate;

        // A BIOS dated in the future is impossible data (bad clock or a
        // misread firmware date), not a recent BIOS: it must not pass.
        // One day of tolerance covers clock skew.
        if (age.TotalDays < -1d)
        {
            return new BuyerCheck(id, title,
                $"The BIOS date is {biosDate:yyyy-MM-dd}, which is in the future, so it cannot be trusted.",
                BuyerCheckState.NotApplicable,
                "Ask the seller to check the machine clock and the BIOS date.");
        }

        return age.TotalDays switch
        {
            <= 730 => new BuyerCheck(id, title,
                $"BIOS date is {biosDate:yyyy-MM-dd}, less than two years old.",
                BuyerCheckState.Pass,
                "Nothing to ask."),
            <= 1095 => new BuyerCheck(id, title,
                $"BIOS date is {biosDate:yyyy-MM-dd}, between two and three years old.",
                BuyerCheckState.Warning,
                "Ask whether the BIOS has ever been updated."),
            _ => new BuyerCheck(id, title,
                $"BIOS date is {biosDate:yyyy-MM-dd}, over three years old.",
                BuyerCheckState.Fail,
                "Ask why a laptop sold as new has an old BIOS."),
        };
    }

    private static BuyerCheck CheckWindowsInstallDate(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "windows-install";
        const string title = "Windows install date is recent";

        if (!TryGetSpec(byId, "system", out var value, SpecLabels.WindowsInstallDate) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var installDate))
        {
            return new BuyerCheck(id, title,
                "The Windows install date was not available.",
                BuyerCheckState.NotApplicable,
                "Ask when Windows was last installed.");
        }

        var age = DateTimeOffset.UtcNow - installDate;

        // A Windows install dated in the future is impossible data, not a
        // fresh install: it must not pass. One day covers clock skew.
        if (age.TotalDays < -1d)
        {
            return new BuyerCheck(id, title,
                $"Windows was installed on {installDate:yyyy-MM-dd}, which is in the future, so it cannot be trusted.",
                BuyerCheckState.NotApplicable,
                "Ask the seller to check the machine clock and when Windows was installed.");
        }

        return age.TotalDays switch
        {
            <= 60 => new BuyerCheck(id, title,
                $"Windows was installed on {installDate:yyyy-MM-dd}, within the last two months.",
                BuyerCheckState.Pass,
                "Nothing to ask."),
            <= 180 => new BuyerCheck(id, title,
                $"Windows was installed on {installDate:yyyy-MM-dd}, within the last six months.",
                BuyerCheckState.Warning,
                "Ask whether the machine was reset before sale."),
            _ => new BuyerCheck(id, title,
                $"Windows was installed on {installDate:yyyy-MM-dd}, over six months ago.",
                BuyerCheckState.Fail,
                "Ask how the machine was used before it was sold."),
        };
    }

    private static BuyerCheck CheckCriticalErrors(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "critical-errors";
        const string title = "No critical errors in the event log";

        if (!TryGetSpec(byId, "system", out var value, SpecLabels.CriticalErrorsEventLog) ||
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var errors))
        {
            return new BuyerCheck(id, title,
                "The event log could not be read.",
                BuyerCheckState.NotApplicable,
                "Ask the seller to show the Windows event log.");
        }

        return errors == 0
            ? new BuyerCheck(id, title,
                "No critical errors were found.",
                BuyerCheckState.Pass,
                "Nothing to ask.")
            : new BuyerCheck(id, title,
                $"{errors} critical error(s) were found in the event log.",
                BuyerCheckState.Warning,
                "Ask what caused the errors and whether the machine has crashed.");
    }

    private static BuyerCheck CheckSignedDrivers(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "signed-drivers";
        const string title = "All drivers are signed";

        if (!TryGetSpec(byId, "system", out var value, SpecLabels.SignedDriversOnly))
        {
            return new BuyerCheck(id, title,
                "The driver signature state was not available.",
                BuyerCheckState.NotApplicable,
                "Ask the seller whether any unsigned drivers are installed.");
        }

        if (string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase))
        {
            return new BuyerCheck(id, title,
                "Only signed drivers are installed.",
                BuyerCheckState.Pass,
                "Nothing to ask.");
        }

        if (string.Equals(value, "No", StringComparison.OrdinalIgnoreCase))
        {
            return new BuyerCheck(id, title,
                "Unsigned drivers were found.",
                BuyerCheckState.Fail,
                "Ask why unsigned drivers were installed on the machine.");
        }

        return new BuyerCheck(id, title,
            "Windows did not report a complete driver signature state.",
            BuyerCheckState.NotApplicable,
            "Ask the seller whether any unsigned drivers are installed.");
    }

    /// <summary>
    /// U1 authenticity (Blueprint 1): the age-signal cross-checks as one
    /// buyer-check row. A Strong finding fails the row; a Watch-only result
    /// warns; an Info-only result passes with the note in the detail. No
    /// fired check and at least one rule that ran passes on the analyzer's
    /// own "No signs of prior use" verdict; when every rule was skipped
    /// (no age signal was reported at all) the row is NotApplicable, never
    /// a guessed pass. The row's Fail state is what demotes the banner one
    /// grade (see <see cref="BuyerCheckResult"/>); it never touches the
    /// 0-100 health score.
    /// </summary>
    private static BuyerCheck CheckAuthenticity(IReadOnlyList<ComponentSnapshot> components)
    {
        const string id = BuyerCheckResult.AuthenticityCheckId;

        var result = AuthenticityAnalyzer.Analyze(components, DateTimeOffset.UtcNow);
        var strongest = result.Checks
            .OrderByDescending(c => c.Severity)
            .FirstOrDefault();

        if (strongest is null)
        {
            if (AllRulesSkipped(result))
            {
                return new BuyerCheck(id, "No signs of prior use",
                    "No age signal was reported, so none of the authenticity checks could run.",
                    BuyerCheckState.NotApplicable,
                    "Ask the seller for the drive SMART data, the battery cycle count and the Windows install date.");
            }

            return new BuyerCheck(id, "No signs of prior use",
                result.Summary,
                BuyerCheckState.Pass,
                "Nothing to ask.");
        }

        var detail = result.SkippedChecks.Count == 0
            ? strongest.EvidenceLine
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{strongest.EvidenceLine} ({result.SkippedChecks.Count} other check(s) could not run because a signal was not reported.)");
        var title = string.Create(
            CultureInfo.InvariantCulture,
            $"Signs of prior use ({strongest.Title})");

        return strongest.Severity switch
        {
            AuthenticitySeverity.Strong => new BuyerCheck(id, title, detail,
                BuyerCheckState.Fail,
                "Ask the seller how the machine was used before this Windows install."),
            AuthenticitySeverity.Watch => new BuyerCheck(id, title, detail,
                BuyerCheckState.Warning,
                "Ask the seller how the machine was used before this Windows install."),
            _ => new BuyerCheck(id, "No signs of prior use", detail,
                BuyerCheckState.Pass,
                "Nothing to ask."),
        };
    }

    /// <summary>
    /// U3 crash forensics (Blueprint 3): a blue screen in the last 30 days
    /// fails the row ("Recent blue screens recorded") and demotes the banner
    /// one grade. Older blue screens, a recent fatal hardware error or a
    /// recent disk error warn. Corrected hardware errors alone never fail or
    /// warn: they are shown as information. A cleared-log suspicion warns,
    /// because a wiped log reads as a gap, never as a clean record. No crash
    /// component at all is NotApplicable.
    /// </summary>
    private static BuyerCheck CheckRecentCrashes(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = BuyerCheckResult.RecentCrashesCheckId;

        if (!byId.TryGetValue("crash", out var crash) ||
            CrashHistoryParser.FromComponent(crash) is not { } history)
        {
            return new BuyerCheck(id, "No recent blue screens",
                "Crash history was not available for this machine.",
                BuyerCheckState.NotApplicable,
                "Ask the seller whether the machine has ever blue-screened.");
        }

        var now = DateTimeOffset.UtcNow;
        if (CrashHistoryParser.HasRecentEvent(history, CrashClass.BugCheck, now))
        {
            var summary = history.Summaries.Single(s => s.Class == CrashClass.BugCheck);
            return new BuyerCheck(id, "Recent blue screens recorded",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{summary.Count90d} blue screen(s) in the last 90 days, most recently {summary.LastAt:yyyy-MM-dd}."),
                BuyerCheckState.Fail,
                "Ask what caused the blue screens and whether the machine has crashed since.");
        }

        if (history.LogClearedSuspected)
        {
            return new BuyerCheck(id, "No recent blue screens",
                "The event log has no entries before " +
                $"{history.EarliestLogEntry:yyyy-MM-dd}, so earlier crashes would not appear here.",
                BuyerCheckState.Warning,
                "Ask the seller whether the event log was cleared and why.");
        }

        var olderBugCheck = history.Summaries
            .FirstOrDefault(s => s.Class == CrashClass.BugCheck && s.Count90d > 0);
        if (olderBugCheck is not null)
        {
            return new BuyerCheck(id, "No recent blue screens",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{olderBugCheck.Count90d} blue screen(s) in the last 90 days, most recently {olderBugCheck.LastAt:yyyy-MM-dd}. None in the last {CrashHistoryParser.RecentWindowDays} days."),
                BuyerCheckState.Warning,
                "Ask what caused the blue screens and whether the machine has crashed since.");
        }

        if (CrashHistoryParser.HasRecentEvent(history, CrashClass.WheaFatal, now))
        {
            var summary = history.Summaries.Single(s => s.Class == CrashClass.WheaFatal);
            return new BuyerCheck(id, "No recent blue screens",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{summary.Count90d} fatal hardware error(s) in the last 90 days, most recently {summary.LastAt:yyyy-MM-dd}."),
                BuyerCheckState.Warning,
                "Ask what hardware reported the fatal error and whether it was replaced.");
        }

        var diskErrors = history.Summaries
            .FirstOrDefault(s => s.Class == CrashClass.DiskError && s.Count90d > 0);
        if (diskErrors is not null)
        {
            return new BuyerCheck(id, "No recent blue screens",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{diskErrors.Count90d} disk error(s) in the last 90 days, most recently {diskErrors.LastAt:yyyy-MM-dd}."),
                BuyerCheckState.Warning,
                "Ask whether the drive has been checked and whether any files were lost.");
        }

        var corrected = history.Summaries
            .FirstOrDefault(s => s.Class == CrashClass.WheaCorrected && s.Count90d > 0);
        if (corrected is not null)
        {
            return new BuyerCheck(id, "No recent blue screens",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"No blue screens in the last 90 days. {corrected.Count90d} hardware error(s) were corrected automatically, which is normal on some hardware."),
                BuyerCheckState.Pass,
                "Nothing to ask.");
        }

        return new BuyerCheck(id, "No recent blue screens",
            "No blue screens, hardware errors, unexpected shutdowns or disk errors were recorded in the last 90 days.",
            BuyerCheckState.Pass,
            "Nothing to ask.");
    }

    /// <summary>True when every authenticity rule was skipped (no signal ran).</summary>
    private static bool AllRulesSkipped(AuthenticityResult result)
    {
        var titles = new[]
        {
            AuthenticityAnalyzer.R1Title,
            AuthenticityAnalyzer.R2Title,
            AuthenticityAnalyzer.R3Title,
            AuthenticityAnalyzer.R4Title,
            AuthenticityAnalyzer.R5Title,
            AuthenticityAnalyzer.R6Title,
        };

        return titles.All(title => result.SkippedChecks.Any(
            skipped => skipped.StartsWith(title, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Reads a date spec as a <see cref="DateTimeOffset"/>. A missing spec,
    /// the literal SpecLabels.NotReportedByHardware and any other unparseable
    /// value all read as absent, so the caller can fall back to another
    /// component that reports the same reading.
    /// </summary>
    private static bool TryReadDate(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        string componentId,
        string label,
        out DateTimeOffset date)
    {
        date = default;
        return TryGetSpec(byId, componentId, out var value, label) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date);
    }

    private static bool TryGetSpec(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        string componentId,
        out string value,
        string label)
    {
        value = string.Empty;
        if (!byId.TryGetValue(componentId, out var component))
        {
            return false;
        }

        var spec = component.Specs.FirstOrDefault(s =>
            string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase));
        if (spec is null || string.IsNullOrWhiteSpace(spec.Value))
        {
            return false;
        }

        value = spec.Value.Trim();
        return true;
    }

    private static string? SpecValue(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        string componentId,
        string label) =>
        byId.TryGetValue(componentId, out var component)
            ? component.Specs.FirstOrDefault(s =>
                string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase))?.Value
            : null;
}
