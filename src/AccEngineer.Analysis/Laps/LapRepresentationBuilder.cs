using AccEngineer.Analysis.Metrics;
using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Laps;

/// <summary>
/// Builds the distance-based <see cref="LapRepresentation"/> from a reconstructed
/// <see cref="LapTimeline"/>. When the source does not report track progress the
/// distance is integrated from speed and flagged as estimated so consumers can tell
/// measured positions from derived ones.
/// </summary>
public sealed class LapRepresentationBuilder
{
    private readonly LapMetricsCalculator _metrics;

    public LapRepresentationBuilder(LapMetricsCalculator? metricsCalculator = null)
    {
        _metrics = metricsCalculator ?? new LapMetricsCalculator();
    }

    public LapRepresentation Build(
        LapTimeline lap,
        string? trackName = null,
        string? carName = null,
        SessionType sessionType = SessionType.Unknown)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var samples = ResolveDistances(lap, out var estimatedCount);

        var warnings = new List<string>();
        if (estimatedCount > 0)
        {
            warnings.Add(
                $"{estimatedCount} sample(s) had no reported track progress; distance was estimated from speed.");
        }

        return new LapRepresentation
        {
            SessionId = lap.SessionId,
            LapNumber = lap.LapNumber,
            TrackName = trackName,
            CarName = carName,
            SessionType = sessionType,
            IsComplete = lap.IsComplete,
            IsValid = lap.IsValid,
            TotalTimeSeconds = lap.LapTimeSeconds,
            LapTimeSource = lap.LapTimeSource,
            Samples = samples,
            Sectors = lap.Sectors,
            Metrics = _metrics.Calculate(lap),
            Warnings = warnings
        };
    }

    private static IReadOnlyList<LapDistancePoint> ResolveDistances(LapTimeline lap, out int estimatedCount)
    {
        var result = new List<LapDistancePoint>(lap.Samples.Count);
        var estimated = 0;

        double? current = null;
        bool currentFromProgress;
        double? previousTime = null;

        foreach (var sample in lap.Samples)
        {
            if (sample.ProgressMeters is { } reported)
            {
                current = reported;
                currentFromProgress = true;
            }
            else
            {
                var speedMs = ResolveSpeedMetersPerSecond(sample);
                if (current is not null && previousTime is not null && speedMs is { } v)
                {
                    var dt = sample.LapTimeSeconds - previousTime.Value;
                    if (dt > 0)
                    {
                        current += v * dt;
                        currentFromProgress = false;
                    }
                    else
                    {
                        currentFromProgress = false;
                    }
                }
                else
                {
                    currentFromProgress = false;
                }
            }

            if (!currentFromProgress && current is not null)
            {
                estimated++;
            }

            result.Add(new LapDistancePoint
            {
                Sample = sample,
                DistanceMeters = current,
                IsEstimatedDistance = !currentFromProgress && current is not null
            });

            previousTime = sample.LapTimeSeconds;
        }

        estimatedCount = estimated;
        return result;
    }

    private static double? ResolveSpeedMetersPerSecond(LapSample sample)
    {
        if (sample.Source.SpeedMetersPerSecond is { } ms)
        {
            return ms;
        }
        if (sample.Source.SpeedKph is { } kph)
        {
            return kph / 3.6;
        }
        return null;
    }
}
