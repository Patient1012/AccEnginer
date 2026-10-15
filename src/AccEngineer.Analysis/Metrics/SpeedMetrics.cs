namespace AccEngineer.Analysis.Metrics;

/// <summary>Deterministic speed statistics for one lap. Null when no channel present.</summary>
public sealed record SpeedMetrics
{
    public int SampleCount { get; init; }

    public double? MaximumKph { get; init; }

    public double? MinimumKph { get; init; }

    public double? AverageKph { get; init; }

    /// <summary>Speed at the sample nearest the requested track distance, if resolvable.</summary>
    public double? SpeedAtDistanceKph { get; init; }

    public double? DistanceMeters { get; init; }
}

/// <summary>Calculates speed statistics from lap samples. Pure and deterministic.</summary>
public sealed class SpeedMetricsCalculator
{
    public SpeedMetrics Calculate(LapTimeline lap, double? distanceQueryMeters = null)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var speeds = lap.Samples
            .Select(s => s.Source.SpeedKph)
            .Where(v => v.HasValue)
            .Select(v => (double)v!.Value)
            .ToList();

        if (speeds.Count == 0)
        {
            return new SpeedMetrics { SampleCount = 0 };
        }

        double? atDistance = null;
        if (distanceQueryMeters is { } query)
        {
            var nearest = lap.Samples
                .Where(s => s.ProgressMeters is not null && s.Source.SpeedKph is not null)
                .OrderBy(s => Math.Abs(s.ProgressMeters!.Value - query))
                .FirstOrDefault();
            atDistance = nearest?.Source.SpeedKph;
        }

        var distances = lap.Samples
            .Where(s => s.ProgressMeters is not null)
            .Select(s => (double)s.ProgressMeters!.Value)
            .ToList();

        return new SpeedMetrics
        {
            SampleCount = speeds.Count,
            MaximumKph = speeds.Max(),
            MinimumKph = speeds.Min(),
            AverageKph = speeds.Average(),
            SpeedAtDistanceKph = atDistance,
            DistanceMeters = distances.Count > 0 ? distances.Max() - distances.Min() : null
        };
    }
}
