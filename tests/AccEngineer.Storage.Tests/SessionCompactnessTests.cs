using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AccEngineer.Telemetry;

namespace AccEngineer.Storage.Tests;

/// <summary>
/// Milestone 1/3 measurements: the compact (schema v2) encoding with gzip must
/// stay dramatically smaller than the original verbatim v1 JSONL for the same
/// samples, and compression must be transparent to round-tripping.
/// </summary>
public class SessionCompactnessTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static string WriteVerbatimV1(IReadOnlyList<TelemetrySample> samples, string path)
    {
        using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
        writer.NewLine = "\n";
        foreach (var sample in samples)
        {
            writer.Write("{\"type\":\"sample\",\"sample\":");
            writer.Write(JsonSerializer.Serialize(sample, TelemetryJson.Options));
            writer.Write("}\n");
        }

        return path;
    }

    [Fact]
    public void V2Gzip_IsMuchSmallerThan_VerbatimV1_ForTheSameSamples()
    {
        const int count = 5_000;
        var samples = Enumerable.Range(1, count).Select(TestSamples.Create).ToArray();

        string gzipPath;
        using (var recorder = new JsonlSessionRecorder(
                   new SessionRecorderOptions { Directory = _temp.Path },
                   TestSamples.Metadata()))
        {
            foreach (var sample in samples) recorder.Record(sample);
            gzipPath = recorder.FilePath;
        }

        var v1Path = WriteVerbatimV1(samples, Path.Combine(_temp.Path, "verbatim-v1.jsonl"));

        var gzipBytes = new FileInfo(gzipPath).Length;
        var v1Bytes = new FileInfo(v1Path).Length;

        Assert.True(gzipBytes < v1Bytes, $"v2+gzip ({gzipBytes} B) must be smaller than verbatim v1 ({v1Bytes} B)");
        Assert.True(gzipBytes * 2L < v1Bytes, $"expected at least 2x reduction, got {v1Bytes / (double)gzipBytes:0.0}x");

        var loaded = new SessionRepository().Load(gzipPath);
        Assert.Equal(count, loaded.Samples.Count);
        Assert.Equal(0, loaded.SkippedRecordCount);
        Assert.Equal(samples[0].SampleId, loaded.Samples[0].SampleId);
        Assert.Equal(samples[0].ReceivedAtUtc, loaded.Samples[0].ReceivedAtUtc);
        Assert.Equal(samples[^1].SampleId, loaded.Samples[^1].SampleId);
        Assert.Equal(samples[^1].ReceivedAtUtc, loaded.Samples[^1].ReceivedAtUtc);
    }

    [Fact]
    public void DefaultRecorder_ProducedFile_IsGzip_WithEndRecord()
    {
        string path;
        using (var recorder = new JsonlSessionRecorder(
                   new SessionRecorderOptions { Directory = _temp.Path },
                   TestSamples.Metadata()))
        {
            recorder.Record(TestSamples.Create(1));
            path = recorder.FilePath;
        }

        var magic = new byte[2];
        using (var stream = System.IO.File.OpenRead(path))
        {
            stream.ReadExactly(magic, 0, 2);
        }

        Assert.Equal(0x1F, magic[0]);
        Assert.Equal(0x8B, magic[1]);

        var recording = new SessionRepository().Load(path);
        Assert.True(recording.End!.Completed);
    }

    [Fact]
    public void GzipAndPlain_Encodings_RoundTripIdentically()
    {
        var original = new[]
        {
            TestSamples.Create(1),
            TestSamples.CreateEmpty(2) with { SessionId = TestSamples.Metadata().SessionId },
            TestSamples.Create(3) with
            {
                SourceTimestampUtc = TestSamples.T0.AddMilliseconds(75),
                WheelSlip = new[] { 1.5f, 2.5f, 3.5f, 4.5f },
                Quality = DataQualityFlags.Stale | DataQualityFlags.GapDetected
            }
        };

        string gzipPath, plainPath;
        using (var recorder = new JsonlSessionRecorder(
                   new SessionRecorderOptions { Directory = _temp.Path },
                   TestSamples.Metadata()))
        {
            foreach (var sample in original) recorder.Record(sample);
            gzipPath = recorder.FilePath;
        }

        using (var recorder = new JsonlSessionRecorder(
                   new SessionRecorderOptions { Directory = _temp.Path, Compression = RecordingCompression.None },
                   TestSamples.Metadata()))
        {
            foreach (var sample in original) recorder.Record(sample);
            plainPath = recorder.FilePath;
        }

        var gzipLoaded = new SessionRepository().Load(gzipPath).Samples;
        var plainLoaded = new SessionRepository().Load(plainPath).Samples;

        Assert.Equal(original.Length, gzipLoaded.Count);
        Assert.Equal(original.Length, plainLoaded.Count);
        for (var i = 0; i < original.Length; i++)
        {
            AssertSampleEqual(original[i], gzipLoaded[i]);
            AssertSampleEqual(original[i], plainLoaded[i]);
        }
    }

    [Fact]
    public void UncompressedV2_File_StartsWithJsonObject_AndStillLoads()
    {
        string path;
        using (var recorder = new JsonlSessionRecorder(
                   new SessionRecorderOptions { Directory = _temp.Path, Compression = RecordingCompression.None },
                   TestSamples.Metadata()))
        {
            recorder.Record(TestSamples.Create(1));
            path = recorder.FilePath;
        }

        var firstLine = System.IO.File.ReadLines(path).First();
        Assert.StartsWith("{", firstLine);
        Assert.Single(new SessionRepository().Load(path).Samples);
    }

    internal static void AssertSampleEqual(TelemetrySample expected, TelemetrySample actual)
    {
        Assert.Equal(expected.SampleId, actual.SampleId);
        Assert.Equal(expected.ReceivedAtUtc, actual.ReceivedAtUtc);
        Assert.Equal(expected.SourceTimestampUtc, actual.SourceTimestampUtc);
        Assert.Equal(expected.ReceiptTick, actual.ReceiptTick);
        Assert.Equal(expected.SessionId, actual.SessionId);
        Assert.Equal(expected.LapNumber, actual.LapNumber);
        Assert.Equal(expected.IsLapValid, actual.IsLapValid);
        Assert.Equal(expected.CurrentLapTimeMs, actual.CurrentLapTimeMs);
        Assert.Equal(expected.LastLapTimeMs, actual.LastLapTimeMs);
        Assert.Equal(expected.BestLapTimeMs, actual.BestLapTimeMs);
        Assert.Equal(expected.CurrentSectorIndex, actual.CurrentSectorIndex);
        Assert.Equal(expected.SectorCount, actual.SectorCount);
        Assert.Equal(expected.TrackProgressMeters, actual.TrackProgressMeters);
        Assert.Equal(expected.SpeedMetersPerSecond, actual.SpeedMetersPerSecond);
        Assert.Equal(expected.SpeedKph, actual.SpeedKph);
        Assert.Equal(expected.SteeringInput, actual.SteeringInput);
        Assert.Equal(expected.ThrottleInput, actual.ThrottleInput);
        Assert.Equal(expected.BrakeInput, actual.BrakeInput);
        Assert.Equal(expected.Gear, actual.Gear);
        Assert.Equal(expected.EngineRpm, actual.EngineRpm);
        Assert.Equal(expected.LateralAccelerationG, actual.LateralAccelerationG);
        Assert.Equal(expected.LongitudinalAccelerationG, actual.LongitudinalAccelerationG);
        Assert.Equal(expected.AngularVelocityYawRadPerSec, actual.AngularVelocityYawRadPerSec);
        Assert.Equal(expected.HeadingRad, actual.HeadingRad);
        Assert.Equal(expected.PitchRad, actual.PitchRad);
        Assert.Equal(expected.RollRad, actual.RollRad);
        Assert.Equal(expected.PositionXMeters, actual.PositionXMeters);
        Assert.Equal(expected.PositionYMeters, actual.PositionYMeters);
        Assert.Equal(expected.PositionZMeters, actual.PositionZMeters);
        Assert.Equal(expected.PlayerPosition, actual.PlayerPosition);
        Assert.Equal(expected.NormalizedCarPosition, actual.NormalizedCarPosition);
        Assert.Equal(expected.WheelSlip, actual.WheelSlip);
        Assert.Equal(expected.WheelPressure, actual.WheelPressure);
        Assert.Equal(expected.TyreCoreTempC, actual.TyreCoreTempC);
        Assert.Equal(expected.FuelLevel, actual.FuelLevel);
        Assert.Equal(expected.PitLimiterOn, actual.PitLimiterOn);
        Assert.Equal(expected.IsInPitLane, actual.IsInPitLane);
        Assert.Equal(expected.TrackName, actual.TrackName);
        Assert.Equal(expected.CarName, actual.CarName);
        Assert.Equal(expected.TyreCompound, actual.TyreCompound);
        Assert.Equal(expected.SessionType, actual.SessionType);
        Assert.Equal(expected.SessionState, actual.SessionState);
        Assert.Equal(expected.Quality, actual.Quality);
    }
}