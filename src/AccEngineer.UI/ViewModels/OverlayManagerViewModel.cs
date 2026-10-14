using System.Collections.ObjectModel;
using System.Windows.Input;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Overlay;

namespace AccEngineer.UI.ViewModels;

/// <summary>One row in the editor's panel list: visibility + opacity for a panel.</summary>
public sealed class OverlayPanelRow : ObservableObject
{
    private readonly OverlayManager _manager;
    private bool _isVisible;
    private double _opacity;

    public OverlayPanelRow(OverlayManager manager, OverlayPanelInfo info)
    {
        _manager = manager;
        Id = info.Id;
        Title = info.Title;
        _isVisible = info.IsVisible;
        _opacity = info.Opacity;
    }

    public string Id { get; }

    public string Title { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                _manager.SetPanelVisible(Id, value);
            }
        }
    }

    public double Opacity
    {
        get => _opacity;
        set
        {
            if (SetProperty(ref _opacity, value))
            {
                _manager.SetPanelOpacity(Id, value);
            }
        }
    }

    /// <summary>Refreshes the row from the manager without re-triggering a write.</summary>
    public void Sync(OverlayPanelInfo info)
    {
        _isVisible = info.IsVisible;
        _opacity = info.Opacity;
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Opacity));
    }
}

/// <summary>
/// Backs the overlay layout editor window. Wraps <see cref="OverlayManager"/> and
/// exposes profiles, named layouts, appearance settings and per-panel controls.
/// </summary>
public sealed class OverlayManagerViewModel : ObservableObject, IDisposable
{
    private readonly OverlayManager _manager;
    private bool _disposed;

    private string _selectedProfile;
    private string _selectedLayout;
    private string _newLayoutName = string.Empty;
    private string _newProfileName = string.Empty;
    private double _fontScale;
    private int _traceLength;
    private int _relativeRows;
    private bool _relativeShowGap;
    private bool _relativeShowName;
    private bool _isDemoMode;

    public OverlayManagerViewModel(OverlayManager manager)
    {
        _manager = manager;
        _manager.Changed += OnManagerChanged;

        _selectedProfile = manager.ActiveProfileName;
        _selectedLayout = manager.ActiveLayoutName;
        var appearance = manager.Appearance;
        _fontScale = appearance.FontScale;
        _traceLength = appearance.TraceLength;
        _relativeRows = appearance.RelativeRows;
        _relativeShowGap = appearance.RelativeShowGap;
        _relativeShowName = appearance.RelativeShowName;
        _isDemoMode = manager.IsDemoMode;

        RefreshPanels();

        ShowAllCommand = new RelayCommand(() => { _manager.ShowAll(); RefreshPanels(); });
        HideAllCommand = new RelayCommand(() => { _manager.HideAll(); RefreshPanels(); });
        ToggleEditCommand = new RelayCommand(() => { _manager.ToggleEditAll(); RefreshPanels(); });
        ResetLayoutCommand = new RelayCommand(() => { _manager.ResetToDefault(); RefreshPanels(); ReloadFromManager(); });
        SaveLayoutAsCommand = new RelayCommand(SaveLayoutAs, () => !string.IsNullOrWhiteSpace(_newLayoutName));
        DeleteLayoutCommand = new RelayCommand(() => { _manager.DeleteLayout(SelectedLayout); ReloadFromManager(); });
        AddProfileCommand = new RelayCommand(AddProfile, () => !string.IsNullOrWhiteSpace(_newProfileName));
        DeleteProfileCommand = new RelayCommand(() => { _manager.DeleteProfile(SelectedProfile); ReloadFromManager(); },
            () => _manager.ProfileNames.Count > 1);
        PinResolutionCommand = new RelayCommand(() => { _manager.PinCurrentResolution(); OnPropertyChanged(nameof(IsResolutionPinned)); });
        UnpinResolutionCommand = new RelayCommand(() => { _manager.UnpinCurrentResolution(); OnPropertyChanged(nameof(IsResolutionPinned)); });
    }

    public IReadOnlyList<string> Profiles => _manager.ProfileNames;

    public IReadOnlyList<string> Layouts => _manager.LayoutNames;

    public ObservableCollection<OverlayPanelRow> PanelRows { get; } = new();

