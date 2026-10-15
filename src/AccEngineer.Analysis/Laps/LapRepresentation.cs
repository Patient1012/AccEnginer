using AccEngineer.Analysis.Metrics;
using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Laps;

/// <summary>
/// The common, distance-based representation of a lap the rest of the engine works
/// from: metadata, total time, sector times, distance-positioned samples and the
/// fundamental metrics. Corners are attached later, once a corner model exists.
/// </summary>
public sealed record LapRepresentation
{
    public required string SessionId { get; init; }

    public required int LapNumber { get; init; }

    public string? TrackName { get; init; }

    public string? CarName { get; init; }

    public SessionType SessionType { get; init; } = SessionType.Unknown;

    public required bool IsComplete { get; init; }

    public required bool IsValid { get; init; }

    public double? TotalTimeSeconds { get; init; }

    public LapTimeSource LapTimeSource { get; init; } = LapTimeSource.Unavailable;

    public required IReadOnlyList<LapDistancePoint> Samples { get; init; }

    public required IReadOnlyList<SectorTimeline> Sectors { get; init; }

    public required LapMetrics Metrics { get; init; }

    /// <summary>Human-readable warnings surfaced while building this lap.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
