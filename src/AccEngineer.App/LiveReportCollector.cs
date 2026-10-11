using System.Globalization;
using System.IO;
using AccEngineer.AI;
using AccEngineer.Analysis;
using AccEngineer.Pipeline;
using AccEngineer.Reporting;
using AccEngineer.Storage;
using AccEngineer.Telemetry;

namespace AccEngineer.App;

/// <summary>
/// Live event reports for the console app. Mirrors the UI pipeline: it feeds a
/// rolling evidence buffer, analyses each completed lap deterministically, queues
/// significant findings (deduplicated) and writes a JSON + Markdown report per
/// event plus a session summary on exit. AI interpretation is optional and only
/// used when a provider is configured through the environment.
/// </summary>
internal sealed class LiveReportCollector
{
    private const int MaxSessionSamples = 200_000;

    private readonly AppOptions _options;
    private readonly ConsoleDisplay _display;
    private readonly LiveEventPipeline _pipeline;
    private readonly LiveReportMarkdownExporter _markdown = new();
    private readonly JsonReportExporter _json = new();
    private readonly List<TelemetrySample> _session = new();
    private readonly IAnalysisProvider _provider;

    private string? _sessionId;
    private int _analysedLap;
    private int _submittedLap;

    public LiveReportCollector(AppOptions options, ConsoleDisplay display)
    {
        _options = options;
        _display = display;
        _pipeline = new LiveEventPipeline();
        _pipeline.ReportReady += WriteLiveReport;
        _provider = AnalysisProviderFactory.Create(new AiProviderOptions
        {
            Provider = Environment.GetEnvironmentVariable("ACCENGINEER_AI_PROVIDER"),
            Model = Environment.GetEnvironmentVariable("ACCENGINEER_AI_MODEL"),
            Endpoint = Environment.GetEnvironmentVariable("ACCENGINEER_AI_ENDPOINT")
        });
    }

    public void Observe(TelemetrySample sample)
    {
        _pipeline.Observe(sample);

        if (_sessionId != sample.SessionId)
        {
            _session.Clear();
            _sessionId = sample.SessionId;
            _analysedLap = 0;
            _submittedLap = 0;
            _display.Status($"Live reports tracking session {sample.SessionId}.");
        }

        if (_session.Count >= MaxSessionSamples)
        {
            _session.RemoveAt(0);
        }

        _session.Add(sample);

        var completedLap = sample.LapNumber - 1;
        if (completedLap >= 1 && completedLap > _analysedLap)
        {
            AnalyseCompletedLap(completedLap);
        }
    }

    public void Complete()
    {
        try
        {
            var analysis = _session.Count == 0 ? null : new LapAnalyzer().Analyze(_session);
            var session = BuildSession(analysis);
            var laps = analysis?.Laps ?? (IReadOnlyList<LapTimeline>)Array.Empty<LapTimeline>();
            var warnings = analysis?.Warnings ?? Array.Empty<string>();
            var summary = _pipeline.BuildSessionSummary(session, laps, warnings);

            var directory = ReportDirectory();
            var baseName = BuildBaseName(session.SessionId, summary.GeneratedAtUtc, "summary");
            var markdownPath = Path.Combine(directory, baseName + ".live.md");
            var jsonPath = Path.Combine(directory, baseName + ".live.json");
            File.WriteAllText(markdownPath, _markdown.ToMarkdown(summary));
            File.WriteAllText(jsonPath, _json.Serialize(summary));
            _display.Status($"Session summary written: {Path.GetFullPath(markdownPath)}");
        }
        catch (Exception ex)
        {
            _display.StatusError($"Failed to write session summary: {ex.Message}");
        }
    }

    private void AnalyseCompletedLap(int completedLap)
    {
        try
        {
            var analysis = new LapAnalyzer().Analyze(_session);
            var driving = new DrivingAnalyzer().Analyze(analysis);
            var session = BuildSession(analysis);

            var submitted = 0;
            foreach (var finding in driving.Events)
            {
                if (finding.LapNumber is not { } lap || lap <= _submittedLap || lap > completedLap)
                {
                    continue;
                }

                if (_pipeline.Submit(finding, session, driving.Warnings))
                {
                    submitted++;
                }
            }

            _analysedLap = completedLap;
            _submittedLap = completedLap;

            if (submitted > 0)
            {
                _display.Status($"Lap {completedLap}: {submitted} live finding(s) queued.");
            }

            _pipeline.DrainAsync(_provider).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _display.StatusError($"Live lap analysis failed: {ex.Message}");
        }
    }

    private void WriteLiveReport(LiveEventItem item)
    {
        if (item.Report is null)
        {
            return;
        }

        try
        {
            var directory = ReportDirectory();
            var baseName = BuildBaseName(item.Session.SessionId, item.Report.GeneratedAtUtc, item.Id);
            var markdownPath = Path.Combine(directory, baseName + ".live.md");
            var jsonPath = Path.Combine(directory, baseName + ".live.json");
            File.WriteAllText(markdownPath, _markdown.ToMarkdown(item.Report));
            File.WriteAllText(jsonPath, _json.Serialize(item.Report));
            _display.Status($"Live report: {Path.GetFullPath(markdownPath)}");
        }
        catch (Exception ex)
        {
            _display.StatusError($"Failed to write live report: {ex.Message}");
        }
    }

    private ReportSession BuildSession(SessionLapAnalysis? analysis) => new()
    {
        SessionId = analysis?.SessionId ?? _sessionId,
        Track = analysis?.TrackName,
        Car = analysis?.CarName,
        SessionType = analysis?.SessionType ?? SessionType.Unknown
    };

    private string ReportDirectory()
    {
        var directory = string.IsNullOrWhiteSpace(_options.ReportDirectory) ? "reports" : _options.ReportDirectory;
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string BuildBaseName(string? sessionId, DateTimeOffset generatedAtUtc, string suffix)
    {
        var stamp = generatedAtUtc.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var id = string.IsNullOrWhiteSpace(sessionId) ? "session" : SessionPaths.Sanitize(sessionId!);
        return $"{stamp}-{id}-{SessionPaths.Sanitize(suffix)}";
    }
}
