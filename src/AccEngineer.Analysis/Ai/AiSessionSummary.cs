namespace AccEngineer.Analysis.Ai;

/// <summary>
/// The compact, self-contained analysis package handed to the AI. It contains session
/// context, lap times and only the highest-value ranked findings, so the AI reasons over
/// structured evidence rather than raw telemetry.
/// </summary>
public sealed record AiSessionSummary
{
    public string? Track { get; init; }

    public string? Car { get; init; }

    public string? Session { get; init; }

    public double? BestLapSeconds { get; init; }

    public double? ReferenceLapSeconds { get; init; }

    public double? CurrentLapSeconds { get; init; }

    /// <summary>Best/Reference/Current formatted as "m:ss.fff" where known.</summary>
    public string? BestLap { get; init; }

    public string? ReferenceLap { get; init; }

    public string? CurrentLap { get; init; }

    public double? TotalDeltaSeconds { get; init; }

    public required IReadOnlyList<AiFinding> TopFindings { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
