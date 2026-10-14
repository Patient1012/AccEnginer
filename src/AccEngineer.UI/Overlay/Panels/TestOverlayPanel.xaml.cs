using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>Phase 1 classic-style test overlay panel.</summary>
public partial class TestOverlayPanel : OverlayWindowBase
{
    public TestOverlayPanel()
    {
        InitializeComponent();
        PanelId = "test";
    }

    private void OnPanelMouseDown(object sender, MouseButtonEventArgs e) => PanelMouseLeftButtonDown(sender, e);

    private void OnResizeGripDragDelta(object sender, DragDeltaEventArgs e) => ResizeGripDragDelta(sender, e);
}
