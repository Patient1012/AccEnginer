namespace AccEngineer.Analysis.Ai;

/// <summary>
/// A compact, ranked finding formatted for the AI. It carries only the summary and
/// the numbers the AI needs to reason about, never the full telemetry.
/// </summary>
public sealed record AiFinding
{
    public required int Rank { get; init; }

    public required string Id { get; init; }

    public required string Category { get; init; }

    public required string Severity { get; init; }

    public string? ConfidenceLabel { get; init; }

    public required double Confidence { get; init; }

    public int? CornerNumber { get; init; }

    public double? StartDistanceMeters { get; init; }

    public double? EndDistanceMeters { get; init; }

    public double? EstimatedLossSeconds { get; init; }

    public required string Observation { get; init; }

    public required IReadOnlyList<string> Evidence { get; init; }

    public IReadOnlyList<string> PossibleCauses { get; init; } = Array.Empty<string>();

    public string? Recommendation { get; init; }

    public string? ValidationMethod { get; init; }
}
