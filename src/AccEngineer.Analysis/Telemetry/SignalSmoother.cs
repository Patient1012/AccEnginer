using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Telemetry;

/// <summary>
/// Minimal signal smoother - does not aggressively smooth to preserve event timing
/// (brake points, throttle pickup, etc). Only used when explicitly requested.
/// </summary>
public sealed class SignalSmoother
{
    public int WindowSize { get; init; } = 3;

    public TelemetrySample Smooth(TelemetrySample sample, IReadOnlyList<TelemetrySample> window)
    {
        if (window.Count == 0)
        {
            return sample;
        }

        var weightedThrottle = 0f;
        var weightedBrake = 0f;
        var weightedSteering = 0f;
        var count = 0f;

        foreach (var w in window)
        {
            if (w.ThrottleInput.HasValue) { weightedThrottle += w.ThrottleInput.Value; count++; }
        }
        if (count > 0)
        {
            var avgThrottle = weightedThrottle / count;
            sample = sample with { ThrottleInput = (float)Math.Round(avgThrottle, 4) };
        }

        count = 0f;
        weightedBrake = 0f;
        foreach (var w in window)
        {
            if (w.BrakeInput.HasValue) { weightedBrake += w.BrakeInput.Value; count++; }
        }
        if (count > 0)
        {
            var avgBrake = weightedBrake / count;
            sample = sample with { BrakeInput = (float)Math.Round(avgBrake, 4) };
        }

        count = 0f;
        weightedSteering = 0f;
        foreach (var w in window)
        {
            if (w.SteeringInput.HasValue) { weightedSteering += w.SteeringInput.Value; count++; }
        }
        if (count > 0)
        {
            var avgSteering = weightedSteering / count;
            sample = sample with { SteeringInput = (float)Math.Round(avgSteering, 4) };
        }

        return sample;
    }
}
