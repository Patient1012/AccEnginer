using AccEngineer.AI;
using AccEngineer.Analysis;
using AccEngineer.Telemetry;

namespace AccEngineer.AI.Tests;

public class DrivingAnalysisRequestBuilderTests
{
    private static TelemetryEvent MakeFinding(params EvidenceReference[] evidence) => new()
    {
        Id = "event-001",
        Category = EventCategory.Braking,
        Severity = EventSeverity.High,
        Observation = "Brake released earlier than the reference.",
        Interpretation = "May be trail-braking.",
        Confidence = EventConfidence.Verified,
        LapNumber = 2,
        ProgressMeters = 1450,
        Evidence = evidence.Length > 0 ? evidence : new[]
        {
            new EvidenceReference
            {
                Kind = "sample-range",
                Description = "braking zone",
                LapNumber = 2,
                FirstSampleId = 100,
                LastSampleId = 102
            }
        }
    };

    private static LapTimeline MakeLap()
    {
        TelemetrySample Sample(long id, float speed, float throttle, float brake, float steering) => new()
        {
            SampleId = id,
            SessionId = "s",
            LapNumber = 2,
            SpeedKph = speed,
            ThrottleInput = throttle,
            BrakeInput = brake,
            SteeringInput = steering
        };

        return new LapTimeline
        {
            SessionId = "s",
            LapNumber = 2,
            IsComplete = true,
            IsValid = true,
            StartedAtUtc = DateTimeOffset.UtcNow,
            EndedAtUtc = DateTimeOffset.UtcNow,
            LapTimeSeconds = 131.42,
            Samples = new[]
            {
                new LapSample { Source = Sample(99, 150, 0.0f, 0.0f, 0.0f), LapTimeSeconds = 10.0, ProgressMeters = 1400 },
                new LapSample { Source = Sample(100, 145, 0.0f, 0.9f, 0.1f), LapTimeSeconds = 10.2, ProgressMeters = 1440 },
                new LapSample { Source = Sample(101, 140, 0.0f, 0.8f, 0.2f), LapTimeSeconds = 10.4, ProgressMeters = 1450 },
                new LapSample { Source = Sample(102, 135, 0.1f, 0.6f, 0.3f), LapTimeSeconds = 10.6, ProgressMeters = 1460 },
                new LapSample { Source = Sample(103, 130, 0.5f, 0.2f, 0.4f), LapTimeSeconds = 10.8, ProgressMeters = 1470 }
            },
            Sectors = Array.Empty<SectorTimeline>()
        };
    }

    private static SessionLapAnalysis MakeSession(LapTimeline lap) => new()
    {
        SessionId = "s",
        TrackName = "Monza",
        CarName = "AMR V8",
        SessionType = SessionType.Practice,
        Laps = new[] { lap },
        Warnings = new[] { "steering channel intermittently missing" }
    };

    [Fact]
    public void Build_IncludesOnlyEvidenceSampleRange()
    {
        var lap = MakeLap();
        var session = MakeSession(lap);
        var finding = MakeFinding();

        var request = DrivingAnalysisRequestBuilder.Build(finding, session, lap);

        Assert.Equal("Monza", request.Context.Track);
        Assert.Equal("AMR V8", request.Context.Car);
        Assert.Equal(2, request.Context.LapNumber);
        Assert.Equal(131.42, request.Context.LapTimeSeconds);
        Assert.All(request.Samples, s => Assert.InRange(s.SampleId, 100, 102));
        Assert.Equal(3, request.Samples.Count);
        Assert.Contains("steering channel intermittently missing", request.DataQualityWarnings);
    }

    [Fact]
    public void Build_NoEvidenceFallsBackToProgressWindow()
    {
        var lap = MakeLap();
        var session = MakeSession(lap);
        var finding = MakeFinding() with { Evidence = Array.Empty<EvidenceReference>(), ProgressMeters = 1450 };

        var request = DrivingAnalysisRequestBuilder.Build(finding, session, lap);

        Assert.NotEmpty(request.Samples);
        Assert.All(request.Samples, s => Assert.NotNull(s.ProgressMeters));
        Assert.All(request.Samples, s => Assert.True(Math.Abs(s.ProgressMeters!.Value - 1450) <= 50));
    }

    [Fact]
    public void Build_NoLapYieldsNoSamples()
    {
        var session = MakeSession(MakeLap());
        var finding = MakeFinding();

        var request = DrivingAnalysisRequestBuilder.Build(finding, session);

        Assert.Empty(request.Samples);
    }
}
