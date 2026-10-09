using AccEngineer.Analysis;
using AccEngineer.Telemetry;

namespace AccEngineer.Reporting.Tests;

internal static class Synth
{
    public static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static LapTimeline Lap(int lapNumber, double lapTimeSeconds, (double Progress, double Time)[] points, bool valid = true)
    {
        var samples = points
            .Select((point, index) => new LapSample
            {
                Source = new TelemetrySample
                {
                    SampleId = index + 1,
                    ReceiptTick = index + 1,
                    ReceivedAtUtc = T0.AddSeconds(point.Time),
                    SessionId = "sm-1",
                    LapNumber = lapNumber,
                    TrackProgressMeters = (float)point.Progress
                },
                LapTimeSeconds = point.Time,
                ProgressMeters = point.Progress
            })
            .ToArray();

        return new LapTimeline
        {
            SessionId = "sm-1",
            LapNumber = lapNumber,
            IsComplete = true,
            IsValid = valid,
            StartedAtUtc = T0,
            EndedAtUtc = T0.AddSeconds(lapTimeSeconds),
            LapTimeSeconds = lapTimeSeconds,
            Samples = samples,
            Sectors = Array.Empty<SectorTimeline>()
        };
    }

    public static SessionLapAnalysis Analysis(params LapTimeline[] laps) => new()
    {
        SessionId = "sm-1",
        TrackName = "monza",
        CarName = "porsche_991_gt3_r",
        SessionType = SessionType.Practice,
        Laps = laps,
        Warnings = Array.Empty<string>()
    };
}
