using AccEngineer.Telemetry.Broadcasting;

namespace AccEngineer.Telemetry.Tests;

/// <summary>Tests for the deterministic test-data competitor feed.</summary>
public class TestCompetitorFeedTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Snapshot_ProducesRequestedCarCountWithUniquePositions()
    {
        var feed = new TestCompetitorFeed(carCount: 8, trackMeters: 4300);

        var standings = feed.Snapshot(T0);

        Assert.Equal(8, standings.Competitors.Count);
        var positions = standings.Competitors.Select(c => c.Position!.Value).OrderBy(p => p).ToArray();
        Assert.Equal(Enumerable.Range(1, 8).ToArray(), positions);
        Assert.All(standings.Competitors, c => Assert.InRange(c.SplinePosition, 0f, 1f));
    }

    [Fact]
    public void Snapshot_FlagsPlayerAndUsesSimulatedNames()
    {
        var feed = new TestCompetitorFeed();

        var standings = feed.Snapshot(T0, focusedCarIndex: 3);

        var player = Assert.Single(standings.Competitors, c => c.IsPlayer);
        Assert.Equal(3, player.CarIndex);
        Assert.Equal(3, standings.FocusedCarIndex);
        // Names are simulated but must be usable, non-generic labels.
        Assert.All(standings.Competitors, c => Assert.False(string.IsNullOrWhiteSpace(c.DriverFullName)));
        Assert.DoesNotContain(standings.Competitors, c => c.DisplayName.StartsWith("CAR ", StringComparison.Ordinal));
        var names = standings.Competitors.Select(c => c.DisplayName).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public void Snapshot_DefaultCarCountFillsAField()
    {
        var feed = new TestCompetitorFeed();

        var standings = feed.Snapshot(T0);

        Assert.Equal(20, standings.Competitors.Count);
    }

    [Fact]
    public void Snapshot_ExposesEveryCupCategory()
    {
        var feed = new TestCompetitorFeed();

        var standings = feed.Snapshot(T0);

        var categories = standings.Competitors.Select(c => c.CupCategory).Distinct().ToArray();
        Assert.Contains(AccCupCategory.Overall, categories);
        Assert.Contains(AccCupCategory.ProAm, categories);
        Assert.Contains(AccCupCategory.Am, categories);
        Assert.Contains(AccCupCategory.Silver, categories);
        Assert.Contains(AccCupCategory.National, categories);
    }

    [Fact]
    public void Snapshot_IsDeterministicForTheSameTime()
    {
        var feed = new TestCompetitorFeed();

        var first = feed.Snapshot(T0);
        var second = feed.Snapshot(T0);

        Assert.Equal(
            first.Competitors.Select(c => (c.CarIndex, c.Position, c.SplinePosition)),
            second.Competitors.Select(c => (c.CarIndex, c.Position, c.SplinePosition)));
    }

    [Fact]
    public void Snapshot_IncludesTrackLengthForGapDerivation()
    {
        var feed = new TestCompetitorFeed(trackMeters: 5800);

        var standings = feed.Snapshot(T0);

        Assert.Equal(5800, standings.TrackMeters);
        Assert.Equal("Test Circuit", standings.TrackName);
    }

    [Fact]
    public void Snapshot_PlacesSomeCarsInThePit()
    {
        var feed = new TestCompetitorFeed(carCount: 8);

        var standings = feed.Snapshot(T0);

        Assert.Contains(standings.Competitors, c => c.IsInPit);
    }
}
