namespace AccEngineer.Analysis.Metrics;

/// <summary>
/// Longitudinal acceleration summary. <see cref="MeasuredPeakG"/> comes from the
/// simulator's own acceleration channel when present; <see cref="EstimatedPeakG"/>
/// is derived from speed deltas and is explicitly an estimate, because sample
/// timing and simulator telemetry make simple speed differences unreliable.
/// </summary>
public sealed record AccelerationMetrics
{
    public int MeasuredSampleCount { get; init; }

    public double? MeasuredPeakG { get; init; }

    public double? MeasuredMinimumG { get; init; }

    /// <summary>Peak acceleration magnitude estimated from speed deltas (m/s^2 / 9.80665).</summary>
    public double? EstimatedPeakG { get; init; }

    /// <summary>Peak deceleration magnitude estimated from speed deltas (positive value).</summary>
    public double? EstimatedPeakDecelerationG { get; init; }
}

/// <summary>
/// Calculates longitudinal acceleration. Prefers the simulator channel and only
/// falls back to a clearly-labelled estimate from speed deltas.
/// </summary>
public sealed class AccelerationMetricsCalculator
{
    private const double StandardGravity = 9.80665;

    public AccelerationMetrics Calculate(LapTimeline lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var measured = lap.Samples
            .Where(s => s.Source.LongitudinalAccelerationG is not null)
            .Select(s => (double)s.Source.LongitudinalAccelerationG!.Value)
            .ToList();

        double? estimatedPeak = null;
        double? estimatedDecel = null;

        for (var i = 1; i < lap.Samples.Count; i++)
        {
            var previous = lap.Samples[i - 1];
            var current = lap.Samples[i];

            if (previous.Source.SpeedMetersPerSecond is not { } v0 ||
                current.Source.SpeedMetersPerSecond is not { } v1)
            {
                continue;
            }

            var dt = current.LapTimeSeconds - previous.LapTimeSeconds;
            if (dt <= 1e-6)
            {
                continue;
            }

            var accelerationG = (v1 - v0) / dt / StandardGravity;

            if (estimatedPeak is null || accelerationG > estimatedPeak)
            {
                estimatedPeak = accelerationG;
            }
            if (estimatedDecel is null || -accelerationG > estimatedDecel)
            {
                estimatedDecel = -accelerationG;
            }
        }

        return new AccelerationMetrics
        {
            MeasuredSampleCount = measured.Count,
            MeasuredPeakG = measured.Count > 0 ? measured.Max() : null,
            MeasuredMinimumG = measured.Count > 0 ? measured.Min() : null,
            EstimatedPeakG = estimatedPeak,
            EstimatedPeakDecelerationG = estimatedDecel
        };
    }
}
