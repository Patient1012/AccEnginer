namespace AccEngineer.Telemetry.Tests;

public class SampleRingBufferTests
{
    private static TelemetrySample Sample(long id) => new()
    {
        SampleId = id,
        ReceivedAtUtc = DateTimeOffset.UnixEpoch,
        ReceiptTick = id,
        SessionId = "sm-test",
        LapNumber = 1,
        IsLapValid = true
    };

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleRingBuffer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleRingBuffer(-1));
    }

    [Fact]
    public void NewBuffer_IsEmpty()
    {
        var buffer = new SampleRingBuffer(3);

        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void Push_PreservesOldestToNewestOrder()
    {
        var buffer = new SampleRingBuffer(3);
        buffer.Push(Sample(1));
        buffer.Push(Sample(2));
        buffer.Push(Sample(3));

        Assert.Equal(3, buffer.Count);
        var snapshot = buffer.Snapshot();
        Assert.Equal(new long[] { 1, 2, 3 }, snapshot.Select(s => s.SampleId).ToArray());
        Assert.Equal(1, buffer[0].SampleId);
        Assert.Equal(3, buffer[2].SampleId);
    }

    [Fact]
    public void Push_BeyondCapacity_DropsOldestKeepsNewest()
    {
        var buffer = new SampleRingBuffer(3);
        for (var i = 1; i <= 5; i++) buffer.Push(Sample(i));

        Assert.Equal(3, buffer.Count);
        var snapshot = buffer.Snapshot();
        Assert.Equal(new long[] { 3, 4, 5 }, snapshot.Select(s => s.SampleId).ToArray());
        Assert.Equal(5, buffer[buffer.Count - 1].SampleId);
    }

    [Fact]
    public void Push_ManyWraps_KeepsOrdering()
    {
        var buffer = new SampleRingBuffer(4);
        for (var i = 1; i <= 100; i++) buffer.Push(Sample(i));

        var snapshot = buffer.Snapshot();
        Assert.Equal(new long[] { 97, 98, 99, 100 }, snapshot.Select(s => s.SampleId).ToArray());
    }

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        var buffer = new SampleRingBuffer(2);
        buffer.Push(Sample(1));

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[1]);
    }

    [Fact]
    public void Push_NullSample_Throws()
    {
        var buffer = new SampleRingBuffer(2);

        Assert.Throws<ArgumentNullException>(() => buffer.Push(null!));
    }
}
