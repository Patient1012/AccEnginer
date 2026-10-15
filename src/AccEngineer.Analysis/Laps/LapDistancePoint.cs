using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Laps;

/// <summary>
/// A lap sample positioned by track distance. <see cref="IsEstimatedDistance"/> is
/// true when the simulator did not report progress and the distance was integrated
/// from speed; estimated positions are never presented as measured facts.
/// </summary>
public sealed record LapDistancePoint
{
    public required LapSample Sample { get; init; }

    /// <summary>Resolved track distance in metres, or null when it could not be resolved.</summary>
    public double? DistanceMeters { get; init; }

    public bool IsEstimatedDistance { get; init; }
}
