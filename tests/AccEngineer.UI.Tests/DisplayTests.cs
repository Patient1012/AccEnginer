using AccEngineer.UI.Services;

namespace AccEngineer.UI.Tests;

public class DisplayTests
{
    [Fact]
    public void Millimetres_ConvertsMetresToWholeMillimetres()
    {
        Assert.Equal("21", Display.Millimetres(0.021f));
        Assert.Equal("34", Display.Millimetres(0.034f));
        Assert.Equal("0", Display.Millimetres(0f));
    }

    [Fact]
    public void Millimetres_MissingValue_ShowsDash()
    {
        Assert.Equal(Display.Missing, Display.Millimetres(null));
    }
}
