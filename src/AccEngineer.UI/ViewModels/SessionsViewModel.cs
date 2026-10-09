using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.ViewModels;

/// <summary>Browsable library of JSONL recordings: replay, report, or delete.</summary>
public sealed class SessionsViewModel : PageViewModel
{
    private readonly MainViewModel _shell;
    private RecordingInfo? _selected;
    private string _status = "Recordings are listed from the configured folder.";

    public SessionsViewModel(MainViewModel shell)
        : base("Sessions")
    {
        _shell = shell;
        RefreshCommand = new RelayCommand(Load);
        ReplayCommand = new RelayCommand(Replay, () => HasSelection);
        StopReplayCommand = new RelayCommand(() => _shell.Telemetry.StopReplay(), () => _shell.Telemetry.IsReplaying);
        ReportCommand = new RelayCommand(Report, () => HasSelection);
        DeleteCommand = new RelayCommand(Delete, () => HasSelection);
        OpenFolderCommand = new RelayCommand(OpenFolder);
    }

    public ObservableCollection<RecordingInfo> Recordings { get; } = new();

    public ICommand RefreshCommand { get; }

    public ICommand ReplayCommand { get; }

    public ICommand StopReplayCommand { get; }

    public ICommand ReportCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public RecordingInfo? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            OnPropertyChanged(nameof(HasSelection));
            RaiseCanExecute();
        }
    }

    public bool HasSelection => Selected?.IsReadable == true;

    public bool HasRecordings => Recordings.Count > 0;

    public string FolderText => _shell.Options.RecordingsDirectory;

    public string StatusText
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public override void Refresh()
    {
        OnPropertyChanged(nameof(FolderText));
    }

    public void Load()
    {
        Recordings.Clear();
        foreach (var info in _shell.Library.List()) Recordings.Add(info);

        OnPropertyChanged(nameof(HasRecordings));
        OnPropertyChanged(nameof(FolderText));

        Selected = Recordings.FirstOrDefault();
        if (Recordings.Count == 0)
        {
            StatusText = "No recordings found. Use Start Recording on the Overview page.";
        }
    }

    private void Replay()
    {
        if (Selected is null) return;
        try
        {
            var recording = _shell.Library.Load(Selected.FilePath);
            _shell.Telemetry.StartReplay(recording, Selected.FileName);
            _shell.NavigateTo(_shell.Live);
            StatusText = $"Replaying {Selected.FileName}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not load recording: {ex.Message}";
        }
    }

    private void Report()
    {
        if (Selected is null) return;
        try
        {
            var recording = _shell.Library.Load(Selected.FilePath);
            var result = _shell.GenerateReportFromRecording(recording);
            if (result is null)
            {
                StatusText = "Recording contains no samples.";
                return;
            }

            _shell.NavigateTo(_shell.ReportsPage);
            _shell.ReportsPage.Load();
            StatusText = $"Report generated from {Selected.FileName}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not build a report: {ex.Message}";
        }
    }

    private void Delete()
    {
        if (Selected is null) return;
        if (MessageBox.Show(
                $"Delete {Selected.FileName}?",
                "Delete recording",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _shell.Library.Delete(Selected.FilePath);
            Load();
            StatusText = "Recording deleted.";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not delete recording: {ex.Message}";
        }
    }

    private void OpenFolder()
    {
        var dir = _shell.Options.RecordingsDirectory;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }

    private void RaiseCanExecute()
    {
        (ReplayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
