using System.Windows;
using AccEngineer.UI.Themes;

namespace AccEngineer.UI.Services;

/// <summary>
/// Owns the currently active theme and swaps it at runtime.
///
/// The theme is a single <see cref="ResourceDictionary"/> holding every semantic
/// <c>Color*</c> / <c>Brush*</c> token. It is merged last, above the bootstrap palette in
/// <c>App.xaml</c>, so it wins every lookup and every existing
/// <c>{DynamicResource Brush...}</c> reference updates in place - no window or control is
/// duplicated per theme, and nothing is recoloured by hand.
/// </summary>
public static class ThemeManager
{
    private static ResourceDictionary? _applied;

    /// <summary>The id of the theme currently in effect.</summary>
    public static AppTheme Current { get; private set; } = AppTheme.Default;

    public static ThemeDefinition CurrentDefinition { get; private set; } = ThemeCatalog.Default;

    /// <summary>Raised after the active theme has changed, on the calling thread.</summary>
    public static event EventHandler? Changed;

    /// <summary>
    /// Applies a theme. Falls back to <see cref="AppTheme.Default"/> if the id is unknown,
    /// and is safe to call before any window exists.
    /// </summary>
    public static bool Apply(string? themeId)
    {
        var resolved = ThemeCatalog.TryResolve(themeId, out var definition);
        Apply(definition);
        return resolved;
    }

    /// <summary>Applies a theme, replacing the previously applied one.</summary>
    public static void Apply(AppTheme theme)
    {
        Apply(ThemeCatalog.TryGet(theme, out var definition) ? definition : ThemeCatalog.Default);
    }

    /// <summary>Applies a theme definition, replacing the previously applied one.</summary>
    public static void Apply(ThemeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Current = definition.Id;
        CurrentDefinition = definition;

        var app = Application.Current;
        if (app is not null)
        {
            var resources = ThemeCatalog.BuildResources(definition);

            // Remove the previously applied dictionary so themes never stack; the
            // bootstrap palette in App.xaml stays underneath as a safety net.
            if (_applied is not null)
            {
                app.Resources.MergedDictionaries.Remove(_applied);
            }

            app.Resources.MergedDictionaries.Add(resources);
            _applied = resources;
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }
}
