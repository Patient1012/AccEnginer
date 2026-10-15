using AccEngineer.Analysis.Ai;

namespace AccEngineer.AI;

/// <summary>
/// Turns one ranked deterministic <see cref="AiFinding"/> into a compact provider-neutral
/// <see cref="AnalysisRequest"/>. Only the measured summary and evidence strings are
/// forwarded: no raw telemetry samples are attached, because the deterministic engine
/// has already reduced the data to facts the model can reason about.
/// </summary>
public static class FindingAnalysisRequestBuilder
{
    public const string DefaultQuestion =
        "Explain the most likely cause, state what the data does and does not support, and give one concrete test to confirm or rule it out.";

    public static AnalysisRequest Build(AiFinding finding, AiSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(summary);

        var metrics = new List<AnalysisMetric>
        {
            new("Category", finding.Category),
            new("Severity", finding.Severity),
            new("Confidence", finding.ConfidenceLabel ?? "Unknown"),
            new("Confidence score", finding.Confidence.ToString("0.00"))
        };

        if (finding.CornerNumber is { } corner)
        {
            metrics.Add(new AnalysisMetric("Corner", corner.ToString()));
        }

        if (finding.EstimatedLossSeconds is { } loss)
        {
            metrics.Add(new AnalysisMetric("Estimated time loss", loss.ToString("0.000")) { Unit = "s" });
        }

        if (finding.StartDistanceMeters is { } start)
        {
            metrics.Add(new AnalysisMetric("Start distance", start.ToString("0")) { Unit = "m" });
        }

        if (finding.EndDistanceMeters is { } end)
        {
            metrics.Add(new AnalysisMetric("End distance", end.ToString("0")) { Unit = "m" });
        }

        foreach (var evidence in finding.Evidence)
        {
            metrics.Add(new AnalysisMetric("Evidence", evidence));
        }

        return new AnalysisRequest
        {
            Context = new AnalysisContext
            {
                Track = summary.Track,
                Car = summary.Car,
                SessionType = summary.Session,
                LapTimeSeconds = summary.CurrentLapSeconds,
                ReferenceLapTimeSeconds = summary.ReferenceLapSeconds
            },
            EventCategory = finding.Category,
            EventDescription = finding.Observation,
            Metrics = metrics,
            DataQualityWarnings = summary.Warnings,
            ComparisonSummary = BuildComparisonSummary(summary),
            Question = DefaultQuestion
        };
    }

    private static string? BuildComparisonSummary(AiSessionSummary summary)
    {
        if (summary.ReferenceLap is null && summary.CurrentLap is null)
        {
            return null;
        }

        var delta = summary.TotalDeltaSeconds is { } d ? $" ({d:+0.000;-0.000;0.000} s)" : string.Empty;
        return $"Reference lap {summary.ReferenceLap ?? "--"}, current lap {summary.CurrentLap ?? "--"}{delta}.";
    }
}
