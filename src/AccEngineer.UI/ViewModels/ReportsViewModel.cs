using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services;
using Microsoft.Win32;

namespace AccEngineer.UI.ViewModels;

/// <summary>
/// Generates, lists, previews and copies deterministic Markdown reports. Report
/// generation never depends on an AI provider, so this page is fully usable
/// while AI remains unconfigured.
/// </summary>
public sealed class ReportsViewModel : PageViewModel
{
    private readonly MainViewModel _shell;
    private ReportEntry? _selected;
    private string _preview = string.Empty;
    private string _status = "Generate a report from the current session, or pick a file below.";
    private string? _lastPath;

    public ReportsViewModel(MainViewModel shell)
        : base("Reports")
    {
        _shell = shell;
        GenerateCommand = new RelayCommand(Generate);
        RefreshCommand = new RelayCommand(Load);
        CopyCommand = new RelayCommand(Copy, () => HasSelection);
        ExportCommand = new RelayCommand(Export, () => HasSelection);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        DeleteCommand = new RelayCommand(Delete, () => HasSelection);
    }

    public ObservableCollection<ReportEntry> Reports { get; } = new();

    public ICommand GenerateCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand CopyCommand { get; }

    public ICommand ExportCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public ICommand DeleteCommand { get; }

    public ReportEntry? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            LoadPreview();
            OnPropertyChanged(nameof(HasSelection));
            RaiseCanExecute();
        }
    }

    public bool HasSelection => Selected is not null;

    public bool HasReports => Reports.Count > 0;

    public string Preview
    {
        get => _preview;
        private set => SetProperty(ref _preview, value);
    }

    public string StatusText
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public override void Refresh()
    {
    }

    public void Load()
    {
        Reports.Clear();
        var directory = _shell.Options.ReportsDirectory;
        if (Directory.Exists(directory))
        {
            var entries = Directory.EnumerateFiles(directory, "*.report.md")
                .Concat(Directory.EnumerateFiles(directory, "*.live.md"))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();

            foreach (var md in entries)
            {
                var json = Path.ChangeExtension(md, ".json");
                Reports.Add(new ReportEntry(md, json, File.GetLastWriteTimeUtc(md)));
            }
        }

        OnPropertyChanged(nameof(HasReports));

        Selected = Reports.FirstOrDefault(r => r.MarkdownPath == _lastPath) ?? Reports.FirstOrDefault();
        if (Reports.Count == 0)
        {
            Preview = string.Empty;
            StatusText = "No reports yet. Generate one from a live session or a recording.";
        }
    }

    private void Generate()
    {
        var result = _shell.GenerateReport();
        if (result is null)
        {
            StatusText = "No telemetry captured yet — start a session or replay a recording first.";
            return;
        }

        _lastPath = Path.Combine(_shell.Options.ReportsDirectory, result.BaseName + ".report.md");
        Load();
        Selected = Reports.FirstOrDefault(r => r.MarkdownPath == _lastPath) ?? Reports.FirstOrDefault();
        StatusText = $"Generated {Path.GetFileName(_lastPath)}";
    }

    private void Copy()
    {
        if (Selected is null) return;
        try
        {
            Clipboard.SetText(Preview);
            StatusText = "Copied to clipboard — paste into ChatGPT to discuss.";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not access the clipboard: {ex.Message}";
        }
    }

    private void Export()
    {
        if (Selected is null) return;
        var dialog = new SaveFileDialog
        {
            FileName = Selected.FileName,
            Filter = "Markdown (*.md)|*.md|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, Preview);
            StatusText = $"Saved copy to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not save the copy: {ex.Message}";
        }
    }

    private void OpenFolder()
    {
        var dir = _shell.Options.ReportsDirectory;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }

    private void Delete()
    {
        if (Selected is null) return;
        if (MessageBox.Show(
                $"Delete {Selected.FileName}?",
                "Delete report",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            File.Delete(Selected.MarkdownPath);
            if (File.Exists(Selected.JsonPath)) File.Delete(Selected.JsonPath);
            _lastPath = null;
            Load();
            StatusText = "Report deleted.";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not delete the report: {ex.Message}";
        }
    }

    private void LoadPreview()
    {
        if (Selected is null)
        {
            Preview = string.Empty;
            return;
        }

        try
        {
            Preview = File.ReadAllText(Selected.MarkdownPath);
        }
        catch (Exception ex)
        {
            Preview = $"Could not read {Selected.FileName}: {ex.Message}";
        }
    }

    private void RaiseCanExecute()
    {
        (CopyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
