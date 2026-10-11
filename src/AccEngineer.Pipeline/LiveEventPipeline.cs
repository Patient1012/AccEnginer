using System.Globalization;
using AccEngineer.AI;
using AccEngineer.Analysis;
using AccEngineer.Reporting;
using AccEngineer.Telemetry;

namespace AccEngineer.Pipeline;

/// <summary>
/// Live event pipeline (roadmap §9): queues only significant findings, coalesces
/// repeats, attaches a small evidence window from a rolling buffer, applies rate
/// and queue limits, and produces deterministic live reports even when the AI
/// provider is slow, failing, or absent. The collector never waits on it.
/// </summary>
public sealed class LiveEventPipeline
{
    private readonly LiveEventPipelineOptions _options;
    private readonly RollingEvidenceBuffer _buffer;
    private readonly EventDeduplicator _dedup;
    private readonly Queue<LiveEventItem> _queue = new();
    private readonly List<LiveEventItem> _history = new();
    private readonly Dictionary<string, LiveEventItem> _byKey = new(StringComparer.Ordinal);
    private readonly LiveEventReportBuilder _reportBuilder = new();
    private readonly object _sync = new();
    private readonly TimeSpan _minInterval;

    private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;
    private int _accepted;
    private int _coalesced;
    private int _dropped;

    public LiveEventPipeline(LiveEventPipelineOptions? options = null)
    {
        _options = options ?? new LiveEventPipelineOptions();
        _buffer = new RollingEvidenceBuffer(_options.EvidenceBufferCapacity);
        _dedup = new EventDeduplicator(_options.DedupBucketMeters, _options.DedupCooldown);
        _minInterval = _options.MaxRequestsPerMinute <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(60.0 / _options.MaxRequestsPerMinute);
    }

    /// <summary>Raised after a live report is produced for an item.</summary>
    public event Action<LiveEventItem>? ReportReady;

    public LiveEventPipelineOptions Options => _options;

    public int PendingCount { get { lock (_sync) { return _queue.Count; } } }

    public int AcceptedCount => Volatile.Read(ref _accepted);

    public int CoalescedCount => Volatile.Read(ref _coalesced);

    public int DroppedCount => Volatile.Read(ref _dropped);

    public IReadOnlyList<LiveEventItem> History { get { lock (_sync) { return _history.ToArray(); } } }

    public void Observe(TelemetrySample sample) => _buffer.Add(sample);

    public void ObserveRange(IEnumerable<TelemetrySample> samples) => _buffer.AddRange(samples);

    /// <summary>
    /// Offers a finding to the queue. Returns false when it was coalesced into an
    /// existing item or could not be queued.
    /// </summary>
    public bool Submit(TelemetryEvent finding, ReportSession session, IReadOnlyList<string> warnings, DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(session);
        warnings ??= Array.Empty<string>();

        var key = _dedup.KeyFor(finding);
        if (!_dedup.ShouldAccept(finding, nowUtc ?? DateTimeOffset.UtcNow, out var coalesced))
        {
            Interlocked.Increment(ref _coalesced);
            lock (_sync)
            {
                if (_byKey.TryGetValue(key, out var existing))
                {
                    existing.CoalescedCount = Math.Max(existing.CoalescedCount, coalesced);
                }
            }

            return false;
        }

        var (evidence, sampleCount) = BuildEvidence(finding);
        var item = new LiveEventItem
        {
            Id = string.IsNullOrWhiteSpace(finding.Id) ? Guid.NewGuid().ToString("N") : finding.Id,
            Finding = finding,
            Session = session,
            Evidence = evidence,
            DataQualityWarnings = warnings,
            EvidenceSampleCount = sampleCount,
            CoalescedCount = coalesced
        };

        LiveEventItem? dropped = null;
        lock (_sync)
        {
            if (_queue.Count >= _options.MaxQueue)
            {
                dropped = _queue.Dequeue();
                dropped.State = LiveEventState.Dropped;
            }

            _queue.Enqueue(item);
            _byKey[key] = item;
        }

        if (dropped is not null)
        {
            Interlocked.Increment(ref _dropped);
        }

        Interlocked.Increment(ref _accepted);
        return true;
    }

