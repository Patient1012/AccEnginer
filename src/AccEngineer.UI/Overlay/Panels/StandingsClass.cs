using System.Windows.Media;
using AccEngineer.Telemetry.Broadcasting;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Presentation of ACC cup categories (<c>EntryListCar.cup_category</c>) for the standings
/// tower: display label, group order and accent colour.
///
/// These are the categories ACC actually reports - not GT3/GT4 car classes, which the
/// broadcasting data does not expose - so nothing is inferred. The accent palette is the
/// standings design language; the theme's ClassPro/Am/Silver tokens remain the class-badge
/// colours used by the Relative panel.
/// </summary>
public static class StandingsClass
{
    /// <summary>Group display order, most-populated categories first.</summary>
    public static readonly AccCupCategory[] DisplayOrder =
    {
        AccCupCategory.Overall,
        AccCupCategory.ProAm,
        AccCupCategory.Am,
        AccCupCategory.Silver,
        AccCupCategory.National
    };

    /// <summary>Human-readable label for a cup category, empty when unknown.</summary>
    public static string Label(AccCupCategory? category) => category switch
    {
        AccCupCategory.Overall => "OVERALL",
        AccCupCategory.ProAm => "PRO-AM",
        AccCupCategory.Am => "AM",
        AccCupCategory.Silver => "SILVER",
        AccCupCategory.National => "NATIONAL",
        _ => string.Empty
    };

    /// <summary>Class accent colour, used by both the group header and its driver rows.</summary>
    public static Brush Accent(AccCupCategory? category) => category switch
    {
        AccCupCategory.Overall => Amber,
        AccCupCategory.ProAm => Cyan,
        AccCupCategory.Am => Pink,
        AccCupCategory.Silver => Silver,
        AccCupCategory.National => Green,
        _ => Neutral
    };

    // Fixed, saturated design colours (not theme tokens): a five-way class palette that
    // stays distinguishable regardless of the active theme.
    private static readonly Brush Amber = Frozen(0xE5, 0xA0, 0x4B);
    private static readonly Brush Cyan = Frozen(0x4F, 0xB6, 0xD8);
    private static readonly Brush Pink = Frozen(0xE0, 0x56, 0x8C);
    private static readonly Brush Silver = Frozen(0xB9, 0xC0, 0xC9);
    private static readonly Brush Green = Frozen(0x83, 0xB8, 0x79);
    private static readonly Brush Neutral = Frozen(0x9A, 0x9A, 0xA0);

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
