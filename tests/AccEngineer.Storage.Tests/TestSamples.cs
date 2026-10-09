using AccEngineer.Telemetry;

namespace AccEngineer.Storage.Tests;

/// <summary>Creates and cleans up an isolated recording directory per test.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "acc-engineer-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; the OS temp folder will reclaim it.
        }
    }
}

internal static class TestSamples
{
    public static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static TelemetrySample Create(int id)
    {
        var receivedAt = T0.AddMilliseconds(id * 50);
        return new TelemetrySample
        {
            SampleId = id,
            ReceivedAtUtc = receivedAt,
            SourceTimestampUtc = null,
            ReceiptTick = id * 1000L,
            SessionId = "sm-20260101120000-3",
            LapNumber = id / 100 + 1,
            IsLapValid = id % 7 != 0,
            TrackProgressMeters = 123.5f + id,
            SpeedMetersPerSecond = 50.0f + id,
            SpeedKph = 180.0f + id,
            SteeringInput = 0.125f,
            ThrottleInput = 1.0f,
            BrakeInput = 0.0f,
            Gear = 4,
            EngineRpm = 7000 + id,
            LateralAccelerationG = 0.5f,
            LongitudinalAccelerationG = -0.25f,
            AngularVelocityYawRadPerSec = 0.01f,
            PositionXMeters = 100f,
            PositionYMeters = 200f,
            PositionZMeters = 300f,
            WheelSlip = new[] { 0.1f, 0.2f, 0.3f, 0.4f },
            WheelPressure = new[] { 26.5f, 26.6f, 26.4f, 26.7f },
            TyreCoreTempC = new[] { 80f, 81f, 82f, 83f },
            FuelLevel = 42.5f,
            PitLimiterOn = false,
            IsInPitLane = false,
            TrackName = "monza",
            CarName = "porsche_991_gt3_r",
            TyreCompound = "Dry",
            SessionType = SessionType.Practice,
            SessionState = SessionState.Live,
            Quality = id % 5 == 0 ? DataQualityFlags.Stale | DataQualityFlags.PitStop : DataQualityFlags.None
        };
    }

    public static TelemetrySample CreateEmpty(int id) => new()
    {
        SampleId = id,
        ReceivedAtUtc = T0.AddMilliseconds(id * 50)
    };

    public static SessionMetadata Metadata(string sessionId = "sm-20260101120000-3") => new()
    {
        SchemaVersion = RecordingSchema.Version,
        SessionId = sessionId,
        StartedAtUtc = T0,
        TrackName = "monza",
        CarName = "porsche_991_gt3_r",
        SessionType = SessionType.Practice
    };
}
