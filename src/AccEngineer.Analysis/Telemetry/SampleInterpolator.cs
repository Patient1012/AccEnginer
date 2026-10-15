using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Telemetry;

/// <summary>
/// Basic linear interpolator for small gaps. Conservative - only interpolates
/// when values are missing/invalid, does not aggressively smooth existing data.
/// </summary>
public sealed class SampleInterpolator
{
    public int MaxSteps { get; init; } = 5;

    public TelemetrySample Interpolate(
        TelemetrySample previous,
        TelemetrySample current,
        double t)
    {
        if (t < 0.0 || t > 1.0)
        {
            t = t < 0.0 ? 0.0 : 1.0;
        }

        var fraction = (float)t;

        return current with
        {
            SpeedKph = Lerp(previous.SpeedKph, current.SpeedKph, fraction),
            SpeedMetersPerSecond = Lerp(previous.SpeedMetersPerSecond, current.SpeedMetersPerSecond, fraction),
            TrackProgressMeters = Lerp(previous.TrackProgressMeters, current.TrackProgressMeters, fraction),
            ThrottleInput = Lerp(previous.ThrottleInput, current.ThrottleInput, fraction),
            BrakeInput = Lerp(previous.BrakeInput, current.BrakeInput, fraction),
            SteeringInput = Lerp(previous.SteeringInput, current.SteeringInput, fraction),
            EngineRpm = Lerp(previous.EngineRpm, current.EngineRpm, fraction),
            LateralAccelerationG = Lerp(previous.LateralAccelerationG, current.LateralAccelerationG, fraction),
            LongitudinalAccelerationG = Lerp(previous.LongitudinalAccelerationG, current.LongitudinalAccelerationG, fraction)
        };
    }

    private static float? Lerp(float? a, float? b, float fraction)
    {
        if (a.HasValue && b.HasValue)
        {
            return a.Value + (b.Value - a.Value) * fraction;
        }

        if (a.HasValue && !b.HasValue)
        {
            return a.Value;
        }

        if (b.HasValue && !a.HasValue)
        {
            return b.Value;
        }

        return null;
    }

    private static int? Lerp(int? a, int? b, float fraction)
    {
        if (a.HasValue && b.HasValue)
        {
            return (int)Math.Round(a.Value + (b.Value - a.Value) * fraction);
        }

        if (a.HasValue && !b.HasValue)
        {
            return a.Value;
        }

        if (b.HasValue && !a.HasValue)
        {
            return b.Value;
        }

        return null;
    }
}
