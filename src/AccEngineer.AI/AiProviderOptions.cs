namespace AccEngineer.AI;

/// <summary>
/// Configuration for an AI provider. API keys are read from the environment and
/// are intentionally not persisted in settings (roadmap §9).
/// </summary>
public sealed class AiProviderOptions
{
    /// <summary>Provider key: for example "openrouter", "local" or "openai-compatible".</summary>
    public string? Provider { get; init; }

    /// <summary>Base endpoint, for example "https://openrouter.ai/api/v1". Optional for known providers.</summary>
    public string? Endpoint { get; init; }

    public string? Model { get; init; }

    /// <summary>Explicit key. When null the environment is consulted.</summary>
    public string? ApiKey { get; init; }

    public int TimeoutSeconds { get; init; } = 60;

    public int MaxRequestsPerMinute { get; init; } = 20;

    public int MaxConcurrentRequests { get; init; } = 1;
}

/// <summary>
/// Creates a concrete <see cref="IAnalysisProvider"/>. When no usable provider is
/// configured it returns a <see cref="NullAnalysisProvider"/> rather than failing,
/// so deterministic analysis and reporting stay fully available offline.
/// </summary>
public static class AnalysisProviderFactory
{
    public const string ApiKeyEnvironmentVariable = "ACCENGINEER_AI_API_KEY";
    public const string ApiKeyFallbackEnvironmentVariable = "OPENROUTER_API_KEY";

    public static IAnalysisProvider Create(AiProviderOptions? options, HttpMessageHandler? handler = null)
    {
        if (options is null)
        {
            return new NullAnalysisProvider();
        }

        var endpoint = ResolveEndpoint(options);
        if (endpoint is null || string.IsNullOrWhiteSpace(options.Model))
        {
            return new NullAnalysisProvider();
        }

        var name = string.IsNullOrWhiteSpace(options.Provider) ? "openai-compatible" : options.Provider!;
        var timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 600));
        var limiter = new RequestLimiter(options.MaxConcurrentRequests, options.MaxRequestsPerMinute);

        return new OpenAiCompatibleProvider(
            name,
            endpoint,
            options.Model!,
            ResolveApiKey(options),
            timeout,
            limiter,
            handler);
    }

    public static string? ResolveEndpoint(AiProviderOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return options.Endpoint!.Trim().TrimEnd('/');
        }

        return options.Provider?.Trim().ToLowerInvariant() switch
        {
            "openrouter" => "https://openrouter.ai/api/v1",
            "local" or "lmstudio" or "lm-studio" => "http://localhost:1234/v1",
            "ollama" => "http://localhost:11434/v1",
            _ => null
        };
    }

    public static string? ResolveApiKey(AiProviderOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return options.ApiKey;
        }

        return Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable)
            ?? Environment.GetEnvironmentVariable(ApiKeyFallbackEnvironmentVariable);
    }
}
