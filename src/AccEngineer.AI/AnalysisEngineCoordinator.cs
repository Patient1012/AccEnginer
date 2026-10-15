using AccEngineer.Analysis;
using AccEngineer.Analysis.Ai;

namespace AccEngineer.AI;

/// <summary>One finding paired with the model's interpretation of it.</summary>
public sealed record FindingAnalysis
{
    public required AiFinding Finding { get; init; }

    public required AnalysisResult Result { get; init; }
}

/// <summary>Deterministic analysis plus the AI interpretation for its top findings.</summary>
public sealed record EngineAnalysisReport
{
    public required SessionAnalysisResult Analysis { get; init; }

    public IReadOnlyList<FindingAnalysis> AiFindings { get; init; } = Array.Empty<FindingAnalysis>();

    /// <summary>True when no provider was configured and only deterministic output is present.</summary>
    public bool IsAIAvailable => AiFindings.Count > 0;
}

/// <summary>
/// Integration point between the deterministic calculation engine and the AI engineer.
/// It runs <see cref="AnalysisEngine"/>, then asks <see cref="IAnalysisProvider"/> to
/// interpret each ranked finding. When no provider is configured, or a call fails, the
/// measured analysis is returned unchanged: the AI is strictly an interpretation layer.
/// </summary>
public sealed class AnalysisEngineCoordinator
{
    private readonly AnalysisEngine _engine;
    private readonly IAnalysisProvider _provider;

    public AnalysisEngineCoordinator(IAnalysisProvider? provider = null, AnalysisEngineOptions? options = null)
    {
        _provider = provider ?? new NullAnalysisProvider();
        _engine = new AnalysisEngine(options);
    }

    /// <summary>Runs the engine then interprets its top findings.</summary>
    public async Task<EngineAnalysisReport> AnalyzeAsync(
        SessionAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var analysis = _engine.Analyze(request);
        return await InterpretAsync(analysis, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Interprets an already-computed deterministic result.</summary>
    public async Task<EngineAnalysisReport> InterpretAsync(
        SessionAnalysisResult analysis,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        if (!_provider.IsConfigured)
        {
            return new EngineAnalysisReport { Analysis = analysis };
        }

        var interpretations = new List<FindingAnalysis>(analysis.AiSummary.TopFindings.Count);

        foreach (var finding in analysis.AiSummary.TopFindings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = FindingAnalysisRequestBuilder.Build(finding, analysis.AiSummary);
            var result = await AnalyzeFindingAsync(request, cancellationToken).ConfigureAwait(false);
            interpretations.Add(new FindingAnalysis { Finding = finding, Result = result });
        }

        return new EngineAnalysisReport
        {
            Analysis = analysis,
            AiFindings = interpretations
        };
    }

    private async Task<AnalysisResult> AnalyzeFindingAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _provider.AnalyzeAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return AnalysisResult.Failure(AnalysisOutcome.Unavailable, ex.Message);
        }
    }
}
