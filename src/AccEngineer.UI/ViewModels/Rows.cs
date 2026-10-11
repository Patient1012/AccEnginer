using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using AccEngineer.AI;
using AccEngineer.Analysis;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.ViewModels;

/// <summary>One reconstructed lap as shown in the Analysis table.</summary>
public sealed class LapRowViewModel
{
    public LapRowViewModel(LapTimeline lap)
    {
        LapNumber = lap.LapNumber;
        LapTime = Display.LapTime(lap.LapTimeSeconds);
        IsValid = lap.IsValid;
        IsComplete = lap.IsComplete;
        Source = lap.LapTimeSource switch
        {
            LapTimeSource.AccReported => "ACC",
            LapTimeSource.ComputedBoundary => "computed",
            LapTimeSource.PartialSpan => "partial",
            _ => "unavailable"
        };
        Samples = lap.Samples.Count.ToString(CultureInfo.InvariantCulture);
        Sectors = lap.Sectors.Count == 0
            ? Display.Missing
            : string.Join(" / ", lap.Sectors.Select(s => Display.LapTime(s.TimeSeconds)));
        StatusText = !IsComplete ? "Incomplete" : IsValid ? "Valid" : "Invalid";
    }

    public int LapNumber { get; }

    public string LapTime { get; }

    public bool IsValid { get; }

    public bool IsComplete { get; }

    public string Source { get; }

    public string Samples { get; }

    public string Sectors { get; }

    public string StatusText { get; }
}

/// <summary>A gain/loss region within a lap comparison.</summary>
public sealed class RegionRowViewModel
{
    public RegionRowViewModel(GainLossRegion region)
    {
        IsGain = region.IsGain;
        Type = region.IsGain ? "Gain" : "Loss";
        Range = $"{region.StartProgressMeters.ToString("0", CultureInfo.InvariantCulture)}–" +
                $"{region.EndProgressMeters.ToString("0", CultureInfo.InvariantCulture)} m";
        Delta = Display.Delta(region.DeltaSeconds);
    }

    public bool IsGain { get; }

    public string Type { get; }

    public string Range { get; }

    public string Delta { get; }
}

/// <summary>One lap's delta against the reference lap, with its regions.</summary>
public sealed class LapDeltaRowViewModel
{
    public LapDeltaRowViewModel(LapDelta delta, bool isReference)
    {
        LapNumber = delta.LapNumber;
        IsReference = isReference;
        LapTime = Display.LapTime(delta.LapTimeSeconds);
        TotalDelta = isReference ? "reference" : Display.Delta(delta.TotalDeltaSeconds);
        EndDelta = isReference ? "reference" : Display.Delta(delta.DeltaAtEndSeconds);
        Regions = new ObservableCollection<RegionRowViewModel>(delta.Regions.Select(r => new RegionRowViewModel(r)));
    }

    public int LapNumber { get; }

    public bool IsReference { get; }

    public string LapTime { get; }

    public string TotalDelta { get; }

    public string EndDelta { get; }

    public ObservableCollection<RegionRowViewModel> Regions { get; }

    public bool HasRegions => Regions.Count > 0;
}

/// <summary>
/// One car on track for the standings/proximity panel. ACC shared memory does not
/// expose opponent names or timing, so a row carries the car id, whether it is the
/// player, and its world distance from the player.
/// </summary>
public sealed class CarRow
{
    public CarRow(int carId, int slot, bool isPlayer, double? distanceMeters)
    {
        CarId = carId;
        Slot = slot;
        IsPlayer = isPlayer;
        DistanceMeters = distanceMeters;
    }

    public int CarId { get; }

    public int Slot { get; }

    public bool IsPlayer { get; }

    public double? DistanceMeters { get; }

    public string Label => IsPlayer ? "You" : CarId > 0 ? $"Car {CarId}" : $"Car #{Slot + 1}";

    public string DistanceText => DistanceMeters is { } d ? $"{d:0} m" : Display.Missing;
}

