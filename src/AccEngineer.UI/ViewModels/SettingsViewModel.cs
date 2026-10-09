using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.ViewModels;

/// <summary>
/// Operator settings. Values persist under <c>%AppData%</c> so they stay out of
/// the source tree. AI fields are shown honestly as "not configured" until the
/// AI provider milestone lands; report generation and copy do not depend on them.
/// </summary>
public sealed class SettingsViewModel : PageViewModel
{
    private readonly MainViewModel _shell;
    private int _poll;
    private string _recordings;
    private string _reports;
    private string _status = string.Empty;

    public SettingsViewModel(MainViewModel shell)
        : base("Settings")
    {
        _shell = shell;
        var options = shell.Options;
        _poll = options.PollIntervalMs;
        _recordings = options.RecordingsDirectory;
        _reports = options.ReportsDirectory;

        SaveCommand = new RelayCommand(Save);
        OpenRecordingsCommand = new RelayCommand(() => Open(RecordingsDirectory));
        OpenReportsCommand = new RelayCommand(() => Open(ReportsDirectory));
    }

    public int PollIntervalMs
    {
        get => _poll;
        set => SetProperty(ref _poll, value);
    }

    public string RecordingsDirectory
    {
        get => _recordings;
        set => SetProperty(ref _recordings, value);
    }

    public string ReportsDirectory
    {
        get => _reports;
        set => SetProperty(ref _reports, value);
    }

    public bool IsAiConfigured => _shell.Options.IsAiConfigured;

    public string AiProviderText => _shell.Options.AiProvider ?? "not configured";

    public string AiModelText => _shell.Options.AiModel ?? Display.Missing;

    public string AiEndpointText => _shell.Options.AiEndpoint ?? Display.Missing;

    public string AiStatusText => IsAiConfigured
        ? $"{_shell.Options.AiProvider} · {_shell.Options.AiModel ?? "default model"}"
        : "Not configured — reports and copy work fully offline.";

    public string SettingsPathText => AppSettings.SettingsPath;

    public ICommand SaveCommand { get; }

    public ICommand OpenRecordingsCommand { get; }

    public ICommand OpenReportsCommand { get; }

    public string StatusText
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public override void Refresh()
    {
        OnPropertyChanged(nameof(IsAiConfigured));
        OnPropertyChanged(nameof(AiStatusText));
        OnPropertyChanged(nameof(AiProviderText));
        OnPropertyChanged(nameof(AiModelText));
        OnPropertyChanged(nameof(AiEndpointText));
    }

    private void Save()
    {
        var options = _shell.Options;
        options.PollIntervalMs = Math.Clamp(PollIntervalMs, 10, 2000);
        if (!string.IsNullOrWhiteSpace(RecordingsDirectory)) options.RecordingsDirectory = RecordingsDirectory.Trim();
        if (!string.IsNullOrWhiteSpace(ReportsDirectory)) options.ReportsDirectory = ReportsDirectory.Trim();

        PollIntervalMs = options.PollIntervalMs;
        RecordingsDirectory = options.RecordingsDirectory;
        ReportsDirectory = options.ReportsDirectory;

        try
        {
            options.Save();
            StatusText = $"Saved to {AppSettings.SettingsPath}";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not save settings: {ex.Message}";
        }
    }

    private static void Open(string directory)
    {
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }
}
