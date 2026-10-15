namespace AccEngineer.Analysis.Events;

/// <summary>Kinds of discrete driving event the detector can emit.</summary>
public enum DrivingEventKind
{
    Braking,
    ThrottlePickup,
    ThrottleLift,
    TurnIn,
    Apex,
    CornerExit,
    GearChange,
    SteeringCorrection
}

/// <summary>
/// A reusable, measured driving event. Events are computed once from telemetry and
/// then consumed by later analysis instead of re-scanning raw samples. Distances are
/// in metres, times in seconds. <see cref="Confidence"/> is a heuristic weight, not a
/// probability.
/// </summary>
public sealed record DrivingEvent
{
    public required DrivingEventKind Kind { get; init; }

    public int? LapNumber { get; init; }

    /// <summary>Corner number when the event is associated with a defined corner.</summary>
    public int? CornerNumber { get; init; }

    public double? StartDistanceMeters { get; init; }

    public double? EndDistanceMeters { get; init; }

    public double? StartTimeSeconds { get; init; }

    public double? EndTimeSeconds { get; init; }

    /// <summary>Named measured values that describe the event.</summary>
    public IReadOnlyDictionary<string, double> Values { get; init; } =
        new Dictionary<string, double>();

    public double Confidence { get; init; }

    public long? FirstSampleId { get; init; }

    public long? LastSampleId { get; init; }
}

/// <summary>
/// Documented heuristics for event confidence. Measured channels score higher than
/// events derived from proxies such as steering magnitude.
/// </summary>
public static class DrivingEventConfidence
{
    public const double Braking = 0.95;
    public const double ThrottlePickup = 0.90;
    public const double ThrottleLift = 0.85;
    public const double GearChange = 0.98;
    public const double TurnIn = 0.70;
    public const double Apex = 0.70;
    public const double CornerExit = 0.65;
    public const double SteeringCorrection = 0.60;
}
