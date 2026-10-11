namespace AccEngineer.Reporting;

/// <summary>
/// A live event report (roadmap §10 type 1): one meaningful event with a small
/// evidence window. Deterministic fields are always present; an optional AI
/// interpretation is attached separately so a possible cause is never presented
/// as a measurement.
/// </summary>
public sealed record LiveEventReport
{
    public required int SchemaVersion { get; init; }

    public required DateTimeOffset GeneratedAtUtc { get; init; }

    public required string EventId { get; init; }

    public required ReportSession Session { get; init; }

    public required ReportDataQuality DataQuality { get; init; }

    public required LiveEventFinding Finding { get; init; }

    public LiveEventInterpretation? Interpretation { get; init; }

    public required IReadOnlyList<string> Notes { get; init; }
}

/// <summary>Deterministic portion of a live event: measured facts only.</summary>
public sealed record LiveEventFinding
{
    /// <summary>Report category key, for example "Braking" or "CornerExit".</summary>
    public required string Category { get; init; }

    public required string Severity { get; init; }

    public required string Confidence { get; init; }

    public int? LapNumber { get; init; }

    public double? ProgressMeters { get; init; }

    public required string Observation { get; init; }

    /// <summary>Deterministic interpretation from the analysis engine, if any.</summary>
    public string? Interpretation { get; init; }

    public required IReadOnlyList<ReportEvidence> Evidence { get; init; }

    /// <summary>Number of raw samples attached to the evidence window.</summary>
    public int EvidenceSampleCount { get; init; }

    public string? SuggestedTest { get; init; }

    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}

/// <summary>A pointer to the telemetry an event was derived from, plus coalescing info.</summary>
public sealed record ReportEvidence
{
    public required string Kind { get; init; }

    public required string Description { get; init; }

    public long? FirstSampleId { get; init; }

    public long? LastSampleId { get; init; }

    public double? StartProgressMeters { get; init; }

    public double? EndProgressMeters { get; init; }

    /// <summary>How many raw events were folded into this one by deduplication.</summary>
    public int CoalescedCount { get; init; } = 1;
}

/// <summary>
/// Provider-neutral AI interpretation. <see cref="Status"/> records how the
/// attempt ended so a failed or skipped interpretation is explicit.
/// </summary>
public sealed record LiveEventInterpretation
{
    /// <summary>"ok", "noProvider", "unavailable", "invalid" or "cancelled".</summary>
    public required string Status { get; init; }

    public string? Provider { get; init; }

    public string? FindingType { get; init; }

    public string? Observation { get; init; }

    public string? PossibleCause { get; init; }

    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

    public double? Confidence { get; init; }

    public string? SuggestedTest { get; init; }

    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();

    /// <summary>Detail for non-ok statuses.</summary>
    public string? Message { get; init; }
}

/// <summary>
/// Session summary (roadmap §10 type 3): overall trends, best valid laps and
/// consolidated findings. Fully deterministic; no AI is required.
/// </summary>
public sealed record SessionSummaryReport
{
    public required int SchemaVersion { get; init; }

    public required DateTimeOffset GeneratedAtUtc { get; init; }

    public required ReportSession Session { get; init; }

    public required ReportDataQuality DataQuality { get; init; }

    public required SessionSummaryLaps Laps { get; init; }

    public required IReadOnlyList<SessionSummaryFinding> Findings { get; init; }

    public required IReadOnlyList<string> Notes { get; init; }
}

public sealed record SessionSummaryLaps
{
    public required int LapCount { get; init; }

    public required int ValidLapCount { get; init; }

    public double? BestLapSeconds { get; init; }

    public int? BestLapNumber { get; init; }

    public required IReadOnlyList<ReportLap> Laps { get; init; }
}

public sealed record SessionSummaryFinding
{
    public required string Category { get; init; }

    public required string Severity { get; init; }

    public required string Observation { get; init; }

    public int Occurrences { get; init; } = 1;
}
