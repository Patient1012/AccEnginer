namespace AccEngineer.AI;

/// <summary>
/// Provider-neutral analysis interface (roadmap §9). Implementations must never
/// throw for expected failures — invalid output, timeouts, rate limits and
/// cancellation are returned as <see cref="AnalysisResult"/> values so the caller
/// can continue recording telemetry.
/// </summary>
public interface IAnalysisProvider
{
    /// <summary>Friendly provider name used in results and diagnostics.</summary>
    string Name { get; }

    /// <summary>True when the provider has enough configuration to be called.</summary>
    bool IsConfigured { get; }

    Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// No-AI fallback. Always succeeds as a value, reporting that no provider is
/// configured so callers can keep working without an interpretation layer.
/// </summary>
public sealed class NullAnalysisProvider : IAnalysisProvider
{
    public string Name => "none";

    public bool IsConfigured => false;

    public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
        => Task.FromResult(AnalysisResult.Failure(
            AnalysisOutcome.NoProvider,
            "No AI provider is configured. Measured analysis and reports are unaffected."));
}
