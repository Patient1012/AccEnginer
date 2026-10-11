using AccEngineer.Analysis;
using AccEngineer.Reporting;

namespace AccEngineer.Pipeline;

/// <summary>Lifecycle states for a queued live event.</summary>
public enum LiveEventState
{
    Queued,
    Interpreting,
    Completed,
    Dropped
}

/// <summary>Configurable limits for the live event pipeline (roadmap §9, §14).</summary>
public sealed class LiveEventPipelineOptions
{
    /// <summary>How many recent samples the evidence buffer retains.</summary>
    public int EvidenceBufferCapacity { get; set; } = 4000;

    /// <summary>Progress tolerance (metres) when falling back to a location window.</summary>
    public double EvidenceFallbackToleranceMeters { get; set; } = 50;

    /// <summary>Deduplication bucket size along the lap, in metres.</summary>
    public double DedupBucketMeters { get; set; } = 100;

    /// <summary>Repeats of the same key inside this window are coalesced.</summary>
    public TimeSpan DedupCooldown { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Maximum queued events; the oldest is dropped when full.</summary>
    public int MaxQueue { get; set; } = 100;

    /// <summary>Maximum AI interpretation requests started per minute.</summary>
    public int MaxRequestsPerMinute { get; set; } = 12;

    /// <summary>Timeout applied to a single interpretation request.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>Whether to call the AI provider at all. Reports are emitted either way.</summary>
    public bool Interpret { get; set; } = true;

    /// <summary>Maximum raw samples attached to one interpretation request.</summary>
    public int MaxRequestSamples { get; set; } = 40;
}

/// <summary>A live event moving through the pipeline, with its evidence and report.</summary>
public sealed class LiveEventItem
{
    public required string Id { get; init; }

    public required TelemetryEvent Finding { get; init; }

    public required ReportSession Session { get; init; }

    public required IReadOnlyList<ReportEvidence> Evidence { get; set; }

    public required IReadOnlyList<string> DataQualityWarnings { get; init; }

    public int EvidenceSampleCount { get; set; }

    public int CoalescedCount { get; set; } = 1;

    public LiveEventState State { get; set; } = LiveEventState.Queued;

    public LiveEventReport? Report { get; set; }

    public string? InterpretationStatus => Report?.Interpretation?.Status;

    public string Observation => Finding.Observation;

    public string Category => Finding.Category.ToString();

    public string Severity => Finding.Severity.ToString();

    public int? LapNumber => Finding.LapNumber;
}
