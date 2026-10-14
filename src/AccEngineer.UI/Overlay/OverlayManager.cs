using System.IO;
using System.Windows;
using AccEngineer.UI.Overlay.Panels;

namespace AccEngineer.UI.Overlay;

/// <summary>Read-only description of a panel, used by the settings + editor UI.</summary>
public sealed record OverlayPanelInfo(string Id, string Title, bool IsVisible, double Opacity);

/// <summary>
/// Owns the overlay panels and their persisted layout document. Panels are independent
/// transparent windows managed by one manager, which handles global hotkeys, edit mode,
/// demo mode, named layouts, simulator profiles and layout persistence.
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

        /// <summary>Set for panels that honour global appearance settings.</summary>
        public OverlayPanelViewModel? Configurable { get; init; }

        /// <summary>
        /// Intended visibility. Tracked explicitly because <see cref="Window.IsVisible"/>
        /// lags Show()/Hide() and would otherwise be persisted at the wrong moment.
        /// </summary>
        public bool Visible { get; set; }
    }

    private readonly IOverlayTelemetrySource _liveSource;
    private readonly OverlayLayoutStore _store;
    private readonly OverlayHotkeys _hotkeys;
    private readonly OverlayLayoutDocument _document;
    private readonly List<PanelHost> _panels = new();

    private IOverlayTelemetrySource _source;
    private DemoOverlayTelemetrySource? _demoSource;
    private bool _disposed;

    public OverlayManager(IOverlayTelemetrySource liveSource)
    {
        _liveSource = liveSource;
        _source = liveSource;

        _store = new OverlayLayoutStore();
        _document = _store.Load();
        ResolveActiveLayoutForResolution();

        _hotkeys = new OverlayHotkeys();
        _hotkeys.ToggleRequested += ToggleAll;
        _hotkeys.EditRequested += ToggleEditAll;
        _hotkeys.ManagerRequested += RequestEditor;
        _hotkeys.Attach();

        BuildPanels();
    }

    /// <summary>Raised whenever panel/layout state changes so editors can refresh.</summary>
    public event Action? Changed;

    /// <summary>Raised when the user asks to open the layout editor (hotkey or UI).</summary>
    public event Action? EditorRequested;

    /// <summary>True when panels are being fed synthetic data for layout testing.</summary>
    public bool IsDemoMode { get; private set; }

    public bool HotkeysActive =>
        _hotkeys.ToggleHotkeyActive && _hotkeys.EditHotkeyActive && _hotkeys.ManagerHotkeyActive;

    public IReadOnlyList<OverlayPanelInfo> Panels =>
        _panels.Select(p => new OverlayPanelInfo(p.Id, p.Title, p.Visible, p.Window.Opacity)).ToArray();

    public bool AnyVisible => _panels.Any(p => p.Visible);

    public bool AnyEditing => _panels.Any(p => p.Window.IsEditing);

    // ---- Profiles / layouts ---------------------------------------------------

    public IReadOnlyList<string> ProfileNames => _document.Profiles.Keys.ToArray();

    public string ActiveProfileName => _document.ActiveProfile;

    public IReadOnlyList<string> LayoutNames => ActiveProfileRecord.Layouts.Keys.ToArray();

    public string ActiveLayoutName => ActiveProfileRecord.ActiveLayout;

    public string CurrentResolution => CurrentResolutionKey();

    public bool IsResolutionPinned => ActiveProfileRecord.ResolutionLayouts.ContainsKey(CurrentResolutionKey());

    public OverlayAppearance Appearance => ActiveLayoutRecord.Appearance;

    public string StatusText
    {
        get
        {
            var visible = _panels.Count(p => p.Visible);
            var state = visible == 0
                ? "hidden"
                : AnyEditing ? $"{visible}/{_panels.Count} visible - editing" : $"{visible}/{_panels.Count} visible - locked (click-through)";
            var layout = $"profile '{ActiveProfileName}' / layout '{ActiveLayoutName}'";
            var demo = IsDemoMode ? " [DEMO DATA]" : string.Empty;
            return $"{state} - {layout}{demo}";
        }
    }

    private OverlayLayoutProfile ActiveProfileRecord => _document.Profiles[_document.ActiveProfile];

    private OverlayLayout ActiveLayoutRecord => ActiveProfileRecord.Layouts[ActiveProfileRecord.ActiveLayout];

    // ---- Visibility -----------------------------------------------------------

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
            SetVisible(panel, true);
        }

        Save();
    }

    public void HideAll()
    {
        foreach (var panel in _panels)
        {
            panel.Window.IsEditing = false;
            SetVisible(panel, false);
        }

        Save();
    }

    public void ToggleEditAll()
    {
        // Editing only makes sense while visible; ensure everything is shown first.
        var target = !AnyEditing;
        foreach (var panel in _panels)
        {
            if (target) SetVisible(panel, true);
            panel.Window.IsEditing = target;
        }

        Save();
    }

    public void TogglePanel(string id)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == id);
        if (panel is null) return;

        if (panel.Visible) panel.Window.IsEditing = false;
        SetVisible(panel, !panel.Visible);
        Save();
    }

    /// <summary>Sets a single panel's visibility (used by the editor checkboxes).</summary>
    public void SetPanelVisible(string id, bool visible)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == id);
        if (panel is null || panel.Visible == visible) return;

        if (!visible) panel.Window.IsEditing = false;
        SetVisible(panel, visible);
        Save();
    }

    /// <summary>Sets a single panel's opacity (used by the editor sliders).</summary>
    public void SetPanelOpacity(string id, double opacity)
    {
        var panel = _panels.FirstOrDefault(p => p.Id == id);
        if (panel is null) return;

        panel.Window.Opacity = Math.Clamp(opacity, 0.2, 1.0);
        Save();
    }

    private static void SetVisible(PanelHost panel, bool visible)
    {
        panel.Visible = visible;
        if (visible) panel.Window.Show();
        else panel.Window.Hide();
    }

    // ---- Appearance -----------------------------------------------------------

    /// <summary>Replaces the active layout's appearance and pushes it to every panel.</summary>
    public void SetAppearance(OverlayAppearance appearance)
    {
        ActiveLayoutRecord.Appearance = appearance.Clone();
        ApplyAppearanceToPanels();
        Save();
    }

    // ---- Profiles / named layouts --------------------------------------------

    public void SetProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_document.Profiles.ContainsKey(name)) return;
        if (string.Equals(name, _document.ActiveProfile, StringComparison.Ordinal)) return;

        Save();
        _document.ActiveProfile = name;
        ResolveActiveLayoutForResolution();
        RebuildPanels();
        Save();
    }

    public void SetLayout(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !ActiveProfileRecord.Layouts.ContainsKey(name)) return;
        if (string.Equals(name, ActiveProfileRecord.ActiveLayout, StringComparison.Ordinal)) return;

        Save();
        ActiveProfileRecord.ActiveLayout = name;
        RebuildPanels();
        Save();
    }

    /// <summary>Snapshots the current on-screen arrangement under a new layout name.</summary>
    public void SaveLayoutAs(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return;

        var snapshot = CaptureLayout();
        ActiveProfileRecord.Layouts[trimmed] = snapshot;
        ActiveProfileRecord.ActiveLayout = trimmed;
        Save();
    }

    /// <summary>Deletes a named layout (the last remaining layout cannot be removed).</summary>
    public void DeleteLayout(string name)
    {
        if (ActiveProfileRecord.Layouts.Count <= 1) return;
        if (string.IsNullOrWhiteSpace(name) || !ActiveProfileRecord.Layouts.ContainsKey(name)) return;

        ActiveProfileRecord.Layouts.Remove(name);
        if (string.Equals(ActiveProfileRecord.ActiveLayout, name, StringComparison.Ordinal))
        {
            ActiveProfileRecord.ActiveLayout = ActiveProfileRecord.Layouts.Keys.First();
            RebuildPanels();
        }

        Save();
    }

    /// <summary>Creates a new simulator profile with a single default layout and activates it.</summary>
    public void AddProfile(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || _document.Profiles.ContainsKey(trimmed)) return;

        var profile = new OverlayLayoutProfile { Simulator = trimmed };
        profile.Layouts[OverlayLayoutDocument.DefaultLayoutName] = new OverlayLayout();
        _document.Profiles[trimmed] = profile;

        Save();
        _document.ActiveProfile = trimmed;
        RebuildPanels();
        Save();
    }

    /// <summary>Deletes a profile (the last remaining profile cannot be removed).</summary>
    public void DeleteProfile(string name)
    {
        if (_document.Profiles.Count <= 1) return;
        if (string.IsNullOrWhiteSpace(name) || !_document.Profiles.ContainsKey(name)) return;

        _document.Profiles.Remove(name);
        if (string.Equals(_document.ActiveProfile, name, StringComparison.Ordinal))
        {
            _document.ActiveProfile = _document.Profiles.Keys.First();
            ResolveActiveLayoutForResolution();
            RebuildPanels();
        }

        Save();
    }

    /// <summary>Restores the active layout's panels and appearance to factory defaults.</summary>
    public void ResetToDefault()
    {
        ActiveLayoutRecord.Appearance = new OverlayAppearance();
        foreach (var panel in _panels)
        {
            panel.Window.ApplyState(DefaultState(panel.Id));
            panel.Visible = false;
            panel.Window.Hide();
        }

        ApplyAppearanceToPanels();
        Save();
    }

    /// <summary>Pins the current resolution to the active layout (per-resolution override).</summary>
    public void PinCurrentResolution()
    {
        ActiveProfileRecord.ResolutionLayouts[CurrentResolutionKey()] = ActiveProfileRecord.ActiveLayout;
        Save();
    }

    /// <summary>Removes the per-resolution override for the current resolution.</summary>
    public void UnpinCurrentResolution()
    {
        if (ActiveProfileRecord.ResolutionLayouts.Remove(CurrentResolutionKey()))
        {
            Save();
        }
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
        _hotkeys.ManagerRequested -= RequestEditor;
        Save();
        DestroyPanels();
        _hotkeys.Dispose();
        _demoSource?.Dispose();
    }

    private void RequestEditor() => EditorRequested?.Invoke();

    private void BuildPanels()
    {
        // Timing block.
        var timingVm = new TimingPanelViewModel(_source);
        _panels.Add(new PanelHost
        {
            Id = "timing",
            Title = "Timing",
            Window = new OverlayPanelWindow("timing", "ACC Engineer - Timing", new TimingOverlayPanel { DataContext = timingVm }, DefaultState("timing")),
            ViewModel = timingVm,
            Configurable = timingVm
        });

        // Relative / nearby block.
        var relativeVm = new RelativePanelViewModel(_source);
        _panels.Add(new PanelHost
        {
            Id = "relative",
            Title = "Relative",
            Window = new OverlayPanelWindow("relative", "ACC Engineer - Relative", new RelativeOverlayPanel { DataContext = relativeVm }, DefaultState("relative")),
            ViewModel = relativeVm,
            Configurable = relativeVm
        });

        // Inputs / gear block.
        var inputsVm = new InputsPanelViewModel(_source);
        _panels.Add(new PanelHost
        {
            Id = "inputs",
            Title = "Inputs",
            Window = new OverlayPanelWindow("inputs", "ACC Engineer - Inputs", new InputsOverlayPanel { DataContext = inputsVm }, DefaultState("inputs")),
            ViewModel = inputsVm,
            Configurable = inputsVm
        });

        // Track map block.
        var mapVm = new TrackMapViewModel(_source);
        _panels.Add(new PanelHost
        {
            Id = "map",
            Title = "Track Map",
            Window = new OverlayPanelWindow("map", "ACC Engineer - Track Map", new TrackMapOverlayPanel { DataContext = mapVm }, DefaultState("map")),
            ViewModel = mapVm,
            Configurable = mapVm
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

        var layout = ActiveLayoutRecord;

        // First pass: restore intended visibility and placement for EVERY panel.
        // This must happen before any Show(): showing a window fires LayoutChanged -> Save(),
        // and Save() reads each panel's Visible flag. If we interleaved Show() with the
        // restore, the first Show() would persist the not-yet-restored panels as hidden and
        // permanently clobber their visibility.
        foreach (var panel in _panels)
        {
            var state = layout.GetOrCreate(panel.Id, DefaultState(panel.Id));
            panel.Window.ApplyState(state);
            panel.Visible = state.Visible;
        }

        // Second pass: wire persistence, then show the panels that should be visible.
        foreach (var panel in _panels)
        {
            panel.Window.LayoutChanged += (_, _) => Save();
            if (panel.Visible) panel.Window.Show();
        }

        ApplyAppearanceToPanels();
    }

    private void ApplyAppearanceToPanels()
    {
        var appearance = ActiveLayoutRecord.Appearance;
        foreach (var panel in _panels)
        {
            panel.Window.SetContentScale(appearance.FontScale);
            panel.Configurable?.ApplyAppearance(appearance);
        }
    }

    private void RebuildPanels()
    {
        DestroyPanels();
        BuildPanels();
        Changed?.Invoke();
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

    /// <summary>Snapshots the current panel states into a fresh layout document.</summary>
    private OverlayLayout CaptureLayout()
    {
        var layout = new OverlayLayout { Appearance = ActiveLayoutRecord.Appearance.Clone() };
        foreach (var panel in _panels)
        {
            var state = panel.Window.CaptureState();
            state.Id = panel.Id;
            state.Visible = panel.Visible;
            layout.Panels.Add(state);
        }

        return layout;
    }

    private void Save()
    {
        var layout = ActiveLayoutRecord;
        foreach (var panel in _panels)
        {
            var current = panel.Window.CaptureState();
            var state = layout.GetOrCreate(panel.Id, current);
            state.Visible = panel.Visible;
            state.X = current.X;
            state.Y = current.Y;
            state.Width = current.Width;
            state.Height = current.Height;
            state.Opacity = current.Opacity;
        }

        try
        {
            _store.Save(_document);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Never let a failed layout write take down the overlay.
        }

        Changed?.Invoke();
    }

    private void ResolveActiveLayoutForResolution()
    {
        var profile = ActiveProfileRecord;
        if (profile.ResolutionLayouts.TryGetValue(CurrentResolutionKey(), out var name) && profile.Layouts.ContainsKey(name))
        {
            profile.ActiveLayout = name;
        }
    }

    private static string CurrentResolutionKey()
    {
        var width = (int)SystemParameters.PrimaryScreenWidth;
        var height = (int)SystemParameters.PrimaryScreenHeight;
        return $"{width}x{height}";
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
