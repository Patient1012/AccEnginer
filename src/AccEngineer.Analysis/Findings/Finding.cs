namespace AccEngineer.Analysis.Findings;

/// <summary>Where on the track a finding applies.</summary>
public sealed record TrackLocation
{
    public int? LapNumber { get; init; }

    public int? CornerNumber { get; init; }

    public double? StartDistanceMeters { get; init; }

    public double? EndDistanceMeters { get; init; }
}

/// <summary>A single piece of evidence supporting a finding.</summary>
public sealed record FindingEvidence
{
    public required string Description { get; init; }

    /// <summary>Optional measured value associated with the evidence.</summary>
    public double? Value { get; init; }

    public long? FirstSampleId { get; init; }

    public long? LastSampleId { get; init; }
}

/// <summary>
/// The standard structure for everything the analysis engine discovers.
/// <see cref="Observation"/> is the measured fact; <see cref="PossibleCauses"/> are
/// explicitly unproven explanations and must never be presented as diagnoses.
/// </summary>
public sealed record Finding
{
    public required string Id { get; init; }

    public required EventCategory Category { get; init; }

    public required EventSeverity Severity { get; init; }

    public required EventConfidence Confidence { get; init; }

    /// <summary>Quantitative confidence in the range 0..1 backing <see cref="Confidence"/>.</summary>
    public double ConfidenceScore { get; init; }

    public TrackLocation? Location { get; init; }

    /// <summary>What the telemetry definitely shows.</summary>
    public required string Observation { get; init; }

    public required IReadOnlyList<FindingEvidence> Evidence { get; init; }

    public IReadOnlyList<string> PossibleCauses { get; init; } = Array.Empty<string>();

    /// <summary>What the driver could investigate or test.</summary>
    public string? Recommendation { get; init; }

    /// <summary>How the driver can tell whether a change helped.</summary>
    public string? ValidationMethod { get; init; }

    /// <summary>Estimated time opportunity for this finding, in seconds.</summary>
    public double? EstimatedTimeLossSeconds { get; init; }

    /// <summary>How many times the pattern occurred (repeatability signal).</summary>
    public int Frequency { get; init; } = 1;
}
