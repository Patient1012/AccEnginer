namespace AccEngineer.Analysis.Indicators;

/// <summary>Phase of a corner an indicator relates to.</summary>
public enum CornerPhase
{
    Entry,
    MidCorner,
    Exit
}

/// <summary>
/// Handling behaviour indicators. These are explicitly weighted signals, not
/// diagnoses: the AI interprets the evidence, and every indicator states the proxy
/// it relies on and its confidence.
/// </summary>
public enum BehaviourIndicatorKind
{
    PossibleEntryUndersteer,
    PossibleMidCornerUndersteer,
    PossibleExitUndersteer,
    PossibleEntryOversteer,
    PossibleMidCornerOversteer,
    PossibleExitOversteer,
    ExcessiveSteeringCorrection,
    PossibleLockUp,
    PossibleTractionLoss
}

/// <summary>A measured behavioural indicator with its supporting evidence.</summary>
public sealed record BehaviourIndicator
{
    public required BehaviourIndicatorKind Kind { get; init; }

    public int? CornerNumber { get; init; }

    public CornerPhase? Phase { get; init; }

    public double? DistanceMeters { get; init; }

    /// <summary>What the telemetry shows, stated as an observation.</summary>
    public required string Observation { get; init; }

    /// <summary>Numbers and measured patterns supporting the observation.</summary>
    public required IReadOnlyList<string> Evidence { get; init; }

    /// <summary>Heuristic weight, not a probability.</summary>
    public double Confidence { get; init; }

    /// <summary>What the indicator cannot establish from the available channels.</summary>
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}
