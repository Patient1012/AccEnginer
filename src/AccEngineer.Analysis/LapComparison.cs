namespace AccEngineer.Analysis;

public sealed class LapComparisonOptions
{
    /// <summary>Spacing of the common track-progress grid used to compare laps.</summary>
    public double GridStepMeters { get; init; } = 10.0;

    /// <summary>Minimum cumulative change in delta (seconds) for a gain/loss region to be reported.</summary>
    public double RegionThresholdSeconds { get; init; } = 0.05;

    /// <summary>Minimum length (metres) for a gain/loss region to be reported.</summary>
    public double MinimumRegionLengthMeters { get; init; } = 20.0;
}

/// <summary>Time delta between a lap and the reference lap at one progress point.</summary>
public sealed record DeltaPoint(
    double ProgressMeters,
    double? ReferenceTimeSeconds,
    double? LapTimeSeconds,
    double? DeltaSeconds);

/// <summary>
/// A contiguous track region where time is gained (IsGain) or lost relative to the
/// reference lap. DeltaSeconds is the change in delta across the region: negative
/// means the lap gained time there.
/// </summary>
public sealed record GainLossRegion
{
    public required bool IsGain { get; init; }

    public required double StartProgressMeters { get; init; }

    public required double EndProgressMeters { get; init; }

    public required double StartDeltaSeconds { get; init; }

    public required double EndDeltaSeconds { get; init; }

    public double DeltaSeconds => EndDeltaSeconds - StartDeltaSeconds;

    public double LengthMeters => EndProgressMeters - StartProgressMeters;
}

public sealed record LapDelta
{
    public required int LapNumber { get; init; }

    public double? LapTimeSeconds { get; init; }

    public double? ReferenceLapTimeSeconds { get; init; }

    /// <summary>Whole-lap time difference (positive = slower than the reference).</summary>
    public double? TotalDeltaSeconds { get; init; }

    /// <summary>Delta at the last common grid point.</summary>
    public double? DeltaAtEndSeconds { get; init; }

    public required IReadOnlyList<DeltaPoint> Points { get; init; }

    public required IReadOnlyList<GainLossRegion> Regions { get; init; }
}

public sealed record LapComparison
{
    public required int ReferenceLapNumber { get; init; }

    public required double GridStepMeters { get; init; }

    public required IReadOnlyList<LapDelta> Laps { get; init; }
}

/// <summary>
/// Compares laps against a reference lap on a shared track-progress grid.
/// Laps are aligned by interpolated lap time at each progress point rather than
/// by sample index, so differing sample rates or timing do not matter.
/// </summary>
public sealed class LapComparer
{
    private readonly LapComparisonOptions _options;

    public LapComparer(LapComparisonOptions? options = null)
    {
        _options = options ?? new LapComparisonOptions();
    }

    public LapComparison Compare(LapTimeline reference, IReadOnlyList<LapTimeline> laps)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(laps);

        var referencePoints = LapAlignment.BuildControlPoints(reference);
        if (referencePoints.Count < 2)
        {
            throw new InvalidOperationException(
                $"Reference lap {reference.LapNumber} has insufficient track progress data to align against.");
        }

        var referenceMax = referencePoints[^1].ProgressMeters;
        var results = new List<LapDelta>(laps.Count);

        foreach (var lap in laps)
        {
            results.Add(CompareLap(reference, referencePoints, referenceMax, lap));
        }

        return new LapComparison
        {
            ReferenceLapNumber = reference.LapNumber,
            GridStepMeters = _options.GridStepMeters,
            Laps = results
        };
    }

    private LapDelta CompareLap(
        LapTimeline reference,
        IReadOnlyList<LapAlignment.ControlPoint> referencePoints,
        double referenceMax,
        LapTimeline lap)
    {
        var points = LapAlignment.BuildControlPoints(lap);
        var maxProgress = points.Count > 0
            ? Math.Min(referenceMax, points[^1].ProgressMeters)
            : 0.0;

        var deltaPoints = new List<DeltaPoint>();
        for (var progress = 0.0; progress <= maxProgress + 1e-9; progress += _options.GridStepMeters)
        {
            var referenceTime = LapAlignment.TimeAt(referencePoints, progress);
            var lapTime = LapAlignment.TimeAt(points, progress);
            double? delta = referenceTime is { } rt && lapTime is { } lt ? lt - rt : null;
            deltaPoints.Add(new DeltaPoint(progress, referenceTime, lapTime, delta));
        }

        var lastDelta = deltaPoints.LastOrDefault(p => p.DeltaSeconds is not null)?.DeltaSeconds;

        double? totalDelta = lap.LapTimeSeconds is { } lapTime2 && reference.LapTimeSeconds is { } refTime
            ? lapTime2 - refTime
            : null;

        return new LapDelta
        {
            LapNumber = lap.LapNumber,
            LapTimeSeconds = lap.LapTimeSeconds,
            ReferenceLapTimeSeconds = reference.LapTimeSeconds,
            TotalDeltaSeconds = totalDelta,
            DeltaAtEndSeconds = lastDelta,
            Points = deltaPoints,
            Regions = BuildRegions(deltaPoints)
        };
    }

    private IReadOnlyList<GainLossRegion> BuildRegions(IReadOnlyList<DeltaPoint> points)
    {
        var regions = new List<GainLossRegion>();
        var i = 1;

        while (i < points.Count)
        {
            if (points[i].DeltaSeconds is not { } current || points[i - 1].DeltaSeconds is not { } previous)
            {
                i++;
                continue;
            }

            var slope = current - previous;
            if (Math.Abs(slope) < 1e-9)
            {
                i++;
                continue;
            }

            var gaining = slope < 0;
            var start = i;
            var end = i;

            while (end + 1 < points.Count &&
                   points[end + 1].DeltaSeconds is { } next &&
                   (next - points[end].DeltaSeconds!.Value < 0) == gaining)
            {
                end++;
            }

            var startDelta = points[start - 1].DeltaSeconds!.Value;
            var endDelta = points[end].DeltaSeconds!.Value;
            var magnitude = Math.Abs(endDelta - startDelta);
            var length = points[end].ProgressMeters - points[start - 1].ProgressMeters;

            if (magnitude >= _options.RegionThresholdSeconds && length >= _options.MinimumRegionLengthMeters)
            {
                regions.Add(new GainLossRegion
                {
                    IsGain = gaining,
                    StartProgressMeters = points[start - 1].ProgressMeters,
                    EndProgressMeters = points[end].ProgressMeters,
                    StartDeltaSeconds = startDelta,
                    EndDeltaSeconds = endDelta
                });
            }

            i = end + 1;
        }

        return regions;
    }
}

/// <summary>
/// Reference-lap selection policy (kept explicit and documented rather than
/// buried): the fastest valid, complete lap by lap time; ties broken by the
/// lowest lap number. Returns null when no lap qualifies.
/// </summary>
public static class ReferenceLapSelector
{
    public static LapTimeline? SelectFastestValid(IEnumerable<LapTimeline> laps)
    {
        ArgumentNullException.ThrowIfNull(laps);

        LapTimeline? best = null;
        foreach (var lap in laps)
        {
            if (!lap.IsValid || lap.LapTimeSeconds is null) continue;

            if (best is null ||
                lap.LapTimeSeconds < best.LapTimeSeconds ||
                (lap.LapTimeSeconds == best.LapTimeSeconds && lap.LapNumber < best.LapNumber))
            {
                best = lap;
            }
        }

        return best;
    }
}
