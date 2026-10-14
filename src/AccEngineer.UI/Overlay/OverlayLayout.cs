namespace AccEngineer.UI.Overlay;

/// <summary>
/// Persisted overlay layout (see the legacy overlay plan). Versioned so the schema
/// can migrate later. Position and size are device-independent pixels.
/// </summary>
public sealed class OverlayLayout
{
    public int SchemaVersion { get; set; } = 1;

    public string Theme { get; set; } = "ClassicRacelabInspired";

    public List<OverlayPanelState> Panels { get; set; } = new();

    /// <summary>Returns the stored panel with the given id, adding the supplied defaults if absent.</summary>
    public OverlayPanelState GetOrCreate(string id, OverlayPanelState defaults)
    {
        var existing = Panels.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
        if (existing is not null) return existing;

        defaults.Id = id;
        Panels.Add(defaults);
        return defaults;
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
}
