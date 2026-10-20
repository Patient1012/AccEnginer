using System.Globalization;
using System.Windows.Data;

namespace AccEngineer.UI.Converters;

/// <summary>
/// Two-way equality bridge for a string property and a boolean control, used by the
/// page's pill-style segmented controls: <c>Convert</c> reports whether the bound string
/// equals <c>ConverterParameter</c>; <c>ConvertBack</c> returns the parameter when the
/// control becomes checked and leaves the source untouched when it is unchecked, so a
/// RadioButton group writes exactly one value.
/// </summary>
public sealed class StringMatchToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
        {
            return parameter as string ?? string.Empty;
        }

        // Unchecking (the sibling being selected) must not clobber the shared source.
        return Binding.DoNothing;
    }
}
