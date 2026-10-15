using AccEngineer.Analysis.Comparison;
using AccEngineer.Analysis.Indicators;

namespace AccEngineer.Analysis.Findings;

public sealed class FindingBuilderOptions
{
    /// <summary>Smallest estimated time loss (seconds) that becomes a finding.</summary>
    public double MinimumTimeLossSeconds { get; init; } = 0.05;

    public double BrakeDeltaReferenceMeters { get; init; } = 10.0;

    public double SpeedDeltaReferenceKph { get; init; } = 5.0;

    public double PickupDeltaReferenceMeters { get; init; } = 10.0;
}

/// <summary>
/// Converts calculated analysis into the standard <see cref="Finding"/> structure.
/// It reports measurements and offers explicitly-unproven possible causes and tests;
/// it never diagnoses.
/// </summary>
public sealed class FindingBuilder
{
    private readonly FindingBuilderOptions _options;
    private readonly ConfidenceCalculator _confidence = new();

    public FindingBuilder(FindingBuilderOptions? options = null)
    {
        _options = options ?? new FindingBuilderOptions();
    }

    public IReadOnlyList<Finding> FromTimeLoss(TimeLossAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var findings = new List<Finding>();
        var sequence = 0;

        foreach (var corner in analysis.Corners)
        {
            if (corner.EstimatedTimeLossSeconds is not { } loss ||
                Math.Abs(loss) < _options.MinimumTimeLossSeconds)
            {
                continue;
            }

            var category = DominantCategory(corner);
            var coverage = corner.HasSufficientEvidence ? 1.0 : 0.3;
            var adequacy = Adequacy(corner);
            var score = _confidence.Score(new ConfidenceFactors
            {
                ChannelCoverage = coverage,
                SampleAdequacy = adequacy,
                Repeatability = 1.0
            });

            findings.Add(new Finding
            {
                Id = $"finding-tl-{analysis.CurrentLapNumber}-{corner.CornerNumber}-{++sequence}",
                Category = category,
                Severity = SeverityForLoss(loss),
                Confidence = _confidence.Classify(score),
                ConfidenceScore = score,
                Location = new TrackLocation
                {
                    LapNumber = analysis.CurrentLapNumber,
                    CornerNumber = corner.CornerNumber,
                    StartDistanceMeters = corner.WindowStartMeters,
                    EndDistanceMeters = corner.WindowEndMeters
                },
                Observation = BuildObservation(corner, loss),
                Evidence = BuildEvidence(corner),
                PossibleCauses = new[]
                {
                    "The cause is not determined from telemetry alone; car balance, driver input and track conditions are all candidates."
                },
                Recommendation =
                    "Compare the reference and current traces through this corner and review the largest listed difference first.",
                ValidationMethod =
                    "Re-run the comparison and check whether the estimated time loss in this window decreases after the change.",
                EstimatedTimeLossSeconds = loss,
                Frequency = 1
            });
        }

        return findings;
    }

    public IReadOnlyList<Finding> FromIndicators(IEnumerable<BehaviourIndicator> indicators, int? lapNumber)
    {
        ArgumentNullException.ThrowIfNull(indicators);

        var findings = new List<Finding>();
        var sequence = 0;

        foreach (var indicator in indicators)
        {
            findings.Add(new Finding
            {
                Id = $"finding-ind-{lapNumber}-{indicator.CornerNumber}-{++sequence}",
                Category = CategoryForIndicator(indicator.Kind),
                Severity = EventSeverity.Low,
                Confidence = _confidence.Classify(indicator.Confidence),
                ConfidenceScore = indicator.Confidence,
                Location = new TrackLocation
                {
                    LapNumber = lapNumber,
                    CornerNumber = indicator.CornerNumber,
                    StartDistanceMeters = indicator.DistanceMeters,
                    EndDistanceMeters = indicator.DistanceMeters
                },
                Observation = indicator.Observation,
                Evidence = indicator.Evidence
                    .Select(e => new FindingEvidence { Description = e })
                    .ToArray(),
                PossibleCauses = new[]
                {
                    "This is a weighted indicator, not a diagnosis; several causes could produce it."
                },
                Recommendation = "Investigate the referenced corner phase before changing setup or technique.",
                ValidationMethod = "Confirm the indicator disappears (or weakens) in a later comparison.",
                Frequency = 1
            });
        }

        return findings;
    }

