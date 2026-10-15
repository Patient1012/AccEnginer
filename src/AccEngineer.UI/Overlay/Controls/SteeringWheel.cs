using System;
using System.Windows;
using System.Windows.Media;

namespace AccEngineer.UI.Overlay.Controls;

/// <summary>
/// Circular steering-wheel graphic for the inputs panel. The rim, three spokes and
/// central hub rotate to show the current steering angle; a short fixed red marker
/// sits on the upper-right of the rim as the straight-ahead reference. A missing or
/// non-finite input holds the wheel centred rather than inventing a deflection.
/// </summary>
public sealed class SteeringWheel : FrameworkElement
{
    /// <summary>Normalised steering input, -1 (full left) to +1 (full right).</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(SteeringWheel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Wheel rotation in degrees at full lock (|Value| = 1).</summary>
    public static readonly DependencyProperty MaxRotationProperty = DependencyProperty.Register(
        nameof(MaxRotation), typeof(double), typeof(SteeringWheel),
        new FrameworkPropertyMetadata(120.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RimBrushProperty = DependencyProperty.Register(
        nameof(RimBrush), typeof(Brush), typeof(SteeringWheel),
        new FrameworkPropertyMetadata(Brushes.WhiteSmoke, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SpokeBrushProperty = DependencyProperty.Register(
        nameof(SpokeBrush), typeof(Brush), typeof(SteeringWheel),
        new FrameworkPropertyMetadata(Brushes.Gainsboro, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HubBrushProperty = DependencyProperty.Register(
        nameof(HubBrush), typeof(Brush), typeof(SteeringWheel),
        new FrameworkPropertyMetadata(Brushes.WhiteSmoke, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkerBrushProperty = DependencyProperty.Register(
        nameof(MarkerBrush), typeof(Brush), typeof(SteeringWheel),
        new FrameworkPropertyMetadata(Brushes.OrangeRed, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double MaxRotation
    {
        get => (double)GetValue(MaxRotationProperty);
        set => SetValue(MaxRotationProperty, value);
    }

    public Brush RimBrush
    {
        get => (Brush)GetValue(RimBrushProperty);
        set => SetValue(RimBrushProperty, value);
    }

    public Brush SpokeBrush
    {
        get => (Brush)GetValue(SpokeBrushProperty);
        set => SetValue(SpokeBrushProperty, value);
    }

    public Brush HubBrush
    {
        get => (Brush)GetValue(HubBrushProperty);
        set => SetValue(HubBrushProperty, value);
    }

    public Brush MarkerBrush
    {
        get => (Brush)GetValue(MarkerBrushProperty);
        set => SetValue(MarkerBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        var centre = new Point(width / 2, height / 2);
        var radius = Math.Min(width, height) / 2 - 2;
        if (radius <= 2) return;

        var value = Value;
        if (double.IsNaN(value) || double.IsInfinity(value)) value = 0;
        value = Math.Clamp(value, -1, 1);
        var rotation = value * MaxRotation;

        var rimPen = new Pen(RimBrush, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var spokePen = new Pen(SpokeBrush, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        // Rim (fixed) plus the straight-ahead reference marker on the upper-right.
        drawingContext.DrawEllipse(null, rimPen, centre, radius, radius);
        DrawReferenceMarker(drawingContext, centre, radius);

        // Rotating group: three spokes and the hub.
        drawingContext.PushTransform(new RotateTransform(rotation, centre.X, centre.Y));
        var hubRadius = radius * 0.22;
        var spokeLength = radius - 2;
        drawingContext.DrawLine(spokePen, centre, PointOnCircle(centre, spokeLength, 180)); // left
        drawingContext.DrawLine(spokePen, centre, PointOnCircle(centre, spokeLength, 0));   // right
        drawingContext.DrawLine(spokePen, centre, PointOnCircle(centre, spokeLength, 90));  // down
        drawingContext.DrawEllipse(HubBrush, null, centre, hubRadius, hubRadius);
        drawingContext.Pop();
    }

    private void DrawReferenceMarker(DrawingContext drawingContext, Point centre, double radius)
    {
        // Short arc segment sitting on the rim, just right of top dead centre.
        var pen = new Pen(MarkerBrush, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (MarkerBrush.CanFreeze) MarkerBrush.Freeze();
        if (pen.CanFreeze) pen.Freeze();

        var startAngle = -70.0; // degrees, measured clockwise from +X
        var endAngle = -20.0;
        var start = PointOnCircle(centre, radius, startAngle);
        var end = PointOnCircle(centre, radius, endAngle);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, isFilled: false, isClosed: false);
            context.ArcTo(end, new Size(radius, radius), 0,
                isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    /// <summary>Point on a circle using degrees clockwise from the +X axis.</summary>
    private static Point PointOnCircle(Point centre, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        return new Point(centre.X + radius * Math.Cos(radians), centre.Y + radius * Math.Sin(radians));
    }
}
