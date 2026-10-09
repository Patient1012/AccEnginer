namespace AccEngineer.Telemetry.Tests;

public class TelemetryValidatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static TelemetrySample Sample(long id, DateTimeOffset receivedAt, DataQualityFlags quality = DataQualityFlags.None) =>
        new()
        {
            SampleId = id,
            ReceivedAtUtc = receivedAt,
            ReceiptTick = id,
            SessionId = "sm-test",
            LapNumber = 1,
            IsLapValid = true,
            Quality = quality
        };

    [Fact]
    public void FirstSample_ProducesNoFlags()
    {
        var validator = new TelemetryValidator();

        var flags = validator.Validate(Sample(1, T0), T0);

        Assert.Equal(DataQualityFlags.None, flags);
    }

    [Fact]
    public void SteadySampleFlow_ProducesNoFlags()
    {
        var validator = new TelemetryValidator();
        validator.Validate(Sample(1, T0), T0);

        var flags = validator.Validate(Sample(2, T0.AddMilliseconds(50)), T0.AddMilliseconds(50));

        Assert.Equal(DataQualityFlags.None, flags);
    }

    [Fact]
    public void ReceiveGapAboveThreshold_FlagsGapDetected()
    {
        var validator = new TelemetryValidator();
        validator.Validate(Sample(1, T0), T0);

        var flags = validator.Validate(Sample(2, T0.AddMilliseconds(600)), T0.AddMilliseconds(600));

        Assert.Equal(DataQualityFlags.GapDetected, flags);
    }

    [Fact]
    public void ReceiveGapAtExactlyThreshold_IsNotFlagged()
    {
        var validator = new TelemetryValidator();
        validator.Validate(Sample(1, T0), T0);

        var flags = validator.Validate(Sample(2, T0.AddMilliseconds(500)), T0.AddMilliseconds(500));

        Assert.Equal(DataQualityFlags.None, flags);
    }

    [Fact]
    public void DecreasingSampleId_FlagsOutOfOrder()
    {
        var validator = new TelemetryValidator();
        validator.Validate(Sample(5, T0), T0);

        var flags = validator.Validate(Sample(4, T0.AddMilliseconds(50)), T0.AddMilliseconds(50));

        Assert.Equal(DataQualityFlags.OutOfOrder, flags);
    }

    [Fact]
    public void ReceiveTimestampGoingBackwards_FlagsOutOfOrder()
    {
        var validator = new TelemetryValidator();
        validator.Validate(Sample(5, T0.AddMilliseconds(100)), T0.AddMilliseconds(100));

        var flags = validator.Validate(Sample(6, T0.AddMilliseconds(50)), T0.AddMilliseconds(150));

        Assert.Equal(DataQualityFlags.OutOfOrder, flags);
    }

    [Fact]
    public void SampleOlderThanStalenessThreshold_FlagsStale()
    {
        var validator = new TelemetryValidator();

        var flags = validator.Validate(Sample(1, T0), T0.AddSeconds(2));

        Assert.Equal(DataQualityFlags.Stale, flags);
    }

    [Fact]
    public void FreshSample_IsNotStale()
    {
        var validator = new TelemetryValidator();

        var flags = validator.Validate(Sample(1, T0), T0.AddMilliseconds(200));

        Assert.Equal(DataQualityFlags.None, flags);
    }

    [Fact]
    public void WithQuality_MergesFlagsWithoutDroppingExistingOnes()
    {
        var sample = Sample(1, T0, DataQualityFlags.GapDetected);

        var merged = sample with { Quality = sample.Quality | DataQualityFlags.Stale };

        Assert.True(merged.Quality.HasFlag(DataQualityFlags.GapDetected));
        Assert.True(merged.Quality.HasFlag(DataQualityFlags.Stale));
        Assert.Equal(sample.SampleId, merged.SampleId);
        Assert.Equal(sample.SessionId, merged.SessionId);
    }
}
