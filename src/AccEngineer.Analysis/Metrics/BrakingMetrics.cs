namespace AccEngineer.Analysis.Metrics;

/// <summary>
/// Detailed metrics for a single contiguous braking application within a lap.
/// All fields are measured facts; null means the underlying channel was absent.
/// </summary>
public sealed record BrakingMetrics
{
    public int StartSampleIndex { get; init; }

    public int EndSampleIndex { get; init; }

    public long FirstSampleId { get; init; }

    public long LastSampleId { get; init; }

    public double? StartDistanceMeters { get; init; }

    public double? EndDistanceMeters { get; init; }

    public double? DurationSeconds { get; init; }

    public double? PeakBrakeInput { get; init; }

    /// <summary>Distance travelled during the braking application, when progress is known.</summary>
    public double? DistanceTravelledMeters { get; init; }

    /// <summary>Brake input sampled in order across the application, for progression inspection.</summary>
    public IReadOnlyList<double> BrakeInputProgression { get; init; } = Array.Empty<double>();

    /// <summary>Brake input at the point of peak steering input within the entry phase.</summary>
    public double? BrakeInputAtTurnIn { get; init; }

    /// <summary>Seconds spent braking while steering input is non-trivial (trail braking).</summary>
    public double? TrailBrakingSeconds { get; init; }
}

public sealed class BrakingMetricsOptions
{
    /// <summary>Brake input at or above this starts a braking application.</summary>
    public double BrakeOnThreshold { get; init; } = 0.10;

    /// <summary>Brake input at or below this ends a braking application (hysteresis).</summary>
    public double BrakeOffThreshold { get; init; } = 0.05;

    /// <summary>Applications shorter than this many samples are ignored.</summary>
    public int MinimumSamples { get; init; } = 3;

    /// <summary>Absolute steering input counted as meaningful turn-in during trail braking.</summary>
    public double SteeringThreshold { get; init; } = 0.15;
}

/// <summary>Detects and measures braking applications within a lap.</summary>
public sealed class BrakingMetricsCalculator
{
    private readonly BrakingMetricsOptions _options;

    public BrakingMetricsCalculator(BrakingMetricsOptions? options = null)
    {
        _options = options ?? new BrakingMetricsOptions();
    }

    public IReadOnlyList<BrakingMetrics> Calculate(LapTimeline lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var samples = lap.Samples;
        var results = new List<BrakingMetrics>();

        var inZone = false;
        var start = 0;

        for (var i = 0; i < samples.Count; i++)
        {
            var brake = samples[i].Source.BrakeInput;

            if (!inZone)
            {
                if (brake is { } b && b >= _options.BrakeOnThreshold)
                {
                    inZone = true;
                    start = i;
                }
                continue;
            }

            var ended = brake is null || brake <= _options.BrakeOffThreshold;
            if (ended || i == samples.Count - 1)
            {
                var end = ended ? i - 1 : i;
                if (end - start + 1 >= _options.MinimumSamples)
                {
                    results.Add(Build(samples, start, end));
                }
                inZone = false;
            }
        }

        return results;
    }

    private BrakingMetrics Build(IReadOnlyList<LapSample> samples, int start, int end)
    {
        double? peak = null;
        var progression = new List<double>(end - start + 1);

        for (var i = start; i <= end; i++)
        {
            if (samples[i].Source.BrakeInput is { } b)
            {
                progression.Add(b);
                if (peak is null || b > peak)
                {
                    peak = b;
                }
            }
        }

        var startDistance = samples[start].ProgressMeters;
        var endDistance = samples[end].ProgressMeters;
        double? travelled = startDistance is not null && endDistance is not null
            ? endDistance - startDistance
            : null;

        // Turn-in is taken as the peak steering sample inside the braking window.
        var turnIn = -1;
        var peakSteering = 0.0;
        for (var i = start; i <= end; i++)
        {
            if (samples[i].Source.SteeringInput is { } s && Math.Abs(s) > peakSteering)
            {
                peakSteering = Math.Abs(s);
                turnIn = i;
            }
        }

        double trailSeconds = 0;
        var trailSamples = 0;
        for (var i = start; i <= end; i++)
        {
            if (samples[i].Source.SteeringInput is { } s && Math.Abs(s) >= _options.SteeringThreshold)
            {
                trailSamples++;
                if (i > start)
                {
                    trailSeconds += samples[i].LapTimeSeconds - samples[i - 1].LapTimeSeconds;
                }
            }
        }

        return new BrakingMetrics
        {
            StartSampleIndex = start,
            EndSampleIndex = end,
            FirstSampleId = samples[start].Source.SampleId,
            LastSampleId = samples[end].Source.SampleId,
            StartDistanceMeters = startDistance,
            EndDistanceMeters = endDistance,
            DurationSeconds = samples[end].LapTimeSeconds - samples[start].LapTimeSeconds,
            PeakBrakeInput = peak,
            DistanceTravelledMeters = travelled,
            BrakeInputProgression = progression,
            BrakeInputAtTurnIn = turnIn >= 0 ? samples[turnIn].Source.BrakeInput : null,
            TrailBrakingSeconds = trailSamples > 0 ? trailSeconds : null
        };
    }
}
