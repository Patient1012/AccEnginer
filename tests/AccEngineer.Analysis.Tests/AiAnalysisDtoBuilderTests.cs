using AccEngineer.Analysis.Ai;
using AccEngineer.Analysis.Findings;

namespace AccEngineer.Analysis.Tests;

public class AiAnalysisDtoBuilderTests
{
    private static Finding Make(string id, double loss, EventCategory category = EventCategory.CornerExit)
        => new()
        {
            Id = id,
            Category = category,
            Severity = EventSeverity.Medium,
            Confidence = EventConfidence.Verified,
            ConfidenceScore = 0.95,
            Observation = "obs",
            Evidence = new[]
            {
                new FindingEvidence { Description = "evidence one", Value = 1.0 }
            },
            EstimatedTimeLossSeconds = loss,
            Location = new TrackLocation { CornerNumber = 4, StartDistanceMeters = 100, EndDistanceMeters = 200 },
            PossibleCauses = new[] { "cause" },
            Recommendation = "recommend",
            ValidationMethod = "validate"
        };

    [Fact]
    public void Build_KeepsTopFindingsOnly()
    {
        var builder = new AiAnalysisDtoBuilder(maxFindings: 2);
        var context = new AiSessionContext { Track = "Spa", Car = "GT3", Session = "Practice" };

        var summary = builder.Build(context, new[]
        {
            Make("a", 0.05),
            Make("b", 0.30),
            Make("c", 0.20)
        });

        Assert.Equal(2, summary.TopFindings.Count);
        Assert.Equal("b", summary.TopFindings[0].Id);
        Assert.Equal(1, summary.TopFindings[0].Rank);
        Assert.Equal(2, summary.TopFindings[1].Rank);
    }

    [Fact]
    public void Build_FormatsLapTimes()
    {
        var summary = new AiAnalysisDtoBuilder().Build(
            new AiSessionContext { CurrentLapSeconds = 104.820, BestLapSeconds = 104.5 },
            Array.Empty<Finding>());

        Assert.Equal("1:44.820", summary.CurrentLap);
        Assert.Equal("1:44.500", summary.BestLap);
        Assert.Null(summary.ReferenceLap);
    }

    [Fact]
    public void Build_MapsFindingFields()
    {
        var summary = new AiAnalysisDtoBuilder().Build(
            new AiSessionContext(),
            new[] { Make("b", 0.30) });

        var finding = Assert.Single(summary.TopFindings);
        Assert.Equal("CornerExit", finding.Category);
        Assert.Equal("Medium", finding.Severity);
        Assert.Equal("Verified", finding.ConfidenceLabel);
        Assert.Equal(4, finding.CornerNumber);
        Assert.Equal(0.30, finding.EstimatedLossSeconds!.Value, 3);
        Assert.Single(finding.Evidence);
    }
}
