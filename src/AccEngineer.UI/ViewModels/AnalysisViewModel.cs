using System.Collections.ObjectModel;
using System.Windows.Input;
using AccEngineer.Analysis;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.ViewModels;

/// <summary>
/// Lap comparison and session events. Deterministic: it runs the same
/// <see cref="LapAnalyzer"/> / <see cref="LapComparer"/> used offline, so its
/// results can be reproduced from a recording.
/// </summary>
public sealed class AnalysisViewModel : PageViewModel
{
    private readonly MainViewModel _shell;

    public AnalysisViewModel(MainViewModel shell)
        : base("Analysis")
    {
        _shell = shell;
        AnalyseCommand = new RelayCommand(Analyse);
    }

    public ObservableCollection<LapRowViewModel> Laps { get; } = new();

    public ObservableCollection<LapDeltaRowViewModel> Deltas { get; } = new();

    public ICommand AnalyseCommand { get; }

    public string ReferenceText { get; private set; } = Display.Missing;

    public string StatusText { get; private set; } = "No analysis yet. Drive or replay a session, then select Analyse.";

    public string DataQualityText { get; private set; } = Display.Missing;

    public bool HasAnalysis { get; private set; }

    public bool HasDeltas => Deltas.Count > 0;

    public bool HasLaps => Laps.Count > 0;

    public string EventNote =>
        "Driving-event detection (braking, corner, traction) arrives in a later milestone. " +
        "Events below are lap completions, invalid laps and telemetry-quality notices.";

    public ObservableCollection<SessionEvent> Events { get; } = new();

    public override void Refresh()
    {
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

        StatusText =
            $"Analysed {samples.Count} samples · {analysis.Laps.Count} lap(s) · " +
            (comparison is null ? "no valid reference lap" : $"{comparison.Laps.Count - 1} lap(s) compared");

        HasAnalysis = true;
        OnPropertyChanged(nameof(HasAnalysis));
        OnPropertyChanged(nameof(HasDeltas));
        OnPropertyChanged(nameof(HasLaps));
        OnPropertyChanged(nameof(ReferenceText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(DataQualityText));
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
