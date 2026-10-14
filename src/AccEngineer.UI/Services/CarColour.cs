using System.Windows.Media;

namespace AccEngineer.UI.Services;

/// <summary>
/// Stable per-car colour derived from the car id. ACC supplies no class/driver
/// colours for other cars, so we generate a consistent hue per id so a car keeps
/// the same colour across the relative list and the track map.
/// </summary>
public static class CarColour
{
    public static SolidColorBrush ForCar(int carId)
    {
        var hue = (carId * 47) % 360;
        var brush = new SolidColorBrush(HsvToRgb(hue / 360.0, 0.55, 0.85));
        brush.Freeze();
        return brush;
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        var i = (int)(h * 6);
        var f = (h * 6) - i;
        var p = v * (1 - s);
        var q = v * (1 - (f * s));
        var t = v * (1 - ((1 - f) * s));
        var (r, g, b) = (i % 6) switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };
        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
