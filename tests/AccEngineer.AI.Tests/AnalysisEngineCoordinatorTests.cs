using AccEngineer.Analysis;
using AccEngineer.Analysis.Ai;
using AccEngineer.AI;

namespace AccEngineer.AI.Tests;

public class AnalysisEngineCoordinatorTests
{
    private static AiFinding Finding(int rank, string id) => new()
    {
        Rank = rank,
        Id = id,
        Category = "Braking",
        Severity = "Low",
        ConfidenceLabel = "Probable",
        Confidence = 0.55,
        Observation = "Observation " + id,
        Evidence = new[] { "Evidence " + id }
    };

    private static SessionAnalysisResult Result()
    {
        var summary = new AiSessionSummary
        {
            Track = "Spa",
            TopFindings = new[] { Finding(1, "a"), Finding(2, "b") },
            Warnings = Array.Empty<string>()
        };

        return new SessionAnalysisResult
        {
            Session = new SessionLapAnalysis
            {
                SessionId = "s",
                Laps = Array.Empty<LapTimeline>(),
                Warnings = Array.Empty<string>()
            },
            Laps = Array.Empty<Analysis.Laps.LapRepresentation>(),
            Events = Array.Empty<Analysis.Events.DrivingEvent>(),
            Indicators = Array.Empty<Analysis.Indicators.BehaviourIndicator>(),
            Findings = Array.Empty<Analysis.Findings.Finding>(),
            AiSummary = summary
        };
    }

    private sealed class StubProvider : IAnalysisProvider
    {
        private readonly AnalysisResult _result;
        private readonly bool _throws;

        public StubProvider(AnalysisResult result, bool throws = false)
        {
            _result = result;
            _throws = throws;
        }

        public string Name => "stub";

        public bool IsConfigured => true;

        public List<AnalysisRequest> Requests { get; } = new();

        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_throws)
            {
                throw new InvalidOperationException("provider down");
            }

            return Task.FromResult(_result);
        }
    }

    [Fact]
    public async Task InterpretAsync_NoProvider_ReturnsDeterministicOnly()
    {
        var report = await new AnalysisEngineCoordinator(new NullAnalysisProvider())
            .InterpretAsync(Result());

        Assert.False(report.IsAIAvailable);
        Assert.Empty(report.AiFindings);
        Assert.Equal(2, report.Analysis.AiSummary.TopFindings.Count);
    }

    [Fact]
    public async Task InterpretAsync_WithProvider_InterpretsEachFinding()
    {
        var provider = new StubProvider(new AnalysisResult { Outcome = AnalysisOutcome.Success, Observation = "ok" });

        var report = await new AnalysisEngineCoordinator(provider).InterpretAsync(Result());

        Assert.True(report.IsAIAvailable);
        Assert.Equal(2, report.AiFindings.Count);
        Assert.Equal(2, provider.Requests.Count);
        Assert.All(report.AiFindings, a => Assert.True(a.Result.IsSuccess));
        Assert.Equal("a", report.AiFindings[0].Finding.Id);
    }

    [Fact]
    public async Task InterpretAsync_ProviderThrows_ReturnsFailure()
    {
        var provider = new StubProvider(AnalysisResult.Failure(AnalysisOutcome.Unavailable, "x"), throws: true);

        var report = await new AnalysisEngineCoordinator(provider).InterpretAsync(Result());

        Assert.Equal(2, report.AiFindings.Count);
        Assert.All(report.AiFindings, a => Assert.Equal(AnalysisOutcome.Unavailable, a.Result.Outcome));
    }
}
