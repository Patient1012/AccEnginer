using AccEngineer.Telemetry;

namespace AccEngineer.Analysis;

/// <summary>How a lap time was determined.</summary>
public enum LapTimeSource
{
    /// <summary>No usable lap time.</summary>
    Unavailable = 0,

    /// <summary>ACC-reported last lap time (<c>iLastTime</c>).</summary>
    AccReported = 1,

    /// <summary>Computed from the boundary between this lap and the next.</summary>
    ComputedBoundary = 2,

    /// <summary>Time span of an incomplete lap (partial).</summary>
    PartialSpan = 3
}

/// <summary>A telemetry sample positioned within its lap.</summary>
public sealed record LapSample
{
    public required TelemetrySample Source { get; init; }

    /// <summary>Seconds since the first sample of the lap.</summary>
    public required double LapTimeSeconds { get; init; }

    /// <summary>Lap-relative progress in metres, when the source provided it.</summary>
    public double? ProgressMeters { get; init; }
}

/// <summary>One sector within a lap, derived from changes in the sector index.</summary>
public sealed record SectorTimeline
{
    public required int SectorIndex { get; init; }

    public double? StartProgressMeters { get; init; }

    public double? StartLapTimeSeconds { get; init; }

    /// <summary>Sector duration in seconds; null when the lap time is unknown.</summary>
    public double? TimeSeconds { get; init; }
}

/// <summary>One lap reconstructed from a contiguous run of samples.</summary>
public sealed record LapTimeline
{
    public required string SessionId { get; init; }

    public required int LapNumber { get; init; }

    /// <summary>True when a subsequent lap in the same session was observed (i.e. the lap finished).</summary>
    public required bool IsComplete { get; init; }

    /// <summary>
    /// True only when the lap is complete and every sample reports a valid lap
    /// with no invalid-lap quality flag. Invalid laps are never silently dropped;
    /// they are carried with IsValid = false.
    /// </summary>
    public required bool IsValid { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset EndedAtUtc { get; init; }

    public double? LapTimeSeconds { get; init; }

    public LapTimeSource LapTimeSource { get; init; } = LapTimeSource.Unavailable;

    public int? SectorCount { get; init; }

    public required IReadOnlyList<LapSample> Samples { get; init; }

    public required IReadOnlyList<SectorTimeline> Sectors { get; init; }
}

/// <summary>Result of analysing all laps in a session recording.</summary>
public sealed record SessionLapAnalysis
{
    public required string? SessionId { get; init; }

    public string? TrackName { get; init; }

    public string? CarName { get; init; }

    public SessionType SessionType { get; init; } = SessionType.Unknown;

    public required IReadOnlyList<LapTimeline> Laps { get; init; }

    /// <summary>Sample runs too short to form a lap, or otherwise unusable.</summary>
    public int SkippedLapCount { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed class LapAnalysisOptions
{
    /// <summary>Runs shorter than this are skipped rather than reported as laps.</summary>
    public int MinimumSamplesPerLap { get; init; } = 3;
}
