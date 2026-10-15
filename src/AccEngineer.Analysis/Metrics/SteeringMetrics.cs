namespace AccEngineer.Analysis.Metrics;

/// <summary>Measured steering behaviour for a lap. Nulls mean the channel was absent.</summary>
public sealed record SteeringMetrics
{
    public int SampleCount { get; init; }

    public double? MaximumAbsoluteInput { get; init; }

    public double? AverageAbsoluteInput { get; init; }

    /// <summary>Largest absolute change of steering input per second.</summary>
    public double? MaximumRatePerSecond { get; init; }

    /// <summary>
    /// Number of direction changes of the steering input (left-to-right or back),
    /// i.e. deliberate reversals rather than noise.
    /// </summary>
    public int ReversalCount { get; init; }

    /// <summary>
    /// Number of times the steering rate changed sign while the wheel was being held
    /// in one direction: small corrective inputs against the corner.
    /// </summary>
    public int CorrectionCount { get; init; }
}

public sealed class SteeringMetricsOptions
{
    /// <summary>Absolute input change smaller than this is treated as noise, not a reversal.</summary>
    public double ReversalThreshold { get; init; } = 0.02;

    /// <summary>Absolute rate (per second) above which a correction is counted.</summary>
    public double CorrectionRateThreshold { get; init; } = 0.5;
}

/// <summary>Calculates steering statistics from lap samples. Pure and deterministic.</summary>
public sealed class SteeringMetricsCalculator
{
    private readonly SteeringMetricsOptions _options;

    public SteeringMetricsCalculator(SteeringMetricsOptions? options = null)
    {
        _options = options ?? new SteeringMetricsOptions();
    }

    public SteeringMetrics Calculate(LapTimeline lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var samples = lap.Samples;
        var known = samples.Where(s => s.Source.SteeringInput is not null).ToList();
        if (known.Count == 0)
        {
            return new SteeringMetrics { SampleCount = 0 };
        }

        var maximum = known.Max(s => Math.Abs(s.Source.SteeringInput!.Value));
        var average = known.Average(s => Math.Abs(s.Source.SteeringInput!.Value));

        double maximumRate = 0;
        var reversals = 0;
        var corrections = 0;

        var previousSign = 0;
        var previousRate = 0.0;

        for (var i = 0; i < samples.Count; i++)
        {
            if (samples[i].Source.SteeringInput is not { } current || i == 0 ||
                samples[i - 1].Source.SteeringInput is not { } previous)
            {
                continue;
            }

            var dt = samples[i].LapTimeSeconds - samples[i - 1].LapTimeSeconds;
            if (dt <= 1e-6)
            {
                continue;
            }

            var rate = (current - previous) / dt;
            maximumRate = Math.Max(maximumRate, Math.Abs(rate));

            var sign = Math.Abs(current) >= _options.ReversalThreshold
                ? Math.Sign(current)
                : 0;

            if (sign != 0 && previousSign != 0 && sign != previousSign)
            {
                reversals++;
            }
            if (sign != 0)
            {
                previousSign = sign;
            }

            if (Math.Abs(rate) >= _options.CorrectionRateThreshold &&
                previousRate != 0 &&
                Math.Sign(rate) != Math.Sign(previousRate))
            {
                corrections++;
            }
            previousRate = rate;
        }

        return new SteeringMetrics
        {
            SampleCount = known.Count,
            MaximumAbsoluteInput = maximum,
            AverageAbsoluteInput = average,
            MaximumRatePerSecond = maximumRate,
            ReversalCount = reversals,
            CorrectionCount = corrections
        };
    }
}
