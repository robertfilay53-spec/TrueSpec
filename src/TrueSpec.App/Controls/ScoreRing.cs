using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TrueSpec.App.Controls;

/// <summary>
/// Draws the 0 to 100 hero score as a ring. The arc length is the score, the
/// color comes from the health band, and the track stays neutral.
/// </summary>
public sealed class ScoreRing : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ScoreRing, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<ScoreRing, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ScoreRing, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ScoreRing, double>(nameof(Thickness), 12d);

    static ScoreRing()
    {
        AffectsRender<ScoreRing>(
            ValueProperty,
            RingBrushProperty,
            TrackBrushProperty,
            ThicknessProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush? RingBrush
    {
        get => GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0 || Thickness <= 0)
        {
            return;
        }

        var center = new Point(Bounds.Width / 2d, Bounds.Height / 2d);
        var radius = (size / 2d) - (Thickness / 2d);
        if (radius <= 0)
        {
            return;
        }

        var track = TrackBrush ?? new SolidColorBrush(Color.Parse("#2A2E33"));
        context.DrawEllipse(null, new Pen(track, Thickness, lineCap: PenLineCap.Round), center, radius, radius);

        var clamped = Math.Clamp(Value, 0d, 100d);
        if (clamped <= 0.01)
        {
            return;
        }

        var sweep = clamped / 100d * 360d;
        var startAngle = -90d;
        var endAngle = startAngle + sweep;

        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            var start = PointOnCircle(center, radius, startAngle);
            stream.BeginFigure(start, false);
            if (sweep >= 359.99)
            {
                var mid = PointOnCircle(center, radius, startAngle + 180d);
                stream.ArcTo(mid, new Size(radius, radius), 0d, false, SweepDirection.Clockwise);
                stream.ArcTo(start, new Size(radius, radius), 0d, false, SweepDirection.Clockwise);
            }
            else
            {
                var end = PointOnCircle(center, radius, endAngle);
                stream.ArcTo(end, new Size(radius, radius), 0d, sweep > 180d, SweepDirection.Clockwise);
            }

            stream.EndFigure(false);
        }

        var ring = RingBrush ?? new SolidColorBrush(Color.Parse("#22C55E"));
        context.DrawGeometry(null, new Pen(ring, Thickness, lineCap: PenLineCap.Round), geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new Point(
            center.X + (radius * Math.Cos(radians)),
            center.Y + (radius * Math.Sin(radians)));
    }
}
