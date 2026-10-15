using AccEngineer.Analysis.Findings;

namespace AccEngineer.Analysis.Ai;

/// <summary>
/// Session-level context needed to build a compact AI summary. Plain data, no
/// telemetry, so it can come from a run or a recording.
/// </summary>
public sealed record AiSessionContext
{
    public string? Track { get; init; }

    public string? Car { get; init; }

    public string? Session { get; init; }

    public double? BestLapSeconds { get; init; }

    public double? ReferenceLapSeconds { get; init; }

    public double? CurrentLapSeconds { get; init; }

    public double? TotalDeltaSeconds { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Builds the compact AI-facing <see cref="AiSessionSummary"/> from ranked 
/// It keeps only the highest-value findings and attaches the measured numbers the AI
/// needs, so the AI is not fed raw telemetry.
/// </summary>
public sealed class AiAnalysisDtoBuilder
{
    private readonly int _maxFindings;
    private readonly FindingRanker _ranker;

    public AiAnalysisDtoBuilder(int maxFindings = 5, FindingRankerOptions? rankerOptions = null)
    {
        _maxFindings = maxFindings;
        _ranker = new FindingRanker(rankerOptions);
    }

    public AiSessionSummary Build(AiSessionContext context, IEnumerable<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(findings);

        var top = _ranker.Rank(findings).Take(_maxFindings).ToArray();

        return new AiSessionSummary
        {
            Track = context.Track,
            Car = context.Car,
            Session = context.Session,
            BestLapSeconds = context.BestLapSeconds,
            ReferenceLapSeconds = context.ReferenceLapSeconds,
            CurrentLapSeconds = context.CurrentLapSeconds,
            BestLap = FormatLapTime(context.BestLapSeconds),
            ReferenceLap = FormatLapTime(context.ReferenceLapSeconds),
            CurrentLap = FormatLapTime(context.CurrentLapSeconds),
            TotalDeltaSeconds = context.TotalDeltaSeconds,
            TopFindings = top.Select((finding, index) => ToAiFinding(finding, index + 1)).ToArray(),
            Warnings = context.Warnings
        };
    }

    /// <summary>Formats seconds as "m:ss.fff" (e.g. 104.820 -> "1:44.820").</summary>
    public static string? FormatLapTime(double? seconds)
    {
        if (seconds is not { } s || double.IsNaN(s)) return null;

        var minutes = (int)(s / 60);
        var remainder = s - minutes * 60;
        return $"{minutes}:{remainder:00.000}";
    }

    private static AiFinding ToAiFinding(Finding finding, int rank) => ToAiFinding(finding) with { Rank = rank };

    private static AiFinding ToAiFinding(Finding finding) => new()
    {
        Rank = 0,
        Id = finding.Id,
        Category = finding.Category.ToString(),
        Severity = finding.Severity.ToString(),
        ConfidenceLabel = finding.Confidence.ToString(),
        Confidence = finding.ConfidenceScore,
        CornerNumber = finding.Location?.CornerNumber,
        StartDistanceMeters = finding.Location?.StartDistanceMeters,
        EndDistanceMeters = finding.Location?.EndDistanceMeters,
        EstimatedLossSeconds = finding.EstimatedTimeLossSeconds,
        Observation = finding.Observation,
        Evidence = finding.Evidence.Select(e => e.Description).ToArray(),
        PossibleCauses = finding.PossibleCauses,
        Recommendation = finding.Recommendation,
        ValidationMethod = finding.ValidationMethod
    };
}
