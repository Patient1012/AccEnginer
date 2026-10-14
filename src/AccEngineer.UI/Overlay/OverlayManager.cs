using System.IO;
using AccEngineer.UI.Overlay.Panels;

namespace AccEngineer.UI.Overlay;

/// <summary>Read-only description of a panel, used by the settings UI.</summary>
public sealed record OverlayPanelInfo(string Id, string Title, bool IsVisible);

/// <summary>
/// Owns the overlay panels and their persisted layout. Panels are independent
/// transparent windows managed by one manager, which also handles global hotkeys,
/// edit mode, demo mode and layout persistence.
/// </summary>
public sealed class OverlayManager : IDisposable
{
    /// <summary>One hosted panel: its window, optional view model and default placement.</summary>
    private sealed class PanelHost
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required OverlayWindowBase Window { get; init; }
        public IDisposable? ViewModel { get; init; }

        /// <summary>
        /// Intended visibility. Tracked explicitly because <see cref="Window.IsVisible"/>
        /// lags Show()/Hide() and would otherwise be persisted at the wrong moment.
        /// </summary>
        public bool Visible { get; set; }
    }

    private readonly IOverlayTelemetrySource _liveSource;
    private readonly OverlayLayoutStore _store;
    private readonly OverlayHotkeys _hotkeys;
    private readonly OverlayLayout _layout;
    private readonly List<PanelHost> _panels = new();

    private IOverlayTelemetrySource _source;
    private DemoOverlayTelemetrySource? _demoSource;
    private bool _disposed;

    public OverlayManager(IOverlayTelemetrySource liveSource)
    {
        _liveSource = liveSource;
        _source = liveSource;

        _store = new OverlayLayoutStore();
        _layout = _store.Load();

        _hotkeys = new OverlayHotkeys();
        _hotkeys.ToggleRequested += ToggleAll;
        _hotkeys.EditRequested += ToggleEditAll;
        _hotkeys.Attach();

        BuildPanels();
    }

    /// <summary>True when panels are being fed synthetic data for layout testing.</summary>
    public bool IsDemoMode { get; private set; }

    public bool HotkeysActive => _hotkeys.ToggleHotkeyActive && _hotkeys.EditHotkeyActive;

    public IReadOnlyList<OverlayPanelInfo> Panels =>
        _panels.Select(p => new OverlayPanelInfo(p.Id, p.Title, p.Visible)).ToArray();

    public bool AnyVisible => _panels.Any(p => p.Visible);

    public bool AnyEditing => _panels.Any(p => p.Window.IsEditing);

    public string StatusText
    {
        get
        {
            var visible = _panels.Count(p => p.Visible);
            var state = visible == 0
                ? "hidden"
                : AnyEditing ? $"{visible}/{_panels.Count} visible - editing" : $"{visible}/{_panels.Count} visible - locked (click-through)";
            return IsDemoMode ? $"{state} [DEMO DATA]" : state;
        }
    }

    /// <summary>Shows all panels if none are visible, otherwise hides them all.</summary>
    public void ToggleAll()
    {
        if (AnyVisible) HideAll();
        else ShowAll();
    }

    public void ShowAll()
    {
        foreach (var panel in _panels)
        {
            panel.Window.IsEditing = false;
            panel.Visible = true;
            panel.Window.Show();
        }

        Save();
    }

    public void HideAll()
    {
        foreach (var panel in _panels)
        {
            panel.Window.IsEditing = false;
            panel.Visible = false;
            panel.Window.Hide();
        }

        Save();
    }

    public void ToggleEditAll()
    {
        // Editing only makes sense while visible; ensure everything is shown first.
        var target = !AnyEditing;
        foreach (var panel in _panels)
        {
            if (target)
            {
                panel.Visible = true;
                panel.Window.Show();
            }

            panel.Window.IsEditing = target;
        }

        Save();
    }

    public void TogglePanel(string id)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == id);
        if (panel is null) return;

        if (panel.Visible)
        {
            panel.Window.IsEditing = false;
            panel.Visible = false;
            panel.Window.Hide();
        }
        else
        {
            panel.Visible = true;
            panel.Window.Show();
        }

        Save();
    }

    public void ResetToDefault()
    {
        foreach (var panel in _panels)
        {
            panel.Window.ApplyState(DefaultState(panel.Id));
        }

        Save();
    }

    /// <summary>Switches between live telemetry and synthetic demo data, rebuilding panels.</summary>
    public void SetDemoMode(bool demo)
    {
        if (demo == IsDemoMode) return;

        IsDemoMode = demo;
        if (demo)
        {
            _demoSource ??= new DemoOverlayTelemetrySource();
            _source = _demoSource;
        }
        else
        {
            _source = _liveSource;
        }

        RebuildPanels();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _hotkeys.ToggleRequested -= ToggleAll;
        _hotkeys.EditRequested -= ToggleEditAll;
        Save();
        DestroyPanels();
        _hotkeys.Dispose();
        _demoSource?.Dispose();
    }

    private void BuildPanels()
    {
        // Timing block.
        var timingVm = new TimingPanelViewModel(_source);
        _panels.Add(new PanelHost
        {
            Id = "timing",
            Title = "Timing",
            Window = new OverlayPanelWindow("timing", "ACC Engineer - Timing", new TimingOverlayPanel { DataContext = timingVm }, DefaultState("timing")),
            ViewModel = timingVm
        });

        // Relative / nearby block.
        var relativeVm = new RelativePanelViewModel(_source);
        _panels.Add(new PanelHost
        {
            Id = "relative",
            Title = "Relative",
            Window = new OverlayPanelWindow("relative", "ACC Engineer - Relative", new RelativeOverlayPanel { DataContext = relativeVm }, DefaultState("relative")),
            ViewModel = relativeVm
        });

        // Inputs / gear block.
        var inputsVm = new InputsPanelViewModel(_source);
        _panels.Add(new PanelHost
        {
            Id = "inputs",
            Title = "Inputs",
            Window = new OverlayPanelWindow("inputs", "ACC Engineer - Inputs", new InputsOverlayPanel { DataContext = inputsVm }, DefaultState("inputs")),
            ViewModel = inputsVm
        });

        // Track map block.
        var mapVm = new TrackMapViewModel(_source);
        _panels.Add(new PanelHost
        {
            Id = "map",
            Title = "Track Map",
            Window = new OverlayPanelWindow("map", "ACC Engineer - Track Map", new TrackMapOverlayPanel { DataContext = mapVm }, DefaultState("map")),
            ViewModel = mapVm
        });

        // Legacy Phase 1 test panel (kept available; hidden by default).
        var testVm = new TestPanelViewModel(_source);
        _panels.Add(new PanelHost
        {
            Id = "test",
            Title = "Test",
            Window = new TestOverlayPanel { DataContext = testVm, PanelId = "test" },
            ViewModel = testVm
        });

        // Apply persisted placement and restore visibility.
        foreach (var panel in _panels)
        {
            var state = _layout.GetOrCreate(panel.Id, DefaultState(panel.Id));
            panel.Window.ApplyState(state);
            panel.Window.LayoutChanged += (_, _) => Save();
            if (state.Visible)
            {
                panel.Visible = true;
                panel.Window.Show();
            }
        }
    }

    private void RebuildPanels()
    {
        DestroyPanels();
        BuildPanels();
    }

    private void DestroyPanels()
    {
        foreach (var panel in _panels)
        {
            panel.ViewModel?.Dispose();
            panel.Window.AllowClose();
            panel.Window.Close();
        }

        _panels.Clear();
    }

    private void Save()
    {
        foreach (var panel in _panels)
        {
            var current = panel.Window.CaptureState();
            var state = _layout.GetOrCreate(panel.Id, current);
            state.Visible = panel.Visible;
            state.X = current.X;
            state.Y = current.Y;
            state.Width = current.Width;
            state.Height = current.Height;
            state.Opacity = current.Opacity;
        }

        try
        {
            _store.Save(_layout);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Never let a failed layout write take down the overlay.
        }
    }

    /// <summary>Default placement per panel; kept in one place so panels do not overlap.</summary>
    private static OverlayPanelState DefaultState(string id) => id switch
    {
        "timing" => new OverlayPanelState { Id = id, Visible = false, X = 40, Y = 40, Width = 260, Height = 158, Opacity = 0.92 },
        "relative" => new OverlayPanelState { Id = id, Visible = false, X = 40, Y = 210, Width = 232, Height = 178, Opacity = 0.92 },
        "inputs" => new OverlayPanelState { Id = id, Visible = false, X = 300, Y = 40, Width = 240, Height = 210, Opacity = 0.92 },
        "map" => new OverlayPanelState { Id = id, Visible = false, X = 560, Y = 40, Width = 250, Height = 250, Opacity = 0.92 },
        _ => new OverlayPanelState { Id = id, Visible = false, X = 40, Y = 400, Width = 300, Height = 230, Opacity = 0.92 }
    };
}
