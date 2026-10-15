namespace AccEngineer.Analysis.Metrics;

/// <summary>A single gear change observed in the telemetry.</summary>
public sealed record ShiftPoint
{
    public required int SampleIndex { get; init; }

    public long SampleId { get; init; }

    /// <summary>Raw ACC gear value before normalisation (0 = reverse, 1 = neutral).</summary>
    public int FromGearRaw { get; init; }

    public int ToGearRaw { get; init; }

    public double? DistanceMeters { get; init; }

    public double? LapTimeSeconds { get; init; }

    /// <summary>Driver-selected gear number for UI/analysis (raw - 1, so neutral becomes 0).</summary>
    public int ToGear { get; init; }

    public bool IsUpshift => ToGearRaw > FromGearRaw;

    public bool IsDownshift => ToGearRaw < FromGearRaw;
}

/// <summary>Measured gear usage across a lap.</summary>
public sealed record GearMetrics
{
    public int SampleCount { get; init; }

    public IReadOnlyList<int> GearsUsed { get; init; } = Array.Empty<int>();

    public IReadOnlyList<ShiftPoint> Shifts { get; init; } = Array.Empty<ShiftPoint>();

    public int UpshiftCount { get; init; }

    public int DownshiftCount { get; init; }

    /// <summary>Gear (raw) at the sample nearest the requested track distance.</summary>
    public int? GearAtDistanceRaw { get; init; }
}

/// <summary>Calculates gear usage and shift points from lap samples.</summary>
public sealed class GearMetricsCalculator
{
    public GearMetrics Calculate(LapTimeline lap, double? distanceQueryMeters = null)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var samples = lap.Samples;
        var shifts = new List<ShiftPoint>();
        var gears = new HashSet<int>();

        var previousGear = int.MinValue;

        for (var i = 0; i < samples.Count; i++)
        {
            if (samples[i].Source.Gear is not { } gear)
            {
                continue;
            }

            gears.Add(gear);

            if (previousGear != int.MinValue && gear != previousGear)
            {
                shifts.Add(new ShiftPoint
                {
                    SampleIndex = i,
                    SampleId = samples[i].Source.SampleId,
                    FromGearRaw = previousGear,
                    ToGearRaw = gear,
                    ToGear = gear - 1,
                    DistanceMeters = samples[i].ProgressMeters,
                    LapTimeSeconds = samples[i].LapTimeSeconds
                });
            }

            previousGear = gear;
        }

        int? atDistance = null;
        if (distanceQueryMeters is { } query)
        {
            var nearest = samples
                .Where(s => s.ProgressMeters is not null && s.Source.Gear is not null)
                .OrderBy(s => Math.Abs(s.ProgressMeters!.Value - query))
                .FirstOrDefault();
            atDistance = nearest?.Source.Gear;
        }

        return new GearMetrics
        {
            SampleCount = samples.Count(s => s.Source.Gear is not null),
            GearsUsed = gears.OrderBy(g => g).ToArray(),
            Shifts = shifts,
            UpshiftCount = shifts.Count(s => s.IsUpshift),
            DownshiftCount = shifts.Count(s => s.IsDownshift),
            GearAtDistanceRaw = atDistance
        };
    }
}
