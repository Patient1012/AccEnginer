using AccEngineer.Analysis.Telemetry;
using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Tests;

public class TelemetryProcessorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static TelemetrySample Sample(long id, double offsetSeconds, float? speedKph = null, float? speedMs = null) =>
        new()
        {
            SampleId = id,
            ReceivedAtUtc = T0.AddSeconds(offsetSeconds),
            ReceiptTick = id,
            SessionId = "sm-test",
            LapNumber = 1,
            SpeedKph = speedKph,
            SpeedMetersPerSecond = speedMs
        };

    [Fact]
    public void Process_PreservesRawSample()
    {
        var processor = new TelemetryProcessor();
        var sample = Sample(1, 0, speedKph: 100f);

        var processed = processor.Process(sample, T0);

        Assert.Same(sample, processed.Raw);
        Assert.True(processed.IsProcessed);
    }

    [Fact]
    public void Process_NormalisesSpeedKphToMs()
    {
        var processor = new TelemetryProcessor();

        var processed = processor.Process(Sample(1, 0, speedKph: 36f), T0);

        Assert.Equal(36f, processed.SpeedKph!.Value, 3);
        Assert.Equal(10f, processed.SpeedMs!.Value, 3);
    }

    [Fact]
    public void Process_NormalisesSpeedMsToKph()
    {
        var processor = new TelemetryProcessor();

        var processed = processor.Process(Sample(1, 0, speedMs: 10f), T0);

        Assert.Equal(36f, processed.SpeedKph!.Value, 3);
        Assert.Equal(10f, processed.SpeedMs!.Value, 3);
    }

    [Fact]
    public void Process_MissingSpeed_StaysNull()
    {
        var processor = new TelemetryProcessor();

        var processed = processor.Process(Sample(1, 0), T0);

        Assert.Null(processed.SpeedKph);
        Assert.Null(processed.SpeedMs);
    }

    [Fact]
    public void Process_FlagsReceiveGap()
    {
        var processor = new TelemetryProcessor(new TelemetryProcessorOptions
        {
            GapThreshold = TimeSpan.FromMilliseconds(100)
        });

        processor.Process(Sample(1, 0), T0);
        var processed = processor.Process(Sample(2, 1.0), T0.AddSeconds(1));

        Assert.True(processed.HasGap);
    }

    [Fact]
    public void Process_SteadyFlow_NoGap()
    {
        var processor = new TelemetryProcessor();

        processor.Process(Sample(1, 0), T0);
        var processed = processor.Process(Sample(2, 0.05), T0.AddMilliseconds(50));

        Assert.False(processed.HasGap);
    }
}
