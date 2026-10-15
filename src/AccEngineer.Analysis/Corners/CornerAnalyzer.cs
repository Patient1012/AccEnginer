namespace AccEngineer.Analysis.Corners;

public sealed class CornerAnalysisOptions
{
    /// <summary>Distance before the start boundary used when a corner omits one.</summary>
    public double EntryLookaheadMeters { get; init; } = 150.0;

    /// <summary>Distance after the end boundary used when a corner omits one.</summary>
    public double ExitLookaheadMeters { get; init; } = 150.0;

    /// <summary>Absolute steering input counted as turn-in during rotation.</summary>
    public double SteeringThreshold { get; init; } = 0.15;

    /// <summary>Brake input at or above this counts as braking.</summary>
    public double BrakeOnThreshold { get; init; } = 0.10;

    /// <summary>Throttle input at or above this counts as throttle pickup after the apex.</summary>
    public double ThrottlePickupThreshold { get; init; } = 0.5;

    /// <summary>Throttle input at or above this counts as full throttle.</summary>
    public double FullThrottleThreshold { get; init; } = 0.95;

    /// <summary>Distance after the apex at which exit speed is sampled.</summary>
    public double SpeedAfterApexMeters { get; init; } = 50.0;
}

/// <summary>
/// Calculates the entry, rotation and exit phases of a corner from telemetry.
/// Deterministic and unit-testable; boundaries come from the manual corner model,
/// with turn-in and exit detected from steering when the model omits them.
/// </summary>
public sealed class CornerAnalyzer
{
    private readonly CornerAnalysisOptions _options;

    public CornerAnalyzer(CornerAnalysisOptions? options = null)
    {
        _options = options ?? new CornerAnalysisOptions();
    }

    public IReadOnlyList<AnalyzedCorner> AnalyzeAll(LapTimeline lap, TrackCornerDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(lap);
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Corners
            .Select(corner => new AnalyzedCorner
            {
                Corner = corner,
                Metrics = Analyze(lap, corner)
            })
            .ToArray();
    }

    public CornerPhaseMetrics Analyze(LapTimeline lap, Corner corner)
    {
        ArgumentNullException.ThrowIfNull(lap);
        ArgumentNullException.ThrowIfNull(corner);

        var samples = lap.Samples;

        var windowStart = corner.StartDistanceMeters
                          ?? corner.TurnInDistanceMeters
                          ?? corner.ApexDistanceMeters - _options.EntryLookaheadMeters;
        var windowEnd = corner.EndDistanceMeters
                        ?? corner.ExitDistanceMeters
                        ?? corner.ApexDistanceMeters + _options.ExitLookaheadMeters;

        var window = Slice(samples, windowStart, windowEnd + 1e-6);
        if (window.Count == 0)
        {
            return new CornerPhaseMetrics
            {
                CornerNumber = corner.Number,
                CornerName = corner.Name,
                WindowStartMeters = windowStart,
                WindowEndMeters = windowEnd
            };
        }

        var apexIndex = NearestIndex(samples, corner.ApexDistanceMeters);

        var turnInMeters = corner.TurnInDistanceMeters
                           ?? DetectTurnInDistance(window, corner.ApexDistanceMeters)
                           ?? corner.ApexDistanceMeters;
        var exitMeters = corner.ExitDistanceMeters
                         ?? DetectExitDistance(window, corner.ApexDistanceMeters)
                         ?? corner.ApexDistanceMeters;

        var entry = Slice(samples, windowStart, turnInMeters);
        var rotation = Slice(samples, turnInMeters, exitMeters);
        var exit = Slice(samples, exitMeters, windowEnd + 1e-6);

        return new CornerPhaseMetrics
        {
            CornerNumber = corner.Number,
            CornerName = corner.Name,
            WindowStartMeters = windowStart,
            WindowEndMeters = windowEnd,

            EntrySpeedKph = entry.Count > 0 ? entry[0].Source.SpeedKph : null,
            BrakePointMeters = FirstDistance(entry, s => s.Source.BrakeInput is { } b && b >= _options.BrakeOnThreshold),
            PeakBrakeInput = PeakBrake(window),
            BrakeReleaseMeters = LastDistanceBefore(entry, s => s.Source.BrakeInput is { } b && b >= _options.BrakeOnThreshold),
            DownshiftCount = DownshiftCount(window),

            TurnInMeters = turnInMeters,
            TurnInSteeringInput = NearestSteering(samples, turnInMeters),
            MinimumSpeedKph = MinSpeed(rotation),
            ApexSpeedKph = apexIndex >= 0 ? samples[apexIndex].Source.SpeedKph : null,
            ApexDistanceMeters = corner.ApexDistanceMeters,
            SteeringCorrectionCount = CountCorrections(rotation),
            BrakeOverlapSeconds = BrakeOverlapSeconds(rotation),

            ThrottlePickupMeters = FirstDistanceAfter(exit, corner.ApexDistanceMeters, s => s.Source.ThrottleInput is { } t && t >= _options.ThrottlePickupThreshold),
            FullThrottleMeters = FirstDistanceAfter(exit, corner.ApexDistanceMeters, s => s.Source.ThrottleInput is { } t && t >= _options.FullThrottleThreshold),
            ExitSpeedKph = exit.Count > 0 ? exit[0].Source.SpeedKph : null,
            SpeedAfterApexKph = NearestSpeed(samples, corner.ApexDistanceMeters + _options.SpeedAfterApexMeters)
        };
    }

