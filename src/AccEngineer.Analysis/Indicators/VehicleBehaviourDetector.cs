using AccEngineer.Analysis.Corners;

namespace AccEngineer.Analysis.Indicators;

public sealed class VehicleBehaviourDetectorOptions
{
    /// <summary>Absolute steering input considered "high" for understeer proxies.</summary>
    public double HighSteeringInput { get; init; } = 0.30;

    /// <summary>Throttle input considered low.</summary>
    public double LowThrottle { get; init; } = 0.5;

    /// <summary>Steering rate sign changes in a phase that suggest excessive correction.</summary>
    public int CorrectionCountThreshold { get; init; } = 3;

    /// <summary>Minimum speed gain (km/h) expected from apex to exit when on throttle.</summary>
    public double MinimumExitSpeedGainKph { get; init; } = 10.0;

    /// <summary>Distance before the apex used to bound an undefined entry window.</summary>
    public double EntryLookaheadMeters { get; init; } = 150.0;

    /// <summary>Distance after the apex used to bound an undefined exit window.</summary>
    public double ExitLookaheadMeters { get; init; } = 150.0;
}

/// <summary>
/// Derives conservative vehicle-behaviour indicators from corner phase telemetry.
/// These are weighted signals backed by measured patterns; the detector never asserts
/// a cause. Each indicator carries the proxy it relies on and a confidence weight.
/// </summary>
public sealed class VehicleBehaviourDetector
{
    private readonly VehicleBehaviourDetectorOptions _options;

    public VehicleBehaviourDetector(VehicleBehaviourDetectorOptions? options = null)
    {
        _options = options ?? new VehicleBehaviourDetectorOptions();
    }

    public IReadOnlyList<BehaviourIndicator> Detect(LapTimeline lap, TrackCornerDefinition? corners)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var indicators = new List<BehaviourIndicator>();
        if (corners is null || corners.Corners.Count == 0)
        {
            return indicators;
        }

        var analyzer = new CornerAnalyzer();
        foreach (var analyzed in analyzer.AnalyzeAll(lap, corners))
        {
            var corner = analyzed.Corner;
            var metrics = analyzed.Metrics;

            var turnIn = metrics.TurnInMeters ?? corner.ApexDistanceMeters;
            var exit = corner.ExitDistanceMeters ?? corner.ApexDistanceMeters;

            var rotation = Slice(lap, turnIn, exit);
            var exitWindow = Slice(lap, exit, metrics.WindowEndMeters ?? corner.ApexDistanceMeters + _options.ExitLookaheadMeters);

            DetectUndersteer(corner, rotation, exitWindow, indicators);
            DetectExcessiveCorrection(corner, rotation, indicators);
        }

