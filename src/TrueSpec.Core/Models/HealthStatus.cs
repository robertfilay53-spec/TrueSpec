namespace TrueSpec.Core.Models;

/// <summary>
/// The four health bands used everywhere in the product. The band is derived
/// only from a measured percentage. A component with no sensor is never given
/// a made up number; it is reported as <see cref="NotMeasurable"/>.
/// </summary>
public enum HealthStatus
{
    NotMeasurable,
    Healthy,
    Watch,
    Problem,
}

public static class HealthStatusExtensions
{
    public const double HealthyThreshold = 80d;
    public const double WatchThreshold = 60d;

    public static HealthStatus FromHealth(double? healthPercent)
    {
        if (healthPercent is null || double.IsNaN(healthPercent.Value))
        {
            return HealthStatus.NotMeasurable;
        }

        return healthPercent.Value switch
        {
            >= HealthyThreshold => HealthStatus.Healthy,
            >= WatchThreshold => HealthStatus.Watch,
            _ => HealthStatus.Problem,
        };
    }

    /// <summary>Plain English label shown to the buyer.</summary>
    public static string ToDisplayText(this HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "Healthy",
        HealthStatus.Watch => "Watch",
        HealthStatus.Problem => "Problem",
        _ => "Not measurable",
    };

    /// <summary>Theme resource key used for the status dot, bar and ring.</summary>
    public static string ToBrushKey(this HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "HealthyBrush",
        HealthStatus.Watch => "WatchBrush",
        HealthStatus.Problem => "ProblemBrush",
        _ => "NotMeasurableBrush",
    };
}
