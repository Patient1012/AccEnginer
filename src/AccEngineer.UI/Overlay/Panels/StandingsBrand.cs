using System.Windows.Media;
using AccEngineer.Telemetry.Broadcasting;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Presentation of ACC car manufacturers for the standings tower: a short monogram and
/// an accent colour, both keyed off <see cref="AccCarBrandCatalog"/> (the ACC car model
/// id → manufacturer mapping). The monogram is a text badge, not a logo image — the app
/// ships no brand artwork, and only a numeric model id is available per opponent. Cars
/// with unknown model ids render no badge rather than a guessed brand.
/// </summary>
public static class StandingsBrand
{
    /// <summary>Short monogram for a manufacturer; empty when unknown.</summary>
    public static string Code(AccCarBrand brand) => AccCarBrandCatalog.Code(brand);

    /// <summary>Brand accent colour, used behind the monogram badge.</summary>
    public static Brush Accent(AccCarBrand brand) => brand switch
    {
        AccCarBrand.Porsche => Porsche,
        AccCarBrand.Mercedes => Mercedes,
        AccCarBrand.Ferrari => Ferrari,
        AccCarBrand.Audi => Audi,
        AccCarBrand.Lamborghini => Lamborghini,
        AccCarBrand.McLaren => McLaren,
        AccCarBrand.Nissan => Nissan,
        AccCarBrand.BMW => Bmw,
        AccCarBrand.Bentley => Bentley,
        AccCarBrand.AstonMartin => AstonMartin,
        AccCarBrand.Jaguar => Jaguar,
        AccCarBrand.Lexus => Lexus,
        AccCarBrand.Honda => Honda,
        AccCarBrand.Alpine => Alpine,
        AccCarBrand.Chevrolet => Chevrolet,
        AccCarBrand.Ginetta => Ginetta,
        AccCarBrand.Ktm => Ktm,
        AccCarBrand.Maserati => Maserati,
        AccCarBrand.Ford => Ford,
        _ => Neutral
    };

    // Fixed design colours (not theme tokens): a brand palette that stays distinguishable
    // regardless of the active theme, in the same spirit as the class accents.
    private static readonly Brush Porsche = Frozen(0xD5, 0x00, 0x1D);
    private static readonly Brush Mercedes = Frozen(0x00, 0xA1, 0x9E);
    private static readonly Brush Ferrari = Frozen(0xD8, 0x1E, 0x2C);
    private static readonly Brush Audi = Frozen(0xC5, 0x0B, 0x1E);
    private static readonly Brush Lamborghini = Frozen(0xD9, 0xB3, 0x00);
    private static readonly Brush McLaren = Frozen(0xFF, 0x6A, 0x00);
    private static readonly Brush Nissan = Frozen(0xB0, 0x1D, 0x2A);
    private static readonly Brush Bmw = Frozen(0x39, 0x7D, 0xC1);
    private static readonly Brush Bentley = Frozen(0x8C, 0x8C, 0x8C);
    private static readonly Brush AstonMartin = Frozen(0x00, 0x5A, 0x4C);
    private static readonly Brush Jaguar = Frozen(0x2E, 0x6B, 0x3A);
    private static readonly Brush Lexus = Frozen(0x8A, 0x8A, 0x8A);
    private static readonly Brush Honda = Frozen(0xE0, 0x3A, 0x2F);
    private static readonly Brush Alpine = Frozen(0x1F, 0x4F, 0xB0);
    private static readonly Brush Chevrolet = Frozen(0xC9, 0xA2, 0x27);
    private static readonly Brush Ginetta = Frozen(0xE8, 0x6A, 0x00);
    private static readonly Brush Ktm = Frozen(0xFF, 0x66, 0x00);
    private static readonly Brush Maserati = Frozen(0x1B, 0x3A, 0x6B);
    private static readonly Brush Ford = Frozen(0x00, 0x35, 0x8E);
    private static readonly Brush Neutral = Frozen(0x9A, 0x9A, 0xA0);

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