    public async Task<LiveEventItem?> ProcessNextAsync(IAnalysisProvider provider, CancellationToken cancellationToken = default)
    {
        LiveEventItem? item;
        lock (_sync)
        {
            item = _queue.Count > 0 ? _queue.Dequeue() : null;
        }

        if (item is null)
        {
            return null;
        }

        item.State = LiveEventState.Interpreting;

        var interpretation = _options.Interpret && provider is { IsConfigured: true }
            ? await InterpretAsync(provider, item, cancellationToken).ConfigureAwait(false)
            : new LiveEventInterpretation
            {
                Status = "noProvider",
                Message = _options.Interpret
                    ? "No AI provider is configured; deterministic report only."
                    : "AI interpretation is disabled."
            };

        var evidence = item.Evidence
            .Select(e => e with { CoalescedCount = item.CoalescedCount })
            .ToArray();
        item.Evidence = evidence;

        item.Report = _reportBuilder.Build(new LiveEventInput(
            item.Finding,
            item.Session,
            evidence,
            item.EvidenceSampleCount,
            interpretation,
            item.DataQualityWarnings));
        item.State = LiveEventState.Completed;

        lock (_sync)
        {
            _history.Add(item);
            if (_history.Count > 500)
            {
                _history.RemoveRange(0, _history.Count - 500);
            }
        }

        ReportReady?.Invoke(item);
        return item;
    }

    public async Task<int> DrainAsync(IAnalysisProvider provider, CancellationToken cancellationToken = default)
    {
        var processed = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var item = await ProcessNextAsync(provider, cancellationToken).ConfigureAwait(false);
            if (item is null)
            {
                break;
            }

            processed++;
        }

