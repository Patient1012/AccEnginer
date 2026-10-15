namespace AccEngineer.Analysis.Comparison;

/// <summary>Delta of one scalar signal at a specific track distance between two laps.</summary>
public sealed record SignalDeltaPoint(
    double DistanceMeters,
    double? ReferenceValue,
    double? CurrentValue,
    double? Delta);

/// <summary>
/// Compares a single scalar telemetry channel between two laps on a shared track
/// distance grid, interpolating each lap's value at each grid point. Used for
/// speed difference by track distance and the same for throttle, brake, etc.
/// </summary>
public sealed class SignalComparator
{
    /// <summary>
    /// Produces per-distance deltas. Positive <see cref="SignalDeltaPoint.Delta"/> means
    /// the current lap's signal is higher than the reference at that distance.
    /// </summary>
    public IReadOnlyList<SignalDeltaPoint> Compare(
        LapTimeline reference,
        LapTimeline current,
        Func<LapSample, double?> selector,
        double gridStepMeters = 10.0)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(selector);

        if (gridStepMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gridStepMeters), "Grid step must be positive.");
        }

        var referenceValues = BuildValuePoints(reference, selector);
        var currentValues = BuildValuePoints(current, selector);

        var maxProgress = referenceValues.Count > 0 && currentValues.Count > 0
            ? Math.Min(referenceValues[^1].DistanceMeters, currentValues[^1].DistanceMeters)
            : 0.0;

        var points = new List<SignalDeltaPoint>();
        for (var distance = 0.0; distance <= maxProgress + 1e-9; distance += gridStepMeters)
        {
            var referenceValue = ValueAt(referenceValues, distance);
            var currentValue = ValueAt(currentValues, distance);
            double? delta = referenceValue is { } r && currentValue is { } c ? c - r : null;
            points.Add(new SignalDeltaPoint(distance, referenceValue, currentValue, delta));
        }

        return points;
    }

    private static List<ValuePoint> BuildValuePoints(LapTimeline lap, Func<LapSample, double?> selector)
    {
        var points = new List<ValuePoint>(lap.Samples.Count);
        var lastProgress = double.NegativeInfinity;

        foreach (var sample in lap.Samples)
        {
            if (sample.ProgressMeters is not { } progress || selector(sample) is not { } value)
            {
                continue;
            }
            if (progress <= lastProgress)
            {
                continue;
            }
            points.Add(new ValuePoint(progress, value));
            lastProgress = progress;
        }

        return points;
    }

    private static double? ValueAt(IReadOnlyList<ValuePoint> points, double distance)
    {
        if (points.Count == 0) return null;
        if (distance < points[0].DistanceMeters || distance > points[^1].DistanceMeters) return null;
        if (distance <= points[0].DistanceMeters) return points[0].Value;
        if (distance >= points[^1].DistanceMeters) return points[^1].Value;

        var lo = 0;
        var hi = points.Count - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (points[mid].DistanceMeters <= distance) lo = mid;
            else hi = mid;
        }

        var a = points[lo];
        var b = points[hi];
        var span = b.DistanceMeters - a.DistanceMeters;
        if (span <= 0) return a.Value;

        var ratio = (distance - a.DistanceMeters) / span;
        return a.Value + (b.Value - a.Value) * ratio;
    }

    private readonly record struct ValuePoint(double DistanceMeters, double Value);
}
