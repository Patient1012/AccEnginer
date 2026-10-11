using System.Globalization;
using System.Text;

namespace AccEngineer.AI;

/// <summary>
/// Builds the provider-neutral prompt. The system prompt pins the model to a
/// strict JSON schema and, critically, to keeping measured facts separate from
/// hypotheses (roadmap §9). The user prompt carries only the prepared evidence:
/// no whole telemetry stream is ever included.
/// </summary>
public static class AnalysisPrompts
{
    public const string System =
        "You are a motorsport telemetry analyst for Assetto Corsa Competizione. " +
        "You receive measured telemetry facts, a small sample window, and data-quality warnings.\n" +
        "Rules:\n" +
        "- Separate facts from hypotheses. 'observation' may only restate what the provided data shows.\n" +
        "- 'interpretation' is a possible explanation, never a proven diagnosis.\n" +
        "- Never invent values that are not present in the input; list missing channels under 'limitations'.\n" +
        "- Suggested tests must be concrete and repeatable.\n" +
        "- Respond with a single JSON object and no other text, matching exactly this schema:\n" +
        "{\n" +
        "  \"findingType\": \"string\",\n" +
        "  \"observation\": \"string\",\n" +
        "  \"evidence\": [\"string\"],\n" +
        "  \"interpretation\": \"string\",\n" +
        "  \"confidence\": 0.0,\n" +
        "  \"suggestedTest\": \"string\",\n" +
        "  \"limitations\": [\"string\"]\n" +
        "}";

    /// <summary>Maximum raw samples included, regardless of how many are supplied.</summary>
    public const int MaxSamples = 40;

    public static string BuildUserPrompt(AnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var builder = new StringBuilder();

        var prompt = new StringBuilder();
        prompt.AppendLine("METADATA");
        AppendMetadata(prompt, request.Context);
        prompt.AppendLine();

        prompt.AppendLine("EVENT");
        prompt.AppendLine($"Category: {request.EventCategory}");
        prompt.AppendLine($"Observation (measured fact): {request.EventDescription}");
        prompt.AppendLine();

        if (request.Metrics.Count > 0)
        {
            prompt.AppendLine("METRICS");
            foreach (var metric in request.Metrics)
            {
                prompt.AppendLine(string.IsNullOrWhiteSpace(metric.Unit)
                    ? $"- {metric.Name}: {metric.Value}"
                    : $"- {metric.Name}: {metric.Value} {metric.Unit}");
            }
            prompt.AppendLine();
        }

        if (request.Samples.Count > 0)
        {
            prompt.AppendLine("SAMPLES");
            prompt.AppendLine("sampleId, progressM, speedKph, throttle, brake, steering");
            foreach (var sample in request.Samples)
            {
                prompt.AppendLine(string.Join(", ",
                    sample.SampleId.ToString(CultureInfo.InvariantCulture),
                    Fmt(sample.ProgressMeters),
                    Fmt(sample.SpeedKph),
                    Fmt(sample.Throttle),
                    Fmt(sample.Brake),
                    Fmt(sample.Steering)));
            }
            prompt.AppendLine();
        }

        if (request.ComparisonSummary is { Length: > 0 })
        {
            prompt.AppendLine("COMPARISON VS REFERENCE");
            prompt.AppendLine(request.ComparisonSummary);
            prompt.AppendLine();
        }

        if (request.DataQualityWarnings.Count > 0)
        {
            prompt.AppendLine("DATA QUALITY WARNINGS");
            foreach (var warning in request.DataQualityWarnings)
            {
                prompt.AppendLine($"- {warning}");
            }
            prompt.AppendLine();
        }

        prompt.AppendLine("QUESTION");
        prompt.AppendLine(request.Question ?? "Interpret this event. State the likely cause and a concrete test, and flag what the data cannot support.");

        return prompt.ToString();
    }

    private static void AppendMetadata(StringBuilder prompt, AnalysisContext context)
    {
        prompt.AppendLine($"Simulator: {context.Simulator ?? "unknown"}");
        prompt.AppendLine($"Track: {context.Track ?? "unknown"}");
        prompt.AppendLine($"Car: {context.Car ?? "unknown"}");
        prompt.AppendLine($"Session type: {context.SessionType ?? "unknown"}");
        prompt.AppendLine($"Lap: {FmtInt(context.LapNumber)}");
        prompt.AppendLine($"Reference lap: {FmtInt(context.ReferenceLapNumber)}");
        prompt.AppendLine($"Lap time (s): {Fmt(context.LapTimeSeconds)}");
        prompt.AppendLine($"Reference lap time (s): {Fmt(context.ReferenceLapTimeSeconds)}");
    }

    private static string Fmt(double? value) =>
        value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "n/a";

    private static string FmtInt(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "n/a";
}
