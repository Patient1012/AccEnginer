namespace AccEngineer.Analysis.Metrics;

/// <summary>A point where the driver moved from off/low throttle onto the pedal.</summary>
public sealed record ThrottleApplication
{
    public required int SampleIndex { get; init; }

    public long SampleId { get; init; }

    public double? DistanceMeters { get; init; }

    public double? LapTimeSeconds { get; init; }

    /// <summary>Throttle input immediately before the application.</summary>
    public double? InputBefore { get; init; }

    /// <summary>Throttle input at the first sample counted as applied.</summary>
    public double? InputAtApplication { get; init; }

    /// <summary>Rate of throttle change per second across the first step of the application.</summary>
    public double? PickupRatePerSecond { get; init; }
}

/// <summary>An interruption: the throttle was reduced after having been applied.</summary>
public sealed record ThrottleLift
{
    public required int SampleIndex { get; init; }

    public long SampleId { get; init; }

    public double? DistanceMeters { get; init; }

    public double? ThrottleDrop { get; init; }
}

/// <summary>Aggregate throttle usage for a lap. Every value is measured, never inferred.</summary>
public sealed record ThrottleMetrics
{
    public int SampleCount { get; init; }

    public double? FullThrottleFraction { get; init; }

    public double? PartialThrottleSeconds { get; init; }

    public double? FullThrottleSeconds { get; init; }

    public IReadOnlyList<ThrottleApplication> Applications { get; init; } = Array.Empty<ThrottleApplication>();

    public IReadOnlyList<ThrottleLift> Lifts { get; init; } = Array.Empty<ThrottleLift>();

    /// <summary>Lifts that occurred while the driver was on the throttle for a sustained period.</summary>
    public int InterruptionsAfterApplication { get; init; }
}

public sealed class ThrottleMetricsOptions
{
    /// <summary>Throttle input at or below this is considered off.</summary>
    public double OffThreshold { get; init; } = 0.05;

    /// <summary>Throttle input at or above this marks the start of an application.</summary>
    public double ApplicationThreshold { get; init; } = 0.10;

    /// <summary>Throttle input at or above this counts as full throttle.</summary>
    public double FullThrottleThreshold { get; init; } = 0.95;

    /// <summary>Throttle drop at or above this from a prior sample counts as a lift.</summary>
    public double LiftThreshold { get; init; } = 0.15;
}

/// <summary>Detects throttle applications, full-throttle time and interruptions within a lap.</summary>
public sealed class ThrottleMetricsCalculator
{
    private readonly ThrottleMetricsOptions _options;

    public ThrottleMetricsCalculator(ThrottleMetricsOptions? options = null)
    {
        _options = options ?? new ThrottleMetricsOptions();
    }

    public ThrottleMetrics Calculate(LapTimeline lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var samples = lap.Samples;
        var known = samples.Count(s => s.Source.ThrottleInput is not null);
        if (known == 0)
        {
            return new ThrottleMetrics { SampleCount = 0 };
        }

        var applications = new List<ThrottleApplication>();
        var lifts = new List<ThrottleLift>();
        var partialSeconds = 0.0;
        var fullSeconds = 0.0;
        var interruptions = 0;

        var previouslyApplied = false;

        for (var i = 0; i < samples.Count; i++)
        {
            var current = samples[i].Source.ThrottleInput;
            if (current is null)
            {
                continue;
            }

            if (i > 0 &&
                samples[i - 1].Source.ThrottleInput is { } previous)
            {
                var dt = samples[i].LapTimeSeconds - samples[i - 1].LapTimeSeconds;
                if (dt > 0)
                {
                    if (current >= _options.FullThrottleThreshold)
                    {
                        fullSeconds += dt;
                    }
                    else if (current > _options.OffThreshold)
                    {
                        partialSeconds += dt;
                    }
                }

                var drop = previous - current.Value;
                if (previouslyApplied && drop >= _options.LiftThreshold)
                {
                    lifts.Add(new ThrottleLift
                    {
                        SampleIndex = i,
                        SampleId = samples[i].Source.SampleId,
                        DistanceMeters = samples[i].ProgressMeters,
                        ThrottleDrop = drop
                    });
                    interruptions++;
                }
            }

            var applied = current.Value >= _options.ApplicationThreshold;
            if (applied && !previouslyApplied)
            {
                applications.Add(BuildApplication(samples, i));
            }
            previouslyApplied = applied;
        }

        return new ThrottleMetrics
        {
            SampleCount = known,
            FullThrottleFraction = (double)samples.Count(s => s.Source.ThrottleInput is { } t && t >= _options.FullThrottleThreshold) / known,
            PartialThrottleSeconds = partialSeconds,
            FullThrottleSeconds = fullSeconds,
            Applications = applications,
            Lifts = lifts,
            InterruptionsAfterApplication = interruptions
        };
    }

    private static ThrottleApplication BuildApplication(IReadOnlyList<LapSample> samples, int index)
    {
        var before = index > 0 ? samples[index - 1].Source.ThrottleInput : null;
        var at = samples[index].Source.ThrottleInput;

        double? rate = null;
        if (before is { } b && at is { } a && index > 0)
        {
            var dt = samples[index].LapTimeSeconds - samples[index - 1].LapTimeSeconds;
            if (dt > 1e-6)
            {
                rate = (a - b) / dt;
            }
        }

        return new ThrottleApplication
        {
            SampleIndex = index,
            SampleId = samples[index].Source.SampleId,
            DistanceMeters = samples[index].ProgressMeters,
            LapTimeSeconds = samples[index].LapTimeSeconds,
            InputBefore = before,
            InputAtApplication = at,
            PickupRatePerSecond = rate
        };
    }
}
