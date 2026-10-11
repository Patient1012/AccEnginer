using AccEngineer.Telemetry;

namespace AccEngineer.Analysis;

/// <summary>
/// Deterministic driving analysis over reconstructed laps. It reports measured
/// facts (braking zones, corner shape, throttle pickup, tyre summaries) and emits
/// traceable <see cref="TelemetryEvent"/>s. Interpretations are kept explicitly
/// separate and never dressed up as diagnoses; this type has no AI dependency so
/// it can be rerun from a recording.
/// </summary>
public sealed class DrivingAnalyzer
{
    private static readonly string[] WheelNames = { "FL", "FR", "RL", "RR" };

    private readonly DrivingAnalysisOptions _options;

    public DrivingAnalyzer(DrivingAnalysisOptions? options = null)
    {
        _options = options ?? new DrivingAnalysisOptions();
    }

    public DrivingAnalysisResult Analyze(SessionLapAnalysis session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var warnings = new List<string>(session.Warnings);
        var events = new List<TelemetryEvent>();
        var seq = 0;

        var laps = new List<LapDrivingAnalysis>(session.Laps.Count);
        foreach (var lap in session.Laps)
        {
            laps.Add(AnalyzeLap(lap, events, ref seq));
        }

        AddSessionEvents(session, events, ref seq);

        if (events.Count > _options.MaxEvents)
        {
            warnings.Add(
                $"Event count {events.Count} exceeded the cap of {_options.MaxEvents}; the list was truncated.");
            events = events.Take(_options.MaxEvents).ToList();
        }

        return new DrivingAnalysisResult
        {
            Laps = laps,
            Events = events,
            Warnings = warnings
        };
    }

    private LapDrivingAnalysis AnalyzeLap(LapTimeline lap, List<TelemetryEvent> events, ref int seq)
    {
        var brakingZones = DetectBrakingZones(lap);
        var corners = DetectCorners(lap);
        var tyres = SummarizeTyres(lap);

        foreach (var zone in brakingZones)
        {
            events.Add(BuildBrakingEvent(zone, ref seq));
        }

        foreach (var corner in corners)
        {
            events.Add(BuildCornerEvent(corner, ref seq));
        }

        var throttleKnown = lap.Samples.Count(s => s.Source.ThrottleInput is not null);
        var brakeKnown = lap.Samples.Count(s => s.Source.BrakeInput is not null);

        return new LapDrivingAnalysis
        {
            LapNumber = lap.LapNumber,
            BrakingZones = brakingZones,
            Corners = corners,
            Tyres = tyres,
            FullThrottleFraction = throttleKnown > 0
                ? (double)lap.Samples.Count(s => s.Source.ThrottleInput is { } t && t >= _options.FullThrottleThreshold) / throttleKnown
                : null,
            BrakingFraction = brakeKnown > 0
                ? (double)lap.Samples.Count(s => s.Source.BrakeInput is { } b && b >= _options.BrakeOnThreshold) / brakeKnown
                : null
        };
    }

    private IReadOnlyList<BrakingZone> DetectBrakingZones(LapTimeline lap)
    {
        var samples = lap.Samples;
        var zones = new List<BrakingZone>();

        var inZone = false;
        var start = 0;

        for (var i = 0; i < samples.Count; i++)
        {
            var brake = samples[i].Source.BrakeInput;

            if (!inZone)
            {
                if (brake is { } b && b >= _options.BrakeOnThreshold)
                {
                    inZone = true;
                    start = i;
                }
                continue;
            }

            var ended = brake is null || brake <= _options.BrakeOffThreshold;
            if (ended || i == samples.Count - 1)
            {
                var end = ended ? i - 1 : i;
                if (end - start + 1 >= _options.MinimumBrakingSamples)
                {
                    zones.Add(BuildBrakingZone(lap.LapNumber, samples, start, end));
                }
                inZone = false;
            }
        }

        return zones;
    }

