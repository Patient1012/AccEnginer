using AccEngineer.Analysis.Ai;
using AccEngineer.AI;

namespace AccEngineer.AI.Tests;

public class FindingAnalysisRequestBuilderTests
{
    private static AiFinding Finding() => new()
    {
        Rank = 1,
        Id = "f1",
        Category = "CornerExit",
        Severity = "Medium",
        ConfidenceLabel = "Probable",
        Confidence = 0.62,
        CornerNumber = 4,
        StartDistanceMeters = 100,
        EndDistanceMeters = 160,
        EstimatedLossSeconds = 0.25,
        Observation = "Late throttle pickup.",
        Evidence = new[] { "Throttle pickup 30 m later than reference." }
    };

    private static AiSessionSummary Summary() => new()
    {
        Track = "Spa",
        Car = "GT3",
        Session = "Practice",
        ReferenceLapSeconds = 104.5,
        CurrentLapSeconds = 104.82,
        ReferenceLap = "1:44.500",
        CurrentLap = "1:44.820",
        TotalDeltaSeconds = 0.32,
        TopFindings = new[] { Finding() },
        Warnings = new[] { "Sector data incomplete." }
    };

    [Fact]
    public void Build_MapsContextAndCategory()
    {
        var request = FindingAnalysisRequestBuilder.Build(Finding(), Summary());

        Assert.Equal("Spa", request.Context.Track);
        Assert.Equal("GT3", request.Context.Car);
        Assert.Equal("Practice", request.Context.SessionType);
        Assert.Equal("CornerExit", request.EventCategory);
        Assert.Equal("Late throttle pickup.", request.EventDescription);
        Assert.Single(request.DataQualityWarnings);
        Assert.Contains("1:44.820", request.ComparisonSummary);
    }

    [Fact]
    public void Build_IncludesMeasuredMetrics()
    {
        var request = FindingAnalysisRequestBuilder.Build(Finding(), Summary());

        Assert.Contains(request.Metrics, m => m.Name == "Corner" && m.Value == "4");
        Assert.Contains(request.Metrics, m => m.Name == "Estimated time loss" && m.Unit == "s");
        Assert.Contains(request.Metrics, m => m.Name == "Evidence");
        Assert.DoesNotContain("--", request.ComparisonSummary!);
    }

    [Fact]
    public void Build_NeverAttachesRawSamples()
    {
        var request = FindingAnalysisRequestBuilder.Build(Finding(), Summary());

        Assert.Empty(request.Samples);
    }
}