    private static List<LapSample> Slice(IReadOnlyList<LapSample> samples, double start, double end)
    {
        var result = new List<LapSample>();
        foreach (var sample in samples)
        {
            if (sample.ProgressMeters is not { } p) continue;
            if (p < start || p >= end) continue;
            result.Add(sample);
        }
        return result;
    }

    private static int NearestIndex(IReadOnlyList<LapSample> samples, double distance)
    {
        var best = -1;
        var bestDelta = double.MaxValue;
        for (var i = 0; i < samples.Count; i++)
        {
            if (samples[i].ProgressMeters is not { } p) continue;
            var delta = Math.Abs(p - distance);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = i;
            }
        }
        return best;
    }

    private static double? NearestSpeed(IReadOnlyList<LapSample> samples, double distance)
    {
        var index = NearestIndex(samples, distance);
        return index >= 0 ? samples[index].Source.SpeedKph : null;
    }

    private static double? NearestSteering(IReadOnlyList<LapSample> samples, double distance)
    {
        var index = NearestIndex(samples, distance);
        return index >= 0 ? samples[index].Source.SteeringInput : null;
    }

    private double? DetectTurnInDistance(List<LapSample> window, double apex)
    {
        var result = window
            .Where(s => s.ProgressMeters is { } p && p <= apex &&
                        s.Source.SteeringInput is { } st && Math.Abs(st) >= _options.SteeringThreshold)
            .OrderBy(s => s.ProgressMeters)
            .Select(s => (double?)s.ProgressMeters)
            .FirstOrDefault();
        return result;
    }

    private double? DetectExitDistance(List<LapSample> window, double apex)
    {
        var result = window
            .Where(s => s.ProgressMeters is { } p && p >= apex &&
                        s.Source.SteeringInput is { } st && Math.Abs(st) >= _options.SteeringThreshold)
            .OrderByDescending(s => s.ProgressMeters)
            .Select(s => (double?)s.ProgressMeters)
            .FirstOrDefault();
        return result;
    }

    private static double? FirstDistance(List<LapSample> samples, Func<LapSample, bool> predicate)
    {
        foreach (var sample in samples)
        {
            if (predicate(sample)) return sample.ProgressMeters;
        }
        return null;
    }

    private static double? FirstDistanceAfter(
        List<LapSample> samples,
        double minimumDistance,
        Func<LapSample, bool> predicate)
    {
        // Exit list starts at the exit boundary, but full-throttle may only be reached
        // beyond it, so scan the whole list for the first qualifying sample after apex.
        foreach (var sample in samples)
        {
            if (sample.ProgressMeters is { } p && p >= minimumDistance && predicate(sample))
            {
                return p;
            }
        }
        return null;
    }

    private static double? LastDistanceBefore(List<LapSample> samples, Func<LapSample, bool> predicate)
    {
        double? last = null;
        foreach (var sample in samples)
        {
            if (predicate(sample)) last = sample.ProgressMeters;
        }
        return last;
    }

    private static double? PeakBrake(List<LapSample> samples)
    {
        double? peak = null;
        foreach (var sample in samples)
        {
            if (sample.Source.BrakeInput is { } b && (peak is null || b > peak))
            {
                peak = b;
            }
        }
        return peak;
    }

    private static double? MinSpeed(List<LapSample> samples)
    {
        double? min = null;
        foreach (var sample in samples)
        {
            if (sample.Source.SpeedKph is { } s && (min is null || s < min))
            {
                min = s;
            }
        }
        return min;
    }

    private static int DownshiftCount(List<LapSample> samples)
    {
        var count = 0;
        for (var i = 1; i < samples.Count; i++)
        {
            if (samples[i - 1].Source.Gear is { } previous &&
                samples[i].Source.Gear is { } current &&
                current < previous)
            {
                count++;
            }
        }
        return count;
    }

    private int CountCorrections(List<LapSample> samples)
    {
        var corrections = 0;
        var previousRate = 0.0;
        for (var i = 1; i < samples.Count; i++)
        {
            if (samples[i].Source.SteeringInput is not { } current ||
                samples[i - 1].Source.SteeringInput is not { } previous)
            {
                continue;
            }
            var dt = samples[i].LapTimeSeconds - samples[i - 1].LapTimeSeconds;
            if (dt <= 1e-6) continue;

            var rate = (current - previous) / dt;
            if (Math.Abs(rate) >= 0.5 && previousRate != 0 && Math.Sign(rate) != Math.Sign(previousRate))
            {
                corrections++;
            }
            previousRate = rate;
        }
        return corrections;
    }

    private static double? BrakeOverlapSeconds(List<LapSample> samples)
    {
        var seconds = 0.0;
        var any = false;
        for (var i = 1; i < samples.Count; i++)
        {
            var current = samples[i].Source;
            if (current.BrakeInput is { } b && b >= 0.10 &&
                current.SteeringInput is { } s && Math.Abs(s) >= 0.15)
            {
                seconds += samples[i].LapTimeSeconds - samples[i - 1].LapTimeSeconds;
                any = true;
            }
        }
        return any ? seconds : null;
    }
}
