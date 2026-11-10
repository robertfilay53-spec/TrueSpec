namespace TrueSpec.Core.Models;

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
    public BuyerCheckState OverallState
    {
        get
        {
            if (Checks.Any(c => c.State == BuyerCheckState.Fail))
            {
                return BuyerCheckState.Fail;
            }

            if (Checks.Any(c => c.State == BuyerCheckState.Warning))
            {
                return BuyerCheckState.Warning;
            }

            return Checks.Count == 0 ? BuyerCheckState.NotApplicable : BuyerCheckState.Pass;
        }
    }

    public string BannerText => OverallState switch
    {
        BuyerCheckState.Pass => "PASS - this machine matches a new laptop",
        BuyerCheckState.Warning => "WARNING - check the highlighted items",
        BuyerCheckState.Fail => "FAIL - this machine does not look new",
        _ => "Not enough data for a verdict",
    };
}
