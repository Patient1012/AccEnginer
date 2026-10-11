using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AccEngineer.AI;

/// <summary>
/// Calls any OpenAI-compatible chat-completions endpoint (OpenRouter, a local
/// LM Studio/Ollama server, or a custom base URL). The supplied
/// <see cref="HttpMessageHandler"/> makes it fully testable without network access.
/// Expected failures are returned as values and never thrown.
/// </summary>
public sealed class OpenAiCompatibleProvider : IAnalysisProvider
{
    private readonly HttpClient _http;
    private readonly Uri _chatCompletionsUri;
    private readonly string _model;
    private readonly RequestLimiter _limiter;

    public OpenAiCompatibleProvider(
        string name,
        string endpoint,
        string model,
        string? apiKey,
        TimeSpan timeout,
        RequestLimiter? limiter = null,
        HttpMessageHandler? handler = null)
    {
        Name = name;
        _model = model;
        _limiter = limiter ?? new RequestLimiter();

        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = timeout;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        _chatCompletionsUri = new Uri(endpoint.TrimEnd('/') + "/chat/completions");
    }

    public string Name { get; }

    public bool IsConfigured => true;

    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return AnalysisResult.Failure(AnalysisOutcome.Cancelled, "The request was cancelled.");
        }

        try
        {
            using var slot = await _limiter.AcquireAsync(cancellationToken).ConfigureAwait(false);
            using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return AnalysisResult.Failure(
                    AnalysisOutcome.Unavailable,
                    $"The provider returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var content = ExtractContent(body);
            return AnalysisResultParser.Parse(content, Name);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AnalysisResult.Failure(AnalysisOutcome.Cancelled, "The request was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return AnalysisResult.Failure(AnalysisOutcome.Unavailable, "The request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return AnalysisResult.Failure(AnalysisOutcome.Unavailable, $"The provider could not be reached: {ex.Message}");
        }
        catch (Exception ex)
        {
            return AnalysisResult.Failure(AnalysisOutcome.Unavailable, $"Unexpected provider error: {ex.Message}");
        }
    }

    private Task<HttpResponseMessage> SendAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = _model,
            temperature = 0.2,
            messages = new[]
            {
                new { role = "system", content = AnalysisPrompts.System },
                new { role = "user", content = AnalysisPrompts.BuildUserPrompt(request) }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var message = new HttpRequestMessage(HttpMethod.Post, _chatCompletionsUri)
        {
            Content = content
        };

        return _http.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
    }

    private static string? ExtractContent(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return null;
            }

            if (!choices[0].TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content))
            {
                return null;
            }

            return content.ValueKind == JsonValueKind.String ? content.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
