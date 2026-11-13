using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TrueSpec.Core.Sensors;

namespace TrueSpec.App.Controls;

/// <summary>
/// A small temperature-over-time chart for the History page, built on the
/// same rendering pattern as <see cref="Sparkline"/>. Two differences
/// matter: values are nullable in the source log, so a gap (a sample where
/// the sensor did not report, or a stretch where logging was not running)
/// breaks the line instead of being connected across, and an optional
/// threshold value draws a dashed line at that temperature. Segments arrive
/// pre-mapped by <see cref="SensorLogChartMapper"/>: the horizontal axis is
/// time, not sample count, so two samples an hour apart do not look
/// adjacent. The line is never interpolated across a gap.
/// </summary>
public sealed class SensorLogChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<IReadOnlyList<SensorLogChartPoint>>?> SegmentsProperty =
        AvaloniaProperty.Register<SensorLogChart, IReadOnlyList<IReadOnlyList<SensorLogChartPoint>>?>(nameof(Segments));

    public static readonly StyledProperty<double?> ThresholdValueProperty =
        AvaloniaProperty.Register<SensorLogChart, double?>(nameof(ThresholdValue));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<SensorLogChart, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> ThresholdStrokeProperty =
        AvaloniaProperty.Register<SensorLogChart, IBrush?>(nameof(ThresholdStroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<SensorLogChart, double>(nameof(StrokeThickness), 2d);

    static SensorLogChart()
    {
        AffectsRender<SensorLogChart>(
            SegmentsProperty,
            ThresholdValueProperty,
            StrokeProperty,
            ThresholdStrokeProperty,
            StrokeThicknessProperty);
    }

    public IReadOnlyList<IReadOnlyList<SensorLogChartPoint>>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public double? ThresholdValue
    {
        get => GetValue(ThresholdValueProperty);
        set => SetValue(ThresholdValueProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? ThresholdStroke
    {
        get => GetValue(ThresholdStrokeProperty);
        set => SetValue(ThresholdStrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var segments = Segments;
        if (segments is null || Bounds.Width <= 1 || Bounds.Height <= 1)
        {
            return;
        }

        var measuredCount = 0;
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var segment in segments)
        {
            foreach (var point in segment)
            {
                if (double.IsNaN(point.Value) || double.IsInfinity(point.Value))
                {
                    continue;
                }

                measuredCount++;
                min = Math.Min(min, point.Value);
                max = Math.Max(max, point.Value);
            }
        }

        if (measuredCount < 2)
        {
            return;
        }

        if (ThresholdValue is { } threshold && !double.IsNaN(threshold) && !double.IsInfinity(threshold))
        {
            min = Math.Min(min, threshold);
            max = Math.Max(max, threshold);
        }

        if (Math.Abs(max - min) < 0.0001d)
        {
            max = min + 1d;
        }

        var padding = StrokeThickness + 2d;
        var width = Bounds.Width;
        var height = Bounds.Height;
        var usableWidth = Math.Max(1d, width - (padding * 2d));
        var usableHeight = Math.Max(1d, height - (padding * 2d));

        double X(double fraction) => padding + (usableWidth * Math.Clamp(fraction, 0d, 1d));
        double Y(double value) => padding + (usableHeight * (1d - ((value - min) / (max - min))));

        var stroke = Stroke ?? new SolidColorBrush(Color.Parse("#22C55E"));
        var pen = new Pen(stroke, StrokeThickness, lineCap: PenLineCap.Round);

        // Segments arrive pre-split at every gap, so each one draws as a
        // single line. A segment with fewer than two points cannot draw
        // and is skipped.
        foreach (var segment in segments)
        {
            if (segment.Count < 2)
            {
                continue;
            }

            var line = new StreamGeometry();
            using (var stream = line.Open())
            {
                stream.BeginFigure(new Point(X(segment[0].XFraction), Y(segment[0].Value)), false);
                for (var i = 1; i < segment.Count; i++)
                {
                    stream.LineTo(new Point(X(segment[i].XFraction), Y(segment[i].Value)));
                }

                stream.EndFigure(false);
            }

            context.DrawGeometry(null, pen, line);
        }

        if (ThresholdValue is { } thresholdValue && !double.IsNaN(thresholdValue) && !double.IsInfinity(thresholdValue))
        {
            var thresholdBrush = ThresholdStroke ?? new SolidColorBrush(Color.Parse("#F59E0B"));
            var thresholdPen = new Pen(thresholdBrush, 1d, dashStyle: DashStyle.Dash);
            var y = Y(thresholdValue);
            var line = new StreamGeometry();
            using (var stream = line.Open())
            {
                stream.BeginFigure(new Point(padding, y), false);
                stream.LineTo(new Point(width - padding, y));
                stream.EndFigure(false);
            }

            context.DrawGeometry(null, thresholdPen, line);
        }
    }
}
