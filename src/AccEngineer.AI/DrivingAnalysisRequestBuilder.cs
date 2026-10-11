using AccEngineer.Analysis;

namespace AccEngineer.AI;

/// <summary>
/// Turns one deterministic <see cref="TelemetryEvent"/> into a compact
/// <see cref="AnalysisRequest"/>. Only the evidence sample range is attached
/// (capped by <paramref name="maxSamples"/>), so the model reasons over a small,
/// traceable window rather than an entire telemetry stream.
/// </summary>
public static class DrivingAnalysisRequestBuilder
{
    public static AnalysisRequest Build(
        TelemetryEvent finding,
        SessionLapAnalysis session,
        LapTimeline? lap = null,
        LapTimeline? reference = null,
        int maxSamples = AnalysisPrompts.MaxSamples)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(session);

        var context = new AnalysisContext
        {
            Track = finding.Evidence.Count > 0 ? session.TrackName : session.TrackName,
            Car = session.CarName,
            SessionType = session.SessionType.ToString(),
            LapNumber = finding.LapNumber,
            ReferenceLapNumber = reference?.LapNumber,
            LapTimeSeconds = lap?.LapTimeSeconds,
            ReferenceLapTimeSeconds = reference?.LapTimeSeconds
        };

        var metrics = new List<AnalysisMetric>
        {
            new("Category", finding.Category.ToString()),
            new("Severity", finding.Severity.ToString()),
            new("Confidence", finding.Confidence.ToString())
        };

        if (finding.ProgressMeters is { } progress)
        {
            metrics.Add(new AnalysisMetric("Track location", progress.ToString("0")) { Unit = "m" });
        }

        return new AnalysisRequest
        {
            Context = context,
            EventCategory = finding.Category.ToString(),
            EventDescription = finding.Observation,
            Metrics = metrics,
            Samples = BuildSamples(lap, finding, maxSamples),
            DataQualityWarnings = session.Warnings,
            Question = "Explain the most likely cause, state what the data does and does not support, and give one concrete test to confirm or rule it out."
        };
    }

    private static IReadOnlyList<SampleExcerpt> BuildSamples(
        LapTimeline? lap,
        TelemetryEvent finding,
        int maxSamples)
    {
        if (lap is null)
        {
            return Array.Empty<SampleExcerpt>();
        }

        var window = SelectWindow(lap.Samples, finding);
        if (window.Count == 0)
        {
            return Array.Empty<SampleExcerpt>();
        }

        var capped = Math.Clamp(maxSamples, 1, 200);
        var selected = window.Count <= capped ? window : Downsample(window, capped);

        return selected.Select(sample => new SampleExcerpt
        {
            SampleId = sample.Source.SampleId,
            ProgressMeters = sample.ProgressMeters,
            SpeedKph = sample.Source.SpeedKph,
            Throttle = sample.Source.ThrottleInput,
            Brake = sample.Source.BrakeInput,
            Steering = sample.Source.SteeringInput
        }).ToArray();
    }

    private static List<LapSample> SelectWindow(IReadOnlyList<LapSample> samples, TelemetryEvent finding)
    {
        var range = finding.Evidence.FirstOrDefault(e => e.FirstSampleId is not null && e.LastSampleId is not null);
        if (range is not null)
        {
            var first = range.FirstSampleId!.Value;
            var last = range.LastSampleId!.Value;
            var byId = samples.Where(s => s.Source.SampleId >= first && s.Source.SampleId <= last).ToList();
            if (byId.Count > 0)
            {
                return byId;
            }
        }

        if (finding.ProgressMeters is { } progress)
        {
            return samples
                .Where(s => s.ProgressMeters is { } q && Math.Abs(q - progress) <= 50)
                .ToList();
        }

        return new List<LapSample>();
    }

    private static List<LapSample> Downsample(IReadOnlyList<LapSample> samples, int count)
    {
        var result = new List<LapSample>(count);
        var step = (double)(samples.Count - 1) / (count - 1);
        for (var i = 0; i < count; i++)
        {
            result.Add(samples[(int)Math.Round(i * step)]);
        }

        return result;
    }
}