        return processed;
    }

    /// <summary>Consolidates all reported events plus laps into a session summary.</summary>
    public SessionSummaryReport BuildSessionSummary(
        ReportSession session,
        IReadOnlyList<LapTimeline> laps,
        IReadOnlyList<string> warnings,
        DateTimeOffset? generatedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        laps ??= Array.Empty<LapTimeline>();
        warnings ??= Array.Empty<string>();

        var findings = History.Select(h => h.Finding).ToArray();
        return new SessionSummaryReportBuilder().Build(
            new SessionSummaryInput(session, laps, findings, warnings),
            generatedAtUtc);
    }

    public void Reset()
    {
        _buffer.Clear();
        _dedup.Reset();
        lock (_sync)
        {
            _queue.Clear();
            _history.Clear();
            _byKey.Clear();
        }
    }

    private async Task<LiveEventInterpretation> InterpretAsync(IAnalysisProvider provider, LiveEventItem item, CancellationToken cancellationToken)
    {
        try
        {
            await WaitForRateAsync(cancellationToken).ConfigureAwait(false);

            var request = BuildRequest(item);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.RequestTimeout);

            var result = await provider.AnalyzeAsync(request, timeout.Token).ConfigureAwait(false);
            return Map(result);
        }
        catch (OperationCanceledException)
        {
            return new LiveEventInterpretation { Status = "cancelled", Message = "The interpretation was cancelled." };
        }
        catch (Exception ex)
        {
            return new LiveEventInterpretation { Status = "unavailable", Message = $"Interpretation error: {ex.Message}" };
        }
    }

    private async Task WaitForRateAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan delay;
            lock (_sync)
            {
                var now = DateTimeOffset.UtcNow;
                var nextAllowed = _lastRequestUtc + _minInterval;
                if (now >= nextAllowed)
                {
                    _lastRequestUtc = now;
                    return;
                }

                delay = nextAllowed - now;
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private AnalysisRequest BuildRequest(LiveEventItem item)
    {
        var metrics = new List<AnalysisMetric>
        {
            new("Category", item.Finding.Category.ToString()),
            new("Severity", item.Finding.Severity.ToString()),
            new("Confidence", item.Finding.Confidence.ToString())
        };

        if (item.Finding.ProgressMeters is { } progress)
        {
            metrics.Add(new AnalysisMetric("Track location", progress.ToString("0", CultureInfo.InvariantCulture)) { Unit = "m" });
        }

        return new AnalysisRequest
        {
            Context = new AnalysisContext
            {
                Track = item.Session.Track,
                Car = item.Session.Car,
                SessionType = item.Session.SessionType.ToString(),
                LapNumber = item.Finding.LapNumber
            },
            EventCategory = item.Finding.Category.ToString(),
            EventDescription = item.Finding.Observation,
            Metrics = metrics,
            Samples = SelectSamples(item),
            DataQualityWarnings = item.DataQualityWarnings,
            Question = "Explain the most likely cause, state what the data does and does not support, and give one concrete test to confirm or rule it out."
        };
    }

    private IReadOnlyList<SampleExcerpt> SelectSamples(LiveEventItem item)
    {
        IReadOnlyList<TelemetrySample> raw;

        var range = item.Evidence.FirstOrDefault(e => e.FirstSampleId is not null && e.LastSampleId is not null);
        if (range is not null)
        {
            raw = _buffer.Range(range.FirstSampleId!.Value, range.LastSampleId!.Value);
        }
        else if (item.Finding.ProgressMeters is { } progress)
        {
            raw = _buffer.AroundProgress(progress, _options.EvidenceFallbackToleranceMeters);
        }
        else
        {
            raw = Array.Empty<TelemetrySample>();
        }

        if (raw.Count == 0)
        {
            return Array.Empty<SampleExcerpt>();
        }

        var capped = Math.Clamp(_options.MaxRequestSamples, 1, 200);
        var selected = raw.Count <= capped ? raw : Downsample(raw, capped);

        return selected.Select(sample => new SampleExcerpt
        {
            SampleId = sample.SampleId,
            ProgressMeters = sample.TrackProgressMeters,
            SpeedKph = sample.SpeedKph,
            Throttle = sample.ThrottleInput,
            Brake = sample.BrakeInput,
            Steering = sample.SteeringInput
        }).ToArray();
    }

    private static List<TelemetrySample> Downsample(IReadOnlyList<TelemetrySample> samples, int count)
    {
        var result = new List<TelemetrySample>(count);
        var step = (double)(samples.Count - 1) / (count - 1);
        for (var i = 0; i < count; i++)
        {
            result.Add(samples[(int)Math.Round(i * step)]);
        }

        return result;
    }

    private (IReadOnlyList<ReportEvidence> Evidence, int SampleCount) BuildEvidence(TelemetryEvent finding)
    {
        if (finding.Evidence.Count == 0)
        {
            return (Array.Empty<ReportEvidence>(), 0);
        }

        var evidence = finding.Evidence
            .Select(e => new ReportEvidence
            {
                Kind = e.Kind,
                Description = e.Description,
                FirstSampleId = e.FirstSampleId,
                LastSampleId = e.LastSampleId,
                StartProgressMeters = e.StartProgressMeters,
                EndProgressMeters = e.EndProgressMeters,
                CoalescedCount = 1
            })
            .ToArray();

        var sampleCount = 0;
        var primary = finding.Evidence.FirstOrDefault(e => e.FirstSampleId is not null && e.LastSampleId is not null);
        if (primary is not null)
        {
            sampleCount = _buffer.Range(primary.FirstSampleId!.Value, primary.LastSampleId!.Value).Count;
        }

        return (evidence, sampleCount);
    }

    private static LiveEventInterpretation Map(AnalysisResult result) => new()
    {
        Status = result.Outcome switch
        {
            AnalysisOutcome.Success => "ok",
            AnalysisOutcome.NoProvider => "noProvider",
            AnalysisOutcome.InvalidResponse => "invalid",
            AnalysisOutcome.Cancelled => "cancelled",
            _ => "unavailable"
        },
        Provider = result.Provider,
        FindingType = result.FindingType,
        Observation = result.Observation,
        PossibleCause = result.Interpretation,
        Evidence = result.Evidence,
        Confidence = result.Confidence,
        SuggestedTest = result.SuggestedTest,
        Limitations = result.Limitations,
        Message = result.Message
    };
}
