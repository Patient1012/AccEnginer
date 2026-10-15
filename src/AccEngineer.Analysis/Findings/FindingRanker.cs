namespace AccEngineer.Analysis.Findings;

public sealed class FindingRankerOptions
{
    public double TimeLossWeight { get; init; } = 1.0;

    public double FrequencyWeight { get; init; } = 0.10;

    public double ConfidenceWeight { get; init; } = 0.50;

    public double SeverityWeight { get; init; } = 0.20;
}

/// <summary>
/// Ranks findings so the AI and the driver see the highest-value items first.
/// The score combines estimated time loss, frequency, confidence and severity.
/// Ordering is deterministic: ties break on the finding id.
/// </summary>
public sealed class FindingRanker
{
    private readonly FindingRankerOptions _options;

    public FindingRanker(FindingRankerOptions? options = null)
    {
        _options = options ?? new FindingRankerOptions();
    }

    public IReadOnlyList<Finding> Rank(IEnumerable<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return findings
            .OrderByDescending(Score)
            .ThenBy(f => f.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public double Score(Finding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return
            (finding.EstimatedTimeLossSeconds ?? 0) * _options.TimeLossWeight +
            finding.Frequency * _options.FrequencyWeight +
            finding.ConfidenceScore * _options.ConfidenceWeight +
            SeverityScore(finding.Severity) * _options.SeverityWeight;
    }

    private static double SeverityScore(EventSeverity severity) => severity switch
    {
        EventSeverity.High => 1.0,
        EventSeverity.Medium => 0.66,
        EventSeverity.Low => 0.33,
        _ => 0.0
    };
}
