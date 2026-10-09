using System.Diagnostics;
using AccEngineer.Storage;
using AccEngineer.Telemetry;

namespace AccEngineer.UI.Services;

public enum ConnectionState
{
    Starting,
    WaitingForAcc,
    Connected,
    Replay,
    Error,
    Stopped
}

/// <summary>
/// Owns the ACC telemetry reader on a dedicated background thread. It publishes a
/// latest snapshot plus bounded recent/session buffers, manages recording to
/// JSONL, can replay a recording offline through the same pipeline, and raises
/// honest session events. The UI never touches shared memory directly.
/// </summary>
public sealed class LiveTelemetryService : IDisposable
{
    private const int RecentCapacity = 6000;
    private const int MaxSessionSamples = 1_500_000;

    private readonly SessionEventLog _events;
    private readonly AppSettings _settings;
    private readonly Func<ITrackTelemetryReader> _readerFactory;
    private readonly TelemetryValidator _validator = new();

    private readonly object _lifecycle = new();
    private readonly object _samplesGate = new();
    private readonly object _recentGate = new();
    private readonly object _recordGate = new();
    private readonly object _replayGate = new();

    private Thread? _thread;
    private volatile bool _running;
    private ITrackTelemetryReader? _reader;

    private volatile TelemetrySample? _latest;
    private volatile ConnectionState _connection = ConnectionState.Starting;
    private volatile string? _currentSessionId;
    private readonly List<TelemetrySample> _sessionSamples = new();
    private SampleRingBuffer _recent = new(RecentCapacity);

    private ISessionRecorder? _recorder;
    private bool _recordRequested;
    private volatile bool _isRecording;
    private volatile string? _recordingFilePath;
    private string? _recordedSessionId;

    private (string SessionId, int Lap) ? _lastLap;
    private bool _staleEventReported;

    private SessionRecording? _replay;
    private int _replayIndex;
    private DateTimeOffset _replayStartUtc;
    private double _replaySpeed = 1.0;
    private readonly Stopwatch _replayClock = new();
    private volatile bool _replaying;
    private volatile string? _replayName;

