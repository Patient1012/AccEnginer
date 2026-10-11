using System.Collections.ObjectModel;
using System.Windows.Input;
using AccEngineer.AI;
using AccEngineer.Analysis;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.ViewModels;

/// <summary>
/// Lap comparison and session events. Deterministic: it runs the same
/// <see cref="LapAnalyzer"/> / <see cref="LapComparer"/> used offline, so its
/// results can be reproduced from a recording. AI interpretation is optional and
/// per-finding; it never runs automatically and never blocks the deterministic
/// analysis.
/// </summary>
public sealed class AnalysisViewModel : PageViewModel
{
    private readonly MainViewModel _shell;
    private SessionLapAnalysis? _session;
    private LapTimeline? _reference;
    private IAnalysisProvider _provider;

    public AnalysisViewModel(MainViewModel shell)
        : base("Analysis")
    {
        _shell = shell;
        _provider = BuildProvider();
        AnalyseCommand = new RelayCommand(Analyse);
    }

    public ObservableCollection<LapRowViewModel> Laps { get; } = new();

    public ObservableCollection<LapDeltaRowViewModel> Deltas { get; } = new();

    public ObservableCollection<FindingRowViewModel> Findings { get; } = new();

    public ICommand AnalyseCommand { get; }

    public string ReferenceText { get; private set; } = Display.Missing;

    public string StatusText { get; private set; } = "No analysis yet. Drive or replay a session, then select Analyse.";

    public string DataQualityText { get; private set; } = Display.Missing;

    public bool HasAnalysis { get; private set; }

    public bool HasDeltas => Deltas.Count > 0;

    public bool HasLaps => Laps.Count > 0;

    public bool HasFindings => Findings.Count > 0;

    public string FindingsSummary { get; private set; } = Display.Missing;

    public string EventNote =>
        "Driving findings are measured facts from braking, corner, throttle and tyre channels. " +
        "Any possible explanation is labelled and kept separate from the measurement.";

    public bool IsAiConfigured => _shell.Options.IsAiConfigured;

    public string AiNote => IsAiConfigured
        ? "An AI provider is configured. Use Interpret with AI on a finding to request one interpretation; only the selected finding's evidence window is sent."
        : "No AI provider configured. Findings are deterministic measurements and reports stay available offline. Configure a provider in Settings.";

    public ObservableCollection<SessionEvent> Events { get; } = new();

    public override void Refresh()
    {
        _provider = BuildProvider();
        OnPropertyChanged(nameof(IsAiConfigured));
        OnPropertyChanged(nameof(AiNote));
        RefreshEvents();
    }

    public void Analyse()
    {
        var samples = _shell.Telemetry.SessionSamplesSnapshot();
        if (samples.Count == 0)
        {
            StatusText = "No samples available yet. Start a session (or replay a recording) first.";
            return;
        }

        var analysis = new LapAnalyzer().Analyze(samples);
        var reference = ReferenceLapSelector.SelectFastestValid(analysis.Laps);
        LapComparison? comparison = reference is null ? null : new LapComparer().Compare(reference, analysis.Laps);

        _session = analysis;
        _reference = reference;

        Laps.Clear();
        foreach (var lap in analysis.Laps) Laps.Add(new LapRowViewModel(lap));

        Deltas.Clear();
        if (comparison is not null)
        {
            foreach (var delta in comparison.Laps)
            {
                Deltas.Add(new LapDeltaRowViewModel(delta, delta.LapNumber == comparison.ReferenceLapNumber));
            }

            ReferenceText = reference is null
                ? Display.Missing
                : $"Lap {reference.LapNumber} — {Display.LapTime(reference.LapTimeSeconds)}";
        }
        else
        {
            ReferenceText = Display.Missing;
        }

        DataQualityText = analysis.Warnings.Count == 0
            ? "good"
            : $"{analysis.Warnings.Count} warning(s)";

        var driving = new DrivingAnalyzer().Analyze(analysis);
        Findings.Clear();
        foreach (var finding in driving.Events
                     .OrderByDescending(e => e.Severity)
                     .ThenBy(e => e.Category))
        {
            Findings.Add(new FindingRowViewModel(finding, InterpretFinding));
        }
        FindingsSummary = driving.Events.Count == 0 ? "none" : $"{driving.Events.Count} finding(s)";

        StatusText =
            $"Analysed {samples.Count} samples · {analysis.Laps.Count} lap(s) · " +
            (comparison is null ? "no valid reference lap" : $"{comparison.Laps.Count - 1} lap(s) compared");

        HasAnalysis = true;
        OnPropertyChanged(nameof(HasAnalysis));
        OnPropertyChanged(nameof(HasDeltas));
        OnPropertyChanged(nameof(HasLaps));
        OnPropertyChanged(nameof(HasFindings));
        OnPropertyChanged(nameof(FindingsSummary));
        OnPropertyChanged(nameof(ReferenceText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(DataQualityText));
    }

    private IAnalysisProvider BuildProvider() => AnalysisProviderFactory.Create(new AiProviderOptions
    {
        Provider = _shell.Options.AiProvider,
        Model = _shell.Options.AiModel,
        Endpoint = _shell.Options.AiEndpoint
    });

    private async Task<AnalysisResult> InterpretFinding(TelemetryEvent finding, CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            return AnalysisResult.Failure(AnalysisOutcome.NoProvider, "Run Analyse before requesting an interpretation.");
        }

        if (!_provider.IsConfigured)
        {
            return AnalysisResult.Failure(
                AnalysisOutcome.NoProvider,
                "No AI provider is configured. Set provider/model/endpoint in Settings and the key in ACCENGINEER_AI_API_KEY.");
        }

        var lap = _session.Laps.FirstOrDefault(l => l.LapNumber == finding.LapNumber);
        var request = DrivingAnalysisRequestBuilder.Build(finding, _session, lap, _reference);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));

        return await _provider.AnalyzeAsync(request, timeout.Token);
    }

    private void RefreshEvents()
    {
        var interesting = _shell.EventLog.Events
            .Where(e => e.Kind is SessionEventKind.LapCompleted
                or SessionEventKind.InvalidLap
                or SessionEventKind.DataQuality
                or SessionEventKind.Recording)
            .Take(40)
            .ToArray();

        if (Events.Count == interesting.Length && Events.Zip(interesting).All(p => ReferenceEquals(p.First, p.Second)))
        {
            return;
        }

        Events.Clear();
        foreach (var e in interesting) Events.Add(e);
    }
}
