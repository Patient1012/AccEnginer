using System.Globalization;
using System.IO;
using AccEngineer.AI;
using AccEngineer.Analysis;
using AccEngineer.Pipeline;
using AccEngineer.Reporting;
using AccEngineer.Telemetry;

namespace AccEngineer.UI.Services;

public sealed record LiveReportFiles(string MarkdownPath, string JsonPath);

/// <summary>
/// Drives the live event pipeline from the telemetry service. It feeds a rolling
/// evidence buffer, analyses each completed lap deterministically, submits new
/// findings to the queue (which coalesces repeats and applies rate/queue limits),
/// and drains interpretations on a background thread so the UI pump never blocks.
/// Deterministic reports are written even when no AI provider is configured.
/// </summary>
public sealed class LiveReportService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly LiveTelemetryService _telemetry;
    private readonly SessionEventLog _events;
    private readonly LiveEventPipeline _pipeline;
    private readonly LiveReportMarkdownExporter _markdown = new();
    private readonly JsonReportExporter _json = new();
    private readonly object _gate = new();

    private long _lastObservedSampleId = -1;
    private string? _sessionId;
    private int _analysedLap;
    private int _submittedLap;
    private volatile bool _draining;
    private IAnalysisProvider _provider;
    private string _providerSignature;

    public LiveReportService(AppSettings settings, LiveTelemetryService telemetry, SessionEventLog events)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _pipeline = new LiveEventPipeline();
        _provider = BuildProvider(out _providerSignature);
    }

    /// <summary>Raised when findings or counters change, so the page can refresh.</summary>
    public event Action? Changed;

    public LiveEventPipeline Pipeline => _pipeline;

    public bool IsAiConfigured => _provider.IsConfigured;

    public void Poll()
    {
        var latest = _telemetry.Latest;
        if (latest is null) return;

        if (_sessionId != latest.SessionId)
        {
            ResetForSession(latest.SessionId);
        }

        ObserveNewSamples();

        var completedLap = latest.LapNumber - 1;
        if (completedLap >= 1 && completedLap > _analysedLap)
        {
            AnalyseCompletedLap(completedLap);
        }

        StartDrain();
    }

    /// <summary>Rebuilds the AI provider when the settings changed.</summary>
    public void EnsureProvider()
    {
        var provider = BuildProvider(out var signature);
        if (signature != _providerSignature)
        {
            _provider = provider;
            _providerSignature = signature;
            Changed?.Invoke();
        }
    }

    public LiveReportFiles ExportLiveReport(LiveEventItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Report is null)
        {
            throw new InvalidOperationException("The event has no report yet.");
        }

        var baseName = BuildBaseName(item.Session, item.Report.GeneratedAtUtc, item.Id);
        return Write(baseName, _markdown.ToMarkdown(item.Report), _json.Serialize(item.Report));
    }

    public LiveReportFiles ExportSessionSummary()
    {
        var summary = BuildSessionSummary();
        var baseName = BuildBaseName(summary.Session, summary.GeneratedAtUtc, "summary");
        return Write(baseName, _markdown.ToMarkdown(summary), _json.Serialize(summary));
    }

    public SessionSummaryReport BuildSessionSummary()
    {
        var samples = _telemetry.SessionSamplesSnapshot();
        var analysis = samples.Count == 0 ? null : new LapAnalyzer().Analyze(samples);
        var session = BuildReportSession(analysis);
        var laps = analysis?.Laps ?? (IReadOnlyList<LapTimeline>)Array.Empty<LapTimeline>();
        var warnings = analysis?.Warnings ?? Array.Empty<string>();
        return _pipeline.BuildSessionSummary(session, laps, warnings);
    }

    public void Reset()
    {
        _pipeline.Reset();
        lock (_gate)
        {
            _lastObservedSampleId = -1;
            _sessionId = null;
            _analysedLap = 0;
            _submittedLap = 0;
        }
    }

    private void ResetForSession(string sessionId)
    {
        _pipeline.Reset();
        lock (_gate)
        {
            _lastObservedSampleId = -1;
            _analysedLap = 0;
            _submittedLap = 0;
            _sessionId = sessionId;
        }

        _events.Enqueue(new SessionEvent
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Kind = SessionEventKind.Report,
            Severity = EventSeverity.Info,
            Message = $"Live event pipeline tracking session {sessionId}."
        });
    }

    private void ObserveNewSamples()
    {
        var recent = _telemetry.SnapshotRecent();
        var newSamples = recent.Where(s => s.SampleId > _lastObservedSampleId).ToArray();
        if (newSamples.Length == 0) return;

        _pipeline.ObserveRange(newSamples);
        _lastObservedSampleId = newSamples[^1].SampleId;
    }

    private void AnalyseCompletedLap(int completedLap)
    {
        try
        {
            var samples = _telemetry.SessionSamplesSnapshot();
            if (samples.Count == 0)
            {
                return;
            }

            var analysis = new LapAnalyzer().Analyze(samples);
            var driving = new DrivingAnalyzer().Analyze(analysis);
            var session = BuildReportSession(analysis);

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
                _events.Enqueue(new SessionEvent
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Kind = SessionEventKind.Report,
                    Severity = EventSeverity.Info,
                    Message = $"Lap {completedLap} analysed: {submitted} new live finding(s) queued."
                });
                Changed?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _events.Enqueue(new SessionEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Kind = SessionEventKind.Report,
                Severity = EventSeverity.Warning,
                Message = $"Live lap analysis failed: {ex.Message}"
            });
        }
    }

    private void StartDrain()
    {
        if (_draining)
        {
            return;
        }

        if (_pipeline.PendingCount == 0 && _provider.Name == "none")
        {
            return;
        }

        _draining = true;
        var provider = _provider;
        _ = Task.Run(async () =>
        {
            try
            {
                await _pipeline.DrainAsync(provider, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _events.Enqueue(new SessionEvent
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Kind = SessionEventKind.Report,
                    Severity = EventSeverity.Warning,
                    Message = $"Live interpretation drain failed: {ex.Message}"
                });
            }
            finally
            {
                _draining = false;
                Changed?.Invoke();
            }
        });
    }

    private ReportSession BuildReportSession(SessionLapAnalysis? analysis)
    {
        var latest = _telemetry.Latest;
        return new ReportSession
        {
            SessionId = analysis?.SessionId ?? latest?.SessionId,
            Track = analysis?.TrackName ?? latest?.TrackName,
            Car = analysis?.CarName ?? latest?.CarName,
            SessionType = analysis is { SessionType: not SessionType.Unknown }
                ? analysis.SessionType
                : latest?.SessionType ?? SessionType.Unknown
        };
    }

    private IAnalysisProvider BuildProvider(out string signature)
    {
        signature = string.Join(
            '|',
            _settings.AiProvider ?? string.Empty,
            _settings.AiModel ?? string.Empty,
            _settings.AiEndpoint ?? string.Empty);

        return AnalysisProviderFactory.Create(new AiProviderOptions
        {
            Provider = _settings.AiProvider,
            Model = _settings.AiModel,
            Endpoint = _settings.AiEndpoint
        });
    }

    private LiveReportFiles Write(string baseName, string markdown, string json)
    {
        Directory.CreateDirectory(_settings.ReportsDirectory);
        var markdownPath = Path.Combine(_settings.ReportsDirectory, baseName + ".live.md");
        var jsonPath = Path.Combine(_settings.ReportsDirectory, baseName + ".live.json");
        File.WriteAllText(markdownPath, markdown);
        File.WriteAllText(jsonPath, json);
        return new LiveReportFiles(markdownPath, jsonPath);
    }

    private static string BuildBaseName(ReportSession session, DateTimeOffset generatedAtUtc, string suffix)
    {
        var stamp = generatedAtUtc.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var sessionId = string.IsNullOrWhiteSpace(session.SessionId)
            ? "session"
            : Storage.SessionPaths.Sanitize(session.SessionId);
        var safeSuffix = Storage.SessionPaths.Sanitize(suffix);
        return $"{stamp}-{sessionId}-{safeSuffix}";
    }

    public void Dispose() => Reset();
}
