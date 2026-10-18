using System.Diagnostics;
using AccEngineer.Analysis.Fuel;
using AccEngineer.Storage;
using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;

namespace AccEngineer.UI.Services;

public enum ConnectionState
{
    Starting,
    WaitingForAcc,
    Connected,

    /// <summary>Running on generated test data; no simulator is connected.</summary>
    TestData,
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
    private readonly object _readerGate = new();

    private Thread? _thread;
    private volatile bool _running;
    private ITrackTelemetryReader? _reader;
    private volatile bool _testDataMode;

    private AccBroadcastClient? _broadcast;
    private readonly TestCompetitorFeed _testCompetitors = new();
    private volatile bool _broadcastingEnabled;

    private volatile TelemetrySample? _latest;
    private volatile ConnectionState _connection = ConnectionState.Starting;
    private volatile string? _currentSessionId;
    private readonly List<TelemetrySample> _sessionSamples = new();
    private SampleRingBuffer _recent = new(RecentCapacity);

    /// <summary>Measures per-lap fuel consumption from the incoming samples.</summary>
    private readonly FuelConsumptionTracker _fuelTracker = new();

    private ISessionRecorder? _recorder;
    private bool _recordRequested;
    private volatile bool _isRecording;
    private volatile string? _recordingFilePath;
    private string? _recordedSessionId;

    private (string SessionId, int Lap)? _lastLap;
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
        _testDataMode = settings.TestDataMode;
        _broadcastingEnabled = settings.BroadcastingEnabled;

        // The default provider follows the current mode; an explicit factory (used by
        // tests) always wins so callers keep full control.
        _readerFactory = readerFactory ?? (() => _testDataMode
            ? new SimulatedTelemetryReader()
            : new AccSharedMemoryReader());
    }

    public ConnectionState Connection => _connection;

    /// <summary>
    /// True when the reader actually producing samples is the built-in test-data
    /// provider. Derived from the live reader, not the saved preference, so the UI
    /// always reflects what is really in effect.
    /// </summary>
    public bool IsTestData
    {
        get
        {
            lock (_readerGate) return _reader is ISimulatedTelemetryReader;
        }
    }

    public TelemetrySample? Latest => _latest;

    /// <summary>
    /// Current per-lap fuel-consumption measurements derived from the incoming samples.
    /// A thread-safe snapshot, so it is safe to read from the UI thread. Works the same
    /// way for live ACC telemetry, generated test data and replays, and resets itself
    /// when a new session is detected.
    /// </summary>
    public FuelConsumptionSnapshot FuelConsumption => _fuelTracker.Snapshot();

    public string? CurrentSessionId => _currentSessionId;

    public bool IsRecording => _isRecording;

    public string? RecordingFilePath => _recordingFilePath;

    public bool IsReplaying => _replaying;

    public string? ReplayName => _replayName;

    /// <summary>Whether the user has enabled the ACC broadcasting connection.</summary>
    public bool IsBroadcastingEnabled => _broadcastingEnabled;

    /// <summary>
    /// Latest competitor standings. Comes from the ACC broadcasting client in a live
    /// session, or from the deterministic test feed in test-data mode. Null while
    /// broadcasting is off/connecting or before ACC sends any data.
    /// </summary>
    public CompetitorStandings? Standings
    {
        get
        {
            if (IsTestData) return _testCompetitors.Snapshot(DateTimeOffset.UtcNow);
            return _broadcast?.Snapshot();
        }
    }

    /// <summary>True when competitor timing is actually being received (not just requested).</summary>
    public bool IsBroadcasting => !IsTestData && _broadcast?.IsRegistered == true;

    /// <summary>Short human-readable broadcasting state for the UI.</summary>
    public string BroadcastingStatusText
    {
        get
        {
            if (IsTestData) return "Test data";
            if (!_broadcastingEnabled) return "Disabled";
            var client = _broadcast;
            if (client is null) return "Connecting";
            if (!client.IsRegistered) return client.Error ?? "Waiting for ACC broadcasting";
            return client.Snapshot() is null ? "Waiting for session" : "Live";
        }
    }

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

        ApplyBroadcasting();
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

        _broadcast?.Dispose();
        _broadcast = null;

        lock (_readerGate)
        {
            _reader?.Dispose();
            _reader = null;
        }

        _connection = ConnectionState.Stopped;
    }

    /// <summary>
    /// Switches between the real shared-memory reader and the built-in test-data
    /// provider. Applies immediately without a restart: the current reader is
    /// disposed and the loop creates the provider for the new mode on its next tick.
    /// </summary>
    public void SetTestDataMode(bool enabled)
    {
        lock (_readerGate)
        {
            if (_testDataMode == enabled) return;
            _testDataMode = enabled;
            _reader?.Dispose();
            _reader = null;
        }

        _events.Enqueue(Event(EventSeverity.Info, SessionEventKind.Connection, enabled
            ? "Test data mode enabled: using generated telemetry (no simulator)."
            : "Test data mode disabled: reading ACC shared memory."));

        ApplyBroadcasting();
    }

    /// <summary>
    /// Enables or disables the ACC broadcasting connection. Applies immediately:
    /// the client is created and started, or disposed, without a restart.
    /// </summary>
    public void SetBroadcastingEnabled(bool enabled)
    {
        _broadcastingEnabled = enabled;
        _events.Enqueue(Event(EventSeverity.Info, SessionEventKind.Connection, enabled
            ? "Broadcasting enabled: connecting for competitor data."
            : "Broadcasting disabled: competitor data unavailable."));

        ApplyBroadcasting();
    }

    /// <summary>
    /// Starts the broadcasting client when it is wanted (enabled, live mode, running)
    /// and tears it down otherwise. Broadcasting only makes sense outside test-data
    /// mode and while the service is running.
    /// </summary>
    private void ApplyBroadcasting()
    {
        var want = _broadcastingEnabled && !_testDataMode && _running;

        if (want)
        {
            if (_broadcast is not null) return;

            var options = new AccBroadcastClientOptions
            {
                Port = Math.Clamp(_settings.BroadcastingPort, 1, 65535),
                ConnectionPassword = _settings.BroadcastingPassword,
                CommandPassword = _settings.BroadcastingPassword
            };

            try
            {
                _broadcast = new AccBroadcastClient(options, new UdpAccBroadcastTransport(options.Host, options.Port));
                _broadcast.Start();
            }
            catch (Exception ex)
            {
                _broadcast = null;
                _events.Enqueue(Event(EventSeverity.Warning, SessionEventKind.Connection,
                    $"Could not start broadcasting on port {options.Port}: {ex.Message}"));
            }
        }
        else
        {
            _broadcast?.Dispose();
            _broadcast = null;
        }
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
        ITrackTelemetryReader reader;
        lock (_readerGate)
        {
            reader = _reader ??= _readerFactory();
        }

        var testData = reader is ISimulatedTelemetryReader;

        TelemetrySample? sample;
        try
        {
            sample = reader.ReadLatest();
        }
        catch (Exception ex)
        {
            SetConnection(ConnectionState.Error, $"Read error ({ex.GetType().Name}): {ex.Message}");
            return;
        }

        if (sample is not null)
        {
            ProcessSample(sample, live: true);
            SetConnection(testData ? ConnectionState.TestData : ConnectionState.Connected);
        }
        else
        {
            SetConnection(testData
                ? ConnectionState.TestData
                : reader.IsConnected ? ConnectionState.Connected : ConnectionState.WaitingForAcc);
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

        _fuelTracker.Observe(sample);

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
            ConnectionState.TestData => (EventSeverity.Info, "Running on generated test data (no simulator)."),
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
