using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using AccEngineer.AI;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Overlay;
using AccEngineer.UI.Services;
using AccEngineer.UI.Themes;

namespace AccEngineer.UI.ViewModels;

/// <summary>
/// Operator settings. Values persist under <c>%AppData%</c> so they stay out of
/// the source tree. AI provider/model/endpoint are editable here; the API key is
/// never stored and is read from environment variables at use time. Report
/// generation and copy do not depend on AI configuration.
///
/// The theme is the one setting that applies as soon as it is picked, because
/// judging a theme on a static screenshot is the whole point of a selector.
/// </summary>
public sealed class SettingsViewModel : PageViewModel, IDisposable
{
    private readonly MainViewModel _shell;
    private int _poll;
    private string _recordings;
    private string _reports;
    private string _aiProvider = string.Empty;
    private string _aiModel = string.Empty;
    private string _aiEndpoint = string.Empty;
    private string _status = string.Empty;
    private ThemeDefinition _selectedTheme = ThemeCatalog.Default;
    private bool _applyingTheme;
    private bool _testDataMode;
    private bool _broadcastingEnabled;
    private int _broadcastingPort;
    private string _broadcastingPassword = string.Empty;

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
        _testDataMode = options.TestDataMode;
        _broadcastingEnabled = options.BroadcastingEnabled;
        _broadcastingPort = options.BroadcastingPort;
        _broadcastingPassword = options.BroadcastingPassword;

        // Show what is actually in effect, which is the resolved value rather than
        // whatever the file happened to contain.
        _selectedTheme = ThemeCatalog.TryResolve(options.Theme, out var theme) ? theme : ThemeCatalog.Default;
        ThemeManager.Changed += OnThemeChanged;

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

    /// <summary>
    /// Runs the whole app on generated telemetry instead of ACC shared memory.
    /// Applies immediately and is persisted. The label below reflects the provider
    /// actually in effect, not just this preference.
    /// </summary>
    public bool TestDataMode
    {
        get => _testDataMode;
        set
        {
            if (!SetProperty(ref _testDataMode, value)) return;
            ApplyTestDataMode(value);
        }
    }

    /// <summary>Honest description of the provider the app is really using right now.</summary>
    public string TestDataStatusText => _shell.Telemetry.IsTestData
        ? "Test Data: ON — the app is showing generated telemetry; no simulator is connected."
        : "Test Data: OFF — reading ACC shared memory.";

    /// <summary>
    /// Connects to the ACC UDP Broadcasting API for competitor data (names, race
    /// numbers, teams, official gaps). Applies immediately and is persisted.
    /// </summary>
    public bool BroadcastingEnabled
    {
        get => _broadcastingEnabled;
        set
        {
            if (!SetProperty(ref _broadcastingEnabled, value)) return;
            ApplyBroadcastingSettings();
        }
    }

    /// <summary>Must match <c>udpListenerPort</c> in ACC's broadcasting.json.</summary>
    public int BroadcastingPort
    {
        get => _broadcastingPort;
        set => SetProperty(ref _broadcastingPort, value);
    }

    /// <summary>Must match <c>connectionPassword</c> in ACC's broadcasting.json.</summary>
    public string BroadcastingPassword
    {
        get => _broadcastingPassword;
        set => SetProperty(ref _broadcastingPassword, value);
    }

    public string BroadcastingStatusText => $"Broadcasting: {_shell.Telemetry.BroadcastingStatusText}.";

    /// <summary>Every known theme, in the order the selector should list them.</summary>
    public IReadOnlyList<ThemeDefinition> Themes => ThemeCatalog.All;

    /// <summary>The theme currently in effect. Setting it applies and persists immediately.</summary>
    public ThemeDefinition SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (!SetProperty(ref _selectedTheme, value) || value is null)
            {
                return;
            }

