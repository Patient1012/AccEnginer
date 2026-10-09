using System.Windows;
using System.Windows.Media;

namespace AccEngineer.UI.Controls;

/// <summary>
/// Lightweight sparkline for a numeric series. Draws only what it is given:
/// an empty series renders nothing (no invented data). No external charting
/// dependency is used.
/// </summary>
public sealed class TraceChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IEnumerable<double>),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(double),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill),
        typeof(Brush),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineThicknessProperty = DependencyProperty.Register(
        nameof(LineThickness),
        typeof(double),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double LineThickness
    {
        get => (double)GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 1 || height <= 1 || Values is null) return;

        var points = Values as IReadOnlyList<double> ?? Values.ToArray();
        if (points.Count < 2) return;

        var range = Maximum - Minimum;
        if (range <= 0) range = 1;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = points[0];
            var x = 0.0;
            var y = MapY(first, height, range);

            if (Fill is not null)
            {
                context.BeginFigure(new Point(0, height), isFilled: true, isClosed: true);
                context.LineTo(new Point(0, y), isStroked: false, isSmoothJoin: false);
            }
            else
            {
                context.BeginFigure(new Point(x, y), isFilled: false, isClosed: false);
            }

            for (var i = 1; i < points.Count; i++)
            {
                x = width * i / (points.Count - 1);
                y = MapY(points[i], height, range);
                context.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: false);
            }

            if (Fill is not null)
            {
                context.LineTo(new Point(width, height), isStroked: false, isSmoothJoin: false);
            }
        }

        geometry.Freeze();

        if (Fill is not null)
        {
            drawingContext.DrawGeometry(Fill, null, geometry);
        }

        var pen = new Pen(Stroke, LineThickness);
        if (Stroke.CanFreeze) Stroke.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    private double MapY(double value, double height, double range)
    {
        var normalized = (value - Minimum) / range;
        if (double.IsNaN(normalized) || double.IsInfinity(normalized)) normalized = 0;
        return height - Math.Clamp(normalized, 0, 1) * height;
    }
}
