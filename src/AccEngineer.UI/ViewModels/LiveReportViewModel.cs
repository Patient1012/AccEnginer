using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using AccEngineer.Pipeline;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.ViewModels;

/// <summary>One live event with its deterministic report and optional interpretation.</summary>
public sealed class LiveFindingRowViewModel : ObservableObject
{
    private readonly Action<LiveEventItem>? _export;

    public LiveFindingRowViewModel(LiveEventItem item, Action<LiveEventItem>? export = null)
    {
        Item = item;
        _export = export;

        var finding = item.Finding;
        Category = finding.Category.ToString();
        Severity = finding.Severity.ToString();
        Confidence = finding.Confidence.ToString();
        Observation = finding.Observation;
        LocalInterpretation = finding.Interpretation;
        LapNumber = finding.LapNumber;
        Location = finding.ProgressMeters is { } progress
            ? string.Format(CultureInfo.InvariantCulture, "{0:0} m", progress)
            : string.Empty;
        Header = finding.LapNumber is { } lap ? $"Lap {lap} · {Category}" : Category;
        CoalescedText = item.CoalescedCount > 1
            ? string.Format(CultureInfo.InvariantCulture, "x{0}", item.CoalescedCount)
            : string.Empty;
        EvidenceText = item.EvidenceSampleCount > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0} sample(s)", item.EvidenceSampleCount)
            : "no evidence window";

        var ai = item.Report?.Interpretation;
        AiStatus = ai?.Status ?? "pending";
        AiObservation = ai?.Observation;
        AiPossibleCause = ai?.PossibleCause;
        AiTest = ai?.SuggestedTest;
        AiConfidence = ai?.Confidence is { } confidence
            ? confidence.ToString("0.00", CultureInfo.InvariantCulture)
            : null;
        AiNote = ai is null
            ? "Interpretation pending."
            : ai.Status == "ok"
                ? string.Empty
                : $"AI interpretation not available ({ai.Status}). The deterministic report is unaffected.";

        ExportCommand = new RelayCommand(Export, () => item.Report is not null);
    }

    public LiveEventItem Item { get; }

    public string Header { get; }

    public string Category { get; }

    public string Severity { get; }

    public string Confidence { get; }

    public string Observation { get; }

    public string? LocalInterpretation { get; }

    public int? LapNumber { get; }

    public string Location { get; }

    public string CoalescedText { get; }

    public bool HasCoalesced => !string.IsNullOrEmpty(CoalescedText);

    public string EvidenceText { get; }

    public string AiStatus { get; }

    public string? AiObservation { get; }

    public string? AiPossibleCause { get; }

    public string? AiTest { get; }

    public string? AiConfidence { get; }

    public string AiNote { get; }

    public bool HasAiNote => !string.IsNullOrEmpty(AiNote);

    public bool HasAiResult => !string.IsNullOrEmpty(AiObservation);

    public bool HasAiPossibleCause => !string.IsNullOrEmpty(AiPossibleCause);

    public bool HasAiTest => !string.IsNullOrEmpty(AiTest);

    public bool HasLocalInterpretation => !string.IsNullOrEmpty(LocalInterpretation);

    public ICommand ExportCommand { get; }

    private void Export() => _export?.Invoke(Item);
}

/// <summary>
/// Live Events page. Shows the queue of significant findings detected during a
/// session, coalesced repeats, and each finding's deterministic report. Reports
/// are always available; AI interpretation is optional, rate-limited and never
/// blocks the collector.
/// </summary>
public sealed class LiveReportViewModel : PageViewModel
{
    private readonly MainViewModel _shell;
    private readonly LiveReportService _service;
    private volatile bool _pendingRefresh;

    public LiveReportViewModel(MainViewModel shell)
        : base("Live Events")
    {
        _shell = shell;
        _service = new LiveReportService(shell.Options, shell.Telemetry, shell.EventLog);
        _service.Changed += () => _pendingRefresh = true;

        ExportSummaryCommand = new RelayCommand(ExportSummary);
        ClearCommand = new RelayCommand(Clear);
    }