    public string SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value) && value is not null)
            {
                _manager.SetProfile(value);
                ReloadFromManager();
            }
        }
    }

    public string SelectedLayout
    {
        get => _selectedLayout;
        set
        {
            if (SetProperty(ref _selectedLayout, value) && value is not null)
            {
                _manager.SetLayout(value);
                ReloadFromManager();
                RefreshPanels();
            }
        }
    }

    public string NewLayoutName
    {
        get => _newLayoutName;
        set
        {
            if (SetProperty(ref _newLayoutName, value))
            {
                (SaveLayoutAsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            if (SetProperty(ref _newProfileName, value))
            {
                (AddProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public double FontScale
    {
        get => _fontScale;
        set { if (SetProperty(ref _fontScale, value)) PushAppearance(); }
    }

    public int TraceLength
    {
        get => _traceLength;
        set { if (SetProperty(ref _traceLength, value)) PushAppearance(); }
    }

    public int RelativeRows
    {
        get => _relativeRows;
        set { if (SetProperty(ref _relativeRows, value)) PushAppearance(); }
    }

    public bool RelativeShowGap
    {
        get => _relativeShowGap;
        set { if (SetProperty(ref _relativeShowGap, value)) PushAppearance(); }
    }

    public bool RelativeShowName
    {
        get => _relativeShowName;
        set { if (SetProperty(ref _relativeShowName, value)) PushAppearance(); }
    }

    public bool IsDemoMode
    {
        get => _isDemoMode;
        set
        {
            if (SetProperty(ref _isDemoMode, value))
            {
                _manager.SetDemoMode(value);
                RefreshPanels();
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsResolutionPinned => _manager.IsResolutionPinned;

    public string ResolutionText => _manager.CurrentResolution;

    public string StatusText => _manager.StatusText;

    public string HotkeyText => _manager.HotkeysActive
        ? "Global hotkeys: Ctrl+Alt+O show/hide, Ctrl+Alt+E edit, Ctrl+Alt+M editor."
        : "Global hotkeys unavailable (registered elsewhere); use the buttons.";

    public ICommand ShowAllCommand { get; }

    public ICommand HideAllCommand { get; }

    public ICommand ToggleEditCommand { get; }

    public ICommand ResetLayoutCommand { get; }

    public ICommand SaveLayoutAsCommand { get; }

    public ICommand DeleteLayoutCommand { get; }

    public ICommand AddProfileCommand { get; }

    public ICommand DeleteProfileCommand { get; }

    public ICommand PinResolutionCommand { get; }

    public ICommand UnpinResolutionCommand { get; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _manager.Changed -= OnManagerChanged;
    }

    private void SaveLayoutAs()
    {
        _manager.SaveLayoutAs(_newLayoutName);
        NewLayoutName = string.Empty;
        ReloadFromManager();
        RefreshPanels();
    }

    private void AddProfile()
    {
        _manager.AddProfile(_newProfileName);
        NewProfileName = string.Empty;
        ReloadFromManager();
        RefreshPanels();
    }

    private void PushAppearance() => _manager.SetAppearance(new OverlayAppearance
    {
        FontScale = _fontScale,
        TraceLength = _traceLength,
        RelativeRows = _relativeRows,
        RelativeShowGap = _relativeShowGap,
        RelativeShowName = _relativeShowName
    });

    private void RefreshPanels()
    {
        // Rebuild rows from the manager; used after global commands so checkboxes match.
        PanelRows.Clear();
        foreach (var info in _manager.Panels)
        {
            PanelRows.Add(new OverlayPanelRow(_manager, info));
        }
    }

    /// <summary>Re-reads profile/layout/appearance from the manager after a switch.</summary>
    private void ReloadFromManager()
    {
        _selectedProfile = _manager.ActiveProfileName;
        _selectedLayout = _manager.ActiveLayoutName;
        var appearance = _manager.Appearance;
        _fontScale = appearance.FontScale;
        _traceLength = appearance.TraceLength;
        _relativeRows = appearance.RelativeRows;
        _relativeShowGap = appearance.RelativeShowGap;
        _relativeShowName = appearance.RelativeShowName;
        _isDemoMode = _manager.IsDemoMode;

        OnPropertyChanged(nameof(Profiles));
        OnPropertyChanged(nameof(Layouts));
        OnPropertyChanged(nameof(SelectedProfile));
        OnPropertyChanged(nameof(SelectedLayout));
        OnPropertyChanged(nameof(FontScale));
        OnPropertyChanged(nameof(TraceLength));
        OnPropertyChanged(nameof(RelativeRows));
        OnPropertyChanged(nameof(RelativeShowGap));
        OnPropertyChanged(nameof(RelativeShowName));
        OnPropertyChanged(nameof(IsDemoMode));
        OnPropertyChanged(nameof(IsResolutionPinned));
        OnPropertyChanged(nameof(StatusText));
        (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void OnManagerChanged()
    {
        // Cheap notifications only; avoid rebuilding rows mid-drag.
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsResolutionPinned));
    }
}
