using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TrueSpec.App.Controls;

/// <summary>
/// A small temperature-over-time chart for the History page, built on the
/// same rendering pattern as <see cref="Sparkline"/>. Two differences
/// matter: values are nullable, so a gap (a sample where the sensor did not
/// report) breaks the line instead of being connected across, and an
/// optional threshold value draws a dashed line at that temperature.
/// Samples are plotted in order; the line is never interpolated across a gap.
/// </summary>
public sealed class SensorLogChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<double?>?> ValuesProperty =
        AvaloniaProperty.Register<SensorLogChart, IReadOnlyList<double?>?>(nameof(Values));

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
            ValuesProperty,
            ThresholdValueProperty,
            StrokeProperty,
            ThresholdStrokeProperty,
            StrokeThicknessProperty);
    }

    public IReadOnlyList<double?>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
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
        var values = Values;
        if (values is null || Bounds.Width <= 1 || Bounds.Height <= 1)
        {
            return;
        }

        var measuredCount = 0;
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var value in values)
        {
            if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                continue;
            }

            measuredCount++;
            min = Math.Min(min, value.Value);
            max = Math.Max(max, value.Value);
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

        double X(int index) => padding + (usableWidth * index / (values.Count - 1d));
        double Y(double value) => padding + (usableHeight * (1d - ((value - min) / (max - min))));

        var stroke = Stroke ?? new SolidColorBrush(Color.Parse("#22C55E"));
        var pen = new Pen(stroke, StrokeThickness, lineCap: PenLineCap.Round);

        // One geometry per run of measured samples: a gap ends the segment,
        // so the line never connects across missing data.
        var segment = new List<Point>();
        void FlushSegment()
        {
            if (segment.Count < 2)
            {
                segment.Clear();
                return;
            }

            var line = new StreamGeometry();
            using (var stream = line.Open())
            {
                stream.BeginFigure(segment[0], false);
                for (var i = 1; i < segment.Count; i++)
                {
                    stream.LineTo(segment[i]);
                }

                stream.EndFigure(false);
            }

            context.DrawGeometry(null, pen, line);
            segment.Clear();
        }

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                FlushSegment();
                continue;
            }

            segment.Add(new Point(X(i), Y(value.Value)));
        }

        FlushSegment();

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
