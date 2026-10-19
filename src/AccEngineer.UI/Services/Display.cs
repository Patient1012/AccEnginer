using System.Globalization;
using AccEngineer.Telemetry.Broadcasting;

namespace AccEngineer.UI.Services;

/// <summary>
/// Presentation formatting for telemetry values. Missing data is always shown as
/// an em dash, never as a fabricated zero (see roadmap data-model principle).
/// </summary>
public static class Display
{
    public const string Missing = "—";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Formats seconds as <c>m:ss.mmm</c> (for example <c>0:45.200</c> or <c>1:23.456</c>).</summary>
    public static string LapTime(double? seconds)
    {
        if (seconds is not { } value) return Missing;
        var span = TimeSpan.FromSeconds(value);
        return $"{(int)span.TotalMinutes}:{span.Seconds:00}.{span.Milliseconds:000}";
    }

    public static string LapTimeMs(int? milliseconds) =>
        milliseconds is null or < 0 ? Missing : LapTime(milliseconds.Value / 1000.0);

    public static string Delta(double? seconds) =>
        seconds is null ? Missing : seconds.Value.ToString("+0.000;-0.000;0.000", Inv) + " s";

    /// <summary>Formats a signed lap delta given in milliseconds as seconds (for example <c>-0.180 s</c>).</summary>
    public static string DeltaMs(int? milliseconds) =>
        milliseconds is null ? Missing : Delta(milliseconds.Value / 1000.0);

    public static string Gear(int? raw) => raw switch
    {
        null => Missing,
        0 => "R",
        1 => "N",
        >= 2 => (raw.Value - 1).ToString(Inv),
        _ => Missing
    };

    /// <summary>
    /// Formats a signed relative time gap in seconds for the relative/standings gap
    /// columns: behind the reference is positive, ahead is negative, the reference row
    /// is <c>0.000</c>. Unavailable gaps read <c>--</c> (the overlay's own convention,
    /// not the dashboard em dash) so the fixed-width column never jitters.
    /// </summary>
    public static string RelativeGapSeconds(double? seconds) =>
        seconds is not { } value ? "--" : value.ToString("+0.000;-0.000;0.000", Inv);

    /// <summary>
    /// Compact badge text for a car's vehicle class (<c>GT3</c>/<c>GT4</c>/<c>GT2</c>/
    /// <c>GTC</c>/<c>TCX</c>). Unknown classes read empty so the badge collapses instead
    /// of inventing a class.
    /// </summary>
    public static string CarClassCode(AccCarClass carClass) => carClass switch
    {
        AccCarClass.Gt3 => "GT3",
        AccCarClass.Gt4 => "GT4",
        AccCarClass.Gt2 => "GT2",
        AccCarClass.Gtc => "GTC",
        AccCarClass.Tcx => "TCX",
        _ => string.Empty
    };

    /// <summary>
    /// Compact badge text for a driver's category (<c>PLT</c>/<c>GLD</c>/<c>SLV</c>/
    /// <c>BRO</c>). Unknown categories read empty so the badge collapses.
    /// </summary>
    public static string DriverCategoryCode(AccDriverCategory? category) => category switch
    {
        AccDriverCategory.Platinum => "PLT",
        AccDriverCategory.Gold => "GLD",
        AccDriverCategory.Silver => "SLV",
        AccDriverCategory.Bronze => "BRO",
        _ => string.Empty
    };

    public static string Number(double? value, string format = "0.0") =>
        value is null ? Missing : value.Value.ToString(format, Inv);

    public static string Kmh(float? value) => value is null ? Missing : value.Value.ToString("0", Inv);

    public static string Percent(float? fraction) =>
        fraction is null ? Missing : (fraction.Value * 100.0).ToString("0", Inv);

    public static string Degrees(float? celsius) => celsius is null ? Missing : celsius.Value.ToString("0.0", Inv);

    public static string Pressure(float? value) => value is null ? Missing : value.Value.ToString("0.0", Inv);

    public static string Metres(float? metres) => metres is null ? Missing : metres.Value.ToString("0", Inv);

    /// <summary>
    /// Formats a length given in metres as whole millimetres (for example suspension
    /// travel). Values are not clamped; missing data stays <see cref="Missing"/>.
    /// </summary>
    public static string Millimetres(float? metres) =>
        metres is null ? Missing : (metres.Value * 1000.0).ToString("0", Inv);

    public static string Fuel(float? kg) => kg is null ? Missing : kg.Value.ToString("0.0", Inv);

    public static string Litres(float? litres) => litres is null ? Missing : litres.Value.ToString("0.0", Inv);

    /// <summary>
    /// Formats a fuel quantity in the selected display unit. In litres mode the value is
    /// shown as "<c>x.x L</c>". In laps mode it is divided by the effective litres-per-lap
    /// and shown as "<c>x.x laps</c>"; when the consumption is unknown (or non-positive)
    /// the value cannot be expressed in laps and reads <see cref="Missing"/>.
    /// </summary>
    public static string FuelQuantity(double? litres, bool lapsMode, double? litresPerLap, int decimals = 1)
    {
        if (litres is not { } value) return Missing;

        if (!lapsMode)
        {
            return value.ToString("0.0", Inv) + " L";
        }

        if (litresPerLap is not { } perLap || !double.IsFinite(perLap) || perLap <= 0d)
        {
            return Missing;
        }

        var laps = value / perLap;
        return laps.ToString(decimals == 1 ? "0.0" : "0.##", Inv) + " laps";
    }

    /// <summary>
    /// Formats a signed fuel surplus/shortfall in the selected display unit (litres or
    /// laps), preserving the explicit sign. Reads <see cref="Missing"/> when the value
    /// cannot be expressed in the selected unit.
    /// </summary>
    public static string FuelSigned(double? litres, bool lapsMode, double? litresPerLap)
    {
        if (litres is not { } value) return Missing;

        if (!lapsMode)
        {
            return value.ToString("+0.0;-0.0;0.0", Inv) + " L";
        }

        if (litresPerLap is not { } perLap || !double.IsFinite(perLap) || perLap <= 0d)
        {
            return Missing;
        }

        var laps = value / perLap;
        return laps.ToString("+0.0;-0.0;0.0", Inv) + " laps";
    }

    public static string Steering(float? normalized)
    {
        if (normalized is null) return Missing;
        var degrees = normalized.Value * 180.0;
        return degrees.ToString("+0;-0;0", Inv) + "°";
    }

    public static string Rpm(int? rpm) => rpm is null ? Missing : rpm.Value.ToString("0", Inv);
}
