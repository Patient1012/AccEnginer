using System.Reflection;
using AccEngineer.UI.Themes;

namespace AccEngineer.UI.Tests;

/// <summary>
/// Guards the theme catalogue contract: every built-in theme fills every semantic token,
/// ids are unique, and persisted ids resolve (with a safe fallback).
/// </summary>
public class ThemeCatalogTests
{
    [Fact]
    public void EveryTheme_FillsEveryToken()
    {
        var properties = typeof(ThemePalette)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .ToArray();

        Assert.NotEmpty(properties);

        foreach (var theme in ThemeCatalog.All)
        {
            Assert.All(properties, property =>
            {
                var value = (string?)property.GetValue(theme.Palette);
                Assert.False(string.IsNullOrWhiteSpace(value),
                    $"{theme.Name} is missing the '{property.Name}' token.");
            });
        }
    }

    [Fact]
    public void ThemeIds_AreUnique_AndIncludeClassicRacing()
    {
        var ids = ThemeCatalog.All.Select(t => t.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Contains(AppTheme.ClassicRacing, ids);
    }

    [Fact]
    public void TryResolve_RoundTripsPersistedId()
    {
        foreach (var theme in ThemeCatalog.All)
        {
            Assert.True(ThemeCatalog.TryResolve(theme.Id.ToString(), out var resolved));
            Assert.Equal(theme.Id, resolved.Id);
        }
    }

    [Fact]
    public void TryResolve_UnknownId_FallsBackToDefault()
    {
        // The bool reports "recognised"; the out value is always usable (Default when
        // unknown), which is what the settings loader relies on.
        Assert.False(ThemeCatalog.TryResolve("not-a-theme", out var resolved));
        Assert.Equal(ThemeCatalog.Default.Id, resolved.Id);

        Assert.False(ThemeCatalog.TryResolve(null, out var fromNull));
        Assert.Equal(ThemeCatalog.Default.Id, fromNull.Id);
    }

    [Fact]
    public void ClassicRacing_IsDark_WithRacingAccents()
    {
        var theme = ThemeCatalog.All.Single(t => t.Id == AppTheme.ClassicRacing);

        Assert.True(theme.IsDark);
        Assert.StartsWith("#FF", theme.Palette.Background);

        // Overlay panels carry their own alpha, so only check they are defined.
        Assert.False(string.IsNullOrWhiteSpace(theme.Palette.OverlayPanel));
        Assert.NotEqual(theme.Palette.OverlayAccent, theme.Palette.OverlaySuccess);
    }
}
