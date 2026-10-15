using AccEngineer.Analysis.Findings;

namespace AccEngineer.Analysis.Tests;

public class FindingRankerTests
{
    private static Finding Make(string id, double? loss, double confidence = 1.0, EventSeverity severity = EventSeverity.Low)
        => new()
        {
            Id = id,
            Category = EventCategory.CornerExit,
            Severity = severity,
            Confidence = EventConfidence.Verified,
            ConfidenceScore = confidence,
            Observation = "obs",
            Evidence = Array.Empty<FindingEvidence>(),
            EstimatedTimeLossSeconds = loss
        };

    [Fact]
    public void Rank_OrdersByEstimatedTimeLoss()
    {
        var findings = new[]
        {
            Make("a", 0.05),
            Make("b", 0.30),
            Make("c", 0.15)
        };

        var ranked = new FindingRanker().Rank(findings);

        Assert.Equal(new[] { "b", "c", "a" }, ranked.Select(f => f.Id));
    }

    [Fact]
    public void Rank_TiesBreakOnIdDeterministically()
    {
        var findings = new[]
        {
            Make("z", 0.10),
            Make("a", 0.10)
        };

        var ranked = new FindingRanker().Rank(findings);

        Assert.Equal(new[] { "a", "z" }, ranked.Select(f => f.Id));
    }

    [Fact]
    public void Score_CombinesFactors()
    {
        var ranker = new FindingRanker(new FindingRankerOptions
        {
            TimeLossWeight = 1.0,
            FrequencyWeight = 0.0,
            ConfidenceWeight = 0.0,
            SeverityWeight = 0.0
        });

        Assert.Equal(0.25, ranker.Score(Make("a", 0.25)), 3);
    }
}
