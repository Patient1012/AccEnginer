using System.Windows.Media;
using AccEngineer.Telemetry.Broadcasting;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Presentation of ACC vehicle classes (<c>EntryListCar.car_model</c> via
/// <see cref="AccCarModelCatalog"/>) for the standings tower: display label, group
/// order and accent colour.
///
/// These are the car classes ACC's model list defines (GT3/GT4/GT2/GTC/TCX) — the
/// tower groups by them, while the driver's Bronze..Platinum category is shown as the
/// separate rating column. Cars with unknown model ids group under "OTHER" rather
/// than being assigned a class.
/// </summary>
public static class StandingsClass
{
    /// <summary>Group display order; anything not listed (unknown) is grouped last.</summary>
    public static readonly AccCarClass[] DisplayOrder =
    {
        AccCarClass.Gt3,
        AccCarClass.Gt4,
        AccCarClass.Gt2,
        AccCarClass.Gtc,
        AccCarClass.Tcx
    };

    /// <summary>Human-readable label for a vehicle class; "OTHER" when unknown.</summary>
    public static string Label(AccCarClass carClass) => carClass switch
    {
        AccCarClass.Gt3 => "GT3",
        AccCarClass.Gt4 => "GT4",
        AccCarClass.Gt2 => "GT2",
        AccCarClass.Gtc => "GTC",
        AccCarClass.Tcx => "TCX",
        _ => "OTHER"
    };

    /// <summary>Class accent colour, used by both the group header and its driver rows.</summary>
    public static Brush Accent(AccCarClass carClass) => carClass switch
    {
        AccCarClass.Gt3 => Amber,
        AccCarClass.Gt4 => Cyan,
        AccCarClass.Gt2 => Pink,
        AccCarClass.Gtc => Green,
        AccCarClass.Tcx => Silver,
        _ => Neutral
    };

    // Fixed, saturated design colours (not theme tokens): a class palette that stays
    // distinguishable regardless of the active theme.
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
