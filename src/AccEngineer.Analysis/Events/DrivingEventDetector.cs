using AccEngineer.Analysis.Corners;
using AccEngineer.Analysis.Metrics;

namespace AccEngineer.Analysis.Events;

/// <summary>
/// Detects reusable driving events once per lap from the fundamental metrics and,
/// when available, the corner model. Deterministic and free of ACC/UI dependencies.
/// </summary>
public sealed class DrivingEventDetector
{
    private readonly BrakingMetricsCalculator _braking;
    private readonly ThrottleMetricsCalculator _throttle;
    private readonly GearMetricsCalculator _gear = new();
    private readonly CornerAnalyzer _corners;

    public DrivingEventDetector(
        BrakingMetricsOptions? brakingOptions = null,
        ThrottleMetricsOptions? throttleOptions = null,
        CornerAnalysisOptions? cornerOptions = null)
    {
        _braking = new BrakingMetricsCalculator(brakingOptions);
        _throttle = new ThrottleMetricsCalculator(throttleOptions);
        _corners = new CornerAnalyzer(cornerOptions);
    }

    public IReadOnlyList<DrivingEvent> Detect(LapTimeline lap, TrackCornerDefinition? corners = null)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var events = new List<DrivingEvent>();

        AddBrakingEvents(lap, events);
        AddThrottleEvents(lap, events);
        AddGearEvents(lap, events);
        AddSteeringEvents(lap, events);

        if (corners is not null)
        {
            AddCornerEvents(lap, corners, events);
        }

