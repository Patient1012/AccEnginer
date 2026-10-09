namespace AccEngineer.Analysis;

/// <summary>
/// Builds monotonic (progress, time) control points from a lap and interpolates
/// lap time at arbitrary track progress. This is what lets laps with different
/// sample timing be compared: comparison happens on the progress axis, never on
/// raw sample index or timestamp.
/// </summary>
internal static class LapAlignment
{
    internal readonly record struct ControlPoint(double ProgressMeters, double TimeSeconds);

    /// <summary>
    /// Control points with strictly increasing progress. Samples that jump
    /// backwards (resets, respawns) or lack progress are skipped.
    /// </summary>
    public static List<ControlPoint> BuildControlPoints(LapTimeline lap)
    {
        var points = new List<ControlPoint>(lap.Samples.Count);
        var lastProgress = double.NegativeInfinity;

        foreach (var sample in lap.Samples)
        {
            if (sample.ProgressMeters is not { } progress) continue;
            if (progress <= lastProgress) continue;

            points.Add(new ControlPoint(progress, sample.LapTimeSeconds));
            lastProgress = progress;
        }

        return points;
    }

    /// <summary>
    /// Linear interpolation of lap time at the given progress. Returns null when
    /// progress lies outside the known range.
    /// </summary>
    public static double? TimeAt(IReadOnlyList<ControlPoint> points, double progress)
    {
        if (points.Count == 0) return null;
        if (progress < points[0].ProgressMeters) return null;
        if (progress > points[^1].ProgressMeters) return null;
        if (progress <= points[0].ProgressMeters) return points[0].TimeSeconds;
        if (progress >= points[^1].ProgressMeters) return points[^1].TimeSeconds;

        var lo = 0;
        var hi = points.Count - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (points[mid].ProgressMeters <= progress) lo = mid;
            else hi = mid;
        }

        var a = points[lo];
        var b = points[hi];
        var span = b.ProgressMeters - a.ProgressMeters;
        if (span <= 0) return a.TimeSeconds;

        var ratio = (progress - a.ProgressMeters) / span;
        return a.TimeSeconds + ratio * (b.TimeSeconds - a.TimeSeconds);
    }
}