    private static BrakingZone BuildBrakingZone(int lapNumber, IReadOnlyList<LapSample> samples, int start, int end)
    {
        double? peak = null;
        double? minSpeed = null;

        for (var i = start; i <= end; i++)
        {
            if (samples[i].Source.BrakeInput is { } b && (peak is null || b > peak))
            {
                peak = b;
            }
            if (samples[i].Source.SpeedKph is { } sp && (minSpeed is null || sp < minSpeed))
            {
                minSpeed = sp;
            }
        }

        return new BrakingZone
        {
            LapNumber = lapNumber,
            StartProgressMeters = samples[start].ProgressMeters,
            EndProgressMeters = samples[end].ProgressMeters,
            SampleCount = end - start + 1,
            PeakBrakeInput = peak,
            EntrySpeedKph = samples[start].Source.SpeedKph,
            MinimumSpeedKph = minSpeed,
            DurationSeconds = samples[end].LapTimeSeconds - samples[start].LapTimeSeconds,
            FirstSampleId = samples[start].Source.SampleId,
            LastSampleId = samples[end].Source.SampleId
        };
    }

    private IReadOnlyList<CornerAnalysis> DetectCorners(LapTimeline lap)
    {
        var samples = lap.Samples;
        var corners = new List<CornerAnalysis>();

        var start = -1;
        var last = -1;

        for (var i = 0; i < samples.Count; i++)
        {
            var steering = samples[i].Source.SteeringInput;
            var inCorner = steering is { } s && Math.Abs(s) >= _options.CornerSteeringThreshold;

            if (inCorner)
            {
                if (start < 0)
                {
                    start = i;
                }
                last = i;
            }
            else if (start >= 0 && i - last > _options.CornerMergeGapSamples)
            {
                AddCornerIfLongEnough(lap.LapNumber, samples, start, last, corners);
                start = -1;
            }
        }

        if (start >= 0)
        {
            AddCornerIfLongEnough(lap.LapNumber, samples, start, last, corners);
        }

        return corners;
    }

    private void AddCornerIfLongEnough(
        int lapNumber,
        IReadOnlyList<LapSample> samples,
        int start,
        int end,
        List<CornerAnalysis> corners)
    {
        if (end - start + 1 < _options.MinimumCornerSamples)
        {
            return;
        }

        corners.Add(BuildCorner(lapNumber, samples, start, end));
    }

    private CornerAnalysis BuildCorner(int lapNumber, IReadOnlyList<LapSample> samples, int start, int end)
    {
        double? peakSteering = null;
        double? minSpeed = null;
        var apex = start;

        for (var i = start; i <= end; i++)
        {
            if (samples[i].Source.SteeringInput is { } s && (peakSteering is null || Math.Abs(s) > peakSteering))
            {
                peakSteering = Math.Abs(s);
            }
            if (samples[i].Source.SpeedKph is { } sp && (minSpeed is null || sp < minSpeed))
            {
                minSpeed = sp;
                apex = i;
            }
        }

        // Throttle pickup and full throttle are searched past the steering-based
        // corner exit: the driver usually keeps applying throttle onto the exit.
        var pickup = -1;
        var full = -1;
        for (var i = apex + 1; i < samples.Count; i++)
        {
            if (samples[i].Source.ThrottleInput is not { } t)
            {
                continue;
            }
            if (pickup < 0 && t >= _options.ThrottlePickupThreshold)
            {
                pickup = i;
            }
            if (full < 0 && t >= _options.FullThrottleThreshold)
            {
                full = i;
                break;
            }
        }

        return new CornerAnalysis
        {
            LapNumber = lapNumber,
            EntryProgressMeters = samples[start].ProgressMeters,
            ApexProgressMeters = samples[apex].ProgressMeters,
            ExitProgressMeters = samples[end].ProgressMeters,
            MinimumSpeedKph = minSpeed,
            PeakSteeringInput = peakSteering,
            ThrottlePickupProgressMeters = pickup >= 0 ? samples[pickup].ProgressMeters : null,
            ApexToFullThrottleSeconds = full >= 0
                ? samples[full].LapTimeSeconds - samples[apex].LapTimeSeconds
                : null,
            FirstSampleId = samples[start].Source.SampleId,
            LastSampleId = samples[end].Source.SampleId
        };
    }