/// <summary>
/// One row of the race timing tower shown on the Live Data page. ACC shared memory
/// exposes only each car's id and world coordinates (no names, gaps or lap times),
/// so the order and the gaps are an estimate: each car's position is projected onto
/// the player's recent line and compared by track progress metres.
/// </summary>
public sealed class LeaderboardRowViewModel
{
    public LeaderboardRowViewModel(
        int towerPosition,
        int carId,
        bool isPlayer,
        double? gapToLeaderMeters,
        double? intervalToPreviousMeters)
    {
        TowerPosition = towerPosition;
        CarId = carId;
        IsPlayer = isPlayer;
        GapToLeaderMeters = gapToLeaderMeters;
        IntervalToPreviousMeters = intervalToPreviousMeters;
    }

    /// <summary>1-based estimated running position (P1, P2, ...).</summary>
    public int TowerPosition { get; }

    public int CarId { get; }

    public bool IsPlayer { get; }

    public double? GapToLeaderMeters { get; }

    public double? IntervalToPreviousMeters { get; }

    public string PositionText => $"P{TowerPosition}";

    public string CarText => IsPlayer ? "YOU" : CarId > 0 ? $"CAR {CarId}" : "CAR";

    public string CarSubText => IsPlayer && CarId > 0 ? $"car id {CarId}" : string.Empty;

    public bool HasCarSub => !string.IsNullOrEmpty(CarSubText);

    public string GapText => TowerPosition == 1 ? "—" : GapToLeaderMeters is { } gap
        ? $"+{gap.ToString("0", CultureInfo.InvariantCulture)} m"
        : Display.Missing;

    public string IntervalText => TowerPosition == 1 ? "—" : IntervalToPreviousMeters is { } interval
        ? $"+{interval.ToString("0", CultureInfo.InvariantCulture)} m"
        : Display.Missing;
}

/// <summary>
/// One deterministic driving finding. The measured observation is shown plainly;
/// any interpretation is labelled and shown separately so a hypothesis is never
/// mistaken for a diagnosis. An AI interpretation can be requested per finding;
/// the provider is provider-neutral and the result stays visually separated from
/// the measurement.
/// </summary>
public sealed class FindingRowViewModel : ObservableObject
{
    private readonly Func<TelemetryEvent, CancellationToken, Task<AnalysisResult>> _interpret;
    private bool _isAiBusy;
    private string? _aiObservation;
    private string? _aiInterpretation;
    private string? _aiConfidence;
    private string? _aiTest;
    private string? _aiLimitations;
    private string? _aiError;

    public FindingRowViewModel(
        TelemetryEvent finding,
        Func<TelemetryEvent, CancellationToken, Task<AnalysisResult>>? interpret = null)
    {
        Finding = finding;
        _interpret = interpret ?? ((_, _) => Task.FromResult(
            AnalysisResult.Failure(AnalysisOutcome.NoProvider, "No AI provider is configured.")));

        Category = finding.Category.ToString();
        Severity = finding.Severity.ToString();
        Confidence = finding.Confidence.ToString();
        Observation = finding.Observation;
        Interpretation = finding.Interpretation;
        LapNumber = finding.LapNumber;
        Location = finding.ProgressMeters is { } p
            ? $"{p.ToString("0", CultureInfo.InvariantCulture)} m"
            : string.Empty;
        Limitation = finding.Limitations.Count > 0 ? string.Join(" ", finding.Limitations) : string.Empty;

        InterpretCommand = new RelayCommand(Interpret, () => !IsAiBusy);
    }

    public TelemetryEvent Finding { get; }

    public string Category { get; }

    public string Severity { get; }

    public string Confidence { get; }

    public string Observation { get; }

    public string? Interpretation { get; }

    public int? LapNumber { get; }

    public string Location { get; }

    public string Limitation { get; }

    public bool HasInterpretation => !string.IsNullOrEmpty(Interpretation);

    public bool HasLimitation => !string.IsNullOrEmpty(Limitation);

