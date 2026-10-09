using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using AccEngineer.Storage;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.ViewModels;

/// <summary>Immutable snapshot of shell status consumed by the top bar.</summary>
public sealed record StatusSnapshot(
    ConnectionState Connection,
    string ConnectionText,
    string SessionText,
    string TrackText,
    string CarText,
    bool IsRecording,
    string RecordingText,
    int RecordedSamples,
    bool IsReplaying,
    string? ReplayName);

/// <summary>
/// Application shell: owns the long-lived services, the page list, the status
/// snapshot, and a UI pump that drains events and refreshes the active page.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private PageViewModel _currentPage;
    private StatusSnapshot _status;

    public MainViewModel()
    {
        Options = AppSettings.Load();
        EventLog = new SessionEventLog();
        Telemetry = new LiveTelemetryService(EventLog, Options);
        Reports = new ReportService(Options);
        Library = new RecordingLibrary(Options);

        Live = new LiveDataViewModel(Telemetry);
        Overview = new OverviewViewModel(this);
        Analysis = new AnalysisViewModel(this);
        ReportsPage = new ReportsViewModel(this);
        Sessions = new SessionsViewModel(this);
        SettingsPage = new SettingsViewModel(this);

        Pages = new ObservableCollection<PageViewModel>
        {
            Overview,
            Live,
            Analysis,
            ReportsPage,
            Sessions
        };

        _currentPage = Overview;
        _status = BuildStatus();

        ToggleRecordingCommand = new RelayCommand(() => Telemetry.ToggleRecording());
        OpenSettingsCommand = new RelayCommand(() => NavigateTo(SettingsPage));
        OpenSessionsCommand = new RelayCommand(() => NavigateTo(Sessions));

        ReportsPage.Load();
        Sessions.Load();

        Telemetry.Start();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Pump();
        _timer.Start();
    }

    public AppSettings Options { get; }

    public SessionEventLog EventLog { get; }

    public LiveTelemetryService Telemetry { get; }

    public ReportService Reports { get; }

    public RecordingLibrary Library { get; }

    public LiveDataViewModel Live { get; }

    public OverviewViewModel Overview { get; }

    public AnalysisViewModel Analysis { get; }

    public ReportsViewModel ReportsPage { get; }

    public SessionsViewModel Sessions { get; }

    public SettingsViewModel SettingsPage { get; }

    public ObservableCollection<PageViewModel> Pages { get; }

    public ICommand ToggleRecordingCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    public ICommand OpenSessionsCommand { get; }

    public ReportResult? LastReport { get; private set; }

    public PageViewModel CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value))
            {
                value?.Refresh();
            }
        }
    }

    public StatusSnapshot Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public void NavigateTo(PageViewModel page)
    {
        if (page is null) return;
        CurrentPage = page;
    }

    /// <summary>Builds and saves a report from the live session's captured samples.</summary>
    public ReportResult? GenerateReport()
    {
        var samples = Telemetry.SessionSamplesSnapshot();
        if (samples.Count == 0) return null;

        var result = Reports.Generate(samples, BuildMetadata(), DateTimeOffset.UtcNow);
        return Adopt(result);
    }

    /// <summary>Builds and saves a report from a loaded recording.</summary>
    public ReportResult? GenerateReportFromRecording(SessionRecording recording)
    {
        ArgumentNullException.ThrowIfNull(recording);
        var result = Reports.Generate(recording.Samples, recording.Metadata, DateTimeOffset.UtcNow);
        return Adopt(result);
    }

    /// <summary>Generates a report from the live session and switches to the Reports page.</summary>
    public void GenerateReportAndShow()
    {
        var result = GenerateReport();
        if (result is null)
        {
            EventLog.Warning("No telemetry captured yet; start a session or replay a recording first.");
            return;
        }

        NavigateTo(ReportsPage);
        ReportsPage.Load();
    }

    private ReportResult? Adopt(ReportResult? result)
    {
        if (result is null) return null;

        Reports.Save(result);
        LastReport = result;
        OnPropertyChanged(nameof(LastReport));
        EventLog.Enqueue(new SessionEvent
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Kind = SessionEventKind.Report,
            Severity = EventSeverity.Success,
            Message = $"Report generated: {result.BaseName}"
        });
        return result;
    }

    private SessionMetadata? BuildMetadata()
    {
        var latest = Telemetry.Latest;
        if (latest is null) return null;

        return new SessionMetadata
        {
            SchemaVersion = RecordingSchema.Version,
            SessionId = latest.SessionId,
            StartedAtUtc = latest.ReceivedAtUtc,
            TrackName = latest.TrackName,
            CarName = latest.CarName,
            SessionType = latest.SessionType
        };
    }

    private void Pump()
    {
        EventLog.Pump();
        Status = BuildStatus();
        CurrentPage?.Refresh();
    }

    private StatusSnapshot BuildStatus()
    {
        var connection = Telemetry.Connection;
        var latest = Telemetry.Latest;
        var recording = Telemetry.IsRecording;

        var connectionText = connection switch
        {
            ConnectionState.Connected => "CONNECTED",
            ConnectionState.WaitingForAcc => "WAITING FOR ACC",
            ConnectionState.Replay => "REPLAY",
            ConnectionState.Error => "ERROR",
            ConnectionState.Stopped => "STOPPED",
            _ => "STARTING"
        };

        return new StatusSnapshot(
            connection,
            connectionText,
            latest is null ? "NO SESSION" : latest.SessionType.ToString(),
            latest?.TrackName ?? Display.Missing,
            latest?.CarName ?? Display.Missing,
            recording,
            recording ? $"RECORDING · {Telemetry.RecordedSampleCount}" : "NOT RECORDING",
            Telemetry.RecordedSampleCount,
            Telemetry.IsReplaying,
            Telemetry.ReplayName);
    }

    public void Dispose()
    {
        _timer.Stop();
        Telemetry.Dispose();
    }
}
