using System.Globalization;

namespace TrueSpec.Core.Sensors;

/// <summary>One threshold crossing, in plain English, with the time it fired.</summary>
public sealed record ThresholdAlert(
    string Label,
    double Threshold,
    string Unit,
    int DurationSeconds,
    DateTimeOffset RaisedAt,
    string Text);

/// <summary>
/// Watches one sensor value against a user-set threshold. An alert fires
/// only when the value stays at or above the threshold for the whole
/// configured duration: a single-sample spike never alerts. One alert per
/// crossing episode: while the value stays above, no second alert fires,
/// and the evaluator only re-arms after a measured value falls back below
/// the threshold. A missing reading (a gap) resets the pending timer, since
/// persistence cannot be proven from a gap, but it does not end an episode
/// that already fired: only a measured value below the threshold does that.
/// </summary>
public sealed class ThresholdAlertEvaluator
{
    /// <summary>The label used for the CPU temperature threshold and its alerts.</summary>
    public const string CpuTemperatureLabel = "CPU temperature";

    private readonly string _label;
    private readonly string _unit;
    private readonly List<ThresholdAlert> _alerts = new();
    private DateTimeOffset? _aboveSince;

    public ThresholdAlertEvaluator(string label, string unit)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("An alert label is required.", nameof(label));
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("An alert unit is required.", nameof(unit));
        }

        _label = label;
        _unit = unit;
    }

    /// <summary>The threshold, or null when alerting is off (the default: no diagnosis implied).</summary>
    public double? Threshold { get; private set; }

    /// <summary>How long the value must stay at or above the threshold before alerting, in seconds.</summary>
    public int DurationSeconds { get; private set; } = Settings.AppSettings.DefaultCpuTemperatureThresholdSeconds;

    /// <summary>True when a threshold is set.</summary>
    public bool IsEnabled => Threshold is not null;

    /// <summary>True between the alert firing and a measured value falling back below the threshold.</summary>
    public bool EpisodeActive { get; private set; }

    /// <summary>Alerts fired so far this session, oldest first, capped at 50.</summary>
    public IReadOnlyList<ThresholdAlert> Alerts => _alerts;

    /// <summary>Raised once per episode, when the alert fires.</summary>
    public event EventHandler<ThresholdAlert>? AlertRaised;

    /// <summary>
    /// Applies a new threshold from settings. A null threshold switches
    /// alerting off. The duration is clamped to the settings range
    /// (5-300s). Reconfiguring resets the pending timer and any active
    /// episode: the old threshold's crossing is no longer being watched.
    /// </summary>
    public void Configure(double? threshold, int durationSeconds)
    {
        Threshold = threshold is null || double.IsNaN(threshold.Value) || double.IsInfinity(threshold.Value)
            ? null
            : Math.Clamp(threshold.Value, Settings.AppSettings.MinCpuTemperatureThresholdC, Settings.AppSettings.MaxCpuTemperatureThresholdC);
        DurationSeconds = Math.Clamp(durationSeconds, Settings.AppSettings.MinCpuTemperatureThresholdSeconds, Settings.AppSettings.MaxCpuTemperatureThresholdSeconds);
        _aboveSince = null;
        EpisodeActive = false;
    }

    /// <summary>
    /// Feeds one sample in. Returns the alert when this sample completes a
    /// crossing episode, otherwise null.
    /// </summary>
    public ThresholdAlert? Evaluate(double? value, DateTimeOffset at)
    {
        if (Threshold is null)
        {
            _aboveSince = null;
            EpisodeActive = false;
            return null;
        }

        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            // A gap proves nothing: restart the pending timer, but do not
            // end an episode that already fired.
            _aboveSince = null;
            return null;
        }

        if (value.Value < Threshold.Value)
        {
            _aboveSince = null;
            EpisodeActive = false;
            return null;
        }

        _aboveSince ??= at;
        if (EpisodeActive)
        {
            return null;
        }

        if (at - _aboveSince.Value < TimeSpan.FromSeconds(DurationSeconds))
        {
            return null;
        }

        EpisodeActive = true;
        var durationText = DurationSeconds == 1
            ? "1 second"
            : string.Create(CultureInfo.InvariantCulture, $"{DurationSeconds} seconds");
        var alert = new ThresholdAlert(
            _label,
            Threshold.Value,
            _unit,
            DurationSeconds,
            at,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{_label} exceeded {Threshold.Value:0.#} {_unit} for {durationText} at {at:HH:mm}."));
        _alerts.Add(alert);
        if (_alerts.Count > 50)
        {
            _alerts.RemoveAt(0);
        }

        AlertRaised?.Invoke(this, alert);
        return alert;
    }
}
