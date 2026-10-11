namespace AccEngineer.AI;

/// <summary>Outcome of an analysis attempt. Failures are values, never exceptions.</summary>
public enum AnalysisOutcome
{
    /// <summary>The provider returned a usable, schema-valid result.</summary>
    Success,

    /// <summary>No provider is configured; deterministic output is unaffected.</summary>
    NoProvider,

    /// <summary>The provider replied, but the payload was missing or malformed.</summary>
    InvalidResponse,

    /// <summary>The provider could not be reached, returned an error status, or timed out.</summary>
    Unavailable,

    /// <summary>The request was cancelled before a result was produced.</summary>
    Cancelled
}

/// <summary>Session and lap context supplied to the model.</summary>
public sealed record AnalysisContext
{
    public string? Simulator { get; init; } = "Assetto Corsa Competizione";

    public string? Track { get; init; }

    public string? Car { get; init; }

    public string? SessionType { get; init; }

    public int? LapNumber { get; init; }

    public int? ReferenceLapNumber { get; init; }

    public double? LapTimeSeconds { get; init; }

    public double? ReferenceLapTimeSeconds { get; init; }
}

/// <summary>A single calculated metric presented to the model.</summary>
public sealed record AnalysisMetric(string Name, string Value)
{
    public string? Unit { get; init; }
}

/// <summary>
/// A small window of raw samples around an event. Only a handful of values are
/// sent; a whole high-frequency stream is never forwarded to the model.
/// </summary>
public sealed record SampleExcerpt
{
    public long SampleId { get; init; }

    public double? ProgressMeters { get; init; }

    public double? SpeedKph { get; init; }

    public double? Throttle { get; init; }

    public double? Brake { get; init; }

    public double? Steering { get; init; }
}

/// <summary>
/// Provider-neutral request. It carries measured facts, a compact sample window,
/// quality warnings and an explicit question, and instructs the model to keep
/// observation, interpretation and recommendation separate.
/// </summary>
public sealed record AnalysisRequest
{
    public required AnalysisContext Context { get; init; }

    public required string EventCategory { get; init; }

    /// <summary>The measured observation, phrased as a fact.</summary>
    public required string EventDescription { get; init; }

    public IReadOnlyList<AnalysisMetric> Metrics { get; init; } = Array.Empty<AnalysisMetric>();

    public IReadOnlyList<SampleExcerpt> Samples { get; init; } = Array.Empty<SampleExcerpt>();

    public IReadOnlyList<string> DataQualityWarnings { get; init; } = Array.Empty<string>();

    public string? ComparisonSummary { get; init; }

    public string? Question { get; init; }
}

/// <summary>
/// Schema-validated model output. <see cref="Observation"/> restates the measured
/// finding; <see cref="Interpretation"/> is a possible cause and must never be
/// presented as a diagnosis. On failure the value carries the outcome and message
/// instead of throwing.
/// </summary>
public sealed record AnalysisResult
{
    public required AnalysisOutcome Outcome { get; init; }

    public string? FindingType { get; init; }

    public string? Observation { get; init; }

    public string? Interpretation { get; init; }

    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

    public double? Confidence { get; init; }

    public string? SuggestedTest { get; init; }

    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();

    /// <summary>Provider name that produced the result, when applicable.</summary>
    public string? Provider { get; init; }

    /// <summary>Human-readable detail for non-success outcomes.</summary>
    public string? Message { get; init; }

    public bool IsSuccess => Outcome == AnalysisOutcome.Success;

    public static AnalysisResult Failure(AnalysisOutcome outcome, string message) => new()
    {
        Outcome = outcome,
        Message = message
    };
}
