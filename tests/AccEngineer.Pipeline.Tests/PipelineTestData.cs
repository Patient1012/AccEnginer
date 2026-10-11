using AccEngineer.AI;
using AccEngineer.Analysis;
using AccEngineer.Reporting;
using AccEngineer.Telemetry;

namespace AccEngineer.Pipeline.Tests;

internal static class PipelineTestData
{
    public static ReportSession Session() => new()
    {
        SessionId = "s1",
        Track = "Monza",
        Car = "GT3",
        SessionType = SessionType.Practice
    };

    public static TelemetrySample Sample(
        long id,
        double progress,
        double speed = 100,
        double throttle = 0.5,
        double brake = 0,
        double steering = 0) => new()
    {
        SampleId = id,
        SessionId = "s1",
        LapNumber = 1,
        TrackProgressMeters = (float)progress,
        SpeedKph = (float)speed,
        ThrottleInput = (float)throttle,
        BrakeInput = (float)brake,
        SteeringInput = (float)steering,
        ReceivedAtUtc = DateTimeOffset.UnixEpoch.AddMilliseconds(id)
    };

    public static TelemetryEvent Event(
        string id,
        EventCategory category = EventCategory.Braking,
        int lap = 1,
        double progress = 500,
        EventSeverity severity = EventSeverity.High,
        string observation = "Late braking at turn 1.",
        long first = 10,
        long last = 20) => new()
    {
        Id = id,
        Category = category,
        Severity = severity,
        Observation = observation,
        Interpretation = "Brake release may be too abrupt.",
        LapNumber = lap,
        ProgressMeters = progress,
        Confidence = EventConfidence.Verified,
        SuggestedTest = "Move the braking point 10 m earlier and compare entry speed.",
        Evidence = new[]
        {
            new EvidenceReference
            {
                Kind = "samples",
                Description = "Braking window",
                LapNumber = lap,
                FirstSampleId = first,
                LastSampleId = last,
                StartProgressMeters = progress - 10,
                EndProgressMeters = progress + 10
            }
        }
    };

    public static LapTimeline Lap(int number, bool valid, double? time) => new()
    {
        SessionId = "s1",
        LapNumber = number,
        IsComplete = valid,
        IsValid = valid,
        StartedAtUtc = DateTimeOffset.UnixEpoch,
        EndedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(time ?? 90),
        LapTimeSeconds = time,
        Samples = Array.Empty<LapSample>(),
        Sectors = Array.Empty<SectorTimeline>()
    };

    public static void Prefill(RollingEvidenceBuffer buffer, long from, long to)
    {
        var samples = new List<TelemetrySample>();
        for (var id = from; id <= to; id++)
        {
            samples.Add(Sample(id, id * 10.0));
        }

        buffer.AddRange(samples);
    }

    internal sealed class FakeProvider : IAnalysisProvider
    {
        public string Name => "fake";

        public bool IsConfigured => true;

        public AnalysisRequest? LastRequest { get; private set; }

        public AnalysisResult Result { get; set; } = new()
        {
            Outcome = AnalysisOutcome.Success,
            FindingType = "braking",
            Observation = "Brake pressure peaks early.",
            Interpretation = "Possible premature trail-off.",
            Confidence = 0.7,
            SuggestedTest = "Smooth the release and compare minimum speed.",
            Provider = "fake"
        };

        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}
