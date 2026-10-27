using System.Collections.ObjectModel;
using System.Windows.Input;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services.RaceEngineer;

namespace AccEngineer.UI.ViewModels;

public sealed class RaceEngineerViewModel : PageViewModel, IDisposable
{
    private readonly MainViewModel _shell;
    private RaceEngineerService? _service;
    private bool _enabled;
    private bool _muted;
    private string _statusText = "Initializing...";

    public RaceEngineerViewModel(MainViewModel shell)
        : base("Race Engineer")
    {
        _shell = shell;
        Announcements = new ObservableCollection<RaceEngineerAnnouncement>();
        InitializeService();
        ToggleEnabledCommand = new RelayCommand(ToggleEnabled);
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        AskQuestionCommand = new RelayCommand(AskQuestion);
    }

    private void InitializeService()
    {
        try
        {
            var options = new RaceEngineerOptions
            {
                Enabled = _shell.Options.RaceEngineerEnabled,
                Muted = _shell.Options.RaceEngineerMuted,
                EmergencyOverrideMute = _shell.Options.RaceEngineerEmergencyOverrideMute,
                SpeechVolume = _shell.Options.RaceEngineerSpeechVolume,
                PushToTalkKey = _shell.Options.RaceEngineerPushToTalkKey ?? "LeftAlt",
                EnableFuel = _shell.Options.RaceEngineerEnableFuel,
                EnablePace = _shell.Options.RaceEngineerEnablePace,
                EnableTyres = _shell.Options.RaceEngineerEnableTyres,
                EnableSession = _shell.Options.RaceEngineerEnableSession,
                EnableSetup = _shell.Options.RaceEngineerEnableSetup
            };
            _service = new RaceEngineerService(options);
            _service.Announcement += OnAnnouncement;
            _enabled = _service.IsEnabled;
            _muted = _service.IsMuted;
            _service.Start();
            _statusText = "Race Engineer Ready (Local TTS + Speech Recognition)";
        }
        catch (Exception ex)
        {
            _statusText = $"Error: {ex.Message}";
        }
    }

    public ObservableCollection<RaceEngineerAnnouncement> Announcements { get; }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetProperty(ref _enabled, value) && _service != null)
            {
                _service.IsEnabled = value;
                _shell.Options.RaceEngineerEnabled = value;
                _shell.Options.Save();
            }
        }
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            if (SetProperty(ref _muted, value) && _service != null)
            {
                _service.IsMuted = value;
                _shell.Options.RaceEngineerMuted = value;
                _shell.Options.Save();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public ICommand ToggleEnabledCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand AskQuestionCommand { get; }

    private void ToggleEnabled() => Enabled = !Enabled;
    private void ToggleMute() => Muted = !Muted;

    private void AskQuestion()
    {
        // Placeholder for manual question input
        var ann = new RaceEngineerAnnouncement
        {
            Message = "Ask a question via push-to-talk (hold Left Alt) or use voice input.",
            Priority = AnnouncementPriority.Routine,
            Category = "Info"
        };
        Announcements.Insert(0, ann);
    }

    private void OnAnnouncement(object? sender, RaceEngineerAnnouncement ann)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Announcements.Insert(0, ann);
            if (Announcements.Count > 100)
                Announcements.RemoveAt(Announcements.Count - 1);
        });
    }

    public override void Refresh()
    {
        // Forward latest telemetry if service is available
        var latest = _shell.Telemetry.Latest;
        if (latest != null && _service != null)
        {
            _service.OnTelemetry(latest);
        }
    }

    public void Dispose()
    {
        if (_service != null)
        {
            _service.Announcement -= OnAnnouncement;
            _service.Dispose();
            _service = null;
        }
    }
}
