using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace AccEngineer.UI.Themes;

/// <summary>Metadata for one built-in theme.</summary>
/// <param name="Id">Stable identifier; this is what is persisted.</param>
/// <param name="Name">Human-readable name shown in the Settings selector.</param>
/// <param name="Description">One-line description shown next to the selector.</param>
/// <param name="IsDark">True for dark backgrounds. Available for future contrast rules.</param>
/// <param name="Palette">The theme's semantic colour tokens.</param>
public sealed record ThemeDefinition(AppTheme Id, string Name, string Description, bool IsDark, ThemePalette Palette);

/// <summary>
/// The built-in theme catalogue and the bridge between a <see cref="ThemePalette"/> and
/// the WPF resource dictionary the application consumes. Adding a theme means adding one
/// entry to <see cref="All"/> - the resource keys are derived from the palette property
/// names, so there is no separate key list to keep in sync.
/// </summary>
public static class ThemeCatalog
{
    private static readonly PropertyInfo[] TokenProperties =
        typeof(ThemePalette).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.CanRead)
            .OrderBy(p => p.MetadataToken)
            .ToArray();

    /// <summary>The original palette, before any user-selected theme is applied.</summary>
    public static ThemeDefinition Default { get; } = new(
        AppTheme.Default,
        "Default",
        "The original ACC Engineer palette.",
        IsDark: true,
        Palette: DefaultPalette());

    /// <summary>Every theme, in the order the Settings selector should list them.</summary>
    public static IReadOnlyList<ThemeDefinition> All { get; } = new[]
    {
        Default,
        new ThemeDefinition(AppTheme.GruvboxDark, "Gruvbox Dark", "Warm charcoal with cream text and earthy orange, yellow, red and green.", true, ThemePalettes.GruvboxDark),
        new ThemeDefinition(AppTheme.GruvboxLight, "Gruvbox Light", "Warm off-white with the same earthy accents.", false, ThemePalettes.GruvboxLight),
        new ThemeDefinition(AppTheme.Dracula, "Dracula", "Dark purple-grey with purple, pink, cyan and green.", true, ThemePalettes.Dracula),
        new ThemeDefinition(AppTheme.CatppuccinMocha, "Catppuccin Mocha", "Dark blue-grey with pastel accents.", true, ThemePalettes.CatppuccinMocha),
        new ThemeDefinition(AppTheme.CatppuccinMacchiato, "Catppuccin Macchiato", "Softer dark surfaces with balanced pastels.", true, ThemePalettes.CatppuccinMacchiato),
        new ThemeDefinition(AppTheme.CatppuccinLatte, "Catppuccin Latte", "Light with readable pastel accents.", false, ThemePalettes.CatppuccinLatte),
        new ThemeDefinition(AppTheme.Nord, "Nord", "Cool blue-grey with subdued accents.", true, ThemePalettes.Nord),
        new ThemeDefinition(AppTheme.TokyoNight, "Tokyo Night", "Deep navy with blue, cyan and purple.", true, ThemePalettes.TokyoNight),
        new ThemeDefinition(AppTheme.OneDark, "One Dark", "Charcoal with familiar editor-inspired colours.", true, ThemePalettes.OneDark)
    };

    /// <summary>The resource key a token is published under, e.g. <c>ColorAccent</c>.</summary>
    public static string ColorKey(string token) => "Color" + token;

    /// <summary>The brush key a token is published under, e.g. <c>BrushAccent</c>.</summary>
    public static string BrushKey(string token) => "Brush" + token;

    /// <summary>Every token name, in declaration order.</summary>
    public static IReadOnlyList<string> TokenNames { get; } =
        TokenProperties.Select(p => p.Name).ToArray();

    public static bool TryGet(AppTheme id, out ThemeDefinition definition)
    {
        var found = All.FirstOrDefault(t => t.Id == id);
        definition = found ?? Default;
        return found is not null;
    }

    /// <summary>
    /// Resolves a persisted theme id. Accepts either the stable enum name (what this
    /// application writes) or the display name (so a hand-edited settings file using
    /// "Gruvbox Dark" still works). Missing, unparseable and out-of-range values all
    /// fall back to <see cref="Default"/>, so a stale or hand-edited settings file can
    /// never leave the application unstyled.
    /// </summary>
    public static bool TryResolve(string? id, out ThemeDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var text = id.Trim();

            if (Enum.TryParse(text, ignoreCase: true, out AppTheme parsed) && Enum.IsDefined(parsed))
            {
                return TryGet(parsed, out definition);
            }

            var byName = All.FirstOrDefault(t =>
                string.Equals(t.Name, text, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                definition = byName;
                return true;
            }
        }

        definition = Default;
        return false;
    }

    /// <summary>
    /// Builds the resource dictionary for a theme. Every token is published as both a
    /// <c>Color</c> and a frozen <c>SolidColorBrush</c>, so brushes can be referenced
    /// directly and colours can be composed (for example into an overlay panel brush).
    /// </summary>
    public static ResourceDictionary BuildResources(ThemeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var resources = new ResourceDictionary();
        foreach (var property in TokenProperties)
        {
            var token = property.Name;
            var hex = (string?)property.GetValue(definition.Palette) ?? string.Empty;
            if (!TryParseColor(hex, out var color)) continue;

            resources[ColorKey(token)] = color;

            var brush = new SolidColorBrush(color);
            if (brush.CanFreeze) brush.Freeze();
            resources[BrushKey(token)] = brush;
        }

        return resources;
    }

    /// <summary>Parses an ARGB hex string (<c>#RGB</c>, <c>#ARGB</c>, <c>#RRGGBB</c>, <c>#AARRGGBB</c>).</summary>
    public static bool TryParseColor(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        var text = hex.Trim();
        if (!text.StartsWith('#')) return false;
        text = text[1..];

        // ColorConverter accepts named colours; only hex is allowed here so a typo in a
        // palette cannot silently become an unintended colour.
        if (text.Length is not (3 or 4 or 6 or 8)) return false;
        if (text.Any(c => !Uri.IsHexDigit(c))) return false;

        if (text.Length <= 4)
        {
            text = string.Concat(text.Select(c => $"{c}{c}"));
        }

        if (text.Length == 6) text = "FF" + text;

        try
        {
            color = Color.FromArgb(
                System.Convert.ToByte(text[..2], 16),
                System.Convert.ToByte(text[2..4], 16),
                System.Convert.ToByte(text[4..6], 16),
                System.Convert.ToByte(text[6..8], 16));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static ThemePalette DefaultPalette() => new()
    {
        // Surfaces
        Background = "#FF0A0E14",
        Surface = "#FF151C24",
        SurfaceAlt = "#FF243040",
        SurfaceHigh = "#FF2A3646",

        // Text
        TextPrimary = "#FFFFFFFF",
        TextSecondary = "#FFC4CFDA",
        TextMuted = "#FF8E9AAB",
        TextOnAccent = "#FF0B0F14",

        // Lines
        Border = "#FF4A5766",
        Divider = "#FF33414F",

        // Accent
        Accent = "#FFC9F84A",
        AccentHover = "#FFD7FF6B",
        AccentDim = "#2EC9F84A",

        // Interactive controls
        ControlBg = "#FF1B2430",
        ControlBgHover = "#FF243040",
        ControlBgPressed = "#FF2E3D4F",
        ControlDisabled = "#FF4A5766",
        InputBg = "#FF0F1620",
        InputBorder = "#FF4A5766",
        TooltipBg = "#FF2A3646",
        TooltipText = "#FFFFFFFF",

        // States
        Selection = "#2EC9F84A",
        Focus = "#FFC9F84A",

        // Status
        Success = "#FF6FE39A",
        Warning = "#FFFFC24D",
        Error = "#FFFF6B74",
        Info = "#FF61AFEF",

        // Charts and data visualisation
        ChartGrid = "#FF2A3646",
        ChartAxis = "#FF4A5766",
        ChartLine = "#FFC9F84A",
        ChartFill = "#33C9F84A",
        ChartThrottle = "#FF63D67F",
        ChartBrake = "#FFE4574C",
        ChartSteer = "#FF4FA3FF",
        ChartMapPath = "#FF8E9AAB",
        ChartMapMarker = "#FFC4CFDA",
        ChartMapPlayer = "#FFC9F84A",

        // Overlay panels
        OverlayPanel = "#E6101012",
        OverlayHeader = "#FF161718",
        OverlayBorder = "#FF4A4A4E",
        OverlaySeparator = "#33FFFFFF",
        OverlayText = "#FFF0F0F0",
        OverlayMuted = "#FFA6ACB4",
        OverlayAccent = "#FFB7F34A",
        OverlaySuccess = "#FF63D67F",
        OverlayWarning = "#FFE7C34A",
        OverlaySelectedRow = "#22B7F34A",
        OverlayPit = "#4D9AA0A6",
        OverlayBarTrack = "#33FFFFFF",
        OverlayGearBadge = "#14FFFFFF",
        OverlayGraphBg = "#FF0A0A0A",
        OverlayPersonalBest = "#FF6FE39A",
        OverlaySessionBest = "#FFC08CFF",


        // Flags
        FlagBlue = "#FF4FA3FF",
        FlagYellow = "#FFE7C34A",
        FlagRed = "#FFE4574C",
        FlagGreen = "#FF63D67F",
        FlagWhite = "#FFF5F5F5",
        FlagBlack = "#FF9AA0A6",
        FlagCheckered = "#B3B7F34A",

        // Driver classes
        ClassPro = "#FF4FA3FF",
        ClassAm = "#FFFFC24D",
        ClassSilver = "#FFD0D4DA"
    };
}
