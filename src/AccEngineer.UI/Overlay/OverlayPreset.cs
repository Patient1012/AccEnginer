namespace AccEngineer.UI.Overlay;

/// <summary>
/// A named overlay layout preset: a description plus the set of panel ids that should be
/// visible. Applying a preset creates (or switches to) a layout of the same name and sets
/// each panel's visibility accordingly, so a driver can jump between a practice, quali,
/// race or minimal HUD arrangement without toggling panels by hand.
/// </summary>
public sealed record OverlayPreset(string Name, string Description, IReadOnlySet<string> VisiblePanels)
{
    public bool Shows(string panelId) => VisiblePanels.Contains(panelId);
}

/// <summary>The built-in preset library. Panel ids must match those registered by
/// <see cref="OverlayManager"/> (timing, relative, standings, inputs, map, conditions, tyres, dynamics).</summary>
public static class OverlayPresets
{
    public const string Practice = "Practice";
    public const string Qualifying = "Qualifying";
    public const string Race = "Race";
    public const string MinimalHud = "Minimal HUD";

    public static IReadOnlyList<OverlayPreset> All { get; } = new[]
    {
        new OverlayPreset(
            Practice,
            "Timing, tyres, dynamics, conditions and inputs for solo running.",
            new HashSet<string>(StringComparer.Ordinal) { "timing", "tyres", "dynamics", "conditions", "inputs", "map" }),

        new OverlayPreset(
            Qualifying,
            "Timing with delta, nearby cars, standings and inputs for qualifying.",
            new HashSet<string>(StringComparer.Ordinal) { "timing", "relative", "standings", "inputs", "map" }),

        new OverlayPreset(
            Race,
            "Standings, relative, timing, tyres and inputs for wheel-to-wheel racing.",
            new HashSet<string>(StringComparer.Ordinal) { "standings", "relative", "timing", "tyres", "inputs", "map" }),

        new OverlayPreset(
            MinimalHud,
            "Just timing and inputs for an uncluttered view.",
            new HashSet<string>(StringComparer.Ordinal) { "timing", "inputs" })
    };
}
