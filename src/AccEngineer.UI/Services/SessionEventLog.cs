using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace AccEngineer.UI.Services;

/// <summary>
/// Thread-safe sink for session events. Background workers enqueue; the UI thread
/// drains via <see cref="Pump"/> so the observable collection is only ever
/// mutated on the dispatcher thread.
/// </summary>
public sealed class SessionEventLog
{
    private const int MaxRetained = 300;

    private readonly ConcurrentQueue<SessionEvent> _pending = new();

    public ObservableCollection<SessionEvent> Events { get; } = new();

    public void Enqueue(SessionEvent sessionEvent) => _pending.Enqueue(sessionEvent);

    public void Info(string message, bool success = false) => Enqueue(new SessionEvent
    {
        TimestampUtc = DateTimeOffset.UtcNow,
        Kind = SessionEventKind.Info,
        Severity = success ? EventSeverity.Success : EventSeverity.Info,
        Message = message
    });

    public void Warning(string message) => Enqueue(new SessionEvent
    {
        TimestampUtc = DateTimeOffset.UtcNow,
        Kind = SessionEventKind.DataQuality,
        Severity = EventSeverity.Warning,
        Message = message
    });

    public void Error(string message) => Enqueue(new SessionEvent
    {
        TimestampUtc = DateTimeOffset.UtcNow,
        Severity = EventSeverity.Error,
        Kind = SessionEventKind.DataQuality,
        Message = message
    });

    /// <summary>Moves queued events into the observable collection (newest first).</summary>
    public void Pump()
    {
        var added = false;
        while (_pending.TryDequeue(out var sessionEvent))
        {
            Events.Insert(0, sessionEvent);
            added = true;
        }

        while (Events.Count > MaxRetained)
        {
            Events.RemoveAt(Events.Count - 1);
        }

        _ = added;
    }

    public void Clear()
    {
        while (_pending.TryDequeue(out _)) { }
        Events.Clear();
    }
}
