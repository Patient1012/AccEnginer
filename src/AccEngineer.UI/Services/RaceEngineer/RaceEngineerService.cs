using AccEngineer.AI;
using AccEngineer.Telemetry;

namespace AccEngineer.UI.Services.RaceEngineer;

public class RaceEngineerService : IRaceEngineerService
{
    private readonly IAnalysisProvider? _aiProvider;
    private readonly RaceEngineerOptions _options;
    private readonly FuelMonitor _fuelMonitor = new();
    private readonly LocalSpeechSynthesizer _tts = new();
    private readonly LocalSpeechRecognizer _recognizer = new();
    private readonly SpeechQueue _queue = new();
    private bool _enabled;
    private bool _muted;
    private bool _pushToTalkHeld;
    private DateTimeOffset _lastSessionEvent = DateTimeOffset.MinValue;
    private string? _lastSessionId;

    public RaceEngineerService(RaceEngineerOptions? options = null, IAnalysisProvider? aiProvider = null)
    {
        _options = options ?? new RaceEngineerOptions();
        _aiProvider = aiProvider;
        _enabled = _options.Enabled;
        _muted = _options.Muted;
        _queue.SpeakRequested += OnSpeakRequested;
        _tts.Initialize();
        _recognizer.Initialize();
    }

    public bool IsEnabled { get => _enabled; set => _enabled = value; }
    public bool IsMuted { get => _muted; set => _muted = value; }
    public bool IsPushToTalkHeld 
    { 
        get => _pushToTalkHeld; 
        set 
        { 
            if (_pushToTalkHeld == value) return;
            _pushToTalkHeld = value;
            if (value && _enabled)
                _recognizer.StartPushToTalk();
            else
                _recognizer.StopPushToTalk();
        } 
    }

    public event EventHandler<RaceEngineerAnnouncement>? Announcement;

    public void Start()
    {
    }

    public void Stop()
    {
        _queue.Clear();
        _tts.Stop();
    }

    public void OnTelemetry(TelemetrySample sample)
    {
        if (!_enabled) return;

        // Fuel monitoring
        if (_options.EnableFuel)
        {
            var fuelAnn = _fuelMonitor.CheckFuel(sample);
            if (fuelAnn != null)
            {
                Announcement?.Invoke(this, fuelAnn);
                if (!ShouldSuppressSpeech(fuelAnn)) _queue.Enqueue(fuelAnn);
            }
        }

        // Session start announcement
        if (_lastSessionId != sample.SessionId && !string.IsNullOrEmpty(sample.SessionId))
        {
            var now = DateTimeOffset.UtcNow;
            if ((now - _lastSessionEvent).TotalSeconds > 30)
            {
                _lastSessionEvent = now;
                var ann = new RaceEngineerAnnouncement
                {
                    Message = $"Race engineer online. Session {sample.SessionType}.",
                    Priority = AnnouncementPriority.Routine,
                    Category = "Session"
                };
                Announcement?.Invoke(this, ann);
                if (!ShouldSuppressSpeech(ann) && _options.EnableSession) _queue.Enqueue(ann);
            }
            _lastSessionId = sample.SessionId;
        }
    }

    private bool ShouldSuppressSpeech(RaceEngineerAnnouncement ann)
    {
        if (!_options.Enabled) return true;
        if (_muted && !(ann.Priority == AnnouncementPriority.Critical && _options.EmergencyOverrideMute))
            return true;
        return false;
    }

    private void OnSpeakRequested(object? sender, RaceEngineerAnnouncement ann)
    {
        if (ShouldSuppressSpeech(ann)) return;
        try
        {
            _tts.Speak(ann.Message, _options.SpeechVolume);
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
        _queue.Dispose();
        _tts.Dispose();
        _recognizer.Dispose();
    }
}
