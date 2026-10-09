using System.Globalization;
using System.IO;
using AccEngineer.Analysis;
using AccEngineer.Reporting;
using AccEngineer.Storage;
using AccEngineer.Telemetry;

namespace AccEngineer.UI.Services;

public sealed record ReportFiles(string MarkdownPath, string JsonPath);

public sealed record ReportResult(
    LapReport Report,
    string Markdown,
    string Json,
    int? ReferenceLapNumber,
    string BaseName);

/// <summary>
/// Builds deterministic lap reports from recorded or live samples. Runs entirely
/// offline and never calls an AI provider, so a report is always available even
/// when no provider is configured.
/// </summary>
public sealed class ReportService
{
    private readonly AppSettings _settings;

    public ReportService(AppSettings settings) =>
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public ReportResult? Generate(IReadOnlyList<TelemetrySample> samples, SessionMetadata? metadata, DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0) return null;

        var analysis = new LapAnalyzer().Analyze(samples);
        if (metadata is not null)
        {
            analysis = analysis with
            {
                SessionId = metadata.SessionId,
                TrackName = metadata.TrackName ?? analysis.TrackName,
                CarName = metadata.CarName ?? analysis.CarName,
                SessionType = metadata.SessionType != SessionType.Unknown ? metadata.SessionType : analysis.SessionType
            };
        }

        var reference = ReferenceLapSelector.SelectFastestValid(analysis.Laps);
        var comparison = reference is null ? null : new LapComparer().Compare(reference, analysis.Laps);
        var report = new ReportBuilder().Build(analysis, comparison, generatedAtUtc);

        return new ReportResult(
            report,
            new MarkdownReportExporter().ToMarkdown(report),
            new JsonReportExporter().ToJson(report),
            reference?.LapNumber,
            BuildBaseName(metadata, generatedAtUtc));
    }

    public ReportFiles Save(ReportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(_settings.ReportsDirectory);

        var markdownPath = Path.Combine(_settings.ReportsDirectory, result.BaseName + ".report.md");
        var jsonPath = Path.Combine(_settings.ReportsDirectory, result.BaseName + ".report.json");
        File.WriteAllText(markdownPath, result.Markdown);
        File.WriteAllText(jsonPath, result.Json);
        return new ReportFiles(markdownPath, jsonPath);
    }

    public static string BuildBaseName(SessionMetadata? metadata, DateTimeOffset generatedAtUtc)
    {
        var stamp = generatedAtUtc.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var sessionId = string.IsNullOrWhiteSpace(metadata?.SessionId)
            ? "session"
            : SessionPaths.Sanitize(metadata!.SessionId);
        return $"{stamp}-{sessionId}";
    }
}
