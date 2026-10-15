using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AccEngineer.UI.Overlay.Controls;

/// <summary>
/// 0..1 bar used for throttle, brake and fuel style readouts. Draws a recessed
/// track plus a fill, either horizontally (left to right) or vertically (bottom
/// to top). A NaN or non-positive value renders an empty bar rather than a
/// misleading sliver of data.
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

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(OverlayBar),
        new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsRender));

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

    /// <summary>Fill direction: Horizontal grows left-to-right, Vertical grows bottom-to-top.</summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
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

        var fraction = Math.Clamp(value, 0, 1);
        var rect = Orientation == Orientation.Vertical
            ? new Rect(0, height * (1 - fraction), width, height * fraction)
            : new Rect(0, 0, width * fraction, height);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        drawingContext.DrawRectangle(Fill, null, rect);
    }
}
