namespace AccEngineer.UI.Themes;

/// <summary>
/// One theme's complete set of semantic colour tokens, as ARGB hex strings
/// (<c>#AARRGGBB</c>). The property names are the token names: each one is published as
/// both <c>Color{Name}</c> and <c>Brush{Name}</c> in the active theme dictionary, which
/// is why they match the keys the existing markup already references.
///
/// Every theme must supply every token. <see cref="ThemeCatalog"/> builds the resource
/// dictionary from these properties by reflection, so a new token only needs a property
/// here and an entry in each theme - no separate key list to keep in sync.
/// </summary>
public sealed record ThemePalette
{
    // ---- Surfaces -------------------------------------------------------------------
    /// <summary>Main window / page background.</summary>
    public required string Background { get; init; }

    /// <summary>Panel and card backgrounds.</summary>
    public required string Surface { get; init; }

    /// <summary>Raised surfaces: hovered rows, navigation selection, secondary panels.</summary>
    public required string SurfaceAlt { get; init; }

    /// <summary>Highest surface: dropdown popups, tooltips, context menus.</summary>
    public required string SurfaceHigh { get; init; }

    // ---- Text -----------------------------------------------------------------------
    public required string TextPrimary { get; init; }

    public required string TextSecondary { get; init; }

    /// <summary>Lowest-emphasis text: captions, hints, disabled labels.</summary>
    public required string TextMuted { get; init; }

    /// <summary>Text drawn on top of an accent-filled control.</summary>
    public required string TextOnAccent { get; init; }

    // ---- Lines ----------------------------------------------------------------------
    /// <summary>Outlines and control borders.</summary>
    public required string Border { get; init; }

    /// <summary>Softer internal separators and table rules.</summary>
    public required string Divider { get; init; }

    // ---- Accent ---------------------------------------------------------------------
    public required string Accent { get; init; }

    public required string AccentHover { get; init; }

    /// <summary>Translucent accent for selected rows, badges and focus fills.</summary>
    public required string AccentDim { get; init; }

    // ---- Interactive controls -------------------------------------------------------
    public required string ControlBg { get; init; }

    public required string ControlBgHover { get; init; }

    public required string ControlBgPressed { get; init; }

    public required string ControlDisabled { get; init; }

    public required string InputBg { get; init; }

    public required string InputBorder { get; init; }

    public required string TooltipBg { get; init; }

    public required string TooltipText { get; init; }

    // ---- States ---------------------------------------------------------------------
    /// <summary>Selected row / active item fill.</summary>
    public required string Selection { get; init; }

    /// <summary>Keyboard focus outline.</summary>
    public required string Focus { get; init; }

    // ---- Status ---------------------------------------------------------------------
    public required string Success { get; init; }

    public required string Warning { get; init; }

    public required string Error { get; init; }

    public required string Info { get; init; }

    // ---- Charts and data visualisation ----------------------------------------------
    /// <summary>Grid lines inside charts and the track map.</summary>
    public required string ChartGrid { get; init; }

    /// <summary>Axis strokes and axis labels.</summary>
    public required string ChartAxis { get; init; }

    public required string ChartLine { get; init; }

    public required string ChartFill { get; init; }

    /// <summary>Input channels. Deliberately distinct from the status colours so an
    /// input trace never reads as a warning or an error.</summary>
    public required string ChartThrottle { get; init; }

    public required string ChartBrake { get; init; }

    public required string ChartSteer { get; init; }

    public required string ChartMapPath { get; init; }

    public required string ChartMapMarker { get; init; }

    public required string ChartMapPlayer { get; init; }

    // ---- Overlay panels -------------------------------------------------------------
    /// <summary>Panel background. Carries its own alpha so the sim stays visible.</summary>
    public required string OverlayPanel { get; init; }

    public required string OverlayHeader { get; init; }

    public required string OverlayBorder { get; init; }

    public required string OverlaySeparator { get; init; }

    public required string OverlayText { get; init; }

    public required string OverlayMuted { get; init; }

    public required string OverlayAccent { get; init; }

    public required string OverlaySuccess { get; init; }

    public required string OverlayWarning { get; init; }

    /// <summary>Highlight for the player's own row in a standings list.</summary>
    public required string OverlaySelectedRow { get; init; }

    public required string OverlayPit { get; init; }

    public required string OverlayBarTrack { get; init; }

    public required string OverlayGearBadge { get; init; }

    /// <summary>Backdrop behind the throttle/brake trace chart.</summary>
    public required string OverlayGraphBg { get; init; }

    /// <summary>
    /// Timing readout conventions: green for the driver's own best lap, purple for the
    /// overall session best. Kept separate from the status colours so a fast lap never
    /// reads as a warning.
    /// </summary>
    public required string OverlayPersonalBest { get; init; }

    public required string OverlaySessionBest { get; init; }

    // ---- Flags ----------------------------------------------------------------------
    public required string FlagBlue { get; init; }

    public required string FlagYellow { get; init; }

    public required string FlagRed { get; init; }

    public required string FlagGreen { get; init; }

    public required string FlagWhite { get; init; }

    public required string FlagBlack { get; init; }

    public required string FlagCheckered { get; init; }

    // ---- Driver classes -------------------------------------------------------------
    public required string ClassPro { get; init; }

    public required string ClassAm { get; init; }

    public required string ClassSilver { get; init; }
}
