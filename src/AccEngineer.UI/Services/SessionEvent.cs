using System.Globalization;

namespace AccEngineer.UI.Services;

public enum SessionEventKind
{
    Info,
    Connection,
    Recording,
    LapCompleted,
    InvalidLap,
    DataQuality,
    Replay,
    Report
}

public enum EventSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// A single, honest observation surfaced to the operator. Only events derived
/// from real telemetry or real application state are created; nothing here is
/// fabricated analysis output.
/// </summary>
public sealed record SessionEvent
{
    public required DateTimeOffset TimestampUtc { get; init; }

    public required SessionEventKind Kind { get; init; }

    public required EventSeverity Severity { get; init; }

    public required string Message { get; init; }

    public int? LapNumber { get; init; }

    public double? LapDistanceMeters { get; init; }

    public string TimeText => TimestampUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}
