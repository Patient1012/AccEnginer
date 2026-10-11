using System.Text.Json;

namespace AccEngineer.AI;

/// <summary>
/// Parses and validates model output. Any malformed or schema-invalid payload is
/// returned as an <see cref="AnalysisOutcome.InvalidResponse"/> value so a bad
/// model reply can never interrupt the application.
/// </summary>
public static class AnalysisResultParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static AnalysisResult Parse(string? content, string? provider = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return AnalysisResult.Failure(AnalysisOutcome.InvalidResponse, "The model returned an empty response.");
        }

        var json = StripCodeFences(content.Trim());

        ResponseDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ResponseDto>(json, Options);
        }
        catch (JsonException ex)
        {
            return Failure($"The model response was not valid JSON: {ex.Message}", provider);
        }

        if (dto is null)
        {
            return Failure("The model response was empty JSON.", provider);
        }

        if (string.IsNullOrWhiteSpace(dto.Observation))
        {
            return Failure("The model response is missing the required 'observation' field.", provider);
        }

        double? confidence = dto.Confidence;
        if (confidence is { } value && (value < 0 || value > 1))
        {
            confidence = Math.Clamp(value, 0, 1);
        }

        return new AnalysisResult
        {
            Outcome = AnalysisOutcome.Success,
            Provider = provider,
            FindingType = NullIfBlank(dto.FindingType),
            Observation = dto.Observation.Trim(),
            Interpretation = NullIfBlank(dto.Interpretation),
            Evidence = dto.Evidence ?? Array.Empty<string>(),
            Confidence = confidence,
            SuggestedTest = NullIfBlank(dto.SuggestedTest),
            Limitations = dto.Limitations ?? Array.Empty<string>()
        };
    }

    private static AnalysisResult Failure(string message, string? provider) => new()
    {
        Outcome = AnalysisOutcome.InvalidResponse,
        Message = message,
        Provider = provider
    };

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string StripCodeFences(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewline = text.IndexOf('\n');
        if (firstNewline < 0)
        {
            return text;
        }

        var body = text[(firstNewline + 1)..];
        var closing = body.LastIndexOf("```", StringComparison.Ordinal);
        if (closing >= 0)
        {
            body = body[..closing];
        }

        return body.Trim();
    }

    private sealed class ResponseDto
    {
        public string? FindingType { get; set; }

        public string? Observation { get; set; }

        public string[]? Evidence { get; set; }

        public string? Interpretation { get; set; }

        public double? Confidence { get; set; }

        public string? SuggestedTest { get; set; }

        public string[]? Limitations { get; set; }
    }
}
