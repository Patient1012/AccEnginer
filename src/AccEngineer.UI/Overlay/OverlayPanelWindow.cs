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
            Style = (Style)Application.Current.FindResource("OverlayEditOutline"),
            Visibility = Visibility.Collapsed
        };

        var grip = new Thumb
        {
            Style = (Style)Application.Current.FindResource("OverlayResizeGrip"),
            Visibility = Visibility.Collapsed
        };
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
    }
}
