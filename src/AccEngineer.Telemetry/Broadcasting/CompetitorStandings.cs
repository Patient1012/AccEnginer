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

    /// <summary>
    /// Signed time gap to the focused car in milliseconds, wrapped to the nearest half
    /// lap: negative means ahead of the focused car, positive means behind. Only a
    /// source that genuinely knows every car's position in time populates it (the
    /// deterministic test feed); the real ACC Broadcasting API reports no opponent time
    /// gaps, so it stays null and the overlay falls back to a pace-profile estimate.
    /// </summary>
    public int? RelativeTimeGapMs { get; init; }

    /// <summary>
    /// ACC car model id from the broadcast entry list (the same id space as the Kunos
    /// server handbook's car model list). Null when the entry list has not reported the
    /// car yet — never defaulted, so <see cref="CarClass"/> stays unknown without data.
    /// </summary>
    public byte? CarModelType { get; init; }

    /// <summary>
    /// Categorisation of the current driver (Bronze/Silver/Gold/Platinum) from the
    /// broadcast entry list. Null when the entry list has not reported the driver.
    /// This is the driver's tier; it is not the safety rating, which ACC exposes to no
    /// telemetry API.
    /// </summary>
    public AccDriverCategory? DriverCategory { get; init; }

    /// <summary>Vehicle class (GT3/GT4/...) derived from <see cref="CarModelType"/>,
    /// or unknown when the model id is missing or undocumented.</summary>
    public AccCarClass CarClass =>
        CarModelType is { } modelId ? AccCarModelCatalog.ClassOf(modelId) : AccCarClass.Unknown;
}

/// <summary>
/// A point-in-time view of the session standings from the ACC Broadcasting API.
/// <see cref="Competitors"/> is ordered by official position (unclassified cars
/// last). Gaps between cars are intentionally not stored here; the overlay derives
/// them from <see cref="SplinePosition"/> and <see cref="TrackMeters"/> (distances)
/// and from <see cref="Competitor.RelativeTimeGapMs"/> or the player's pace profile
/// (times).
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
