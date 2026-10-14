using System.Windows;
using System.Windows.Media;

namespace AccEngineer.UI.Overlay.Controls;

/// <summary>
/// Horizontal 0..1 bar used for throttle, brake and fuel style readouts. Draws a
/// recessed track plus a fill. A NaN or non-positive value renders an empty bar
/// rather than a misleading sliver of data.
/// </summary>
public sealed class OverlayBar : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(OverlayBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(OverlayBar),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(OverlayBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush? Track
    {
        get => (Brush?)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        if (Track is not null)
        {
            drawingContext.DrawRectangle(Track, null, new Rect(0, 0, width, height));
        }

        var value = Value;
        if (double.IsNaN(value) || value <= 0) return;

        var fillWidth = Math.Clamp(value, 0, 1) * width;
        if (fillWidth <= 0) return;
        drawingContext.DrawRectangle(Fill, null, new Rect(0, 0, fillWidth, height));
    }
}
