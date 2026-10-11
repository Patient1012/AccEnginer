using System.Net;
using System.Text;
using AccEngineer.AI;

namespace AccEngineer.AI.Tests;

public class NullProviderTests
{
    [Fact]
    public async Task Analyze_NoProvider_ReturnsNoProviderOutcome()
    {
        var provider = new NullAnalysisProvider();
        Assert.False(provider.IsConfigured);
        Assert.Equal("none", provider.Name);

        var request = new AnalysisRequest
        {
            Context = new AnalysisContext { Track = "Monza", Car = "AMR V8" },
            EventCategory = "Braking",
            EventDescription = "Slightly deeper initial brake entry."
        };

        var result = await provider.AnalyzeAsync(request, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(AnalysisOutcome.NoProvider, result.Outcome);
    }
}
