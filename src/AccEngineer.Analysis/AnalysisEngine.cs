using AccEngineer.Analysis.Ai;
using AccEngineer.Analysis.Comparison;
using AccEngineer.Analysis.Corners;
using AccEngineer.Analysis.Events;
using AccEngineer.Analysis.Findings;
using AccEngineer.Analysis.Indicators;
using AccEngineer.Analysis.Laps;
using AccEngineer.Telemetry;

namespace AccEngineer.Analysis;

/// <summary>Everything the engine needs to analyse a session recording.</summary>
public sealed record SessionAnalysisRequest
{
    public required IReadOnlyList<TelemetrySample> Samples { get; init; }

    public TrackCornerDefinition? Corners { get; init; }

    public int? ReferenceLapNumber { get; init; }

    public int? CurrentLapNumber { get; init; }

    public string? Track { get; init; }

    public string? Car { get; init; }

    public string? Session { get; init; }

    public SessionType SessionType { get; init; } = SessionType.Unknown;
}

public sealed class AnalysisEngineOptions
{
    public LapAnalysisOptions Lap { get; init; } = new();

    public LapComparisonOptions Comparison { get; init; } = new();

    public CornerAnalysisOptions Corner { get; init; } = new();

    public VehicleBehaviourDetectorOptions Behaviour { get; init; } = new();

    public FindingBuilderOptions Findings { get; init; } = new();

    public int MaxFindings { get; init; } = 5;
}

/// <summary>The complete, deterministic analysis output for a session.</summary>
public sealed record SessionAnalysisResult
{
    public required SessionLapAnalysis Session { get; init; }

    public required IReadOnlyList<LapRepresentation> Laps { get; init; }

    public required IReadOnlyList<DrivingEvent> Events { get; init; }

    public required IReadOnlyList<BehaviourIndicator> Indicators { get; init; }

    public TimeLossAnalysis? TimeLoss { get; init; }

    /// <summary>All findings, ranked highest-value first.</summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    public required AiSessionSummary AiSummary { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Orchestrates the whole deterministic pipeline: raw telemetry, validation and lap
/// reconstruction, distance-based lap representation, metrics, comparison, corner
/// analysis, events, behaviour indicators, findings and the compact AI summary. It has
/// no AI-provider, UI or ACC dependency, so it can run from a recording or a test.
/// </summary>
public sealed class AnalysisEngine
{
    private readonly AnalysisEngineOptions _options;

    public AnalysisEngine(AnalysisEngineOptions? options = null)
    {
        _options = options ?? new AnalysisEngineOptions();
    }

    public SessionAnalysisResult Analyze(SessionAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lapAnalysis = new LapAnalyzer(_options.Lap).Analyze(request.Samples);
        var builder = new LapRepresentationBuilder();
        var representations = lapAnalysis.Laps
            .Select(lap => builder.Build(lap, request.Track, request.Car, request.SessionType))
            .ToArray();

        var warnings = new List<string>(lapAnalysis.Warnings);

        var reference = SelectReference(lapAnalysis.Laps, request.ReferenceLapNumber, warnings);
        var current = SelectCurrent(lapAnalysis.Laps, reference, request.CurrentLapNumber);

        if (reference is null || current is null)
        {
            warnings.Add("Not enough valid laps were available to compare.");
            return EmptyResult(lapAnalysis, representations, warnings, request);
        }

        var comparison = new LapComparer(_options.Comparison).Compare(reference, lapAnalysis.Laps);
        var currentDelta = comparison.Laps.FirstOrDefault(l => l.LapNumber == current.LapNumber);

        var eventDetector = new DrivingEventDetector(cornerOptions: _options.Corner);
        var events = eventDetector.Detect(current, request.Corners);

        var indicators = new VehicleBehaviourDetector(_options.Behaviour).Detect(current, request.Corners);

        var timeLoss = new TimeLossCalculator(_options.Corner, _options.Comparison)
            .Calculate(reference, current, request.Corners);

        var findingBuilder = new FindingBuilder(_options.Findings);
        var findings = findingBuilder.FromTimeLoss(timeLoss)
            .Concat(findingBuilder.FromIndicators(indicators, current.LapNumber));

        var ranked = new FindingRanker().Rank(findings);

        var best = ReferenceLapSelector.SelectFastestValid(lapAnalysis.Laps);

        var summary = new AiAnalysisDtoBuilder(_options.MaxFindings).Build(
            new AiSessionContext
            {
                Track = request.Track,
                Car = request.Car,
                Session = request.Session,
                BestLapSeconds = best?.LapTimeSeconds,
                ReferenceLapSeconds = reference.LapTimeSeconds,
                CurrentLapSeconds = current.LapTimeSeconds,
                TotalDeltaSeconds = currentDelta?.TotalDeltaSeconds,
                Warnings = warnings
            },
            ranked);

        return new SessionAnalysisResult
        {
            Session = lapAnalysis,
            Laps = representations,
            Events = events,
            Indicators = indicators,
            TimeLoss = timeLoss,
            Findings = ranked,
            AiSummary = summary,
            Warnings = warnings
        };
    }

    private SessionAnalysisResult EmptyResult(
        SessionLapAnalysis session,
        IReadOnlyList<LapRepresentation> laps,
        List<string> warnings,
        SessionAnalysisRequest request)
    {
        var summary = new AiAnalysisDtoBuilder(_options.MaxFindings).Build(
            new AiSessionContext
            {
                Track = request.Track,
                Car = request.Car,
                Session = request.Session,
                Warnings = warnings
            },
            Array.Empty<Finding>());

        return new SessionAnalysisResult
        {
            Session = session,
            Laps = laps,
            Events = Array.Empty<DrivingEvent>(),
            Indicators = Array.Empty<BehaviourIndicator>(),
            TimeLoss = null,
            Findings = Array.Empty<Finding>(),
            AiSummary = summary,
            Warnings = warnings
        };
    }

    private static LapTimeline? SelectReference(
        IReadOnlyList<LapTimeline> laps,
        int? requested,
        List<string> warnings)
    {
        if (requested is { } number)
        {
            var match = laps.FirstOrDefault(l => l.LapNumber == number);
            if (match is null)
            {
                warnings.Add($"Requested reference lap {number} was not found; falling back to the fastest valid lap.");
            }
            else
            {
                return match;
            }
        }

        return ReferenceLapSelector.SelectFastestValid(laps);
    }

    private static LapTimeline? SelectCurrent(
        IReadOnlyList<LapTimeline> laps,
        LapTimeline? reference,
        int? requested)
    {
        if (requested is { } number)
        {
            return laps.FirstOrDefault(l => l.LapNumber == number);
        }

        if (reference is null)
        {
            return null;
        }

        // Prefer the most recent valid lap other than the reference; else the reference.
        for (var i = laps.Count - 1; i >= 0; i--)
        {
            var lap = laps[i];
            if (lap.LapNumber != reference.LapNumber && lap.IsValid && lap.LapTimeSeconds is not null)
            {
                return lap;
            }
        }

        return reference;
    }
}
