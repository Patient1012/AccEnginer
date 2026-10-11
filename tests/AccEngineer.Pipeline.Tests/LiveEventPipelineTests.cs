using AccEngineer.AI;
using AccEngineer.Analysis;
using AccEngineer.Reporting;

namespace AccEngineer.Pipeline.Tests;

public class LiveEventPipelineTests
{
    private static readonly DateTimeOffset SessionStart = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static LiveEventPipeline CreatePipeline(Action<LiveEventPipelineOptions>? configure = null)
    {
        var options = new LiveEventPipelineOptions { MaxRequestsPerMinute = 0 };
        configure?.Invoke(options);
        return new LiveEventPipeline(options);
    }

    private static Telemetry.TelemetrySample[] Samples(long from, long to)
        => Enumerable.Range((int)from, (int)(to - from + 1))
            .Select(i => PipelineTestData.Sample(i, i * 10.0))
            .ToArray();

    [Fact]
    public async Task ReportsDeterministicallyWithoutProvider()
    {
        var pipeline = CreatePipeline();
        pipeline.ObserveRange(Samples(1, 30));

        var submitted = pipeline.Submit(
            PipelineTestData.Event("e1", first: 10, last: 20),
            PipelineTestData.Session(),
            Array.Empty<string>(),
            SessionStart);

        var item = await pipeline.ProcessNextAsync(new NullAnalysisProvider());

        Assert.True(submitted);
        Assert.NotNull(item);
        Assert.NotNull(item.Report);
        Assert.Equal(ReportSchema.Version, item.Report.SchemaVersion);
        Assert.Equal("Late braking at turn 1.", item.Report.Finding.Observation);
        Assert.Equal("noProvider", item.Report.Interpretation?.Status);
        Assert.Equal(11, item.Report.Finding.EvidenceSampleCount);
        Assert.Equal(1, item.Report.Finding.Evidence[0].CoalescedCount);
        Assert.Single(pipeline.History);
    }

    [Fact]
    public async Task CoalescedRepeatUpdatesCountOnReport()
    {
        var pipeline = CreatePipeline();
        pipeline.ObserveRange(Samples(1, 30));

        Assert.True(pipeline.Submit(PipelineTestData.Event("e1", first: 10, last: 20), PipelineTestData.Session(), Array.Empty<string>(), SessionStart));
        var secondAccepted = pipeline.Submit(PipelineTestData.Event("e2", first: 10, last: 20), PipelineTestData.Session(), Array.Empty<string>(), SessionStart.AddSeconds(1));

        Assert.False(secondAccepted);
        Assert.Equal(1, pipeline.AcceptedCount);
        Assert.Equal(1, pipeline.CoalescedCount);

        var item = await pipeline.ProcessNextAsync(new NullAnalysisProvider());

        Assert.Equal(2, item!.Report!.Finding.Evidence[0].CoalescedCount);
    }

    [Fact]
    public async Task QueueLimitDropsOldest()
    {
        var pipeline = CreatePipeline(o => o.MaxQueue = 2);
        var session = PipelineTestData.Session();

        pipeline.Submit(PipelineTestData.Event("a", progress: 100), session, Array.Empty<string>(), SessionStart);
        pipeline.Submit(PipelineTestData.Event("b", progress: 300), session, Array.Empty<string>(), SessionStart.AddSeconds(1));
        pipeline.Submit(PipelineTestData.Event("c", progress: 500), session, Array.Empty<string>(), SessionStart.AddSeconds(2));

        Assert.Equal(3, pipeline.AcceptedCount);
        Assert.Equal(1, pipeline.DroppedCount);
        Assert.Equal(2, pipeline.PendingCount);

        var processed = await pipeline.DrainAsync(new NullAnalysisProvider());
        Assert.Equal(2, processed);
        Assert.Equal(0, pipeline.PendingCount);
    }

    [Fact]
    public async Task InterpretsWithProviderAndAttachesWindow()
    {
        var provider = new PipelineTestData.FakeProvider();
        var pipeline = CreatePipeline();
        pipeline.ObserveRange(Samples(1, 40));

        pipeline.Submit(PipelineTestData.Event("e1", first: 10, last: 20), PipelineTestData.Session(), Array.Empty<string>(), SessionStart);
        var item = await pipeline.ProcessNextAsync(provider);

        Assert.NotNull(item);
        Assert.Equal("ok", item.Report?.Interpretation?.Status);
        Assert.Equal("Possible premature trail-off.", item.Report?.Interpretation?.PossibleCause);
        Assert.NotNull(provider.LastRequest);
        Assert.Equal("Braking", provider.LastRequest.EventCategory);
        Assert.Equal(11, provider.LastRequest.Samples.Count);
        Assert.Equal(10, provider.LastRequest.Samples[0].SampleId);
        Assert.Equal(100, provider.LastRequest.Samples[0].ProgressMeters);
    }

