using System.Collections.ObjectModel;
using System.Windows.Input;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.ViewModels;

/// <summary>Default page: current session at a glance, recent events, and actions.</summary>
public sealed class OverviewViewModel : PageViewModel
{
    private readonly MainViewModel _shell;

    public OverviewViewModel(MainViewModel shell)
        : base("Overview")
    {
        _shell = shell;
        ToggleRecordingCommand = new RelayCommand(() => _shell.Telemetry.ToggleRecording());
        GenerateReportCommand = new RelayCommand(() => _shell.GenerateReportAndShow());
        GoToAnalysisCommand = new RelayCommand(() => _shell.NavigateTo(_shell.Analysis));
        GoToLiveCommand = new RelayCommand(() => _shell.NavigateTo(_shell.Live));
    }

    public LiveDataViewModel Live => _shell.Live;

    public ObservableCollection<SessionEvent> RecentEvents => _shell.EventLog.Events;

    public ICommand ToggleRecordingCommand { get; }

    public ICommand GenerateReportCommand { get; }

    public ICommand GoToAnalysisCommand { get; }

    public ICommand GoToLiveCommand { get; }

    public string RecordingButtonText => _shell.Status.IsRecording ? "Stop Recording" : "Start Recording";

    public bool HasEvents => RecentEvents.Count > 0;

    public string DeltaText { get; private set; } = Display.Missing;

    public string DeltaCaption { get; private set; } = "no reference comparison";

    public string StatusHint => _shell.Status.Connection switch
    {
        ConnectionState.Connected => "Live telemetry streaming.",
        ConnectionState.Replay => "Replaying a recording (not live).",
        ConnectionState.WaitingForAcc => "Start ACC and enter a session to see live data.",
        ConnectionState.Error => "Telemetry reader reported an error — see recent events.",
        _ => "Starting telemetry reader…"
    };

    public override void Refresh()
    {
        Live.Refresh();
        OnPropertyChanged(nameof(HasEvents));
        OnPropertyChanged(nameof(RecordingButtonText));
        OnPropertyChanged(nameof(StatusHint));
        UpdateDelta();
    }

    private void UpdateDelta()
    {
        var report = _shell.LastReport;
        if (report is null)
        {
            DeltaText = Display.Missing;
            DeltaCaption = "no reference comparison";
        }
        else
        {
            var reference = report.Report.Comparison?.ReferenceLapNumber;
            var latest = report.Report.Comparison?.Laps
                .Where(l => l.TotalDeltaSeconds is not null)
                .OrderByDescending(l => l.LapNumber)
                .FirstOrDefault();

            if (latest?.TotalDeltaSeconds is { } delta)
            {
                DeltaText = Display.Delta(delta);
                DeltaCaption = $"lap {latest.LapNumber} vs lap {reference}";
            }
            else
            {
                DeltaText = Display.Missing;
                DeltaCaption = reference is null ? "no valid reference lap" : $"reference lap {reference}";
            }
        }

        OnPropertyChanged(nameof(DeltaText));
        OnPropertyChanged(nameof(DeltaCaption));
    }
}
