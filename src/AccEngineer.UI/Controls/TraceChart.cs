using System.Globalization;
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

    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid),
        typeof(bool),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush),
        typeof(Brush),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridRowsProperty = DependencyProperty.Register(
        nameof(GridRows),
        typeof(int),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridColumnsProperty = DependencyProperty.Register(
        nameof(GridColumns),
        typeof(int),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(6, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowYAxisProperty = DependencyProperty.Register(
        nameof(ShowYAxis),
        typeof(bool),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AxisBrushProperty = DependencyProperty.Register(
        nameof(AxisBrush),
        typeof(Brush),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AxisTextBrushProperty = DependencyProperty.Register(
        nameof(AxisTextBrush),
        typeof(Brush),
        typeof(TraceChart),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

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

    /// <summary>Draws a faint evenly spaced grid behind the trace.</summary>
    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public Brush GridBrush
    {
        get => (Brush)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public int GridRows
    {
        get => (int)GetValue(GridRowsProperty);
        set => SetValue(GridRowsProperty, value);
    }

    public int GridColumns
    {
        get => (int)GetValue(GridColumnsProperty);
        set => SetValue(GridColumnsProperty, value);
    }

    /// <summary>
    /// Draws a labelled y-axis scale (minimum, midpoint, maximum) in a left gutter.
    /// Off by default so existing sparklines keep their full-width, label-free look.
    /// </summary>
    public bool ShowYAxis
    {
        get => (bool)GetValue(ShowYAxisProperty);
        set => SetValue(ShowYAxisProperty, value);
    }

    /// <summary>Brush for the small tick marks next to the scale labels.</summary>
    public Brush AxisBrush
    {
        get => (Brush)GetValue(AxisBrushProperty);
        set => SetValue(AxisBrushProperty, value);
    }

    public Brush AxisTextBrush
    {
        get => (Brush)GetValue(AxisTextBrushProperty);
        set => SetValue(AxisTextBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 1 || height <= 1 || Values is null) return;

        var points = Values as IReadOnlyList<double> ?? Values.ToArray();

        // Optional left gutter holding the scale labels; the trace plots to its right.
        var gutter = 0.0;
        ScaleLabels? scale = null;
        if (ShowYAxis && points.Count >= 2 && double.IsFinite(Minimum) && double.IsFinite(Maximum))
        {
            var built = BuildScale();
            if (built.GutterWidth < width - 24)
            {
                scale = built;
                gutter = built.GutterWidth;
            }
        }

        if (ShowGrid)
        {
            DrawGrid(drawingContext, width, height, gutter);
        }

        var plotWidth = width - gutter;
        if (plotWidth <= 1 || points.Count < 2) return;

        var range = Maximum - Minimum;
        if (range <= 0) range = 1;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = points[0];
            var y = MapY(first, height, range);

            if (Fill is not null)
            {
                context.BeginFigure(new Point(gutter, height), isFilled: true, isClosed: true);
                context.LineTo(new Point(gutter, y), isStroked: false, isSmoothJoin: false);
            }
            else
            {
                context.BeginFigure(new Point(gutter, y), isFilled: false, isClosed: false);
            }

            for (var i = 1; i < points.Count; i++)
            {
                var x = gutter + plotWidth * i / (points.Count - 1);
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

        if (scale is not null)
        {
            DrawScale(drawingContext, scale, gutter, height);
        }
    }

    private sealed record ScaleLabels(
        FormattedText Top,
        FormattedText Middle,
        FormattedText Bottom,
        double GutterWidth);

    private static readonly Typeface AxisTypeface = new(
        new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);

    private ScaleLabels BuildScale()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        FormattedText Label(double value) => new(
            FormatScaleLabel(value),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            AxisTypeface,
            9,
            AxisTextBrush,
            dpi.PixelsPerDip);

        var top = Label(Maximum);
        var middle = Label((Minimum + Maximum) / 2);
        var bottom = Label(Minimum);

        var labelWidth = Math.Max(top.Width, Math.Max(middle.Width, bottom.Width));

        // A fixed floor keeps the traces of several stacked charts starting at the
        // same x even when their label widths differ (e.g. "320" vs "0.5").
        return new ScaleLabels(top, middle, bottom, Math.Max(38, Math.Ceiling(labelWidth) + 12));
    }

    private void DrawScale(DrawingContext drawingContext, ScaleLabels scale, double gutter, double height)
    {
        if (AxisBrush.CanFreeze) AxisBrush.Freeze();
        var tickPen = new Pen(AxisBrush, 1);
        if (tickPen.CanFreeze) tickPen.Freeze();

        var labelRight = gutter - 8;
        var tickStart = gutter - 5;

        DrawEntry(scale.Top, 0);
        DrawEntry(scale.Middle, height / 2);
        DrawEntry(scale.Bottom, height);

        void DrawEntry(FormattedText text, double y)
        {
            var centerY = Math.Clamp(y, text.Height / 2, Math.Max(text.Height / 2, height - text.Height / 2));
            drawingContext.DrawText(text, new Point(labelRight - text.Width, centerY - text.Height / 2));
            drawingContext.DrawLine(tickPen, new Point(tickStart, centerY), new Point(gutter, centerY));
        }
    }

    private static string FormatScaleLabel(double value)
    {
        var rounded = Math.Round(value, 1);
        return Math.Abs(rounded % 1) < 1e-9
            ? rounded.ToString("0", CultureInfo.InvariantCulture)
            : rounded.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private void DrawGrid(DrawingContext drawingContext, double width, double height, double gutter)
    {
        var pen = new Pen(GridBrush, 1);
        if (GridBrush.CanFreeze) GridBrush.Freeze();
        if (pen.CanFreeze) pen.Freeze();

        var rows = Math.Max(1, GridRows);
        for (var r = 1; r < rows; r++)
        {
            var y = height * r / rows;
            drawingContext.DrawLine(pen, new Point(gutter, y), new Point(width, y));
        }

        var columns = Math.Max(1, GridColumns);
        for (var c = 1; c < columns; c++)
        {
            var x = gutter + (width - gutter) * c / columns;
            drawingContext.DrawLine(pen, new Point(x, 0), new Point(x, height));
        }
    }

    private double MapY(double value, double height, double range)
    {
        var normalized = (value - Minimum) / range;
        if (double.IsNaN(normalized) || double.IsInfinity(normalized)) normalized = 0;
        return height - Math.Clamp(normalized, 0, 1) * height;
    }
}
