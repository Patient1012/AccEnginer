using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Telemetry;

/// <summary>
/// Represents telemetry after processing, while preserving raw source.
/// </summary>
public sealed record ProcessedSample
{
    public required TelemetrySample Raw { get; init; }

    public bool IsProcessed { get; init; }

    public bool HasGap { get; init; }

    public bool IsInterpolated { get; init; }

    /// <summary>Track distance in metres; normalised/validated if applicable.</summary>
    public float? TrackDistanceMeters { get; init; }

    /// <summary>Speed in km/h (normalised form).</summary>
    public float? SpeedKph { get; init; }

    /// <summary>Speed in m/s (normalised form).</summary>
    public float? SpeedMs { get; init; }
}