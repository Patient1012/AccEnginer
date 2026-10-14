using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Base window for overlay panels: transparent, borderless, always on top and
/// non-activating. While locked the window is click-through so input reaches the
/// simulator; in edit mode it accepts a drag (move) and a resize grip.
/// </summary>
public abstract class OverlayWindowBase : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register(
        nameof(IsEditing), typeof(bool), typeof(OverlayWindowBase),
        new PropertyMetadata(false, OnIsEditingChanged));

    public static readonly DependencyProperty IsClickThroughProperty = DependencyProperty.Register(
        nameof(IsClickThrough), typeof(bool), typeof(OverlayWindowBase),
        new PropertyMetadata(true, OnIsClickThroughChanged));

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    private bool _allowClose;

    protected OverlayWindowBase()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SnapsToDevicePixels = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        MinWidth = 120;
        MinHeight = 80;
    }

    /// <summary>Stable identity used to persist this panel's placement.</summary>
    public string PanelId { get; set; } = string.Empty;

    /// <summary>True while the panel accepts mouse input for moving and resizing.</summary>
    public bool IsEditing
    {
        get => (bool)GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    /// <summary>True while mouse input passes through to the simulator.</summary>
    public bool IsClickThrough
    {
        get => (bool)GetValue(IsClickThroughProperty);
        set => SetValue(IsClickThroughProperty, value);
    }

    /// <summary>Raised when placement or edit state changes so it can be persisted.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>Allows the window to close for real (used during shutdown).</summary>
    public void AllowClose() => _allowClose = true;

    /// <summary>
    /// Applies a uniform content scale (used by the layout editor's font scale). The
    /// base does nothing; panel-hosting windows override it.
    /// </summary>
    public virtual void SetContentScale(double scale) { }

    /// <summary>
    /// Captures the current placement and appearance for persistence. Sanitized because
    /// Left/Top/Width/Height are NaN until the window is first arranged, and this runs
    /// from the layout-changed handler during initial show.
    /// </summary>
    public OverlayPanelState CaptureState() => new OverlayPanelState
    {
        Id = PanelId,
        Visible = IsVisible,
        X = Left,
        Y = Top,
        Width = Width,
        Height = Height,
        Opacity = Opacity
    }.Sanitize();

    /// <summary>Applies persisted placement and appearance, clamped to the visible desktop.</summary>
    public void ApplyState(OverlayPanelState state)
    {
        Width = Math.Max(MinWidth, state.Width);
        Height = Math.Max(MinHeight, state.Height);
        Opacity = Math.Clamp(state.Opacity, 0.2, 1.0);
        var (x, y) = ClampToVirtualScreen(state.X, state.Y, Width, Height);
        Left = x;
        Top = y;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyExStyles();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        base.OnClosing(e);
    }

    protected void PanelMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEditing) return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if the mouse button was already released.
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected void ResizeGripDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!IsEditing) return;

        Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinHeight, Height + e.VerticalChange);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnIsEditingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var window = (OverlayWindowBase)d;
        window.IsClickThrough = !(bool)e.NewValue;
        window.LayoutChanged?.Invoke(window, EventArgs.Empty);
    }

    private static void OnIsClickThroughChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OverlayWindowBase)d).ApplyExStyles();

    private void ApplyExStyles()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var style = GetWindowLong(hwnd, GwlExStyle);
        style |= WsExNoActivate | WsExToolWindow;
        style = IsClickThrough ? style | WsExTransparent : style & ~WsExTransparent;
        SetWindowLong(hwnd, GwlExStyle, style);
    }

    private static (double X, double Y) ClampToVirtualScreen(double x, double y, double width, double height)
    {
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var right = left + SystemParameters.VirtualScreenWidth;
        var bottom = top + SystemParameters.VirtualScreenHeight;

        var clampedX = Math.Clamp(x, left, Math.Max(left, right - width));
        var clampedY = Math.Clamp(y, top, Math.Max(top, bottom - height));
        return (clampedX, clampedY);
    }
}