    public string Header => LapNumber is { } lap ? $"Lap {lap} · {Category}" : Category;

    public string LocationText => string.IsNullOrEmpty(Location) ? string.Empty : $"at {Location}";

    public ICommand InterpretCommand { get; }

    public bool IsAiBusy
    {
        get => _isAiBusy;
        private set
        {
            if (SetProperty(ref _isAiBusy, value))
            {
                OnPropertyChanged(nameof(InterpretLabel));
                (InterpretCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string InterpretLabel => IsAiBusy ? "Interpreting..." : "Interpret with AI";

    public string? AiObservation
    {
        get => _aiObservation;
        private set { if (SetProperty(ref _aiObservation, value)) OnPropertyChanged(nameof(HasAiResult)); }
    }

    public string? AiInterpretation
    {
        get => _aiInterpretation;
        private set { if (SetProperty(ref _aiInterpretation, value)) OnPropertyChanged(nameof(HasAiInterpretation)); }
    }

    public string? AiConfidence
    {
        get => _aiConfidence;
        private set { if (SetProperty(ref _aiConfidence, value)) OnPropertyChanged(nameof(HasAiConfidence)); }
    }

    public string? AiTest
    {
        get => _aiTest;
        private set { if (SetProperty(ref _aiTest, value)) OnPropertyChanged(nameof(HasAiTest)); }
    }

    public string? AiLimitations
    {
        get => _aiLimitations;
        private set { if (SetProperty(ref _aiLimitations, value)) OnPropertyChanged(nameof(HasAiLimitations)); }
    }

    public string? AiError
    {
        get => _aiError;
        private set { if (SetProperty(ref _aiError, value)) OnPropertyChanged(nameof(HasAiError)); }
    }

    public bool HasAiResult => !string.IsNullOrEmpty(AiObservation);

    public bool HasAiInterpretation => !string.IsNullOrEmpty(AiInterpretation);

    public bool HasAiConfidence => !string.IsNullOrEmpty(AiConfidence);

    public bool HasAiTest => !string.IsNullOrEmpty(AiTest);

    public bool HasAiLimitations => !string.IsNullOrEmpty(AiLimitations);

    public bool HasAiError => !string.IsNullOrEmpty(AiError);

    private async void Interpret()
    {
        if (IsAiBusy)
        {
            return;
        }

        IsAiBusy = true;
        AiError = null;

        try
        {
            var result = await _interpret(Finding, CancellationToken.None);
            if (result.IsSuccess)
            {
                AiObservation = result.Observation;
                AiInterpretation = result.Interpretation;
                AiConfidence = result.Confidence is { } confidence
                    ? confidence.ToString("0.00", CultureInfo.InvariantCulture)
                    : null;
                AiTest = result.SuggestedTest;
                AiLimitations = result.Limitations.Count > 0 ? string.Join(" ", result.Limitations) : null;
            }
            else
            {
                AiError = result.Message ?? result.Outcome.ToString();
            }
        }
        catch (Exception ex)
        {
            AiError = $"Interpretation failed: {ex.Message}";
        }
        finally
        {
            IsAiBusy = false;
        }
    }
}

/// <summary>A generated report file pair on disk (lap report, live event or session summary).</summary>
public sealed class ReportEntry
{
    public ReportEntry(string markdownPath, string jsonPath, DateTimeOffset lastWriteUtc)
    {
        MarkdownPath = markdownPath;
        JsonPath = jsonPath;
        LastWriteUtc = lastWriteUtc;
        FileName = Path.GetFileName(markdownPath);
        Kind = FileName.EndsWith(".live.md", StringComparison.OrdinalIgnoreCase) ? "Live" : "Lap";
    }

    public string MarkdownPath { get; }

    public string JsonPath { get; }

    public DateTimeOffset LastWriteUtc { get; }

    public string FileName { get; }

    /// <summary>"Lap" for lap comparison reports, "Live" for live event / session summary reports.</summary>
    public string Kind { get; }

    public string CreatedText => LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
