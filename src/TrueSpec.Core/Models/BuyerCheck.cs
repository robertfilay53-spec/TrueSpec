namespace TrueSpec.Core.Models;

/// <summary>
/// Values the buyer supplies from the seller listing. They are clearly not
/// hardware readings and are only used for passport-style comparisons.
/// </summary>
public sealed record BuyerCheckContext(string? AdvertisedRam = null);

public enum BuyerCheckState
{
    Pass,
    Warning,
    Fail,
    NotApplicable,
}

/// <summary>
/// One row in the "New Laptop Check" view. A failed row always tells the buyer
/// what it means and what to ask the seller.
/// </summary>
public sealed record BuyerCheck(
    string Id,
    string Title,
    string Detail,
    BuyerCheckState State,
    string WhatToAsk)
{
    public bool IsPass => State == BuyerCheckState.Pass;

    public string StateText => State switch
    {
        BuyerCheckState.Pass => "Pass",
        BuyerCheckState.Warning => "Warning",
        BuyerCheckState.Fail => "Fail",
        _ => "Not applicable",
    };

    public HealthStatus ToHealthStatus() => State switch
    {
        BuyerCheckState.Pass => HealthStatus.Healthy,
        BuyerCheckState.Warning => HealthStatus.Watch,
        BuyerCheckState.Fail => HealthStatus.Problem,
        _ => HealthStatus.NotMeasurable,
    };
}

public sealed record BuyerCheckResult(IReadOnlyList<BuyerCheck> Checks)
{
    /// <summary>Buyer-check id for the authenticity finding (U1).</summary>
    public const string AuthenticityCheckId = "authenticity";

    /// <summary>Buyer-check id for the recent-crash finding (U3).</summary>
    public const string RecentCrashesCheckId = "recent-crashes";

    /// <summary>Buyer-check id for the load-test finding (U2). The check only exists once a load test has run.</summary>
    public const string LoadTestCheckId = "cpu-load-test";

    public BuyerCheckState OverallState
    {
        get
        {
            var baseState = BaseOverallState();
            var drops = FindingDrops();
            if (drops == 0)
            {
                return baseState;
            }

            // Each finding demotes the banner one grade: PASS -> WARNING ->
            // FAIL. A machine with no usable base readings still has the
            // finding itself as data, so it lands on WARNING (one finding)
            // or FAIL (two), never "not enough data".
            var rank = baseState switch
            {
                BuyerCheckState.Pass => 0,
                BuyerCheckState.Warning => 1,
                BuyerCheckState.Fail => 2,
                _ => 0,
            };

            return Math.Min(2, rank + drops) switch
            {
                0 => BuyerCheckState.Pass,
                1 => BuyerCheckState.Warning,
                _ => BuyerCheckState.Fail,
            };
        }
    }

    public string BannerText
    {
        get
        {
            var finding = StrongestFinding();
            if (finding is null)
            {
                return OverallState switch
                {
                    BuyerCheckState.Pass => "PASS - this machine matches a new laptop",
                    BuyerCheckState.Warning => "WARNING - check the highlighted items",
                    BuyerCheckState.Fail => "FAIL - this machine does not look new",
                    _ => "Not enough data for a verdict",
                };
            }

            // The banner names the strongest finding, so the buyer sees why
            // the verdict dropped without hunting through the checklist.
            var stateText = OverallState switch
            {
                BuyerCheckState.Pass => "PASS",
                BuyerCheckState.Warning => "WARNING",
                BuyerCheckState.Fail => "FAIL",
                _ => "Not enough data",
            };

            return $"{stateText} - {finding}";
        }
    }

    /// <summary>
    /// The overall state of the ordinary checks. The three findings-driven
    /// entries (authenticity, recent crashes, load test) are excluded here on
    /// purpose: their banner effect is the one-grade drop in
    /// <see cref="OverallState"/>, not the worst-state rule. A throttling
    /// machine can therefore still score 90 on passive health while the
    /// banner shows the throttle finding separately.
    /// </summary>
    private BuyerCheckState BaseOverallState()
    {
        var baseChecks = Checks.Where(c =>
            c.Id != AuthenticityCheckId && c.Id != RecentCrashesCheckId && c.Id != LoadTestCheckId);

        if (baseChecks.Any(c => c.State == BuyerCheckState.Fail))
        {
            return BuyerCheckState.Fail;
        }

        if (baseChecks.Any(c => c.State == BuyerCheckState.Warning))
        {
            return BuyerCheckState.Warning;
        }

        // PASS needs at least one check that actually passed. A machine
        // with no usable readings is "not enough data", never a pass.
        if (baseChecks.Any(c => c.State == BuyerCheckState.Pass))
        {
            return BuyerCheckState.Pass;
        }

        return BuyerCheckState.NotApplicable;
    }

    /// <summary>One drop per Fail findings entry (authenticity, recent crashes, load test).</summary>
    private int FindingDrops() => Checks.Count(c =>
        c.State == BuyerCheckState.Fail &&
        (c.Id == AuthenticityCheckId || c.Id == RecentCrashesCheckId || c.Id == LoadTestCheckId));

    /// <summary>
    /// The title of the strongest Fail findings entry, or null when no
    /// finding failed. Authenticity outranks crashes, which outrank the
    /// load test: a Strong authenticity check is the finding the banner
    /// names when several fired.
    /// </summary>
    private string? StrongestFinding()
    {
        var authenticity = Checks.FirstOrDefault(c =>
            c.Id == AuthenticityCheckId && c.State == BuyerCheckState.Fail);
        if (authenticity is not null)
        {
            return authenticity.Title;
        }

        var crashes = Checks.FirstOrDefault(c =>
            c.Id == RecentCrashesCheckId && c.State == BuyerCheckState.Fail);
        if (crashes is not null)
        {
            return crashes.Title;
        }

        return Checks.FirstOrDefault(c =>
            c.Id == LoadTestCheckId && c.State == BuyerCheckState.Fail)?.Title;
    }
}
