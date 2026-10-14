using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Hosts a panel <see cref="FrameworkElement"/> in a transparent overlay window.
/// While editing it shows the shared outline + resize grip and drags when clicked;
/// while locked the base makes the whole window click-through.
/// </summary>
public sealed class OverlayPanelWindow : OverlayWindowBase
{
    public OverlayPanelWindow(string panelId, string title, FrameworkElement panel, OverlayPanelState defaults)
    {
        PanelId = panelId;
        Title = title;

        var root = new Grid();

        // The panel draws its own surface; we only wrap it with edit affordances.
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
}