    private static IReadOnlyList<TyreWheelSummary> SummarizeTyres(LapTimeline lap)
    {
        var summaries = new List<TyreWheelSummary>(WheelNames.Length);

        for (var wheel = 0; wheel < WheelNames.Length; wheel++)
        {
            var pressures = new List<double>();
            var temps = new List<double>();

            foreach (var sample in lap.Samples)
            {
                if (sample.Source.WheelPressure is { } p && wheel < p.Length)
                {
                    pressures.Add(p[wheel]);
                }
                if (sample.Source.TyreCoreTempC is { } t && wheel < t.Length)
                {
                    temps.Add(t[wheel]);
                }
            }

            summaries.Add(new TyreWheelSummary
            {
                Wheel = WheelNames[wheel],
                PressureSampleCount = pressures.Count,
                PressureMin = pressures.Count > 0 ? pressures.Min() : null,
                PressureMax = pressures.Count > 0 ? pressures.Max() : null,
                PressureAverage = pressures.Count > 0 ? pressures.Average() : null,
                CoreTempSampleCount = temps.Count,
                CoreTempMinC = temps.Count > 0 ? temps.Min() : null,
                CoreTempMaxC = temps.Count > 0 ? temps.Max() : null,
                CoreTempAvgC = temps.Count > 0 ? temps.Average() : null
            });
        }

        return summaries;
    }

    private static TelemetryEvent BuildBrakingEvent(BrakingZone zone, ref int seq)
    {
        var location = zone.StartProgressMeters is { } p ? $"around {p:0} m" : "at an unknown track location";
        var peak = zone.PeakBrakeInput is { } pb ? $"{pb * 100:0}%" : "unknown";
        var min = zone.MinimumSpeedKph is { } ms ? $"{ms:0} km/h" : "unknown";
        var duration = zone.DurationSeconds is { } d ? $"{d:0.00} s" : "unknown";

        return new TelemetryEvent
        {
            Id = $"ev-{++seq:D4}",
            Category = EventCategory.Braking,
            Severity = EventSeverity.Info,
            LapNumber = zone.LapNumber,
            ProgressMeters = zone.StartProgressMeters,
            Observation = $"Braking detected {location}: peak {peak}, minimum speed {min}, duration {duration}.",
            Confidence = EventConfidence.Verified,
            Evidence = new[]
            {
                SampleRangeEvidence(zone.LapNumber, zone.FirstSampleId, zone.LastSampleId,
                    zone.StartProgressMeters, zone.EndProgressMeters, "braking zone samples")
            },
            Limitations = zone.MinimumSpeedKph is null
                ? new[] { "The speed channel was unavailable, so minimum speed could not be measured." }
                : Array.Empty<string>()
        };
    }

    private static TelemetryEvent BuildCornerEvent(CornerAnalysis corner, ref int seq)
    {
        var entry = corner.EntryProgressMeters is { } e ? $"{e:0} m" : "unknown";
        var apex = corner.ApexProgressMeters is { } a ? $"{a:0} m" : "unknown";
        var exit = corner.ExitProgressMeters is { } x ? $"{x:0} m" : "unknown";
        var min = corner.MinimumSpeedKph is { } ms ? $"{ms:0} km/h" : "unknown";
        var pickup = corner.ApexToFullThrottleSeconds is { } t
            ? $"{t:0.00} s after the apex"
            : "not observed";

        return new TelemetryEvent
        {
            Id = $"ev-{++seq:D4}",
            Category = EventCategory.MidCorner,
            Severity = EventSeverity.Info,
            LapNumber = corner.LapNumber,
            ProgressMeters = corner.ApexProgressMeters,
            Observation = $"Corner from {entry} to {exit} (apex near {apex}): minimum speed {min}, full throttle {pickup}.",
            Confidence = EventConfidence.Verified,
            Evidence = new[]
            {
                SampleRangeEvidence(corner.LapNumber, corner.FirstSampleId, corner.LastSampleId,
                    corner.EntryProgressMeters, corner.ExitProgressMeters, "corner samples")
            },
            Limitations = new[]
            {
                "Steering magnitude is a proxy for corner geometry, not a measured radius.",
                "Understeer or oversteer is not concluded from steering input alone."
            }
        };
    }