    public LiveTelemetryService(SessionEventLog events, AppSettings settings, Func<ITrackTelemetryReader>? readerFactory = null)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _readerFactory = readerFactory ?? (() => new AccSharedMemoryReader());
    }

    public ConnectionState Connection => _connection;

    public TelemetrySample? Latest => _latest;

    public string? CurrentSessionId => _currentSessionId;

    public bool IsRecording => _isRecording;

    public string? RecordingFilePath => _recordingFilePath;

    public bool IsReplaying => _replaying;

    public string? ReplayName => _replayName;

    public int RecordedSampleCount => _recorder is JsonlSessionRecorder jsonl ? jsonl.WrittenSampleCount : 0;

    public int DroppedSampleCount => _recorder is JsonlSessionRecorder jsonl ? jsonl.DroppedSampleCount : 0;

    public int SessionSampleCount
    {
        get
        {
            lock (_samplesGate) return _sessionSamples.Count;
        }
    }

    public void Start()
    {
        lock (_lifecycle)
        {
            if (_running) return;
            _running = true;
            _connection = ConnectionState.WaitingForAcc;
            _thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = "acc-telemetry-reader"
            };
            _thread.Start();
        }
    }

    public void Stop()
    {
        Thread? thread;
        lock (_lifecycle)
        {
            if (!_running) return;
            _running = false;
            thread = _thread;
            _thread = null;
        }

        thread?.Join(TimeSpan.FromSeconds(2));

        StopRecording();

        lock (_lifecycle)
        {
            _reader?.Dispose();
            _reader = null;
        }

        _connection = ConnectionState.Stopped;
    }

    public void StartRecording()
    {
        if (_replaying)
        {
            _events.Enqueue(Event(EventSeverity.Warning, SessionEventKind.Recording, "Cannot record while a replay is running."));
            return;
        }

        lock (_recordGate) _recordRequested = true;
        _events.Enqueue(Event(EventSeverity.Info, SessionEventKind.Recording, "Recording armed; starts on the next sample."));

        var sample = _latest;
        if (sample is not null) HandleRecording(sample);
    }

    public void StopRecording()
    {
        lock (_recordGate)
        {
            _recordRequested = false;
            CompleteRecorderLocked();
        }
    }

    public void ToggleRecording()
    {
        if (_isRecording || _recordRequested) StopRecording();
        else StartRecording();
    }

    public void StartReplay(SessionRecording recording, string displayName, double speed = 1.0)
    {
        ArgumentNullException.ThrowIfNull(recording);
        if (recording.Samples.Count == 0)
        {
            _events.Enqueue(Event(EventSeverity.Warning, SessionEventKind.Connection, "Recording contains no samples."));
            return;
        }

        StopRecording();
        lock (_replayGate)
        {
            _replay = recording;
            _replayIndex = 0;
            _replayStartUtc = recording.Samples[0].ReceivedAtUtc;
            _replaySpeed = speed <= 0 ? 1.0 : speed;
            _replayClock.Restart();
            _replayName = displayName;
            _replaying = true;
        }

        lock (_samplesGate)
        {
            _sessionSamples.Clear();
        }
        lock (_recentGate) _recent = new SampleRingBuffer(RecentCapacity);
        _lastLap = null;

        _connection = ConnectionState.Replay;
        _events.Enqueue(Event(EventSeverity.Info, SessionEventKind.Connection,
            $"Replay started: {displayName} ({recording.Samples.Count} samples)."));
    }

    public void StopReplay()
    {
        if (!FinishReplay(announce: false)) return;
        _events.Enqueue(Event(EventSeverity.Info, SessionEventKind.Connection, "Replay stopped."));
    }

    public IReadOnlyList<TelemetrySample> SnapshotRecent()
    {
        lock (_recentGate) return _recent.Snapshot();
    }

    public IReadOnlyList<TelemetrySample> SessionSamplesSnapshot()
    {
        lock (_samplesGate) return _sessionSamples.ToArray();
    }

    private void Loop()
    {
        while (_running)
        {
            try
            {
                if (_replaying)
                {
                    ReplayStep();
                }
                else
                {
                    LiveStep();
                }
            }
            catch (Exception ex)
            {
                SetConnection(ConnectionState.Error, $"Telemetry loop error ({ex.GetType().Name}): {ex.Message}");
            }

            var poll = Math.Clamp(_settings.PollIntervalMs, 10, 2000);
            Thread.Sleep(poll);
        }
    }

    private void LiveStep()
    {
        _reader ??= _readerFactory();

        TelemetrySample? sample;
        try
        {
            sample = _reader.ReadLatest();
        }
        catch (Exception ex)
        {
            SetConnection(ConnectionState.Error, $"Read error ({ex.GetType().Name}): {ex.Message}");
            return;
        }

        if (sample is not null)
        {
            ProcessSample(sample, live: true);
            SetConnection(ConnectionState.Connected);
        }
        else
        {
            SetConnection(_reader.IsConnected ? ConnectionState.Connected : ConnectionState.WaitingForAcc);
        }
    }

    private void ReplayStep()
    {
        SessionRecording? replay;
        int index;
        double speed;
        DateTimeOffset startUtc;
        lock (_replayGate)
        {
            replay = _replay;
            index = _replayIndex;
            speed = _replaySpeed;
            startUtc = _replayStartUtc;
        }

        if (replay is null || index >= replay.Samples.Count)
        {
            FinishReplay();
            return;
        }

        var sample = replay.Samples[index];
        var targetMs = (sample.ReceivedAtUtc - startUtc).TotalMilliseconds / speed;
        var waitMs = targetMs - _replayClock.Elapsed.TotalMilliseconds;
        if (waitMs > 1)
        {
            Thread.Sleep((int)Math.Min(waitMs, Math.Clamp(_settings.PollIntervalMs, 10, 2000)));
            return;
        }

        ProcessSample(sample, live: false);

        bool finished;
        lock (_replayGate)
        {
            _replayIndex++;
            finished = _replayIndex >= replay.Samples.Count;
        }

        if (finished) FinishReplay();
    }

    private bool FinishReplay(bool announce = true)
    {
        if (!_replaying) return false;

        _replaying = false;
        lock (_replayGate)
        {
            _replay = null;
            _replayIndex = 0;
        }

        SetConnection(ConnectionState.WaitingForAcc, announce ? "Replay finished; waiting for ACC." : null);
        if (announce)
        {
            _events.Enqueue(Event(EventSeverity.Info, SessionEventKind.Connection, $"Replay of {_replayName} finished."));
        }
        _replayName = null;
        return true;
    }

    private void ProcessSample(TelemetrySample sample, bool live)
    {
        if (live)
        {
            var extra = _validator.Validate(sample, DateTimeOffset.UtcNow);
            if (extra != DataQualityFlags.None) sample = sample with { Quality = sample.Quality | extra };

            if (sample.Quality.HasFlag(DataQualityFlags.Stale))
            {
                if (!_staleEventReported)
                {
                    _staleEventReported = true;
                    _events.Enqueue(Event(EventSeverity.Warning, SessionEventKind.DataQuality, "Telemetry is stale; ACC has not published new data."));
                }
            }
            else
            {
                _staleEventReported = false;
            }
        }

        _latest = sample;

        lock (_recentGate) _recent.Push(sample);

        if (_currentSessionId != sample.SessionId)
        {
            var previous = _currentSessionId;
            _currentSessionId = sample.SessionId;
            lock (_samplesGate)
            {
                _sessionSamples.Clear();
                _sessionSamples.Add(sample);
            }
            _lastLap = null;
            if (previous is not null)
            {
                _events.Enqueue(Event(EventSeverity.Info, SessionEventKind.Connection, $"New session detected ({sample.SessionId})."));
            }
        }
        else
        {
            lock (_samplesGate)
            {
                if (_sessionSamples.Count >= MaxSessionSamples) _sessionSamples.RemoveAt(0);
                _sessionSamples.Add(sample);
            }
        }

        DetectLapEvents(sample);

        if (live) HandleRecording(sample);
    }

    private void DetectLapEvents(TelemetrySample sample)
    {
        var key = (sample.SessionId, sample.LapNumber);
        if (_lastLap is { } last && last.SessionId == key.SessionId && key.LapNumber > last.Lap)
        {
            var completed = last.Lap;
            var timeMs = sample.LastLapTimeMs;
            var message = timeMs is > 0
                ? $"Lap {completed} completed in {Display.LapTimeMs(timeMs)}."
                : $"Lap {completed} completed.";

            _events.Enqueue(new SessionEvent
            {
                TimestampUtc = sample.ReceivedAtUtc,
                Kind = SessionEventKind.LapCompleted,
                Severity = EventSeverity.Success,
                Message = message,
                LapNumber = completed,
                LapDistanceMeters = sample.TrackProgressMeters
            });

            if (!sample.IsLapValid || sample.Quality.HasFlag(DataQualityFlags.InvalidLap))
            {
                _events.Enqueue(new SessionEvent
                {
                    TimestampUtc = sample.ReceivedAtUtc,
                    Kind = SessionEventKind.InvalidLap,
                    Severity = EventSeverity.Warning,
                    Message = $"Lap {completed} was flagged invalid by ACC.",
                    LapNumber = completed,
                    LapDistanceMeters = sample.TrackProgressMeters
                });
            }
        }

        _lastLap = key;
    }

    private void HandleRecording(TelemetrySample sample)
    {
        lock (_recordGate)
        {
            if (!_recordRequested) return;

            if (_recorder is not null && sample.SessionId == _recordedSessionId)
            {
                _recorder.Record(sample);
                return;
            }

            CompleteRecorderLocked();

            var metadata = new SessionMetadata
            {
                SchemaVersion = RecordingSchema.Version,
                SessionId = sample.SessionId,
                StartedAtUtc = sample.ReceivedAtUtc,
                TrackName = sample.TrackName,
                CarName = sample.CarName,
                SessionType = sample.SessionType
            };

            var recorder = new JsonlSessionRecorder(
                new SessionRecorderOptions { Directory = _settings.RecordingsDirectory },
                metadata);

            _recorder = recorder;
            _recordedSessionId = sample.SessionId;
            _isRecording = true;
            _recordingFilePath = recorder.FilePath;

            recorder.Record(sample);
            _events.Enqueue(Event(EventSeverity.Info, SessionEventKind.Recording, $"Recording to {recorder.FilePath}"));
        }
    }

    private void CompleteRecorderLocked()
    {
        var recorder = _recorder;
        _recorder = null;
        _recordedSessionId = null;
        _isRecording = false;
        if (recorder is null) return;

        _recordingFilePath = recorder.FilePath;
        try
        {
            recorder.Complete();
            var detail = $"Recording saved: {recorder.FilePath} ({recorder.WrittenSampleCount} samples";
            if (recorder.DroppedSampleCount > 0) detail += $", {recorder.DroppedSampleCount} dropped";
            detail += recorder is JsonlSessionRecorder { Fault: { } fault } ? $", writer fault: {fault.Message})" : ")";
            _events.Enqueue(Event(EventSeverity.Success, SessionEventKind.Recording, detail));
        }
        catch (Exception ex)
        {
            _events.Enqueue(Event(EventSeverity.Error, SessionEventKind.Recording, $"Failed to close recording: {ex.Message}"));
        }
    }

    private void SetConnection(ConnectionState next, string? detail = null)
    {
        var previous = _connection;
        if (previous == next && detail is null) return;
        _connection = next;
        if (previous == next) return;

        var (severity, message) = next switch
        {
            ConnectionState.Connected => (EventSeverity.Success, "Connected to ACC telemetry."),
            ConnectionState.WaitingForAcc => (EventSeverity.Info, "Waiting for ACC shared memory (start ACC and enter a session)."),
            ConnectionState.Replay => (EventSeverity.Info, "Replaying a recorded session."),
            ConnectionState.Error => (EventSeverity.Error, detail ?? "Telemetry error."),
            ConnectionState.Stopped => (EventSeverity.Info, "Telemetry reader stopped."),
            _ => (EventSeverity.Info, detail ?? "Starting telemetry reader.")
        };

        _events.Enqueue(Event(severity, SessionEventKind.Connection, detail ?? message));
    }

    private static SessionEvent Event(EventSeverity severity, SessionEventKind kind, string message) => new()
    {
        TimestampUtc = DateTimeOffset.UtcNow,
        Kind = kind,
        Severity = severity,
        Message = message
    };

    public IReadOnlyList<TelemetrySample> SnapshotRecent(int max)
    {
        lock (_recentGate)
        {
            var count = Math.Min(max, _recent.Count);
            var result = new TelemetrySample[count];
            var offset = _recent.Count - count;
            for (var i = 0; i < count; i++) result[i] = _recent[offset + i];
            return result;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
