using System.IO;
using AccEngineer.UI.Overlay.Panels;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Owns the overlay panels and their persisted layout. Phase 1 manages a single
/// classic-style test panel; later phases add the timing, standings, map and input
/// panels behind this same manager.
/// </summary>
public sealed class OverlayManager : IDisposable
{
    private const string TestPanelId = "test";

    private readonly LiveTelemetryService _telemetry;
    private readonly OverlayLayoutStore _store;
    private readonly OverlayHotkeys _hotkeys;
    private readonly OverlayLayout _layout;

    private TestOverlayPanel? _panel;
    private bool _disposed;

    public OverlayManager(LiveTelemetryService telemetry)
    {
        _telemetry = telemetry;
        _store = new OverlayLayoutStore();
        _layout = _store.Load();

        _hotkeys = new OverlayHotkeys();
        _hotkeys.ToggleRequested += Toggle;
        _hotkeys.EditRequested += ToggleEdit;
        _hotkeys.Attach();

        if (_layout.GetOrCreate(TestPanelId, DefaultState()).Visible)
        {
            Show();
        }
    }

    public bool IsVisible => _panel?.IsVisible == true;

    public bool IsEditing => _panel?.IsEditing == true;

    public bool HotkeysActive => _hotkeys.ToggleHotkeyActive && _hotkeys.EditHotkeyActive;

    public string StatusText => !IsVisible
        ? "Overlay hidden"
        : IsEditing
            ? "Overlay visible - editing"
            : "Overlay visible - locked (click-through)";

    public void Toggle()
    {
        if (IsVisible) Hide();
        else Show();
    }

    public void Show()
    {
        EnsurePanel();
        _panel!.IsEditing = false;
        _panel.Show();
        Save();
    }

    public void Hide()
    {
        if (_panel is null) return;
        _panel.IsEditing = false;
        _panel.Hide();
        Save();
    }

    public void ToggleEdit()
    {
        EnsurePanel();
        if (!_panel!.IsVisible) _panel.Show();
        _panel.IsEditing = !_panel.IsEditing;
        Save();
    }

    public void ResetToDefault()
    {
        EnsurePanel();
        _panel!.ApplyState(DefaultState());
        Save();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _hotkeys.ToggleRequested -= Toggle;
        _hotkeys.EditRequested -= ToggleEdit;
        Save();

        if (_panel is not null)
        {
            (_panel.DataContext as TestPanelViewModel)?.Dispose();
            _panel.AllowClose();
            _panel.Close();
            _panel = null;
        }

        _hotkeys.Dispose();
    }

    private void EnsurePanel()
    {
        if (_panel is not null) return;

        _panel = new TestOverlayPanel { DataContext = new TestPanelViewModel(_telemetry) };
        _panel.ApplyState(_layout.GetOrCreate(_panel.PanelId, DefaultState()));
        _panel.LayoutChanged += (_, _) => Save();
    }

    private void Save()
    {
        if (_panel is null) return;

        var current = _panel.CaptureState();
        var state = _layout.GetOrCreate(_panel.PanelId, current);
        state.Visible = current.Visible;
        state.X = current.X;
        state.Y = current.Y;
        state.Width = current.Width;
        state.Height = current.Height;
        state.Opacity = current.Opacity;

        try
        {
            _store.Save(_layout);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Never let a failed layout write take down the overlay.
        }
    }

    private static OverlayPanelState DefaultState() => new()
    {
        Id = TestPanelId,
        Visible = false,
        X = 40,
        Y = 40,
        Width = 300,
        Height = 230,
        Opacity = 0.92
    };
}