    private static void AddSessionEvents(SessionLapAnalysis session, List<TelemetryEvent> events, ref int seq)
    {
        AddTyreChannelEvent(session, events, ref seq);
        AddConsistencyEvent(session.Laps, events, ref seq);
        AddWarningEvents(session.Warnings, events, ref seq);
    }

    private static void AddTyreChannelEvent(SessionLapAnalysis session, List<TelemetryEvent> events, ref int seq)
    {
        if (session.Laps.Count == 0)
        {
            return;
        }

        var any = session.Laps
            .SelectMany(l => l.Samples)
            .Any(s => (s.Source.WheelPressure?.Length ?? 0) > 0 || (s.Source.TyreCoreTempC?.Length ?? 0) > 0);

        if (any)
        {
            return;
        }

        events.Add(new TelemetryEvent
        {
            Id = $"ev-{++seq:D4}",
            Category = EventCategory.DataQuality,
            Severity = EventSeverity.Low,
            Observation = "No tyre pressure or core-temperature channels were present in this recording.",
            Confidence = EventConfidence.Verified,
            Evidence = Array.Empty<EvidenceReference>(),
            Limitations = new[] { "Tyre analysis is unavailable without these channels; no values are inferred." }
        });
    }

    private static void AddConsistencyEvent(IReadOnlyList<LapTimeline> laps, List<TelemetryEvent> events, ref int seq)
    {
        var timed = laps
            .Where(l => l.IsComplete && l.IsValid && l.LapTimeSeconds is not null)
            .Select(l => (l.LapNumber, Time: l.LapTimeSeconds!.Value))
            .ToList();

        if (timed.Count < 2)
        {
            return;
        }

        var best = timed.MinBy(t => t.Time);
        var worst = timed.MaxBy(t => t.Time);
        var spread = worst.Time - best.Time;

        events.Add(new TelemetryEvent
        {
            Id = $"ev-{++seq:D4}",
            Category = EventCategory.Consistency,
            Severity = spread >= 1.0 ? EventSeverity.Low : EventSeverity.Info,
            Observation =
                $"Across {timed.Count} valid laps the lap time ranged from {FormatLap(best.Time)} (lap {best.LapNumber}) " +
                $"to {FormatLap(worst.Time)} (lap {worst.LapNumber}); spread {spread:0.000} s.",
            Confidence = EventConfidence.Verified,
            Evidence = timed
                .Select(t => new EvidenceReference
                {
                    Kind = "lap",
                    Description = $"lap {t.LapNumber} lap time",
                    LapNumber = t.LapNumber
                })
                .ToArray()
        });
    }

    private static void AddWarningEvents(IReadOnlyList<string> warnings, List<TelemetryEvent> events, ref int seq)
    {
        foreach (var warning in warnings)
        {
            events.Add(new TelemetryEvent
            {
                Id = $"ev-{++seq:D4}",
                Category = EventCategory.DataQuality,
                Severity = EventSeverity.Low,
                Observation = warning,
                Confidence = EventConfidence.Verified,
                Evidence = Array.Empty<EvidenceReference>()
            });
        }
    }

    private static EvidenceReference SampleRangeEvidence(
        int lapNumber,
        long firstSampleId,
        long lastSampleId,
        double? startProgress,
        double? endProgress,
        string description) => new()
        {
            Kind = "sample-range",
            Description = description,
            LapNumber = lapNumber,
            FirstSampleId = firstSampleId,
            LastSampleId = lastSampleId,
            StartProgressMeters = startProgress,
            EndProgressMeters = endProgress
        };

    private static string FormatLap(double seconds)
    {
        var timespan = TimeSpan.FromSeconds(seconds);
        return $"{(int)timespan.TotalMinutes}:{timespan.Seconds:00}.{timespan.Milliseconds:000}";
    }
}
