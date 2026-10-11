using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Tests;

public class DrivingAnalyzerTests
{
    private static LapTimeline Lap(int lapNumber, double lapTimeSeconds, IReadOnlyList<TelemetrySample> samples)
    {
        var lapSamples = samples
            .Select((sample, index) => new LapSample
            {
                Source = sample,
                LapTimeSeconds = samples.Count > 1
                    ? (double)index / (samples.Count - 1) * lapTimeSeconds
                    : 0.0,
                ProgressMeters = sample.TrackProgressMeters
            })
            .ToArray();

        return new LapTimeline
        {
            SessionId = "sm-test",
            LapNumber = lapNumber,
            IsComplete = true,
            IsValid = true,
            StartedAtUtc = Synth.T0,
            EndedAtUtc = Synth.T0.AddSeconds(lapTimeSeconds),
            LapTimeSeconds = lapTimeSeconds,
            Samples = lapSamples,
            Sectors = Array.Empty<SectorTimeline>()
        };
    }

    private static SessionLapAnalysis Session(params LapTimeline[] laps) => new()
    {
        SessionId = "sm-test",
        Laps = laps,
        Warnings = Array.Empty<string>()
    };

    [Fact]
    public void Analyze_BrakingZone_ReportedWithPeakAndEvidence()
    {
        var samples = new List<TelemetrySample>();
        long id = 0;

        void Add(double progress, float? brake, float speed)
            => samples.Add(Synth.Sample(++id, 1, progress, id * 0.1, speedKph: speed, brake: brake));

        Add(40, 0f, 220);
        Add(50, 0f, 220);
        Add(60, 0f, 215);
        var firstBrakingId = id + 1;
        Add(100, 0.8f, 200);
        Add(110, 0.7f, 180);
        Add(120, 0.6f, 160);
        Add(130, 0.5f, 150);
        var lastBrakingId = id;
        Add(180, 0f, 160);
        Add(190, 0f, 170);

        var result = new DrivingAnalyzer().Analyze(Session(Lap(1, 90, samples)));
        var lap = result.Laps[0];

        var zone = Assert.Single(lap.BrakingZones);
        Assert.Equal(100.0, zone.StartProgressMeters!.Value, 6);
        Assert.Equal(130.0, zone.EndProgressMeters!.Value, 6);
        Assert.Equal(4, zone.SampleCount);
        Assert.Equal(0.8, zone.PeakBrakeInput!.Value, 6);
        Assert.Equal(200.0, zone.EntrySpeedKph!.Value, 6);
        Assert.Equal(150.0, zone.MinimumSpeedKph!.Value, 6);
        Assert.Equal(firstBrakingId, zone.FirstSampleId);
        Assert.Equal(lastBrakingId, zone.LastSampleId);

        var brakingEvent = Assert.Single(result.Events, e => e.Category == EventCategory.Braking);
        Assert.Equal(EventConfidence.Verified, brakingEvent.Confidence);
        var evidence = Assert.Single(brakingEvent.Evidence);
        Assert.Equal("sample-range", evidence.Kind);
        Assert.Equal(firstBrakingId, evidence.FirstSampleId);
        Assert.Equal(lastBrakingId, evidence.LastSampleId);
    }

    [Fact]
    public void Analyze_ShortBrakingSpike_Ignored()
    {
        var samples = new List<TelemetrySample>();
        long id = 0;
        samples.Add(Synth.Sample(++id, 1, 0, 0.1, brake: 0f));
        samples.Add(Synth.Sample(++id, 1, 10, 0.2, brake: 0.9f));
        samples.Add(Synth.Sample(++id, 1, 20, 0.3, brake: 0f));
        samples.Add(Synth.Sample(++id, 1, 30, 0.4, brake: 0f));

        var result = new DrivingAnalyzer().Analyze(Session(Lap(1, 60, samples)));

        Assert.Empty(result.Laps[0].BrakingZones);
    }

