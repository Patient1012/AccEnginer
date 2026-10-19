using AccEngineer.UI.Overlay;
using Xunit;

namespace AccEngineer.UI.Tests;

public sealed class OverlayPresetTests
{
    [Fact]
    public void All_ContainsTheFourNamedPresets()
    {
        var names = OverlayPresets.All.Select(p => p.Name).ToArray();

        Assert.Contains(OverlayPresets.Practice, names);
        Assert.Contains(OverlayPresets.Qualifying, names);
        Assert.Contains(OverlayPresets.Race, names);
        Assert.Contains(OverlayPresets.MinimalHud, names);
    }

    [Fact]
    public void EveryPreset_HasADescriptionAndShowsTiming()
    {
        foreach (var preset in OverlayPresets.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(preset.Description));
            Assert.True(preset.Shows("timing"), $"{preset.Name} should show the timing panel");
        }
    }

    [Fact]
    public void MinimalHud_ShowsOnlyTimingAndInputs()
    {
        var preset = OverlayPresets.All.Single(p => p.Name == OverlayPresets.MinimalHud);

        Assert.True(preset.Shows("timing"));
        Assert.True(preset.Shows("inputs"));
        Assert.False(preset.Shows("standings"));
        Assert.False(preset.Shows("relative"));
        Assert.False(preset.Shows("tyres"));
        Assert.False(preset.Shows("dynamics"));
    }

    [Fact]
    public void Race_ShowsCompetitorPanels()
    {
        var preset = OverlayPresets.All.Single(p => p.Name == OverlayPresets.Race);

        Assert.True(preset.Shows("standings"));
        Assert.True(preset.Shows("relative"));
        Assert.True(preset.Shows("timing"));
        Assert.True(preset.Shows("fuel"));
    }

    [Fact]
    public void Practice_ShowsVehiclePanelsButNotStandings()
    {
        var preset = OverlayPresets.All.Single(p => p.Name == OverlayPresets.Practice);

        Assert.True(preset.Shows("tyres"));
        Assert.True(preset.Shows("dynamics"));
        Assert.False(preset.Shows("standings"));
    }
}
