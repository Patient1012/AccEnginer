using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using AccEngineer.AI;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Overlay;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.ViewModels;

/// <summary>
/// Operator settings. Values persist under <c>%AppData%</c> so they stay out of
/// the source tree. AI provider/model/endpoint are editable here; the API key is
/// never stored and is read from environment variables at use time. Report
/// generation and copy do not depend on AI configuration.
/// </summary>
public sealed class SettingsViewModel : PageViewModel
{
    private readonly MainViewModel _shell;
    private int _poll;
    private string _recordings;
    private string _reports;
    private string _aiProvider = string.Empty;
    private string _aiModel = string.Empty;
    private string _aiEndpoint = string.Empty;
    private string _status = string.Empty;

    public SettingsViewModel(MainViewModel shell)
        : base("Settings")
    {
        _shell = shell;
        var options = shell.Options;
        _poll = options.PollIntervalMs;
        _recordings = options.RecordingsDirectory;
        _reports = options.ReportsDirectory;
        _aiProvider = options.AiProvider ?? string.Empty;
        _aiModel = options.AiModel ?? string.Empty;
        _aiEndpoint = options.AiEndpoint ?? string.Empty;

        SaveCommand = new RelayCommand(Save);
        OpenRecordingsCommand = new RelayCommand(() => Open(RecordingsDirectory));
        OpenReportsCommand = new RelayCommand(() => Open(ReportsDirectory));

        ToggleOverlayCommand = new RelayCommand(() =>
        {
            _shell.Overlays.ToggleAll();
            RefreshOverlayStatus();
        });
        EditOverlayCommand = new RelayCommand(() =>
        {
            _shell.Overlays.ToggleEditAll();
            RefreshOverlayStatus();
        });
        ResetOverlayCommand = new RelayCommand(() =>
        {
            _shell.Overlays.ResetToDefault();
            RefreshOverlayStatus();
        });
        ToggleDemoOverlayCommand = new RelayCommand(() =>
        {
            // Feed the panels synthetic data so the layout can be tuned without ACC.
            _shell.Overlays.SetDemoMode(!_shell.Overlays.IsDemoMode);
            RefreshOverlayStatus();
        });
        ConfigureOverlayCommand = new RelayCommand(() => _shell.OpenOverlayManager());
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

    public string AiProvider
    {
        get => _aiProvider;
        set
        {
            if (SetProperty(ref _aiProvider, value))
            {
                RefreshAiStatus();
            }
        }
    }

    public string AiModel
    {
        get => _aiModel;
        set
        {
            if (SetProperty(ref _aiModel, value))
            {
                RefreshAiStatus();
            }
        }
    }

    public string AiEndpoint
    {
        get => _aiEndpoint;
        set
        {
            if (SetProperty(ref _aiEndpoint, value))
            {
                RefreshAiStatus();
            }
        }
    }

    public bool IsAiConfigured
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_aiModel))
            {
                return false;
            }

            return AnalysisProviderFactory.ResolveEndpoint(new AiProviderOptions
            {
                Provider = _aiProvider,
                Endpoint = _aiEndpoint
            }) is not null;
        }
    }

    public string AiStatusText
    {
        get
        {
            if (IsAiConfigured)
            {
                var provider = string.IsNullOrWhiteSpace(_aiProvider) ? "openai-compatible" : _aiProvider.Trim();
                return $"AI configured: {provider} / {_aiModel.Trim()}. The key is read from ACCENGINEER_AI_API_KEY (or OPENROUTER_API_KEY).";
            }

            return string.IsNullOrWhiteSpace(_aiModel)
                ? "Not configured - add a model and an optional endpoint. Reports and copy work fully offline."
                : "Missing a resolvable endpoint - use openrouter, ollama, lmstudio, or a full base URL. Reports and copy work fully offline.";
        }
    }

    public string SettingsPathText => AppSettings.SettingsPath;

    public string OverlayStatusText
    {
        get
        {
            var overlay = _shell.Overlays;
            var hotkeys = overlay.HotkeysActive
                ? "Global hotkeys: Ctrl+Alt+O show/hide, Ctrl+Alt+E toggle edit, Ctrl+Alt+M editor."
                : "Global hotkeys are unavailable (already registered by another app); use the buttons.";
            return $"{overlay.StatusText}. {hotkeys}";
        }
    }

    public ICommand SaveCommand { get; }

    public ICommand OpenRecordingsCommand { get; }

    public ICommand OpenReportsCommand { get; }

    public ICommand ToggleOverlayCommand { get; }

    public ICommand EditOverlayCommand { get; }

    public ICommand ResetOverlayCommand { get; }

    public ICommand ToggleDemoOverlayCommand { get; }

    public ICommand ConfigureOverlayCommand { get; }

    /// <summary>Panels and their current visibility, for the per-panel toggles.</summary>
    public IReadOnlyList<OverlayPanelInfo> OverlayPanels => _shell.Overlays.Panels;

    public string StatusText
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public override void Refresh()
    {
        _poll = _shell.Options.PollIntervalMs;
        _recordings = _shell.Options.RecordingsDirectory;
        _reports = _shell.Options.ReportsDirectory;
        _aiProvider = _shell.Options.AiProvider ?? string.Empty;
        _aiModel = _shell.Options.AiModel ?? string.Empty;
        _aiEndpoint = _shell.Options.AiEndpoint ?? string.Empty;

        OnPropertyChanged(nameof(PollIntervalMs));
        OnPropertyChanged(nameof(RecordingsDirectory));
        OnPropertyChanged(nameof(ReportsDirectory));
        OnPropertyChanged(nameof(AiProvider));
        OnPropertyChanged(nameof(AiModel));
        OnPropertyChanged(nameof(AiEndpoint));
        OnPropertyChanged(nameof(IsAiConfigured));
        OnPropertyChanged(nameof(AiStatusText));
        RefreshOverlayStatus();
    }

    private void RefreshOverlayStatus()
    {
        OnPropertyChanged(nameof(OverlayStatusText));
        OnPropertyChanged(nameof(OverlayPanels));
    }

    private void Save()
    {
        var options = _shell.Options;
        options.PollIntervalMs = Math.Clamp(PollIntervalMs, 10, 2000);
        if (!string.IsNullOrWhiteSpace(RecordingsDirectory)) options.RecordingsDirectory = RecordingsDirectory.Trim();
        if (!string.IsNullOrWhiteSpace(ReportsDirectory)) options.ReportsDirectory = ReportsDirectory.Trim();

        options.AiProvider = string.IsNullOrWhiteSpace(AiProvider) ? null : AiProvider.Trim();
        options.AiModel = string.IsNullOrWhiteSpace(AiModel) ? null : AiModel.Trim();
        options.AiEndpoint = string.IsNullOrWhiteSpace(AiEndpoint) ? null : AiEndpoint.Trim();

        PollIntervalMs = options.PollIntervalMs;
        RecordingsDirectory = options.RecordingsDirectory;
        ReportsDirectory = options.ReportsDirectory;

        RefreshAiStatus();

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

    private void RefreshAiStatus()
    {
        OnPropertyChanged(nameof(IsAiConfigured));
        OnPropertyChanged(nameof(AiStatusText));
    }

    private static void Open(string directory)
    {
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }
}