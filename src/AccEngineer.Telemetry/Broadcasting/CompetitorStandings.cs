namespace AccEngineer.Telemetry.Broadcasting;

/// <summary>
/// One competitor as assembled from the broadcasting entry list and realtime car
/// updates. Every field is nullable when the source has not reported it; nothing
/// is defaulted to a plausible-looking value.
/// </summary>
public sealed record Competitor(
    ushort CarIndex,
    bool IsPlayer,
    string? DriverShortName,
    string? DriverFullName,
    int? RaceNumber,
    string? TeamName,
    AccCupCategory? CupCategory,
    AccCarLocation Location,
    int? Position,
    int? TrackPosition,
    int? LapCount,
    int? DeltaMs,
    int? BestLapMs,
    int? LastLapMs,
    float SplinePosition,
    int? SpeedKmh)
{
    /// <summary>Best available display name: short name, then full name, then the car index.</summary>
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(DriverShortName) ? DriverShortName
        : !string.IsNullOrWhiteSpace(DriverFullName) ? DriverFullName
        : $"CAR {CarIndex:00}";

    /// <summary>True when the car is in the pit lane or its entry/exit.</summary>
    public bool IsInPit =>
        Location is AccCarLocation.Pitlane or AccCarLocation.PitEntry or AccCarLocation.PitExit;
}

/// <summary>
/// A point-in-time view of the session standings from the ACC Broadcasting API.
/// <see cref="Competitors"/> is ordered by official position (unclassified cars
/// last). Gaps between cars are intentionally not stored here; the overlay
/// derives metre gaps from <see cref="SplinePosition"/> and <see cref="TrackMeters"/>.
/// </summary>
public sealed record CompetitorStandings(
    string? TrackName,
    int? TrackMeters,
    float SessionTimeMs,
    byte SessionPhase,
    int FocusedCarIndex,
    IReadOnlyList<Competitor> Competitors,
    DateTimeOffset ReceivedAtUtc)
{
    /// <summary>The player's competitor entry when the focused car is present.</summary>
    public Competitor? Player =>
        Competitors.FirstOrDefault(c => c.CarIndex == FocusedCarIndex);
}
