using AccEngineer.Analysis;

namespace AccEngineer.Reporting;

/// <summary>Neutral input for a live event report. Contains no AI provider types.</summary>
public sealed record LiveEventInput(
    TelemetryEvent Event,
    ReportSession Session,
    IReadOnlyList<ReportEvidence> Evidence,
    int EvidenceSampleCount,
    LiveEventInterpretation? Interpretation,
    IReadOnlyList<string> DataQualityWarnings);

/// <summary>Builds a live event report from deterministic analysis plus an optional interpretation.</summary>
public sealed class LiveEventReportBuilder
{
    public const int SchemaVersion = ReportSchema.Version;

    public LiveEventReport Build(LiveEventInput input, DateTimeOffset? generatedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        var notes = new List<string>
        {
            "The observation is a measured fact. Any possible cause is an interpretation and is not a proven diagnosis."
        };

        if (input.Interpretation is { Status: not "ok" })
        {
            notes.Add($"No AI interpretation is included ({input.Interpretation.Status}). Deterministic analysis is unaffected.");
        }

        return new LiveEventReport
        {
            SchemaVersion = SchemaVersion,
            GeneratedAtUtc = generatedAtUtc ?? DateTimeOffset.UtcNow,
            EventId = input.Event.Id,
            Session = input.Session,
            DataQuality = new ReportDataQuality
            {
                Status = input.DataQualityWarnings.Count == 0 ? "good" : "warnings",
                Warnings = input.DataQualityWarnings
            },
            Finding = new LiveEventFinding
            {
                Category = input.Event.Category.ToString(),
                Severity = input.Event.Severity.ToString(),
                Confidence = input.Event.Confidence.ToString(),
                LapNumber = input.Event.LapNumber,
                ProgressMeters = input.Event.ProgressMeters,
                Observation = input.Event.Observation,
                Interpretation = input.Event.Interpretation,
                Evidence = input.Evidence,
                EvidenceSampleCount = input.EvidenceSampleCount,
                SuggestedTest = input.Event.SuggestedTest,
                Limitations = input.Event.Limitations
            },
            Interpretation = input.Interpretation,
            Notes = notes
        };
    }
}

/// <summary>Neutral input for a session summary.</summary>
public sealed record SessionSummaryInput(
    ReportSession Session,
    IReadOnlyList<LapTimeline> Laps,
    IReadOnlyList<TelemetryEvent> Findings,
    IReadOnlyList<string> Warnings);

/// <summary>Consolidates laps and findings into a deterministic session summary.</summary>
public sealed class SessionSummaryReportBuilder
{
    public const int SchemaVersion = ReportSchema.Version;

    public SessionSummaryReport Build(SessionSummaryInput input, DateTimeOffset? generatedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        var validLaps = input.Laps.Where(l => l.IsValid && l.LapTimeSeconds is not null).ToArray();
        var best = validLaps.OrderBy(l => l.LapTimeSeconds).FirstOrDefault();

        var findings = input.Findings
            .GroupBy(e => (e.Category, e.Observation))
            .Select(g => new SessionSummaryFinding
            {
                Category = g.Key.Category.ToString(),
                Severity = g.Max(e => e.Severity).ToString(),
                Observation = g.Key.Observation,
                Occurrences = g.Count()
            })
            .OrderByDescending(f => SeverityRank(f.Severity))
            .ThenBy(f => f.Category, StringComparer.Ordinal)
            .ToArray();

        var notes = new List<string>
        {
            "Findings list measured observations. Occurrences counts how many times the same observation was detected across the session."
        };

        var warnings = input.Warnings.ToList();
        var invalid = input.Laps.Where(l => !l.IsValid).Select(l => l.LapNumber).ToArray();
        if (invalid.Length > 0)
        {
            warnings.Add($"Invalid or incomplete laps: {string.Join(", ", invalid)}.");
        }

        return new SessionSummaryReport
        {
            SchemaVersion = SchemaVersion,
            GeneratedAtUtc = generatedAtUtc ?? DateTimeOffset.UtcNow,
            Session = input.Session,
            DataQuality = new ReportDataQuality
            {
                Status = warnings.Count == 0 ? "good" : "warnings",
                Warnings = warnings
            },
            Laps = new SessionSummaryLaps
            {
                LapCount = input.Laps.Count,
                ValidLapCount = input.Laps.Count(l => l.IsValid),
                BestLapSeconds = best?.LapTimeSeconds,
                BestLapNumber = best?.LapNumber,
                Laps = input.Laps.Select(ReportBuilder.ToReportLap).ToArray()
            },
            Findings = findings,
            Notes = notes
        };
    }

    private static int SeverityRank(string severity) => severity switch
    {
        nameof(EventSeverity.High) => 3,
        nameof(EventSeverity.Medium) => 2,
        nameof(EventSeverity.Low) => 1,
        _ => 0
    };
}
