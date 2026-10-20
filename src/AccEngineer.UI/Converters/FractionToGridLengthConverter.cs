using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AccEngineer.UI.Converters;

/// <summary>
/// Converts a 0..1 fraction into a star <see cref="GridLength"/>, used to size a bar's
/// filled portion inside a two-column grid (the fill column gets the fraction as its
/// star weight, the remainder column gets 1 - fraction). Values are clamped so a stray
/// fraction can never invert or overflow the bar.
/// </summary>
public sealed class FractionToGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value switch
        {
            double d when double.IsFinite(d) => d,
            float f when float.IsFinite(f) => f,
            _ => 0d
        };

        fraction = Math.Clamp(fraction, 0d, 1d);

        // The "remainder" side of the bar is produced by passing parameter "inverse".
        var inverse = parameter is string s && s.Equals("inverse", StringComparison.OrdinalIgnoreCase);
        var weight = inverse ? 1d - fraction : fraction;

        return new GridLength(weight, GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
