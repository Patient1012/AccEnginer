namespace AccEngineer.Telemetry.Tests;

/// <summary>
/// Reader tests that must pass with or without ACC running (CI-safe):
/// the first read never yields a sample because the reader first baselines
/// the packet id, and connection failures surface as null rather than exceptions.
/// </summary>
public class AccSharedMemoryReaderTests
{
    [Fact]
    public void FirstRead_ReturnsNull_WhetherOrNotAccIsRunning()
    {
        using var reader = new AccSharedMemoryReader(new AccReaderOptions
        {
            ReconnectRetryInterval = TimeSpan.FromMilliseconds(1)
        });

        Assert.Null(reader.ReadLatest());
    }

    [Fact]
    public void Dispose_WhenNeverConnected_IsIdempotent()
    {
        var reader = new AccSharedMemoryReader();

        reader.Dispose();
        reader.Dispose();
    }

    [Fact]
    public void Reader_ImplementsInterfaceContract()
    {
        ITrackTelemetryReader reader = new AccSharedMemoryReader();
        reader.Dispose();

        Assert.NotNull(reader);
    }
}