    private EventCategory DominantCategory(CornerTimeLoss corner)
    {
        var brake = Math.Abs(corner.BrakePointDeltaMeters ?? 0) / _options.BrakeDeltaReferenceMeters;
        var speed = Math.Abs(corner.MinimumSpeedDeltaKph ?? 0) / _options.SpeedDeltaReferenceKph;
        var pickup = Math.Abs(corner.ThrottlePickupDeltaMeters ?? 0) / _options.PickupDeltaReferenceMeters;

        if (pickup >= brake && pickup >= speed) return EventCategory.CornerExit;
        if (speed >= brake) return EventCategory.MidCorner;
        return EventCategory.CornerEntry;
    }

    private static double Adequacy(CornerTimeLoss corner)
    {
        var present = 0;
        if (corner.BrakePointDeltaMeters is not null) present++;
        if (corner.MinimumSpeedDeltaKph is not null) present++;
        if (corner.ThrottlePickupDeltaMeters is not null) present++;
        return present / 3.0;
    }

    private static EventSeverity SeverityForLoss(double loss)
    {
        var magnitude = Math.Abs(loss);
        return magnitude switch
        {
            >= 0.30 => EventSeverity.High,
            >= 0.15 => EventSeverity.Medium,
            _ => EventSeverity.Low
        };
    }

    private static string BuildObservation(CornerTimeLoss corner, double loss)
    {
        var direction = loss > 0 ? "lost" : "gained";
        var name = corner.CornerName is { } n ? $"Corner {corner.CornerNumber} ({n})" : $"Corner {corner.CornerNumber}";
        return $"{name}: an estimated {Math.Abs(loss):0.000} s was {direction} across this window.";
    }

    private static IReadOnlyList<FindingEvidence> BuildEvidence(CornerTimeLoss corner)
    {
        var evidence = new List<FindingEvidence>();

        if (corner.ReferenceBrakePointMeters is { } rb && corner.CurrentBrakePointMeters is { } cb)
        {
            evidence.Add(new FindingEvidence
            {
                Description = $"Brake point reference {rb:0} m vs current {cb:0} m.",
                Value = corner.BrakePointDeltaMeters
            });
        }

        if (corner.ReferenceMinimumSpeedKph is { } rs && corner.CurrentMinimumSpeedKph is { } cs)
        {
            evidence.Add(new FindingEvidence
            {
                Description = $"Minimum speed reference {rs:0} km/h vs current {cs:0} km/h.",
                Value = corner.MinimumSpeedDeltaKph
            });
        }

        if (corner.ReferenceThrottlePickupMeters is { } rp && corner.CurrentThrottlePickupMeters is { } cp)
        {
            evidence.Add(new FindingEvidence
            {
                Description = $"Throttle pickup reference {rp:0} m vs current {cp:0} m.",
                Value = corner.ThrottlePickupDeltaMeters
            });
        }

        return evidence;
    }

    private static EventCategory CategoryForIndicator(BehaviourIndicatorKind kind) => kind switch
    {
        BehaviourIndicatorKind.PossibleEntryUndersteer or
        BehaviourIndicatorKind.PossibleEntryOversteer => EventCategory.CornerEntry,
        BehaviourIndicatorKind.PossibleMidCornerUndersteer or
        BehaviourIndicatorKind.PossibleMidCornerOversteer or
        BehaviourIndicatorKind.ExcessiveSteeringCorrection => EventCategory.MidCorner,
        BehaviourIndicatorKind.PossibleExitUndersteer or
        BehaviourIndicatorKind.PossibleExitOversteer => EventCategory.CornerExit,
        BehaviourIndicatorKind.PossibleTractionLoss => EventCategory.Traction,
        BehaviourIndicatorKind.PossibleLockUp => EventCategory.Braking,
        _ => EventCategory.DataQuality
    };
}
