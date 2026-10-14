using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Global show/hide and edit hotkeys for the overlay, registered on a hidden
/// message-only window so they work while the simulator has focus.
/// </summary>
public sealed class OverlayHotkeys : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const int ToggleId = 0x4F01;
    private const int EditId = 0x4F02;
    private const uint VkO = 0x4F;
    private const uint VkE = 0x45;

    private static readonly IntPtr HwndMessage = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    private HwndSource? _source;

    public event Action? ToggleRequested;

    public event Action? EditRequested;

    public bool ToggleHotkeyActive { get; private set; }

    public bool EditHotkeyActive { get; private set; }

    public void Attach()
    {
        if (_source is not null) return;

        var parameters = new HwndSourceParameters("AccEngineerOverlayHotkeys")
        {
            ParentWindow = HwndMessage,
            WindowStyle = 0,
            Width = 0,
            Height = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        var hwnd = _source.Handle;
        ToggleHotkeyActive = RegisterHotKey(hwnd, ToggleId, ModControl | ModAlt | ModNoRepeat, VkO);
        EditHotkeyActive = RegisterHotKey(hwnd, EditId, ModControl | ModAlt | ModNoRepeat, VkE);
    }

    public void Dispose()
    {
        if (_source is null) return;

        UnregisterHotKey(_source.Handle, ToggleId);
        UnregisterHotKey(_source.Handle, EditId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            switch (wParam.ToInt32())
            {
                case ToggleId:
                    ToggleRequested?.Invoke();
                    handled = true;
                    break;
                case EditId:
                    EditRequested?.Invoke();
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }
}
