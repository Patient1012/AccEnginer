namespace AccEngineer.Telemetry.Broadcasting;

/// <summary>
/// Deterministic synthetic standings for the built-in test-data mode. Positions,
/// laps, gaps and pit states are a pure function of the supplied time so the same
/// session always produces the same tower. Driver names are clearly fictional
/// simulated entries (the overlay also shows a TEST DATA badge); this feed is only
/// ever used when the generated provider is active, never for a real session.
/// </summary>
public sealed class TestCompetitorFeed
{
    private const int LapTimeMs = 100_000;

    // Fictional, clearly-simulated driver names. Fixed so the feed stays deterministic
    // and so nothing resembling a real competitor is ever invented for live data.
    private static readonly string[] SimulatedDriverNames =
    {
        "K. Aldrin", "M. Vasquez", "T. Okafor", "R. Lindqvist", "J. Moreau",
        "D. Petrov", "S. Nakamura", "L. Rossi", "A. Brennan", "P. Kowalski",
        "H. Meier", "N. Abara", "G. Santos", "E. Laurent", "V. Novak",
        "B. Wilson", "F. Delgado", "I. Haugen", "W. Zhang", "O. Reyes"
    };

    // Documented ACC car model ids cycled across the synthetic field so the standings
    // badges and class grouping show a realistic spread of brands without inventing data.
    private static readonly byte[] SimulatedCarModels =
    {
        0,   // Porsche 991 GT3 R   (GT3)
        2,   // Ferrari 488 GT3     (GT3)
        1,   // Mercedes-AMG GT3    (GT3)
        3,   // Audi R8 LMS         (GT3)
        50,  // Alpine A110 GT4     (GT4)
        4    // Lamborghini Huracan (GT3)
    };

    private readonly int _carCount;
    private readonly int _trackMeters;

    public TestCompetitorFeed(int carCount = 20, int trackMeters = 4300)
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

        // Emit in official running order: CompetitorStandings' contract is that the
        // list is classified position-first (matching the ACC client), and consumers
        // such as the relative panel window their rows directly off that order.
        var playerDistance = distances[focusedCarIndex];
        var competitors = new List<Competitor>(_carCount);
        foreach (var i in order)
        {
            var distance = distances[i];
            var laps = (int)Math.Floor(distance);
            var spline = (float)(distance - laps);
            var delta = (int)Math.Round((playerDistance - distance) * LapTimeMs);
            var relativeTimeGapMs = (int)Math.Round(WrapToHalfLap(playerDistance - distance) * LapTimeMs);
            var inPit = i % 4 == 0;

            competitors.Add(new Competitor(
                (ushort)i,
                IsPlayer: i == focusedCarIndex,
                DriverShortName: null,
                DriverFullName: SimulatedDriverNames[(i - 1) % SimulatedDriverNames.Length],
                RaceNumber: (i * 7) % 99 + 1,
                TeamName: null,
                CupCategory: (AccCupCategory)(i % 5),
                Location: inPit ? AccCarLocation.Pitlane : AccCarLocation.Track,
                Position: positions[i],
                TrackPosition: positions[i],
                LapCount: laps,
                DeltaMs: delta,
                BestLapMs: LapTimeMs + (i % 5) * 250,
                LastLapMs: LapTimeMs + (i % 5) * 250 + 120,
                SplinePosition: spline,
                SpeedKmh: inPit ? 45 : 200 + (i % 4) * 8)
            {
                // Signed, half-lap-wrapped time gap to the player (negative = ahead),
                // in whole milliseconds so both cars ahead and behind display as
                // plausible seconds rather than lap fractions.
                RelativeTimeGapMs = relativeTimeGapMs,
                // Mixed field for the overlay's class grouping and brand badges: the model
                // ids cycle through a few documented GT3/GT4/GTC entries (991 GT3 R, 488
                // GT3, AMG GT3, R8 LMS, A110 GT4, Huracan GT3) so every badge colour is
                // exercised. Driver categories cycle Bronze..Platinum so the rating column
                // shows every tier.
                CarModelType = SimulatedCarModels[(i - 1) % SimulatedCarModels.Length],
                DriverCategory = (AccDriverCategory)((i - 1) % 4)
            });
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

    /// <summary>Wraps a lap difference into (-0.5, +0.5] laps so a full-lap separation
    /// reads as a proximity instead of a whole lap of time.</summary>
    private static double WrapToHalfLap(double deltaLaps) =>
        deltaLaps - Math.Floor(deltaLaps + 0.5);
}
