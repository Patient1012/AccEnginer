using AccEngineer.Telemetry;

namespace AccEngineer.Storage.Tests;

public class SessionRecorderTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private JsonlSessionRecorder CreateRecorder(SessionMetadata? metadata = null) =>
        new(new SessionRecorderOptions { Directory = _temp.Path }, metadata ?? TestSamples.Metadata());

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Recorder_CreatesFileInConfiguredDirectoryAndWritesHeaderImmediately()
    {
        using var recorder = CreateRecorder();

        Assert.True(File.Exists(recorder.FilePath));
        Assert.Equal(_temp.Path, Path.GetDirectoryName(Path.GetFullPath(recorder.FilePath)));

        var metadata = new SessionRepository().ReadMetadata(recorder.FilePath);
        Assert.Equal(TestSamples.Metadata().SessionId, metadata.SessionId);
        Assert.Equal(RecordingSchema.Version, metadata.SchemaVersion);
        Assert.Equal("monza", metadata.TrackName);
        Assert.Equal(SessionType.Practice, metadata.SessionType);
    }

    [Fact]
    public void Recorder_RoundTripsSamplesWithAllValuesPreserved()
    {
        var original = TestSamples.Create(1);
        original = original with
        {
            SourceTimestampUtc = TestSamples.T0.AddMilliseconds(25),
            WheelSlip = new[] { 1.5f, 2.5f, 3.5f, 4.5f }
        };

        string path;
        using (var recorder = CreateRecorder())
        {
            recorder.Record(original);
            path = recorder.FilePath;
        }

        var recording = new SessionRepository().Load(path);

        Assert.Single(recording.Samples);
        var loaded = recording.Samples[0];

        Assert.Equal(original.SampleId, loaded.SampleId);
        Assert.Equal(original.ReceivedAtUtc, loaded.ReceivedAtUtc);
        Assert.Equal(original.SourceTimestampUtc, loaded.SourceTimestampUtc);
        Assert.Equal(original.ReceiptTick, loaded.ReceiptTick);
        Assert.Equal(original.SessionId, loaded.SessionId);
        Assert.Equal(original.LapNumber, loaded.LapNumber);
        Assert.Equal(original.IsLapValid, loaded.IsLapValid);
        Assert.Equal(original.CurrentLapTimeMs, loaded.CurrentLapTimeMs);
        Assert.Equal(original.LastLapTimeMs, loaded.LastLapTimeMs);
        Assert.Equal(original.BestLapTimeMs, loaded.BestLapTimeMs);
        Assert.Equal(original.CurrentSectorIndex, loaded.CurrentSectorIndex);
        Assert.Equal(original.SectorCount, loaded.SectorCount);
        Assert.Equal(original.TrackProgressMeters, loaded.TrackProgressMeters);
        Assert.Equal(original.SpeedMetersPerSecond, loaded.SpeedMetersPerSecond);
        Assert.Equal(original.SpeedKph, loaded.SpeedKph);
        Assert.Equal(original.SteeringInput, loaded.SteeringInput);
        Assert.Equal(original.ThrottleInput, loaded.ThrottleInput);
        Assert.Equal(original.BrakeInput, loaded.BrakeInput);
        Assert.Equal(original.Gear, loaded.Gear);
        Assert.Equal(original.EngineRpm, loaded.EngineRpm);
        Assert.Equal(original.LateralAccelerationG, loaded.LateralAccelerationG);
        Assert.Equal(original.LongitudinalAccelerationG, loaded.LongitudinalAccelerationG);
        Assert.Equal(original.AngularVelocityYawRadPerSec, loaded.AngularVelocityYawRadPerSec);
        Assert.Equal(original.PositionXMeters, loaded.PositionXMeters);
        Assert.Equal(original.PositionYMeters, loaded.PositionYMeters);
        Assert.Equal(original.PositionZMeters, loaded.PositionZMeters);
        Assert.Equal(original.WheelSlip, loaded.WheelSlip);
        Assert.Equal(original.WheelPressure, loaded.WheelPressure);
        Assert.Equal(original.TyreCoreTempC, loaded.TyreCoreTempC);
        Assert.Equal(original.FuelLevel, loaded.FuelLevel);
        Assert.Equal(original.PitLimiterOn, loaded.PitLimiterOn);
        Assert.Equal(original.IsInPitLane, loaded.IsInPitLane);
        Assert.Equal(original.TrackName, loaded.TrackName);
        Assert.Equal(original.CarName, loaded.CarName);
        Assert.Equal(original.TyreCompound, loaded.TyreCompound);
        Assert.Equal(original.SessionType, loaded.SessionType);
        Assert.Equal(original.SessionState, loaded.SessionState);
        Assert.Equal(original.Quality, loaded.Quality);
    }

    [Fact]
    public void Recorder_MissingValuesStayNull_NotZero()
    {
        string path;
        using (var recorder = CreateRecorder())
        {
            recorder.Record(TestSamples.CreateEmpty(1));
            path = recorder.FilePath;
        }

        var loaded = new SessionRepository().Load(path).Samples[0];

        Assert.Null(loaded.SpeedKph);
        Assert.Null(loaded.ThrottleInput);
        Assert.Null(loaded.Gear);
        Assert.Null(loaded.EngineRpm);
        Assert.Null(loaded.WheelSlip);
        Assert.Null(loaded.FuelLevel);
        Assert.Null(loaded.TrackName);
        Assert.Null(loaded.TyreCompound);
    }

    [Fact]
    public void Recorder_PreservesSampleOrderAndTimestamps()
    {
        const int count = 250;

        string path;
        using (var recorder = CreateRecorder())
        {
            for (var i = 1; i <= count; i++) recorder.Record(TestSamples.Create(i));
            path = recorder.FilePath;
        }

        var recording = new SessionRepository().Load(path);

        Assert.Equal(count, recording.Samples.Count);
        Assert.Equal(count, recording.End!.SampleCount);
        Assert.True(recording.End.Completed);
        Assert.Equal(0, recording.End.DroppedSampleCount);

        for (var i = 0; i < count; i++)
        {
            Assert.Equal(i + 1, recording.Samples[i].SampleId);
            if (i > 0)
            {
                Assert.True(recording.Samples[i].ReceivedAtUtc >= recording.Samples[i - 1].ReceivedAtUtc);
            }
        }
    }

    [Fact]
    public void Recorder_WritesEndRecordOnComplete()
    {
        using var recorder = CreateRecorder();
        recorder.Record(TestSamples.Create(1));
        recorder.Record(TestSamples.Create(2));
        recorder.Complete();

        var recording = new SessionRepository().Load(recorder.FilePath);

        Assert.NotNull(recording.End);
        Assert.Equal(2, recording.End!.SampleCount);
        Assert.True(recording.End.Completed);
        Assert.Equal(0, recording.End.DroppedSampleCount);
        Assert.Equal(0, recording.SkippedRecordCount);
    }

    [Fact]
    public void Recorder_CompleteIsIdempotent()
    {
        using var recorder = CreateRecorder();
        recorder.Record(TestSamples.Create(1));

        recorder.Complete();
        recorder.Complete();

        Assert.Single(new SessionRepository().Load(recorder.FilePath).Samples);
    }

    [Fact]
    public void Recorder_RecordAfterComplete_Throws()
    {
        using var recorder = CreateRecorder();
        recorder.Complete();

        Assert.Throws<InvalidOperationException>(() => recorder.Record(TestSamples.Create(1)));
    }

    [Fact]
    public void Recorder_RecordAfterDispose_ThrowsObjectDisposed()
    {
        var recorder = CreateRecorder();
        recorder.Dispose();

        Assert.Throws<ObjectDisposedException>(() => recorder.Record(TestSamples.Create(1)));
    }

    [Fact]
    public void Recorder_TwoRecordersForSameSession_GetDistinctFiles()
    {
        string firstPath, secondPath;
        using (var first = CreateRecorder()) firstPath = first.FilePath;
        using (var second = CreateRecorder()) secondPath = second.FilePath;

        Assert.NotEqual(firstPath, secondPath);
        Assert.True(File.Exists(firstPath));
        Assert.True(File.Exists(secondPath));
    }

    [Fact]
    public void Recorder_CountsAreConservative_UnderLoad()
    {
        const int count = 5_000;
        var recorder = CreateRecorder();

        for (var i = 1; i <= count; i++) recorder.Record(TestSamples.Create(i));
        recorder.Complete();

        Assert.Equal(count, recorder.WrittenSampleCount + recorder.DroppedSampleCount);
        Assert.Equal(0, recorder.DroppedSampleCount);
        Assert.Equal(count, new SessionRepository().Load(recorder.FilePath).Samples.Count);
    }

    [Fact]
    public void Recorder_EmptySession_StillLoadsMetadata()
    {
        using var recorder = CreateRecorder();
        var path = recorder.FilePath;

        var recording = new SessionRepository().Load(path);

        Assert.Empty(recording.Samples);
        Assert.Equal(TestSamples.Metadata().SessionId, recording.Metadata.SessionId);
    }
}
