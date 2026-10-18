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
    public void Snapshot_ReportsSignedHalfLapWrappedTimeGapsToThePlayer()
    {
        var feed = new TestCompetitorFeed(carCount: 20, trackMeters: 4300);

        var standings = feed.Snapshot(T0, focusedCarIndex: 1);

        var player = standings.Competitors.Single(c => c.IsPlayer);
        Assert.Equal(0, player.RelativeTimeGapMs);

        // Both sides of the player must be represented: negative = ahead, positive = behind.
        var gaps = standings.Competitors.Select(c => c.RelativeTimeGapMs!.Value).ToList();
        Assert.Contains(gaps, g => g < 0);
        Assert.Contains(gaps, g => g > 0);

        // Half-lap wrapped with a 100 s simulated lap: nothing near a whole lap of time.
        Assert.All(gaps, g => Assert.InRange(g, -50_000, 50_000));

        // Neighbours are a fifth of a lap apart at t=0 (0.05 lap = 5 s at LapTimeMs).
        Assert.Equal(-5_000, standings.Competitors.Single(c => c.CarIndex == 2).RelativeTimeGapMs);
        Assert.Equal(45_000, standings.Competitors.Single(c => c.CarIndex == 12).RelativeTimeGapMs);
    }

    [Fact]
    public void Snapshot_GapsAreDeterministicAndPlayerRelative()
    {
        var feed = new TestCompetitorFeed(carCount: 12, trackMeters: 4300);

        var first = feed.Snapshot(T0, focusedCarIndex: 4);
        var second = feed.Snapshot(T0, focusedCarIndex: 4);

        Assert.Equal(
            first.Competitors.Select(c => c.RelativeTimeGapMs),
            second.Competitors.Select(c => c.RelativeTimeGapMs));
        Assert.Equal(0, first.Competitors.Single(c => c.IsPlayer).RelativeTimeGapMs);
        // Real ACC leaves this null; only the synthetic feed populates it.
        Assert.All(first.Competitors, c => Assert.NotNull(c.RelativeTimeGapMs));
    }

    [Fact]
    public void Snapshot_PlacesSomeCarsInThePit()
    {
        var feed = new TestCompetitorFeed(carCount: 8);

        var standings = feed.Snapshot(T0);

        Assert.Contains(standings.Competitors, c => c.IsInPit);
    }
}
