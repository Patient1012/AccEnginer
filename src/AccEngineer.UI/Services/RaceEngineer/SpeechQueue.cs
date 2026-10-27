namespace AccEngineer.UI.Services.RaceEngineer;

public class SpeechQueue : IDisposable
{
    private readonly object _lock = new();
    private readonly Queue<RaceEngineerAnnouncement> _routineQueue = new();
    private RaceEngineerAnnouncement? _current;
    private bool _isSpeaking;
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _pendingActions = new();

    public event EventHandler<RaceEngineerAnnouncement>? SpeakRequested;
    public bool IsSpeaking => _isSpeaking;

    public void Enqueue(RaceEngineerAnnouncement announcement)
    {
        if (announcement.Priority == AnnouncementPriority.Critical)
        {
            // Critical can interrupt
            lock (_lock)
            {
                // Clear routine queue on critical
                _routineQueue.Clear();
                SpeakRequested?.Invoke(this, announcement);
                _isSpeaking = true;
            }
            return;
        }

        if (announcement.Priority == AnnouncementPriority.Important)
        {
            // Important can interrupt routine if speaking routine
            lock (_lock)
            {
                if (_isSpeaking && _current?.Priority == AnnouncementPriority.Routine)
                {
                    // Request to interrupt - for simplicity, just queue high priority
                }
                SpeakRequested?.Invoke(this, announcement);
                _isSpeaking = true;
                return;
            }
        }

        // Routine - don't interrupt, queue if busy
        lock (_lock)
        {
            if (_isSpeaking && _current?.Priority >= announcement.Priority)
            {
                // Don't interrupt same or higher priority
                if (_routineQueue.Count < 10)
                    _routineQueue.Enqueue(announcement);
                return;
            }
            _current = announcement;
            _isSpeaking = true;
            SpeakRequested?.Invoke(this, announcement);
        }
    }

    public void OnSpeechCompleted()
    {
        lock (_lock)
        {
            _isSpeaking = false;
            _current = null;
            if (_routineQueue.Count > 0)
            {
                var next = _routineQueue.Dequeue();
                _current = next;
                _isSpeaking = true;
                SpeakRequested?.Invoke(this, next);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _routineQueue.Clear();
            _current = null;
            _isSpeaking = false;
        }
    }

    public void Dispose()
    {
        Clear();
    }
}
