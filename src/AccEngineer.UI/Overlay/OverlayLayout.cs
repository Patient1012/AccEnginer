namespace AccEngineer.UI.Overlay;

/// <summary>
/// Current overlay layout schema version. v1 was a bare single layout, v2 introduced
/// simulator profiles + named layouts + resolution pinning, and v3 reshaped the inputs
/// panel from a portrait stack to a landscape strip (so saved inputs geometry is
/// re-based on load).
/// </summary>
public static class OverlayLayoutSchema
{
    public const int Version = 3;

    /// <summary>Landscape default size for the inputs panel introduced in v3.</summary>
    public const double InputsDefaultWidth = 480;

    public const double InputsDefaultHeight = 92;
}


/// <summary>
/// One named overlay layout: theme, panel placement and global appearance. Versioned
/// so the schema can migrate later. Position and size are device-independent pixels.
/// </summary>
public sealed class OverlayLayout
{
    public int SchemaVersion { get; set; } = OverlayLayoutSchema.Version;

    public string Theme { get; set; } = "ClassicRacelabInspired";

    public List<OverlayPanelState> Panels { get; set; } = new();

    public OverlayAppearance Appearance { get; set; } = new();

    /// <summary>Returns the stored panel with the given id, adding the supplied defaults if absent.</summary>
    public OverlayPanelState GetOrCreate(string id, OverlayPanelState defaults)
    {
        var existing = Panels.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
        if (existing is not null) return existing;

        defaults.Id = id;
        Panels.Add(defaults);
        return defaults;
    }

    public OverlayLayout Clone()
    {
        var copy = new OverlayLayout
        {
            SchemaVersion = SchemaVersion,
            Theme = Theme,
            Appearance = Appearance.Clone()
        };
        foreach (var panel in Panels) copy.Panels.Add(panel.Clone());
        return copy;
    }
}

/// <summary>
/// Global appearance knobs shared by the panels in a layout. Kept in one place so the
/// editor can change them without touching individual panel markup.
/// </summary>
public sealed class OverlayAppearance
{
    /// <summary>Uniform scale applied to panel content (text and graphics), 0.6 to 2.0.</summary>
    public double FontScale { get; set; } = 1.0;

    /// <summary>Number of samples retained per input trace, 30 to 400.</summary>
    public int TraceLength { get; set; } = 120;

    /// <summary>Maximum rows shown in the relative panel, 3 to 20.</summary>
    public int RelativeRows { get; set; } = 8;

    /// <summary>Whether the relative panel shows the gap column.</summary>
    public bool RelativeShowGap { get; set; } = true;

    /// <summary>Whether the relative panel shows the driver/name column.</summary>
    public bool RelativeShowName { get; set; } = true;

    /// <summary>Maximum rows shown in the standings panel, 3 to 30.</summary>
    public int StandingsRows { get; set; } = 12;

    public OverlayAppearance Clone() => (OverlayAppearance)MemberwiseClone();

    /// <summary>Clamps the appearance knobs into their supported ranges (and finite values).</summary>
    public OverlayAppearance Sanitize()
    {
        if (!double.IsFinite(FontScale)) FontScale = 1.0;
        FontScale = Math.Clamp(FontScale, 0.6, 2.0);
        TraceLength = Math.Clamp(TraceLength, 30, 400);
        RelativeRows = Math.Clamp(RelativeRows, 3, 20);
        StandingsRows = Math.Clamp(StandingsRows, 3, 30);
        return this;
    }
}

/// <summary>
/// A simulator profile: the layouts available for one simulator, which one is active,
/// and optional per-resolution overrides (resolution key like "1920x1080").
/// </summary>
public sealed class OverlayLayoutProfile
{
    public string Simulator { get; set; } = "acc";

    public string ActiveLayout { get; set; } = "Default";

    public Dictionary<string, OverlayLayout> Layouts { get; set; } = new();

    /// <summary>Optional resolution ("WxH") to layout-name overrides for this profile.</summary>
    public Dictionary<string, string> ResolutionLayouts { get; set; } = new();
}

/// <summary>
/// The whole persisted overlay document: the active profile plus every profile's named
/// layouts. Migrated from the older single-layout file on load.
/// </summary>
public sealed class OverlayLayoutDocument
{
    public const string DefaultProfileName = "acc";

    public const string DefaultLayoutName = "Default";

    public int SchemaVersion { get; set; } = OverlayLayoutSchema.Version;

    public string ActiveProfile { get; set; } = DefaultProfileName;

    public Dictionary<string, OverlayLayoutProfile> Profiles { get; set; } = new();

    public static OverlayLayoutDocument CreateDefault()
    {
        var document = new OverlayLayoutDocument();
        var profile = new OverlayLayoutProfile { Simulator = DefaultProfileName };
        profile.Layouts[DefaultLayoutName] = new OverlayLayout();
        document.Profiles[DefaultProfileName] = profile;
        return document;
    }
}

/// <summary>Per-panel persisted state: identity, placement, visibility and appearance.</summary>
public sealed class OverlayPanelState
{
    public string Id { get; set; } = string.Empty;

    public bool Visible { get; set; }

    public double X { get; set; } = 40;

    public double Y { get; set; } = 40;

    public double Width { get; set; } = 300;

    public double Height { get; set; } = 230;

    /// <summary>Panel background opacity, 0.2 to 1.0.</summary>
    public double Opacity { get; set; } = 0.92;

    public OverlayPanelState Clone() => (OverlayPanelState)MemberwiseClone();

    /// <summary>
    /// Coerces the placement values into a serializable, on-screen-safe range. Windows
    /// report <see cref="double.NaN"/> for Left/Top/Width/Height before they are first
    /// arranged, and JSON cannot persist non-finite numbers, so callers sanitize before
    /// saving (a NaN would otherwise throw mid-session and drop the whole layout).
    /// </summary>
    public OverlayPanelState Sanitize()
    {
        if (!double.IsFinite(X)) X = 40;
        if (!double.IsFinite(Y)) Y = 40;
        if (!double.IsFinite(Width) || Width < 1) Width = 300;
        if (!double.IsFinite(Height) || Height < 1) Height = 230;
        if (!double.IsFinite(Opacity)) Opacity = 0.92;
        Opacity = Math.Clamp(Opacity, 0.2, 1.0);
        return this;
    }
}
