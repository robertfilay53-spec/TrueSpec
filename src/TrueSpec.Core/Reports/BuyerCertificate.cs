namespace TrueSpec.Core.Reports;

using System.Globalization;
using TrueSpec.Core.Models;

/// <summary>
/// The buyer certificate (Blueprint 4, U7): the one-page summary a buyer
/// can verify with a re-scan. It carries the PASS / WARNING / FAIL banner,
/// the overall score, the five most important numbers (SSD wear, battery
/// health, cycle count, power-on hours, throttle result when a load test
/// ran), the full DNA hash, and the scan date. Every unmeasured number
/// renders SpecLabels.NotReportedByHardware, never a zero or a guess; a load
/// test that never ran says so instead of inventing a throttle result.
/// The full DNA hash belongs in the certificate file (a hash is not a
/// serial); full serials never appear here.
/// </summary>
public sealed record BuyerCertificate(
    string Banner,
    string Score,
    string SsdWear,
    string BatteryHealth,
    string CycleCount,
    string PowerOnHours,
    string ThrottleResult,
    string? DnaHash,
    DateTimeOffset ScanDate,
    string MachineName)
{
    /// <summary>
    /// "DNA recorded" when this scan hashed at least one usable serial,
    /// "DNA not available" when it ran but no part reported a usable serial,
    /// "DNA not recorded" for a snapshot saved before DNA existed. The last
    /// two are honest states, never a mismatch.
    /// </summary>
    public string DnaStatus => DnaHash is not null
        ? "DNA recorded"
        : MachineDnaPartsRecorded ? "DNA not available" : "DNA not recorded";

    /// <summary>True when this snapshot carries DNA parts (even unidentified ones).</summary>
    public bool MachineDnaPartsRecorded { get; init; }

    /// <summary>True when the certificate carries a DNA hash a re-scan can be checked against.</summary>
    public bool HasDna => DnaHash is not null;

    public static BuyerCertificate FromSnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var storage = snapshot.Find("storage");
        var battery = snapshot.Find("battery");

        return new BuyerCertificate(
            Banner: snapshot.BuyerChecks.BannerText,
            Score: snapshot.Score.HasMeasurableComponents
                ? string.Create(CultureInfo.InvariantCulture, $"{snapshot.Score.OverallScore:0.#} / 100")
                : "Not available",
            SsdWear: SpecValueOf(storage, SpecLabels.WearLevel),
            BatteryHealth: battery?.HealthPercent is { } health
                ? string.Create(CultureInfo.InvariantCulture, $"{health:0}%")
                : SpecLabels.NotReportedByHardware,
            CycleCount: SpecValueOf(battery, SpecLabels.CycleCount),
            PowerOnHours: SpecValueOf(storage, SpecLabels.PowerOnHours),
            ThrottleResult: ThrottleResultOf(snapshot),
            DnaHash: snapshot.MachineDnaHash,
            ScanDate: snapshot.CapturedAt,
            MachineName: snapshot.MachineName)
        {
            MachineDnaPartsRecorded = snapshot.MachineDnaParts is not null,
        };
    }

    private static string ThrottleResultOf(SystemSnapshot snapshot)
    {
        var loadTest = snapshot.LoadTest;
        if (loadTest is null)
        {
            return "Not tested - no load test has run";
        }

        if (!string.IsNullOrWhiteSpace(loadTest.VerdictText))
        {
            return loadTest.VerdictText;
        }

        if (loadTest.Throttled)
        {
            return "Throttled under load";
        }

        return loadTest.PowerLimited
            ? "Power limited under load"
            : "No throttling detected";
    }

    /// <summary>
    /// The value of a spec by label: the exact label first (the primary
    /// drive / battery), then a prefixed label ("Drive 2 - Wear level")
    /// when the primary one is absent. A missing spec is "not reported",
    /// never a zero.
    /// </summary>
    private static string SpecValueOf(ComponentSnapshot? component, string label)
    {
        if (component is null)
        {
            return SpecLabels.NotReportedByHardware;
        }

        var spec = component.Specs.FirstOrDefault(s =>
            string.Equals(s.Label, label, StringComparison.Ordinal));
        spec ??= component.Specs.FirstOrDefault(s =>
            s.Label.EndsWith(" - " + label, StringComparison.Ordinal));
        if (spec is null || string.IsNullOrWhiteSpace(spec.Value))
        {
            return SpecLabels.NotReportedByHardware;
        }

        return spec.Value;
    }
}
