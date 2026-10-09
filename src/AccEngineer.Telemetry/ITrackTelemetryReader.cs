namespace AccEngineer.Telemetry;

/// <summary>
/// Interface for reading live telemetry from a simulator.
/// The implementation is expected to be poll-based.
/// </summary>
public interface ITrackTelemetryReader : IDisposable
{
    bool IsConnected { get; }

    /// <summary>
    /// Returns the latest sample, or null when no new data is available
    /// (e.g., simulator not running, stale data, or no update since last read).
    /// </summary>
    TelemetrySample? ReadLatest();
}
