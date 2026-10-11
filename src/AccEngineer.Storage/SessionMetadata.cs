namespace AccEngineer.Storage;

/// <summary>
/// Static identity and context for one recorded session, written once as the
/// header record of a recording. Mutable result counts (sample totals, end time)
/// live in <see cref="SessionEndInfo"/> so an interrupted recording still exposes
/// valid metadata.
/// </summary>
public sealed record SessionMetadata
{
    /// <summary>Recording schema version; see <see cref="RecordingSchema.Version"/>.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Session identifier from the telemetry reader (stable within a session).</summary>
    public required string SessionId { get; init; }

    /// <summary>Wall-clock time the recording started (first received sample).</summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    public string Simulator { get; init; } = "Assetto Corsa Competizione";

    public string? TrackName { get; init; }

    public string? CarName { get; init; }

    /// <summary>Session type as reported at the start of the recording.</summary>
    public Telemetry.SessionType SessionType { get; init; } = Telemetry.SessionType.Unknown;
}

/// <summary>Summary written when a recording is closed cleanly.</summary>
public sealed record SessionEndInfo
{
    public required DateTimeOffset EndedAtUtc { get; init; }

    /// <summary>Number of samples serialized to disk.</summary>
    public required int SampleCount { get; init; }

    /// <summary>Samples accepted by the caller but dropped because the write queue was full.</summary>
    public required int DroppedSampleCount { get; init; }

    /// <summary>True when the recording was closed via <c>Complete</c>/<c>Dispose</c>.</summary>
    public required bool Completed { get; init; }
}

/// <summary>A fully loaded recording: metadata, ordered samples, and optional end summary.</summary>
public sealed record SessionRecording
{
    public required SessionMetadata Metadata { get; init; }

    /// <summary>Samples in recorded order. Nulls are preserved as nulls, never zero-substituted.</summary>
    public required IReadOnlyList<Telemetry.TelemetrySample> Samples { get; init; }

    /// <summary>Null when the recording was interrupted before it was closed cleanly.</summary>
    public SessionEndInfo? End { get; init; }

    /// <summary>Lines skipped because they were unparseable (e.g. a truncated final line).</summary>
    public int SkippedRecordCount { get; init; }
}

public static class RecordingSchema
{
    /// <summary>
    /// 1 = verbatim per-sample JSONL (full <c>TelemetrySample</c> objects).
    /// 2 = compact sample encoding: relative elapsed-ms timestamps, integer
    /// enum/flag values, and no per-sample <c>sessionId</c> (held in the header).
    /// The loader transparently accepts both and auto-detects gzip compression.
    /// </summary>
    public const int Version = 2;
}