        return indicators;
    }

    private void DetectUndersteer(
        Corner corner,
        List<LapSample> rotation,
        List<LapSample> exitWindow,
        List<BehaviourIndicator> indicators)
    {
        var peakSteering = PeakAbsoluteSteering(rotation);
        var rotationMinSpeed = MinSpeed(rotation);
        var rotationStartSpeed = FirstSpeed(rotation);
        var rotationThrottle = RotationThrottle(rotation);

        if (peakSteering is { } ps && ps >= _options.HighSteeringInput &&
            rotationMinSpeed is { } min && rotationStartSpeed is { } start && min < start &&
            rotationThrottle is { } throttle && throttle < _options.LowThrottle)
        {
            indicators.Add(new BehaviourIndicator
            {
                Kind = BehaviourIndicatorKind.PossibleMidCornerUndersteer,
                CornerNumber = corner.Number,
                Phase = CornerPhase.MidCorner,
                DistanceMeters = corner.ApexDistanceMeters,
                Observation =
                    "Steering input remains high through the rotation while speed falls and throttle stays low.",
                Evidence = new[]
                {
                    $"Peak absolute steering input {ps:0.00}.",
                    $"Speed fell from {start:0} km/h to a minimum of {min:0} km/h across the rotation.",
                    $"Throttle remained at or below {throttle:0.00} through the rotation."
                },
                Confidence = 0.55,
                Limitations = new[]
                {
                    "Steering magnitude is a proxy for corner geometry, not a measured slip angle.",
                    "Understeer cannot be confirmed without yaw rate or wheel-slip channels."
                }
            });
        }

        var exitThrottle = PeakThrottle(exitWindow);
        var exitSteering = PeakAbsoluteSteering(exitWindow);
        var exitSpeedGain = SpeedGain(exitWindow);

        if (exitThrottle is { } et && et >= _options.LowThrottle &&
            exitSteering is { } es && es >= _options.HighSteeringInput &&
            exitSpeedGain is { } gain && gain < _options.MinimumExitSpeedGainKph)
        {
            indicators.Add(new BehaviourIndicator
            {
                Kind = BehaviourIndicatorKind.PossibleExitUndersteer,
                CornerNumber = corner.Number,
                Phase = CornerPhase.Exit,
                DistanceMeters = corner.ExitDistanceMeters ?? corner.ApexDistanceMeters,
                Observation =
                    "Throttle is applied on exit while steering stays high and speed gain stays low.",
                Evidence = new[]
                {
                    $"Peak throttle {et:0.00} during exit.",
                    $"Steering still reached {es:0.00} during exit.",
                    $"Speed gained only {gain:0.0} km/h across the exit window."
                },
                Confidence = 0.5,
                Limitations = new[]
                {
                    "A low exit speed gain can have many causes and is not proof of understeer.",
                    "No lateral acceleration or slip channel is used."
                }
            });
        }
    }

    private void DetectExcessiveCorrection(
        Corner corner,
        List<LapSample> rotation,
        List<BehaviourIndicator> indicators)
    {
        var corrections = CountCorrections(rotation);
        if (corrections >= _options.CorrectionCountThreshold)
        {
            indicators.Add(new BehaviourIndicator
            {
                Kind = BehaviourIndicatorKind.ExcessiveSteeringCorrection,
                CornerNumber = corner.Number,
                Phase = CornerPhase.MidCorner,
                DistanceMeters = corner.ApexDistanceMeters,
                Observation =
                    "Steering rate reversed repeatedly through the rotation, indicating active correction.",
                Evidence = new[]
                {
                    $"{corrections} steering rate sign changes were measured across the rotation."
                },
                Confidence = 0.6,
                Limitations = new[]
                {
                    "Corrections can be deliberate; this counts input reversals, not their cause."
                }
            });
        }
    }

    private static List<LapSample> Slice(LapTimeline lap, double start, double end)
    {
        var result = new List<LapSample>();
        foreach (var sample in lap.Samples)
        {
            if (sample.ProgressMeters is not { } p) continue;
            if (p < start || p >= end) continue;
            result.Add(sample);
        }
        return result;
    }

    private static double? PeakAbsoluteSteering(List<LapSample> samples)
    {
        double? peak = null;
        foreach (var sample in samples)
        {
            if (sample.Source.SteeringInput is { } s && (peak is null || Math.Abs(s) > peak))
            {
                peak = Math.Abs(s);
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

    private static double? FirstSpeed(List<LapSample> samples)
        => samples.Count > 0 ? samples[0].Source.SpeedKph : null;

    private static double? RotationThrottle(List<LapSample> samples)
    {
        double? max = null;
        foreach (var sample in samples)
        {
            if (sample.Source.ThrottleInput is { } t && (max is null || t > max))
            {
                max = t;
            }
        }
        return max;
    }

    private static double? PeakThrottle(List<LapSample> samples) => RotationThrottle(samples);

    private static double? SpeedGain(List<LapSample> samples)
    {
        if (samples.Count == 0) return null;
        var first = samples[0].Source.SpeedKph;
        var last = samples[^1].Source.SpeedKph;
        return first is { } f && last is { } l ? l - f : null;
    }

    private static int CountCorrections(List<LapSample> samples)
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
}