        return events;
    }

    private void AddBrakingEvents(LapTimeline lap, List<DrivingEvent> events)
    {
        foreach (var zone in _braking.Calculate(lap))
        {
            var values = new Dictionary<string, double>();
            if (zone.PeakBrakeInput is { } peak) values["peakBrakeInput"] = peak;
            if (zone.DurationSeconds is { } duration) values["durationSeconds"] = duration;
            if (zone.DistanceTravelledMeters is { } travelled) values["distanceMeters"] = travelled;

            events.Add(new DrivingEvent
            {
                Kind = DrivingEventKind.Braking,
                LapNumber = lap.LapNumber,
                StartDistanceMeters = zone.StartDistanceMeters,
                EndDistanceMeters = zone.EndDistanceMeters,
                StartTimeSeconds = lap.Samples[zone.StartSampleIndex].LapTimeSeconds,
                EndTimeSeconds = lap.Samples[zone.EndSampleIndex].LapTimeSeconds,
                Values = values,
                Confidence = DrivingEventConfidence.Braking,
                FirstSampleId = zone.FirstSampleId,
                LastSampleId = zone.LastSampleId
            });
        }
    }

    private void AddThrottleEvents(LapTimeline lap, List<DrivingEvent> events)
    {
        var metrics = _throttle.Calculate(lap);

        foreach (var application in metrics.Applications)
        {
            var sample = lap.Samples[application.SampleIndex];
            var values = new Dictionary<string, double>();
            if (application.InputAtApplication is { } input) values["inputAtApplication"] = input;
            if (application.PickupRatePerSecond is { } rate) values["pickupRatePerSecond"] = rate;

            events.Add(new DrivingEvent
            {
                Kind = DrivingEventKind.ThrottlePickup,
                LapNumber = lap.LapNumber,
                StartDistanceMeters = sample.ProgressMeters,
                EndDistanceMeters = sample.ProgressMeters,
                StartTimeSeconds = sample.LapTimeSeconds,
                EndTimeSeconds = sample.LapTimeSeconds,
                Values = values,
                Confidence = DrivingEventConfidence.ThrottlePickup,
                FirstSampleId = sample.Source.SampleId,
                LastSampleId = sample.Source.SampleId
            });
        }

        foreach (var lift in metrics.Lifts)
        {
            var sample = lap.Samples[lift.SampleIndex];
            var values = new Dictionary<string, double>();
            if (lift.ThrottleDrop is { } drop) values["throttleDrop"] = drop;

            events.Add(new DrivingEvent
            {
                Kind = DrivingEventKind.ThrottleLift,
                LapNumber = lap.LapNumber,
                StartDistanceMeters = sample.ProgressMeters,
                EndDistanceMeters = sample.ProgressMeters,
                StartTimeSeconds = sample.LapTimeSeconds,
                EndTimeSeconds = sample.LapTimeSeconds,
                Values = values,
                Confidence = DrivingEventConfidence.ThrottleLift,
                FirstSampleId = sample.Source.SampleId,
                LastSampleId = sample.Source.SampleId
            });
        }
    }

    private void AddGearEvents(LapTimeline lap, List<DrivingEvent> events)
    {
        foreach (var shift in _gear.Calculate(lap).Shifts)
        {
            var sample = lap.Samples[shift.SampleIndex];

            events.Add(new DrivingEvent
            {
                Kind = DrivingEventKind.GearChange,
                LapNumber = lap.LapNumber,
                StartDistanceMeters = shift.DistanceMeters,
                EndDistanceMeters = shift.DistanceMeters,
                StartTimeSeconds = sample.LapTimeSeconds,
                EndTimeSeconds = sample.LapTimeSeconds,
                Values = new Dictionary<string, double>
                {
                    ["fromGear"] = shift.FromGearRaw,
                    ["toGear"] = shift.ToGearRaw
                },
                Confidence = DrivingEventConfidence.GearChange,
                FirstSampleId = shift.SampleId,
                LastSampleId = shift.SampleId
            });
        }
    }

    private static void AddSteeringEvents(LapTimeline lap, List<DrivingEvent> events)
    {
        var samples = lap.Samples;
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
                events.Add(new DrivingEvent
                {
                    Kind = DrivingEventKind.SteeringCorrection,
                    LapNumber = lap.LapNumber,
                    StartDistanceMeters = samples[i].ProgressMeters,
                    EndDistanceMeters = samples[i].ProgressMeters,
                    StartTimeSeconds = samples[i].LapTimeSeconds,
                    EndTimeSeconds = samples[i].LapTimeSeconds,
                    Values = new Dictionary<string, double> { ["rate"] = rate },
                    Confidence = DrivingEventConfidence.SteeringCorrection,
                    FirstSampleId = samples[i].Source.SampleId,
                    LastSampleId = samples[i].Source.SampleId
                });
            }

            previousRate = rate;
        }
    }

    private void AddCornerEvents(LapTimeline lap, TrackCornerDefinition corners, List<DrivingEvent> events)
    {
        foreach (var analyzed in _corners.AnalyzeAll(lap, corners))
        {
            var metrics = analyzed.Metrics;

            if (metrics.TurnInMeters is { } turnIn)
            {
                events.Add(CornerEvent(lap, DrivingEventKind.TurnIn, analyzed.Corner.Number,
                    turnIn, metrics.TurnInSteeringInput, DrivingEventConfidence.TurnIn));
            }

            events.Add(CornerEvent(lap, DrivingEventKind.Apex, analyzed.Corner.Number,
                metrics.ApexDistanceMeters, metrics.ApexSpeedKph, DrivingEventConfidence.Apex));

            if (metrics.ThrottlePickupMeters is { } pickup)
            {
                events.Add(CornerEvent(lap, DrivingEventKind.CornerExit, analyzed.Corner.Number,
                    pickup, metrics.ExitSpeedKph, DrivingEventConfidence.CornerExit));
            }
        }
    }

    private static DrivingEvent CornerEvent(
        LapTimeline lap,
        DrivingEventKind kind,
        int cornerNumber,
        double? distance,
        double? value,
        double confidence)
    {
        var sample = NearestSample(lap, distance);
        var values = new Dictionary<string, double>();
        if (value is { } v)
        {
            values[kind == DrivingEventKind.Apex ? "apexSpeedKph" : "value"] = v;
        }

        return new DrivingEvent
        {
            Kind = kind,
            LapNumber = lap.LapNumber,
            CornerNumber = cornerNumber,
            StartDistanceMeters = distance,
            EndDistanceMeters = distance,
            StartTimeSeconds = sample?.LapTimeSeconds,
            EndTimeSeconds = sample?.LapTimeSeconds,
            Values = values,
            Confidence = confidence,
            FirstSampleId = sample?.Source.SampleId,
            LastSampleId = sample?.Source.SampleId
        };
    }

    private static LapSample? NearestSample(LapTimeline lap, double? distance)
    {
        if (distance is not { } target) return null;

        LapSample? best = null;
        var bestDelta = double.MaxValue;
        foreach (var sample in lap.Samples)
        {
            if (sample.ProgressMeters is not { } p) continue;
            var delta = Math.Abs(p - target);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = sample;
            }
        }
        return best;
    }
}
