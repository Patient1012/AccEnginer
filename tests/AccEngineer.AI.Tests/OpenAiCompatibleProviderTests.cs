using System.Net;
using System.Text;
using AccEngineer.AI;

namespace AccEngineer.AI.Tests;

public class OpenAiCompatibleProviderTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _handler(request);
    }

    [Fact]
    public async Task Analyze_Success_ReturnsParsedResult()
    {
        var body = "{\"choices\":[{\"message\":{\"content\":\"{\\\"observation\\\":\\\"Later turn-in\\\",\\\"interpretation\\\":\\\"Trail braking\\\",\\\"confidence\\\":0.7}\"}}]}";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var handler = new StubHandler(_ => Task.FromResult(response));
        using var limiter = new RequestLimiter(1, 600);
        var provider = new OpenAiCompatibleProvider("stub", "http://localhost/v1", "test-model", null, TimeSpan.FromSeconds(5), limiter, handler);

        var request = new AnalysisRequest
        {
            Context = new AnalysisContext { Track = "Monza", Car = "AMR V8" },
            EventCategory = "Braking",
            EventDescription = "Deeper brake zone."
        };

        var result = await provider.AnalyzeAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("stub", result.Provider);
        Assert.Equal("Later turn-in", result.Observation);
    }

    [Fact]
    public async Task Analyze_HttpError_ReturnsUnavailable()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { ReasonPhrase = "Rate limit" };
        using var handler = new StubHandler(_ => Task.FromResult(response));
        using var limiter = new RequestLimiter(1, 600);
        var provider = new OpenAiCompatibleProvider("stub", "http://localhost/v1", "test-model", null, TimeSpan.FromSeconds(5), limiter, handler);

        var request = new AnalysisRequest
        {
            Context = new AnalysisContext { Track = "Monza", Car = "AMR V8" },
            EventCategory = "Braking",
            EventDescription = "Deeper brake zone."
        };

        var result = await provider.AnalyzeAsync(request, CancellationToken.None);
        Assert.Equal(AnalysisOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task Analyze_Cancelled_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var handler = new StubHandler(_ => throw new InvalidOperationException());
        using var limiter = new RequestLimiter(1, 600);
        var provider = new OpenAiCompatibleProvider("stub", "http://localhost/v1", "test-model", null, TimeSpan.FromSeconds(5), limiter, handler);

        var request = new AnalysisRequest
        {
            Context = new AnalysisContext { Track = "Monza", Car = "AMR V8" },
            EventCategory = "Braking",
            EventDescription = "Deeper brake zone."
        };

        var result = await provider.AnalyzeAsync(request, CancellationToken.None);
        // If we cancel before await, returns Cancelled
        var result2 = await provider.AnalyzeAsync(request, cts.Token);
        Assert.Equal(AnalysisOutcome.Cancelled, result2.Outcome);
    }
}
