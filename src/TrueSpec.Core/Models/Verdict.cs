namespace TrueSpec.Core.Models;

/// <summary>
/// Overall machine verdict. "New" is reserved for a machine that scores
/// Like-new AND passes every applicable buyer check; a score alone can only
/// say "Like new" or below, and any failed or warning buyer check caps the
/// verdict at "Used - healthy" (see ScoringEngine.CombineVerdict).
/// </summary>
public enum Verdict
{
    NotMeasurable,
    New,
    LikeNew,
    UsedHealthy,
    Worn,
    ReplaceSoon,
}

public static class VerdictExtensions
{
    public static string ToDisplayText(this Verdict verdict) => verdict switch
    {
        Verdict.New => "New",
        Verdict.LikeNew => "Like new",
        Verdict.UsedHealthy => "Used - healthy",
        Verdict.Worn => "Worn",
        Verdict.ReplaceSoon => "Replace soon",
        _ => "Not measurable",
    };

    public static HealthStatus ToHealthStatus(this Verdict verdict) => verdict switch
    {
        Verdict.New or Verdict.LikeNew => HealthStatus.Healthy,
        Verdict.UsedHealthy or Verdict.Worn => HealthStatus.Watch,
        Verdict.ReplaceSoon => HealthStatus.Problem,
        _ => HealthStatus.NotMeasurable,
    };
}
