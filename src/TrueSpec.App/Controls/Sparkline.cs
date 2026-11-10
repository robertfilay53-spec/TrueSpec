using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TrueSpec.App.Controls;

/// <summary>
/// A small live line chart for the sparkline strip. Values are expected to be
/// replaced every refresh so the control redraws without collection tracking.
/// </summary>
public sealed class Sparkline : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<Sparkline, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(StrokeThickness), 2d);

    static Sparkline()
    {
        AffectsRender<Sparkline>(
            ValuesProperty,
            StrokeProperty,
            FillProperty,
            StrokeThicknessProperty);
    }

    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var values = Values;
        if (values is null || values.Count < 2 || Bounds.Width <= 1 || Bounds.Height <= 1)
        {
            return;
        }

        var padding = StrokeThickness + 2d;
        var width = Bounds.Width;
        var height = Bounds.Height;
        var usableWidth = Math.Max(1d, width - (padding * 2d));
        var usableHeight = Math.Max(1d, height - (padding * 2d));

        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var value in values)
        {
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        if (Math.Abs(max - min) < 0.0001d)
        {
            max = min + 1d;
        }

        var points = new Point[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var x = padding + (usableWidth * i / (values.Count - 1d));
            var normalized = (values[i] - min) / (max - min);
            var y = padding + (usableHeight * (1d - normalized));
            points[i] = new Point(x, y);
        }

        var line = new StreamGeometry();
        using (var stream = line.Open())
        {
            stream.BeginFigure(points[0], false);
            for (var i = 1; i < points.Length; i++)
            {
                stream.LineTo(points[i]);
            }

            stream.EndFigure(false);
        }

        if (Fill is not null)
        {
            var area = new StreamGeometry();
            using (var stream = area.Open())
            {
                stream.BeginFigure(new Point(points[0].X, height - padding), true);
                foreach (var point in points)
                {
                    stream.LineTo(point);
                }

                stream.LineTo(new Point(points[^1].X, height - padding));
                stream.EndFigure(true);
            }

            context.DrawGeometry(Fill, null, area);
        }

        var stroke = Stroke ?? new SolidColorBrush(Color.Parse("#22C55E"));
        context.DrawGeometry(null, new Pen(stroke, StrokeThickness, lineCap: PenLineCap.Round), line);
    }
}