    [Fact]
    public void Analyze_Corner_ApexAndThrottlePickupAreMeasured()
    {
        var samples = new List<TelemetrySample>();
        long id = 0;
        double[] steering = { 0, 0, 0.2, 0.4, 0.5, 0.3, 0.1, 0, 0 };
        float[] speeds = { 180, 160, 140, 110, 90, 120, 150, 170, 180 };
        float[] throttle = { 1, 1, 0.9f, 0.4f, 0.2f, 0.5f, 1.0f, 1, 1 };

        for (var i = 0; i < steering.Length; i++)
        {
            samples.Add(Synth.Sample(++id, 1, i * 50, id * 0.1,
                speedKph: speeds[i],
                steering: (float)steering[i],
                throttle: throttle[i]));
        }

        var result = new DrivingAnalyzer().Analyze(Session(Lap(1, 90, samples)));
        var corner = Assert.Single(result.Laps[0].Corners);

        Assert.Equal(100.0, corner.EntryProgressMeters!.Value, 6);
        Assert.Equal(250.0, corner.ExitProgressMeters!.Value, 6);
        Assert.Equal(200.0, corner.ApexProgressMeters!.Value, 6);
        Assert.Equal(90.0, corner.MinimumSpeedKph!.Value, 6);
        Assert.Equal(0.5, corner.PeakSteeringInput!.Value, 6);
        Assert.Equal(22.5, corner.ApexToFullThrottleSeconds!.Value, 6);

        var cornerEvent = Assert.Single(result.Events, e => e.Category == EventCategory.MidCorner);
        Assert.Contains(cornerEvent.Limitations, l => l.Contains("Understeer", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_TyreSummaries_ComputedPerWheel()
    {
        var samples = new List<TelemetrySample>();
        long id = 0;
        float[] pressuresFl = { 27, 28, 29 };
        float[] tempsFl = { 80, 82, 84 };

        for (var i = 0; i < 3; i++)
        {
            samples.Add(Synth.Sample(++id, 1, i * 10, id * 0.1,
                pressure: new[] { pressuresFl[i], 28, 29, 30 },
                coreTempC: new[] { tempsFl[i], 81, 82, 83 }));
        }

        var result = new DrivingAnalyzer().Analyze(Session(Lap(1, 30, samples)));
        var fl = result.Laps[0].Tyres.Single(t => t.Wheel == "FL");

        Assert.Equal(3, fl.PressureSampleCount);
        Assert.Equal(27.0, fl.PressureMin!.Value, 6);
        Assert.Equal(29.0, fl.PressureMax!.Value, 6);
        Assert.Equal(28.0, fl.PressureAverage!.Value, 6);
        Assert.Equal(3, fl.CoreTempSampleCount);
        Assert.Equal(84.0, fl.CoreTempMaxC!.Value, 6);
    }

    [Fact]
    public void Analyze_NoTyreChannels_EmitsDataQualityEvent()
    {
        var samples = new List<TelemetrySample>();
        long id = 0;
        for (var i = 0; i < 3; i++)
        {
            samples.Add(Synth.Sample(++id, 1, i * 10, id * 0.1, speedKph: 100));
        }

        var result = new DrivingAnalyzer().Analyze(Session(Lap(1, 30, samples)));

        var tyreEvent = Assert.Single(result.Events, e => e.Category == EventCategory.DataQuality
            && e.Observation.Contains("tyre", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(EventSeverity.Low, tyreEvent.Severity);
    }

    [Fact]
    public void Analyze_MultipleValidLaps_ReportsConsistencySpread()
    {
        var lapA = Lap(1, 90.0, new[] { Synth.Sample(1, 1, 0, 0.1) });
        var lapB = Lap(2, 91.0, new[] { Synth.Sample(2, 2, 0, 0.1) });

        var result = new DrivingAnalyzer().Analyze(Session(lapA, lapB));

        var consistency = Assert.Single(result.Events, e => e.Category == EventCategory.Consistency);
        Assert.Contains("1.000 s", consistency.Observation);
        Assert.Equal(2, consistency.Evidence.Count);
    }

    [Fact]
    public void Analyze_InvalidLaps_ExcludedFromConsistency()
    {
        var valid = Lap(1, 90.0, new[] { Synth.Sample(1, 1, 0, 0.1) });
        var invalid = Lap(2, 88.0, new[] { Synth.Sample(2, 2, 0, 0.1) }) with { IsValid = false };

        var result = new DrivingAnalyzer().Analyze(Session(valid, invalid));

        Assert.DoesNotContain(result.Events, e => e.Category == EventCategory.Consistency);
    }
}
