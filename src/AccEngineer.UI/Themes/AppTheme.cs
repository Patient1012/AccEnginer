namespace AccEngineer.UI.Themes;

/// <summary>
/// Stable identifiers for the built-in themes. The enum name (not the display name)
/// is what gets persisted in <c>settings.json</c>, so renaming a display label can
/// never invalidate a saved preference.
/// </summary>
public enum AppTheme
{
    /// <summary>The original ACC Engineer palette. Also the fallback for unknown ids.</summary>
    Default = 0,

    GruvboxDark,
    GruvboxLight,
    Dracula,
    CatppuccinMocha,
    CatppuccinMacchiato,
    CatppuccinLatte,
    Nord,
    TokyoNight,
    OneDark
}