    public ObservableCollection<LiveFindingRowViewModel> Findings { get; } = new();

    public ICommand ExportSummaryCommand { get; }

    public ICommand ClearCommand { get; }

    public bool IsAiConfigured => _service.IsAiConfigured;

    public string AiNote => IsAiConfigured
        ? "An AI provider is configured. New findings are interpreted automatically in the background, rate-limited and coalesced; the collector and deterministic reports never wait on a response."
        : "No AI provider configured. Live findings and their deterministic reports still work; configure a provider in Settings to add possible-cause interpretations.";

    public string PendingText { get; private set; } = "0";

    public string AcceptedText { get; private set; } = "0";

    public string CoalescedText { get; private set; } = "0";

    public string DroppedText { get; private set; } = "0";

    public string StatusText { get; private set; } = "No live findings yet. Complete a lap in ACC to detect significant events.";

    public bool HasFindings => Findings.Count > 0;

    public override void Refresh()
    {
        _service.EnsureProvider();
        _service.Poll();

        if (_pendingRefresh)
        {
            _pendingRefresh = false;
        }

        UpdateCounters();
        RefreshFindings();
    }

    private void UpdateCounters()
    {
        var pipeline = _service.Pipeline;
        PendingText = pipeline.PendingCount.ToString(CultureInfo.InvariantCulture);
        AcceptedText = pipeline.AcceptedCount.ToString(CultureInfo.InvariantCulture);
        CoalescedText = pipeline.CoalescedCount.ToString(CultureInfo.InvariantCulture);
        DroppedText = pipeline.DroppedCount.ToString(CultureInfo.InvariantCulture);
        StatusText = HasFindings
            ? $"{Findings.Count} live finding(s) reported."
            : "No live findings yet. Complete a lap in ACC to detect significant events.";

        OnPropertyChanged(nameof(PendingText));
        OnPropertyChanged(nameof(AcceptedText));
        OnPropertyChanged(nameof(CoalescedText));
        OnPropertyChanged(nameof(DroppedText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsAiConfigured));
        OnPropertyChanged(nameof(AiNote));
    }

    private void RefreshFindings()
    {
        var history = _service.Pipeline.History;
        if (Findings.Count == history.Count
            && Findings.Select(f => f.Item).SequenceEqual(history.Reverse()))
        {
            return;
        }

        Findings.Clear();
        foreach (var item in history.Reverse())
        {
            Findings.Add(new LiveFindingRowViewModel(item, ExportFinding));
        }

        OnPropertyChanged(nameof(HasFindings));
    }

    private void ExportFinding(LiveEventItem item)
    {
        try
        {
            var files = _service.ExportLiveReport(item);
            _shell.EventLog.Enqueue(new SessionEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Kind = SessionEventKind.Report,
                Severity = EventSeverity.Success,
                Message = $"Live report saved: {System.IO.Path.GetFileName(files.MarkdownPath)}"
            });
        }
        catch (Exception ex)
        {
            _shell.EventLog.Enqueue(new SessionEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Kind = SessionEventKind.Report,
                Severity = EventSeverity.Error,
                Message = $"Failed to save live report: {ex.Message}"
            });
        }
    }

    private void ExportSummary()
    {
        try
        {
            var files = _service.ExportSessionSummary();
            _shell.EventLog.Enqueue(new SessionEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Kind = SessionEventKind.Report,
                Severity = EventSeverity.Success,
                Message = $"Session summary saved: {System.IO.Path.GetFileName(files.MarkdownPath)}"
            });
        }
        catch (Exception ex)
        {
            _shell.EventLog.Enqueue(new SessionEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Kind = SessionEventKind.Report,
                Severity = EventSeverity.Error,
                Message = $"Failed to save session summary: {ex.Message}"
            });
        }
    }

    private void Clear()
    {
        _service.Reset();
        Findings.Clear();
        OnPropertyChanged(nameof(HasFindings));
        UpdateCounters();
    }
}
