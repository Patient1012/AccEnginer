using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Tests;

internal static class Synth
{
    public static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static TelemetrySample Sample(
        long id,
        int lap,
        double progressMeters,
        double timeOffsetSeconds,
        string sessionId = "sm-test",
        int? sectorIndex = null,
        bool valid = true,
        int? lastLapTimeMs = null,
        int? sectorCount = null,
        DataQualityFlags quality = DataQualityFlags.None,
        float? speedKph = null,
        float? throttle = null,
        float? brake = null,
        float? steering = null,
        float[]? pressure = null,
        float[]? coreTempC = null,
        int? gear = null,
        float? speedMs = null,
        float? longitudinalG = null)
        => new()
        {
            SampleId = id,
            ReceivedAtUtc = T0.AddSeconds(timeOffsetSeconds),
            ReceiptTick = id,
            SessionId = sessionId,
            LapNumber = lap,
            IsLapValid = valid,
            TrackProgressMeters = (float)progressMeters,
            CurrentSectorIndex = sectorIndex,
            LastLapTimeMs = lastLapTimeMs,
            SectorCount = sectorCount,
            Quality = quality,
            SpeedKph = speedKph,
            SpeedMetersPerSecond = speedMs,
            ThrottleInput = throttle,
            BrakeInput = brake,
            SteeringInput = steering,
            Gear = gear,
            LongitudinalAccelerationG = longitudinalG,
            WheelPressure = pressure,
            TyreCoreTempC = coreTempC
        };

    public static LapTimeline Timeline(int lapNumber, params (double Progress, double Time)[] points)
    {
        var samples = points
            .Select((point, index) => new LapSample
            {
                Source = Sample(index + 1, lapNumber, point.Progress, point.Time),
                LapTimeSeconds = point.Time,
                ProgressMeters = point.Progress
            })
            .ToArray();

        double? lapTime = points.Length > 0 ? points[^1].Time : null;

        return new LapTimeline
        {
            SessionId = "sm-test",
            LapNumber = lapNumber,
            IsComplete = true,
            IsValid = true,
            StartedAtUtc = T0,
            EndedAtUtc = T0.AddSeconds(lapTime ?? 0),
            LapTimeSeconds = lapTime,
            Samples = samples,
            Sectors = Array.Empty<SectorTimeline>()
        };
    }

    /// <summary>
    /// Builds a complete, valid lap from fully-specified samples. Lap-relative time is
    /// derived from each sample's offset from <see cref="T0"/>.
    /// </summary>
    public static LapTimeline Lap(int lapNumber, params TelemetrySample[] samples)
    {
        var lapSamples = samples
            .Select(sample => new LapSample
            {
                Source = sample,
                LapTimeSeconds = (sample.ReceivedAtUtc - T0).TotalSeconds,
                ProgressMeters = sample.TrackProgressMeters
            })
            .ToArray();

        double? lapTime = lapSamples.Length > 0 ? lapSamples[^1].LapTimeSeconds : null;

        return new LapTimeline
        {
            SessionId = samples.Length > 0 ? samples[0].SessionId : "sm-test",
            LapNumber = lapNumber,
            IsComplete = true,
            IsValid = true,
            StartedAtUtc = samples.Length > 0 ? samples[0].ReceivedAtUtc : T0,
            EndedAtUtc = samples.Length > 0 ? samples[^1].ReceivedAtUtc : T0,
            LapTimeSeconds = lapTime,
            Samples = lapSamples,
            Sectors = Array.Empty<SectorTimeline>()
        };
    }
}
