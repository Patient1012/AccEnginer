using AccEngineer.Analysis.Corners;

namespace AccEngineer.Analysis.Comparison;

/// <summary>
/// Measured differences and an estimated time loss for one corner. The differences
/// are facts; <see cref="EstimatedTimeLossSeconds"/> is derived from the lap-delta map
/// and is explicitly an estimate, never a diagnosis.
/// </summary>
public sealed record CornerTimeLoss
{
    public required int CornerNumber { get; init; }

    public string? CornerName { get; init; }

    public double? WindowStartMeters { get; init; }

    public double? WindowEndMeters { get; init; }

    public double? ReferenceBrakePointMeters { get; init; }

    public double? CurrentBrakePointMeters { get; init; }

    public double? BrakePointDeltaMeters { get; init; }

    public double? ReferenceMinimumSpeedKph { get; init; }

    public double? CurrentMinimumSpeedKph { get; init; }

    public double? MinimumSpeedDeltaKph { get; init; }

    public double? ReferenceThrottlePickupMeters { get; init; }

    public double? CurrentThrottlePickupMeters { get; init; }

    public double? ThrottlePickupDeltaMeters { get; init; }

    /// <summary>Estimated time lost (+) or gained (-) across the corner window, in seconds.</summary>
    public double? EstimatedTimeLossSeconds { get; init; }

    /// <summary>False when the delta map or corner channels were incomplete for this corner.</summary>
    public bool HasSufficientEvidence { get; init; }
}

public sealed record TimeLossAnalysis
{
    public required int ReferenceLapNumber { get; init; }

    public required int CurrentLapNumber { get; init; }

    public double? TotalDeltaSeconds { get; init; }

    public required IReadOnlyList<CornerTimeLoss> Corners { get; init; }
}

/// <summary>
/// Turns lap deltas and corner measurements into per-corner time-loss estimates. It
/// reports where time is being lost without asserting a cause, and marks corners
/// where evidence is insufficient rather than guessing.
/// </summary>
public sealed class TimeLossCalculator
{
    private readonly CornerAnalysisOptions _cornerOptions;
    private readonly LapComparisonOptions _comparisonOptions;
    private readonly CornerAnalyzer _cornerAnalyzer;

    public TimeLossCalculator(
        CornerAnalysisOptions? cornerOptions = null,
        LapComparisonOptions? comparisonOptions = null)
    {
        _cornerOptions = cornerOptions ?? new CornerAnalysisOptions();
        _comparisonOptions = comparisonOptions ?? new LapComparisonOptions();
        _cornerAnalyzer = new CornerAnalyzer(_cornerOptions);
    }

    public TimeLossAnalysis Calculate(
        LapTimeline reference,
        LapTimeline current,
        TrackCornerDefinition? corners)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(current);

        var comparison = new LapComparer(_comparisonOptions).Compare(reference, new[] { current });
        var delta = comparison.Laps.FirstOrDefault();

        double? totalDelta = reference.LapTimeSeconds is { } r && current.LapTimeSeconds is { } c
            ? c - r
            : delta?.TotalDeltaSeconds;

        if (corners is null || corners.Corners.Count == 0 || delta is null)
        {
            return new TimeLossAnalysis
            {
                ReferenceLapNumber = reference.LapNumber,
                CurrentLapNumber = current.LapNumber,
                TotalDeltaSeconds = totalDelta,
                Corners = Array.Empty<CornerTimeLoss>()
            };
        }

        var results = new List<CornerTimeLoss>(corners.Corners.Count);
        foreach (var corner in corners.Corners)
        {
            results.Add(BuildCorner(reference, current, corner, delta.Points));
        }

        return new TimeLossAnalysis
        {
            ReferenceLapNumber = reference.LapNumber,
            CurrentLapNumber = current.LapNumber,
            TotalDeltaSeconds = totalDelta,
            Corners = results
        };
    }

    private CornerTimeLoss BuildCorner(
        LapTimeline reference,
        LapTimeline current,
        Corner corner,
        IReadOnlyList<DeltaPoint> deltaPoints)
    {
        var referenceMetrics = _cornerAnalyzer.Analyze(reference, corner);
        var currentMetrics = _cornerAnalyzer.Analyze(current, corner);

        var windowStart = corner.StartDistanceMeters
                          ?? corner.TurnInDistanceMeters
                          ?? corner.ApexDistanceMeters - _cornerOptions.EntryLookaheadMeters;
        var windowEnd = corner.EndDistanceMeters
                        ?? corner.ExitDistanceMeters
                        ?? corner.ApexDistanceMeters + _cornerOptions.ExitLookaheadMeters;

        var startDelta = DeltaAt(deltaPoints, windowStart);
        var endDelta = DeltaAt(deltaPoints, windowEnd);
        double? estimated = startDelta is { } s && endDelta is { } e ? e - s : null;

        var brakeDelta = Delta(currentMetrics.BrakePointMeters, referenceMetrics.BrakePointMeters);
        var speedDelta = Delta(currentMetrics.MinimumSpeedKph, referenceMetrics.MinimumSpeedKph);
        var pickupDelta = Delta(currentMetrics.ThrottlePickupMeters, referenceMetrics.ThrottlePickupMeters);

        var sufficient = estimated is not null;

        return new CornerTimeLoss
        {
            CornerNumber = corner.Number,
            CornerName = corner.Name,
            WindowStartMeters = windowStart,
            WindowEndMeters = windowEnd,
            ReferenceBrakePointMeters = referenceMetrics.BrakePointMeters,
            CurrentBrakePointMeters = currentMetrics.BrakePointMeters,
            BrakePointDeltaMeters = brakeDelta,
            ReferenceMinimumSpeedKph = referenceMetrics.MinimumSpeedKph,
            CurrentMinimumSpeedKph = currentMetrics.MinimumSpeedKph,
            MinimumSpeedDeltaKph = speedDelta,
            ReferenceThrottlePickupMeters = referenceMetrics.ThrottlePickupMeters,
            CurrentThrottlePickupMeters = currentMetrics.ThrottlePickupMeters,
            ThrottlePickupDeltaMeters = pickupDelta,
            EstimatedTimeLossSeconds = estimated,
            HasSufficientEvidence = sufficient
        };
    }

    private static double? Delta(double? current, double? reference)
        => current is { } c && reference is { } r ? c - r : null;

    private static double? DeltaAt(IReadOnlyList<DeltaPoint> points, double distance)
    {
        // Linear interpolation over points that carry a delta.
        DeltaPoint? previous = null;
        foreach (var point in points)
        {
            if (point.DeltaSeconds is null) continue;

            if (point.ProgressMeters >= distance)
            {
                if (previous is null || previous.ProgressMeters == point.ProgressMeters)
                {
                    return point.DeltaSeconds;
                }

                var span = point.ProgressMeters - previous.ProgressMeters;
                if (span <= 0) return point.DeltaSeconds;

                var ratio = (distance - previous.ProgressMeters) / span;
                return previous.DeltaSeconds!.Value +
                       (point.DeltaSeconds.Value - previous.DeltaSeconds.Value) * ratio;
            }

            previous = point;
        }

        return previous?.DeltaSeconds;
    }
}
