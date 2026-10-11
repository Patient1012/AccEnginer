namespace AccEngineer.Analysis;

/// <summary>Category of a detected telemetry event, mirroring the UI analysis groups.</summary>
public enum EventCategory
{
    Braking,
    CornerEntry,
    MidCorner,
    CornerExit,
    Traction,
    Tyres,
    Consistency,
    DataQuality
}

public enum EventSeverity
{
    Info,
    Low,
    Medium,
    High
}

/// <summary>
/// How much weight to place on an event. <see cref="Verified"/> means the
/// observation is a measured fact; <see cref="Probable"/> and
/// <see cref="Uncertain"/> mean the finding leans on an interpretation that the
/// available channels may not fully support.
/// </summary>
public enum EventConfidence
{
    Verified,
    Probable,
    Uncertain
}

/// <summary>
/// A pointer back to the telemetry a finding was derived from, so every event can
/// be traced to real samples (roadmap Milestone 4 acceptance criterion).
/// </summary>
public sealed record EvidenceReference
{
    public required string Kind { get; init; }

    public required string Description { get; init; }

    public int? LapNumber { get; init; }

    public long? FirstSampleId { get; init; }

    public long? LastSampleId { get; init; }

    public double? StartProgressMeters { get; init; }

    public double? EndProgressMeters { get; init; }
}

/// <summary>
/// A compact, traceable finding. <see cref="Observation"/> is a measured fact;
/// <see cref="Interpretation"/> is an explicitly unproven possible cause and must
/// never be presented as a diagnosis.
/// </summary>
public sealed record TelemetryEvent
{
    public required string Id { get; init; }

    public required EventCategory Category { get; init; }

    public required EventSeverity Severity { get; init; }

    public required string Observation { get; init; }

    public string? Interpretation { get; init; }

    public EventConfidence Confidence { get; init; } = EventConfidence.Verified;

    public int? LapNumber { get; init; }

    public double? ProgressMeters { get; init; }

    public required IReadOnlyList<EvidenceReference> Evidence { get; init; }

    public string? SuggestedTest { get; init; }

    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}

/// <summary>A contiguous braking zone within one lap.</summary>
public sealed record BrakingZone
{
    public required int LapNumber { get; init; }

    public double? StartProgressMeters { get; init; }

    public double? EndProgressMeters { get; init; }

    public int SampleCount { get; init; }

    public double? PeakBrakeInput { get; init; }

    public double? EntrySpeedKph { get; init; }

    public double? MinimumSpeedKph { get; init; }

    public double? DurationSeconds { get; init; }

    public long FirstSampleId { get; init; }

    public long LastSampleId { get; init; }
}

/// <summary>
/// A corner approximated from a contiguous run of steering input. Apex is taken as
/// the minimum-speed point, which is the best available proxy because ACC shared
/// memory exposes no corner geometry.
/// </summary>
public sealed record CornerAnalysis
{
    public required int LapNumber { get; init; }

    public double? EntryProgressMeters { get; init; }

    public double? ApexProgressMeters { get; init; }

    public double? ExitProgressMeters { get; init; }

    public double? MinimumSpeedKph { get; init; }

    public double? PeakSteeringInput { get; init; }

    public double? ThrottlePickupProgressMeters { get; init; }

    public double? ApexToFullThrottleSeconds { get; init; }

    public long FirstSampleId { get; init; }

    public long LastSampleId { get; init; }
}

/// <summary>Per-wheel tyre summary for one lap. Null statistics mean the channel was absent.</summary>
public sealed record TyreWheelSummary
{
    public required string Wheel { get; init; }

    public int PressureSampleCount { get; init; }

    public double? PressureMin { get; init; }

    public double? PressureMax { get; init; }

    public double? PressureAverage { get; init; }

    public int CoreTempSampleCount { get; init; }

    public double? CoreTempMinC { get; init; }

    public double? CoreTempMaxC { get; init; }

    public double? CoreTempAvgC { get; init; }
}

/// <summary>Deterministic driving analysis for a single lap.</summary>
public sealed record LapDrivingAnalysis
{
    public required int LapNumber { get; init; }

    public required IReadOnlyList<BrakingZone> BrakingZones { get; init; }

    public required IReadOnlyList<CornerAnalysis> Corners { get; init; }

    public required IReadOnlyList<TyreWheelSummary> Tyres { get; init; }

    /// <summary>Fraction of the lap (by sample count) spent at or above full-throttle threshold.</summary>
    public double? FullThrottleFraction { get; init; }

    /// <summary>Fraction of the lap (by sample count) under braking.</summary>
    public double? BrakingFraction { get; init; }
}

/// <summary>Result of the driving analysis across a whole session.</summary>
public sealed record DrivingAnalysisResult
{
    public required IReadOnlyList<LapDrivingAnalysis> Laps { get; init; }

    public required IReadOnlyList<TelemetryEvent> Events { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed class DrivingAnalysisOptions
{
    /// <summary>Brake input at or above this starts a braking zone.</summary>
    public double BrakeOnThreshold { get; init; } = 0.10;

    /// <summary>Brake input at or below this ends a braking zone (hysteresis).</summary>
    public double BrakeOffThreshold { get; init; } = 0.05;

    /// <summary>Braking zones shorter than this many samples are ignored.</summary>
    public int MinimumBrakingSamples { get; init; } = 3;

    /// <summary>Absolute steering input at or above this marks a corner.</summary>
    public double CornerSteeringThreshold { get; init; } = 0.15;

    /// <summary>Corners shorter than this many samples are ignored.</summary>
    public int MinimumCornerSamples { get; init; } = 3;

    /// <summary>Gaps (in samples) smaller than this are bridged when detecting corners.</summary>
    public int CornerMergeGapSamples { get; init; } = 3;

    /// <summary>Throttle input at or above this counts as a throttle pickup after the apex.</summary>
    public double ThrottlePickupThreshold { get; init; } = 0.5;

    /// <summary>Throttle input at or above this counts as full throttle.</summary>
    public double FullThrottleThreshold { get; init; } = 0.95;

    /// <summary>Cap on the number of emitted events, to avoid flooding reports.</summary>
    public int MaxEvents { get; init; } = 250;
}
