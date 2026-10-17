using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Hosts a panel <see cref="FrameworkElement"/> in a transparent overlay window.
/// While editing it shows the shared outline + resize grip and drags when clicked;
/// while locked the base makes the whole window click-through.
/// </summary>
public sealed class OverlayPanelWindow : OverlayWindowBase
{
    private readonly FrameworkElement _panel;

    public OverlayPanelWindow(string panelId, string title, FrameworkElement panel, OverlayPanelState defaults)
    {
        PanelId = panelId;
        Title = title;

        var root = new Grid();

        // The panel draws its own surface; we only wrap it with edit affordances.
        _panel = panel;
        root.Children.Add(panel);

        var outline = new Border
        {
            Visibility = Visibility.Collapsed
        };

        // Resource references (not a one-shot FindResource) so a live theme swap
        // restyles the edit affordances like the rest of the overlay.
        outline.SetResourceReference(StyleProperty, "OverlayEditOutline");

        var grip = new Thumb
        {
            Visibility = Visibility.Collapsed
        };
        grip.SetResourceReference(StyleProperty, "OverlayResizeGrip");
        grip.DragDelta += ResizeGripDragDelta;

        root.Children.Add(outline);
        root.Children.Add(grip);

        // Clicking the panel drags the window, but only takes effect while editing
        // (PanelMouseLeftButtonDown checks IsEditing).
        root.MouseLeftButtonDown += PanelMouseLeftButtonDown;

        Content = root;

        LayoutChanged += (_, _) =>
        {
            var visibility = IsEditing ? Visibility.Visible : Visibility.Collapsed;
            outline.Visibility = visibility;
            grip.Visibility = visibility;
        };

        ApplyState(defaults);
    }

    /// <summary>
    /// Scales the panel content uniformly (the editor's font scale). Uses a layout
    /// transform from the top-left so text and graphics grow together.
    /// </summary>
    public override void SetContentScale(double scale)
    {
        var clamped = Math.Clamp(scale, 0.6, 2.0);
        _panel.LayoutTransform = Math.Abs(clamped - 1.0) < 0.001
            ? Transform.Identity
            : new ScaleTransform(clamped, clamped);

        // Scaled content can need more room than the window has, so re-derive the
        // minimum immediately; otherwise the panel overflows and its columns collide.
        SyncMinimumToContent();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        SyncMinimumToContent();
    }

    /// <summary>
    /// Keeps the window big enough for its own content. A frame smaller than the content
    /// clips and overlaps (shrinking the window, or scaling the content up, would silently
    /// break the layout), so the minimum follows the measured size. The visible frame is
    /// the larger of the user's requested size and what the content needs: scaling content
    /// up grows the window, scaling it back down returns to the user's size, and the saved
    /// size stays the user's choice.
    /// </summary>
    private void SyncMinimumToContent()
    {
        if (Content is not UIElement root) return;

        // Force a re-measure at infinite size to learn the true desired size (this includes
        // the panel's layout transform). Invalidation has to happen first: Measure on an
        // element whose cache is still valid would hand back the pre-transform size. Then
        // invalidate again so the normal layout pass runs with the window's constraints.
        root.InvalidateMeasure();
        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        root.InvalidateMeasure();

        var desiredWidth = Math.Ceiling(root.DesiredSize.Width);
        var desiredHeight = Math.Ceiling(root.DesiredSize.Height);
        if (!double.IsFinite(desiredWidth) || !double.IsFinite(desiredHeight)) return;
        if (desiredWidth <= 0 || desiredHeight <= 0) return;

        // Never below the base minimums, never beyond a sane cap (guards a degenerate
        // measurement from ballooning the window).
        var minWidth = Math.Clamp(desiredWidth, 120, 4000);

        var changed = false;

        if (MinWidth != minWidth)
        {
            MinWidth = minWidth;
            changed = true;
        }

        var userWidth = double.IsFinite(UserWidth) ? UserWidth : Width;
        var targetWidth = Math.Max(userWidth, minWidth);

        if (!double.IsFinite(Width) || Math.Abs(Width - targetWidth) > 0.5)
        {
            Width = targetWidth;
            changed = true;
        }

        if (AutoFitHeight)
        {
            // Height is owned by SizeToContent: the measured content drives it every
            // layout pass, so there is never dead space below the last row nor clipping
            // when rows are added back. Only the header floor needs to stay reachable.
            if (MinHeight != AutoFitMinHeight)
            {
                MinHeight = AutoFitMinHeight;
                changed = true;
            }
        }
        else
        {
            var minHeight = Math.Clamp(desiredHeight, 80, 4000);
            var userHeight = double.IsFinite(UserHeight) ? UserHeight : Height;
            var targetHeight = Math.Max(userHeight, minHeight);

            if (MinHeight != minHeight)
            {
                MinHeight = minHeight;
                changed = true;
            }

            if (!double.IsFinite(Height) || Math.Abs(Height - targetHeight) > 0.5)
            {
                Height = targetHeight;
                changed = true;
            }
        }

        // Only persists when the frame actually moved; CaptureState reports the user
        // frame, so a content-driven growth never inflates the stored size.
        if (changed) RaiseLayoutChanged();
    }
}
