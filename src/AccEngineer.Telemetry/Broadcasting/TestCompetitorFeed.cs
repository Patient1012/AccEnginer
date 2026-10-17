namespace AccEngineer.Telemetry.Broadcasting;

/// <summary>
/// Deterministic synthetic standings for the built-in test-data mode. Positions,
/// laps, gaps and pit states are a pure function of the supplied time so the same
/// session always produces the same tower, and every driver falls back to a generic
/// <c>CAR nn</c> label - real names are never invented.
/// </summary>
public sealed class TestCompetitorFeed
{
    private const int LapTimeMs = 100_000;

    private readonly int _carCount;
    private readonly int _trackMeters;

    public TestCompetitorFeed(int carCount = 8, int trackMeters = 4300)
    {
        _carCount = Math.Clamp(carCount, 2, 30);
        _trackMeters = trackMeters;
    }

    /// <summary>Builds the synthetic standings at <paramref name="now"/>.</summary>
    public CompetitorStandings Snapshot(DateTimeOffset now, int focusedCarIndex = 1)
    {
        var t = now.ToUnixTimeMilliseconds() / 1000.0;

        var distances = new double[_carCount + 1];
        for (var i = 1; i <= _carCount; i++)
        {
            var speed = 0.00030 + (i % 5) * 0.000015; // laps per second, slightly varied
            var offset = (i - 1) / (double)_carCount;
            distances[i] = t * speed + offset;
        }

        var order = Enumerable.Range(1, _carCount)
            .OrderByDescending(i => distances[i])
            .ToArray();

        var positions = new int[_carCount + 1];
        for (var rank = 0; rank < order.Length; rank++)
        {
            positions[order[rank]] = rank + 1;
        }

        var playerDistance = distances[focusedCarIndex];
        var competitors = new List<Competitor>(_carCount);
        for (var i = 1; i <= _carCount; i++)
        {
            var distance = distances[i];
            var laps = (int)Math.Floor(distance);
            var spline = (float)(distance - laps);
            var delta = (int)Math.Round((playerDistance - distance) * LapTimeMs);
            var inPit = i % 4 == 0;

            competitors.Add(new Competitor(
                (ushort)i,
                IsPlayer: i == focusedCarIndex,
                DriverShortName: null,
                DriverFullName: null,
                RaceNumber: (i * 7) % 99 + 1,
                TeamName: null,
                CupCategory: (AccCupCategory)(i % 4),
                Location: inPit ? AccCarLocation.Pitlane : AccCarLocation.Track,
                Position: positions[i],
                TrackPosition: positions[i],
                LapCount: laps,
                DeltaMs: delta,
                BestLapMs: LapTimeMs + (i % 5) * 250,
                LastLapMs: LapTimeMs + (i % 5) * 250 + 120,
                SplinePosition: spline,
                SpeedKmh: inPit ? 45 : 200 + (i % 4) * 8));
        }

        return new CompetitorStandings(
            TrackName: "Test Circuit",
            TrackMeters: _trackMeters,
            SessionTimeMs: (float)(t * 1000 % 3_600_000),
            SessionPhase: 4,
            FocusedCarIndex: focusedCarIndex,
            Competitors: competitors,
            ReceivedAtUtc: now);
    }
}
