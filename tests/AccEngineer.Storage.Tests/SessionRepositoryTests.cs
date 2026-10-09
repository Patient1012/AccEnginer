namespace AccEngineer.Storage.Tests;

public class SessionRepositoryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string WriteRecording(params string[] lines)
    {
        var path = Path.Combine(_temp.Path, $"manual-{Guid.NewGuid():N}.jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static readonly string Header =
        "{\"type\":\"session\",\"metadata\":{\"schemaVersion\":1,\"sessionId\":\"sm-test\",\"startedAtUtc\":\"2026-01-01T12:00:00+00:00\",\"trackName\":\"monza\",\"carName\":\"car_x\",\"sessionType\":\"Practice\"}}";

    [Fact]
    public void Load_RejectsMissingFile()
    {
        var repository = new SessionRepository();

        Assert.Throws<FileNotFoundException>(() => repository.Load(Path.Combine(_temp.Path, "nope.jsonl")));
    }

    [Fact]
    public void Load_RejectsFileWithoutHeader()
    {
        var path = WriteRecording("{\"type\":\"sample\",\"sample\":{\"sampleId\":1,\"receivedAtUtc\":\"2026-01-01T12:00:00+00:00\"}}");

        Assert.Throws<InvalidDataException>(() => new SessionRepository().Load(path));
    }

    [Fact]
    public void Load_ToleratesTruncatedFinalLine_AndKeepsEarlierSamples()
    {
        var path = WriteRecording(
            Header,
            "{\"type\":\"sample\",\"sample\":{\"sampleId\":1,\"receivedAtUtc\":\"2026-01-01T12:00:00+00:00\",\"lapNumber\":1,\"isLapValid\":true}}",
            "{\"type\":\"sample\",\"sample\":{\"sampleId\":2,\"receivedAtUtc\":\"2026-01-01T12:00:00.050+00:00\"" // truncated

        );

        var recording = new SessionRepository().Load(path);

        Assert.Single(recording.Samples);
        Assert.Equal(1, recording.Samples[0].SampleId);
        Assert.Equal(1, recording.SkippedRecordCount);
        Assert.Null(recording.End);
    }

    [Fact]
    public void Load_SkipsUnknownRecordTypesWithoutFailing()
    {
        var path = WriteRecording(
            Header,
            "{\"type\":\"whatIsThis\",\"value\":1}",
            "{\"type\":\"sample\",\"sample\":{\"sampleId\":1,\"receivedAtUtc\":\"2026-01-01T12:00:00+00:00\",\"lapNumber\":1,\"isLapValid\":true}}"
        );

        var recording = new SessionRepository().Load(path);

        Assert.Single(recording.Samples);
        Assert.Equal(1, recording.SkippedRecordCount);
    }

    [Fact]
    public void Load_ReadsEndRecord()
    {
        var path = WriteRecording(
            Header,
            "{\"type\":\"end\",\"end\":{\"endedAtUtc\":\"2026-01-01T12:05:00+00:00\",\"sampleCount\":0,\"droppedSampleCount\":3,\"completed\":true}}"
        );

        var recording = new SessionRepository().Load(path);

        Assert.NotNull(recording.End);
        Assert.Equal(3, recording.End!.DroppedSampleCount);
        Assert.True(recording.End.Completed);
    }

    [Fact]
    public void ReadMetadata_ReadsOnlyHeader()
    {
        var path = WriteRecording(
            Header,
            "garbage that is not json",
            "{\"type\":\"sample\",\"sample\":{\"sampleId\":1,\"receivedAtUtc\":\"2026-01-01T12:00:00+00:00\"}}"
        );

        var metadata = new SessionRepository().ReadMetadata(path);

        Assert.Equal("sm-test", metadata.SessionId);
        Assert.Equal("monza", metadata.TrackName);
    }

    [Fact]
    public void RoundTrip_RecorderToRepository_PreservesFlagsAndNullableArrays()
    {
        var path = Path.Combine(_temp.Path, $"rtr-{Guid.NewGuid():N}.jsonl");
        using (var recorder = new JsonlSessionRecorder(
                   new SessionRecorderOptions { Directory = _temp.Path },
                   TestSamples.Metadata()))
        {
            recorder.Complete();
            path = recorder.FilePath;
        }

        var recording = new SessionRepository().Load(path);

        Assert.Equal(RecordingSchema.Version, recording.Metadata.SchemaVersion);
        Assert.True(recording.End!.Completed);
    }
}
