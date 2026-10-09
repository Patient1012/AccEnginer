namespace AccEngineer.Storage;

/// <summary>
/// Records telemetry samples to durable storage. Implementations must not block
/// the telemetry capture path: samples may be dropped when a bounded queue fills,
/// and dropped counts are summarised in the recording's end record.
/// </summary>
public interface ISessionRecorder : IDisposable
{
    /// <summary>Full path of the recording file.</summary>
    string FilePath { get; }

    /// <summary>Metadata written as the recording header.</summary>
    SessionMetadata Metadata { get; }

    /// <summary>Samples serialized so far.</summary>
    int WrittenSampleCount { get; }

    /// <summary>Samples dropped because the write queue was full.</summary>
    int DroppedSampleCount { get; }

    /// <summary>Queues a sample for writing. Never blocks; drops when the queue is full.</summary>
    void Record(Telemetry.TelemetrySample sample);

    /// <summary>Flushes and closes the recording, writing the end summary. Idempotent.</summary>
    void Complete();
}
