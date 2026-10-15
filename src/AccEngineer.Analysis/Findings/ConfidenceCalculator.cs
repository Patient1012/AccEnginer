namespace AccEngineer.Analysis.Findings;

/// <summary>
/// Inputs to a confidence score. Each factor is in the range 0..1 and describes how
/// much the telemetry actually supports a finding.
/// </summary>
public readonly record struct ConfidenceFactors
{
    /// <summary>Fraction of the relevant samples that carried the needed channel.</summary>
    public double ChannelCoverage { get; init; }

    /// <summary>How adequate the amount of evidence is (more consistent samples score higher).</summary>
    public double SampleAdequacy { get; init; }

    /// <summary>How repeatable the pattern is across the session.</summary>
    public double Repeatability { get; init; }

    public static ConfidenceFactors Full => new()
    {
        ChannelCoverage = 1.0,
        SampleAdequacy = 1.0,
        Repeatability = 1.0
    };
}

/// <summary>
/// Turns coverage/adequacy/repeatability into a bounded confidence score and a
/// qualitative <see cref="EventConfidence"/>. Deterministic and testable.
/// </summary>
public sealed class ConfidenceCalculator
{
    private readonly double _coverageWeight;
    private readonly double _adequacyWeight;
    private readonly double _repeatabilityWeight;

    public ConfidenceCalculator(
        double coverageWeight = 0.5,
        double adequacyWeight = 0.25,
        double repeatabilityWeight = 0.25)
    {
        _coverageWeight = coverageWeight;
        _adequacyWeight = adequacyWeight;
        _repeatabilityWeight = repeatabilityWeight;
    }

    public double Score(ConfidenceFactors factors)
    {
        var score =
            Clamp(factors.ChannelCoverage) * _coverageWeight +
            Clamp(factors.SampleAdequacy) * _adequacyWeight +
            Clamp(factors.Repeatability) * _repeatabilityWeight;

        return Clamp(score);
    }

    public EventConfidence Classify(double score) => score switch
    {
        >= 0.80 => EventConfidence.Verified,
        >= 0.50 => EventConfidence.Probable,
        _ => EventConfidence.Uncertain
    };

    private static double Clamp(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
}
