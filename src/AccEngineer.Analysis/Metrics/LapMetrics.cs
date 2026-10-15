namespace AccEngineer.Analysis.Metrics;

/// <summary>All fundamental signal metrics for a single lap, calculated deterministically.</summary>
public sealed record LapMetrics
{
    public required int LapNumber { get; init; }

    public required SpeedMetrics Speed { get; init; }

    public required AccelerationMetrics Acceleration { get; init; }

    public required IReadOnlyList<BrakingMetrics> Braking { get; init; }

    public required ThrottleMetrics Throttle { get; init; }

    public required SteeringMetrics Steering { get; init; }

    public required GearMetrics Gear { get; init; }
}

/// <summary>
/// Calculates every fundamental metric for a lap in one pass. This is the single
/// entry point the rest of the engine should use so metrics stay consistent.
/// </summary>
public sealed class LapMetricsCalculator
{
    private readonly SpeedMetricsCalculator _speed = new();
    private readonly AccelerationMetricsCalculator _acceleration = new();
    private readonly BrakingMetricsCalculator _braking;
    private readonly ThrottleMetricsCalculator _throttle;
    private readonly SteeringMetricsCalculator _steering;
    private readonly GearMetricsCalculator _gear = new();

    public LapMetricsCalculator(
        BrakingMetricsOptions? brakingOptions = null,
        ThrottleMetricsOptions? throttleOptions = null,
        SteeringMetricsOptions? steeringOptions = null)
    {
        _braking = new BrakingMetricsCalculator(brakingOptions);
        _throttle = new ThrottleMetricsCalculator(throttleOptions);
        _steering = new SteeringMetricsCalculator(steeringOptions);
    }

    public LapMetrics Calculate(LapTimeline lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        return new LapMetrics
        {
            LapNumber = lap.LapNumber,
            Speed = _speed.Calculate(lap),
            Acceleration = _acceleration.Calculate(lap),
            Braking = _braking.Calculate(lap),
            Throttle = _throttle.Calculate(lap),
            Steering = _steering.Calculate(lap),
            Gear = _gear.Calculate(lap)
        };
    }
}
