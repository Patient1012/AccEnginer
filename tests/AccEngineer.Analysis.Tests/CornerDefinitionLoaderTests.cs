using AccEngineer.Analysis.Corners;

namespace AccEngineer.Analysis.Tests;

public class CornerDefinitionLoaderTests
{
    private const string Json = """
    {
      "track": "Example Circuit",
      "corners": [
        { "number": 2, "name": "Les Combes", "start": 420.0, "turnIn": 455.0, "apex": 485.0, "exit": 530.0 },
        { "number": 1, "apex": 200.0 }
      ]
    }
    """;

    [Fact]
    public void LoadFromJson_ParsesTrackAndCorners()
    {
        var definition = new CornerDefinitionLoader().LoadFromJson(Json);

        Assert.Equal("Example Circuit", definition.Track);
        Assert.Equal(2, definition.Corners.Count);
    }

    [Fact]
    public void LoadFromJson_OrdersByNumber()
    {
        var definition = new CornerDefinitionLoader().LoadFromJson(Json);

        Assert.Equal(new[] { 1, 2 }, definition.Corners.Select(c => c.Number));
    }

    [Fact]
    public void LoadFromJson_MapsAllBoundaries()
    {
        var definition = new CornerDefinitionLoader().LoadFromJson(Json);
        var corner = definition.Corners[1];

        Assert.Equal("Les Combes", corner.Name);
        Assert.Equal(420.0, corner.StartDistanceMeters!.Value, 3);
        Assert.Equal(455.0, corner.TurnInDistanceMeters!.Value, 3);
        Assert.Equal(485.0, corner.ApexDistanceMeters, 3);
        Assert.Equal(530.0, corner.ExitDistanceMeters!.Value, 3);
    }

    [Fact]
    public void LoadFromJson_PartialCornerAllowsApexOnly()
    {
        var definition = new CornerDefinitionLoader().LoadFromJson(Json);
        var corner = definition.Corners[0];

        Assert.Equal(200.0, corner.ApexDistanceMeters, 3);
        Assert.Null(corner.StartDistanceMeters);
        Assert.Null(corner.ExitDistanceMeters);
    }

    [Fact]
    public void LoadFromJson_MissingTrack_Throws()
    {
        var loader = new CornerDefinitionLoader();

        Assert.Throws<InvalidOperationException>(() =>
            loader.LoadFromJson("""{ "corners": [] }"""));
    }

    [Fact]
    public void LoadFromJson_MissingApex_Throws()
    {
        var loader = new CornerDefinitionLoader();

        Assert.Throws<InvalidOperationException>(() =>
            loader.LoadFromJson("""{ "track": "T", "corners": [ { "number": 1, "start": 10.0 } ] }"""));
    }
}