            ApplyTheme(value);
        }
    }

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
        _selectedTheme = ThemeCatalog.TryResolve(_shell.Options.Theme, out var theme) ? theme : ThemeCatalog.Default;
        _testDataMode = _shell.Options.TestDataMode;
        _broadcastingEnabled = _shell.Options.BroadcastingEnabled;
        _broadcastingPort = _shell.Options.BroadcastingPort;
        _broadcastingPassword = _shell.Options.BroadcastingPassword;

        OnPropertyChanged(nameof(PollIntervalMs));
        OnPropertyChanged(nameof(RecordingsDirectory));
        OnPropertyChanged(nameof(ReportsDirectory));
        OnPropertyChanged(nameof(AiProvider));
        OnPropertyChanged(nameof(AiModel));
        OnPropertyChanged(nameof(AiEndpoint));
        OnPropertyChanged(nameof(IsAiConfigured));
        OnPropertyChanged(nameof(AiStatusText));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(TestDataMode));
        OnPropertyChanged(nameof(TestDataStatusText));
        OnPropertyChanged(nameof(BroadcastingEnabled));
        OnPropertyChanged(nameof(BroadcastingPort));
        OnPropertyChanged(nameof(BroadcastingPassword));
        OnPropertyChanged(nameof(BroadcastingStatusText));
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
        options.Theme = SelectedTheme.Id.ToString();

        ApplyBroadcastingSettings(persist: false);

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

    /// <summary>
    /// Pushes the broadcasting preference/port/password to the telemetry service and
    /// (re)creates its client so an edited port or password takes effect immediately.
    /// </summary>
    private void ApplyBroadcastingSettings(bool persist = true)
    {
        var options = _shell.Options;
        options.BroadcastingEnabled = _broadcastingEnabled;
        options.BroadcastingPort = Math.Clamp(_broadcastingPort, 1, 65535);
        options.BroadcastingPassword = _broadcastingPassword ?? string.Empty;
        _broadcastingPort = options.BroadcastingPort;

        if (_shell.Telemetry.IsBroadcastingEnabled) _shell.Telemetry.SetBroadcastingEnabled(false);
        if (_broadcastingEnabled) _shell.Telemetry.SetBroadcastingEnabled(true);

        OnPropertyChanged(nameof(BroadcastingStatusText));

        if (!persist) return;

        try
        {
            options.Save();
            StatusText = _broadcastingEnabled
                ? $"Broadcasting enabled on port {options.BroadcastingPort}; saved."
                : "Broadcasting disabled; saved.";
        }
        catch (Exception ex)
        {
            StatusText = $"Broadcasting setting changed, but it could not be saved: {ex.Message}";
        }
    }

    private void ApplyTestDataMode(bool enabled)
    {
        _shell.Telemetry.SetTestDataMode(enabled);
        _shell.Options.TestDataMode = enabled;

        try
        {
            _shell.Options.Save();
            StatusText = enabled
                ? "Test data mode enabled; saved. The app is now running on generated telemetry."
                : "Test data mode disabled; saved. The app is reading ACC shared memory again.";
        }
        catch (Exception ex)
        {
            StatusText = $"Test data mode {(enabled ? "enabled" : "disabled")}, but it could not be saved: {ex.Message}";
        }

        OnPropertyChanged(nameof(TestDataStatusText));
    }

    private void ApplyTheme(ThemeDefinition definition)
    {
        _applyingTheme = true;
        try
        {
            ThemeManager.Apply(definition);
            _shell.Options.Theme = definition.Id.ToString();
            StatusText = $"Theme: {definition.Name}.";
        }
        finally
        {
            _applyingTheme = false;
        }

        try
        {
            _shell.Options.Save();
            StatusText = $"Theme: {definition.Name}. Saved to {AppSettings.SettingsPath}";
        }
        catch (Exception ex)
        {
            StatusText = $"Theme: {definition.Name}, but it could not be saved: {ex.Message}";
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        // Only reached when something other than this selector changed the theme;
        // re-sync the highlighted item without writing the settings file back.
        if (_applyingTheme || ThemeManager.CurrentDefinition == _selectedTheme)
        {
            return;
        }

        _selectedTheme = ThemeManager.CurrentDefinition;
        OnPropertyChanged(nameof(SelectedTheme));
    }

    public void Dispose()
    {
        ThemeManager.Changed -= OnThemeChanged;
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
