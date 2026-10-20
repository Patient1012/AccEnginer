using System.Globalization;
using System.Windows.Data;
using AccEngineer.UI.Converters;

namespace AccEngineer.UI.Tests;

/// <summary>
/// The pill-style segmented controls on the Fuel Calculator page bind a string property
/// (such as the fuel display unit) to a pair of RadioButtons through this converter. It must
/// report equality one way and write the option back only when a control becomes checked, so
/// selecting one option never clobbers the value when WPF unchecks its sibling.
/// </summary>
public class StringMatchToBoolConverterTests
{
    private readonly StringMatchToBoolConverter _converter = new();

    [Fact]
    public void Convert_MatchesTheParameter_Ordinal()
    {
        Assert.Equal(true, _converter.Convert("Litres", typeof(bool), "Litres", CultureInfo.InvariantCulture));
        Assert.Equal(false, _converter.Convert("Laps", typeof(bool), "Litres", CultureInfo.InvariantCulture));
        Assert.Equal(false, _converter.Convert(null, typeof(bool), "Litres", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_Checked_WritesTheParameter()
    {
        Assert.Equal("Laps", _converter.ConvertBack(true, typeof(string), "Laps", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_Unchecked_LeavesTheSourceUntouched()
    {
        Assert.Equal(Binding.DoNothing, _converter.ConvertBack(false, typeof(string), "Laps", CultureInfo.InvariantCulture));
        Assert.Equal(Binding.DoNothing, _converter.ConvertBack(null, typeof(string), "Laps", CultureInfo.InvariantCulture));
    }
}