    [Fact]
    public async Task ThrowingProviderBecomesUnavailable()
    {
        var throwing = new ThrowingProvider();
        var pipeline = CreatePipeline();

        pipeline.Submit(PipelineTestData.Event("e1"), PipelineTestData.Session(), Array.Empty<string>(), SessionStart);
        var item = await pipeline.ProcessNextAsync(throwing);

        Assert.Equal("unavailable", item!.Report!.Interpretation?.Status);
        Assert.Contains("Interpretation error", item.Report.Interpretation?.Message);
        Assert.Equal(1, item.Report.SchemaVersion);
    }

    [Fact]
    public async Task SessionSummaryConsolidatesFindingsAndSelectsBestValidLap()
    {
        var pipeline = CreatePipeline();
        var session = PipelineTestData.Session();

        pipeline.Submit(PipelineTestData.Event("a", category: EventCategory.Braking, progress: 100), session, Array.Empty<string>(), SessionStart);
        pipeline.Submit(PipelineTestData.Event("b", category: EventCategory.Braking, progress: 100, lap: 2), session, Array.Empty<string>(), SessionStart.AddSeconds(1));
        pipeline.Submit(
            PipelineTestData.Event("c", category: EventCategory.CornerExit, progress: 300, observation: "Early throttle at exit."),
            session,
            Array.Empty<string>(),
            SessionStart.AddSeconds(2));
        await pipeline.DrainAsync(new NullAnalysisProvider());

        var laps = new[]
        {
            PipelineTestData.Lap(1, valid: true, time: 95.0),
            PipelineTestData.Lap(2, valid: true, time: 90.0),
            PipelineTestData.Lap(3, valid: false, time: null)
        };

        var summary = pipeline.BuildSessionSummary(session, laps, Array.Empty<string>());

        Assert.Equal(2, summary.Findings.Count);
        Assert.Equal("Braking", summary.Findings[0].Category);
        Assert.Equal(2, summary.Findings[0].Occurrences);
        Assert.Equal("CornerExit", summary.Findings[1].Category);
        Assert.Equal(90.0, summary.Laps.BestLapSeconds);
        Assert.Equal(2, summary.Laps.BestLapNumber);
        Assert.Equal(2, summary.Laps.ValidLapCount);
        Assert.Contains("Invalid or incomplete laps: 3.", summary.DataQuality.Warnings);
    }

    [Fact]
    public async Task ResetClearsQueueHistoryAndBuffer()
    {
        var pipeline = CreatePipeline();
        pipeline.Observe(PipelineTestData.Sample(1, 10));
        pipeline.Submit(PipelineTestData.Event("e1"), PipelineTestData.Session(), Array.Empty<string>(), SessionStart);
        await pipeline.ProcessNextAsync(new NullAnalysisProvider());

        pipeline.Reset();

        Assert.Equal(0, pipeline.PendingCount);
        Assert.Empty(pipeline.History);
    }

    [Fact]
    public void MarkdownAndJsonExportsForAllReportTypes()
    {
        var exporter = new LiveReportMarkdownExporter();

        var session = PipelineTestData.Session();
        var pipeline = CreatePipeline();
        var summary = pipeline.BuildSessionSummary(session, new[] { PipelineTestData.Lap(1, valid: true, time: 95.0) }, Array.Empty<string>());

        var markdownSummary = exporter.ToMarkdown(summary);
        var jsonSummary = new JsonReportExporter().Serialize(summary);

        Assert.Contains("## Findings", markdownSummary);
        Assert.Contains("| Lap | Valid |", markdownSummary);
        Assert.Contains("\"bestLapNumber\"", jsonSummary);
        Assert.Contains("Monza", jsonSummary);
    }

    private sealed class ThrowingProvider : IAnalysisProvider
    {
        public string Name => "throwing";

        public bool IsConfigured => true;

        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
            => Task.FromException<AnalysisResult>(new InvalidOperationException("boom"));
    }
}