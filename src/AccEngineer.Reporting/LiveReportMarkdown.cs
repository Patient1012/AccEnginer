using System.Globalization;
using System.Text;

namespace AccEngineer.Reporting;

/// <summary>Deterministic Markdown for live event and session summary reports.</summary>
public sealed class LiveReportMarkdownExporter
{
    public string ToMarkdown(LiveEventReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();
        builder.Append("# Live event — ").Append(report.Finding.Category).AppendLine();
        builder.AppendLine();
        builder.Append("- Event: ").AppendLine(report.EventId);
        builder.Append("- Session: ").AppendLine(report.Session.SessionId ?? "?");
        builder.Append("- Track: ").AppendLine(report.Session.Track ?? "?");
        builder.Append("- Car: ").AppendLine(report.Session.Car ?? "?");
        builder.Append("- Lap: ").AppendLine(report.Finding.LapNumber?.ToString(CultureInfo.InvariantCulture) ?? "?");
        if (report.Finding.ProgressMeters is { } progress)
        {
            builder.Append("- Location: ").Append(progress.ToString("0", CultureInfo.InvariantCulture)).AppendLine(" m");
        }
        builder.Append("- Severity: ").AppendLine(report.Finding.Severity);
        builder.Append("- Confidence: ").AppendLine(report.Finding.Confidence);
        builder.Append("- Evidence samples: ").AppendLine(report.Finding.EvidenceSampleCount.ToString(CultureInfo.InvariantCulture));

        builder.AppendLine();
        builder.AppendLine("## Observation (measured fact)");
        builder.AppendLine(report.Finding.Observation);

        if (!string.IsNullOrWhiteSpace(report.Finding.Interpretation))
        {
            builder.AppendLine();
            builder.AppendLine("## Local analysis (possible interpretation)");
            builder.AppendLine(report.Finding.Interpretation);
        }

        if (report.Interpretation is { } ai)
        {
            builder.AppendLine();
            builder.Append("## AI interpretation");
            builder.Append(" (").Append(ai.Status).Append(')');
            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(ai.Observation))
            {
                builder.AppendLine(ai.Observation);
            }
            if (!string.IsNullOrWhiteSpace(ai.PossibleCause))
            {
                builder.AppendLine();
                builder.Append("Possible cause: ").AppendLine(ai.PossibleCause);
            }
            if (ai.Confidence is { } confidence)
            {
                builder.Append("Confidence: ").AppendLine(confidence.ToString("0.00", CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrWhiteSpace(ai.SuggestedTest))
            {
                builder.Append("Suggested test: ").AppendLine(ai.SuggestedTest);
            }
            if (ai.Limitations.Count > 0)
            {
                builder.Append("Limitations: ").AppendLine(string.Join("; ", ai.Limitations));
            }
            if (!string.IsNullOrWhiteSpace(ai.Message))
            {
                builder.Append("Note: ").AppendLine(ai.Message);
            }
        }

        if (report.Finding.Evidence.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Evidence");
            foreach (var evidence in report.Finding.Evidence)
            {
                builder.Append("- ").Append(evidence.Description);
                if (evidence.FirstSampleId is { } first && evidence.LastSampleId is { } last)
                {
                    builder.Append(" (samples ").Append(first).Append('–').Append(last).Append(')');
                }
                if (evidence.CoalescedCount > 1)
                {
                    builder.Append(" [coalesced x").Append(evidence.CoalescedCount).Append(']');
                }
                builder.AppendLine();
            }
        }

        AppendWarnings(builder, report.DataQuality);
        AppendNotes(builder, report.Notes);
        return builder.ToString();
    }

    public string ToMarkdown(SessionSummaryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();
        builder.Append("# Session summary — ").Append(report.Session.Track ?? "unknown track")
            .Append(" (").Append(report.Session.SessionType).AppendLine(")");
        builder.AppendLine();
        builder.Append("- Session: ").AppendLine(report.Session.SessionId ?? "?");
        builder.Append("- Car: ").AppendLine(report.Session.Car ?? "?");
        builder.Append("- Generated: ").Append(report.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture)).AppendLine(" UTC");
        builder.Append("- Laps: ").Append(report.Laps.ValidLapCount.ToString(CultureInfo.InvariantCulture))
            .Append(" valid of ").Append(report.Laps.LapCount.ToString(CultureInfo.InvariantCulture)).AppendLine();
        if (report.Laps.BestLapSeconds is { } best)
        {
            builder.Append("- Best lap: lap ").Append(report.Laps.BestLapNumber?.ToString(CultureInfo.InvariantCulture) ?? "?")
                .Append(" at ").Append(best.ToString("0.000", CultureInfo.InvariantCulture)).AppendLine(" s");
        }

        builder.AppendLine();
        builder.AppendLine("## Findings");
        if (report.Findings.Count == 0)
        {
            builder.AppendLine("No driving findings were produced.");
        }
        else
        {
            builder.AppendLine("| Category | Severity | Occurrences | Observation |");
            builder.AppendLine("| :--- | :--- | ---: | :--- |");
            foreach (var finding in report.Findings)
            {
                builder.Append("| ").Append(finding.Category)
                    .Append(" | ").Append(finding.Severity)
                    .Append(" | ").Append(finding.Occurrences.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(finding.Observation)
                    .AppendLine(" |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Laps");
        builder.AppendLine("| Lap | Valid | Lap time | Samples |");
        builder.AppendLine("| ---: | :---: | ---: | ---: |");
        foreach (var lap in report.Laps.Laps)
        {
            builder.Append("| ").Append(lap.LapNumber.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(lap.IsValid ? "yes" : "no")
                .Append(" | ").Append(lap.LapTimeSeconds is { } t ? t.ToString("0.000", CultureInfo.InvariantCulture) : "?")
                .Append(" | ").Append(lap.SampleCount.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" |");
        }

        AppendWarnings(builder, report.DataQuality);
        AppendNotes(builder, report.Notes);
        return builder.ToString();
    }

    private static void AppendWarnings(StringBuilder builder, ReportDataQuality quality)
    {
        if (quality.Warnings.Count == 0) return;

        builder.AppendLine();
        builder.AppendLine("## Data quality warnings");
        foreach (var warning in quality.Warnings)
        {
            builder.Append("- ").AppendLine(warning);
        }
    }

    private static void AppendNotes(StringBuilder builder, IReadOnlyList<string> notes)
    {
        if (notes.Count == 0) return;

        builder.AppendLine();
        builder.AppendLine("## Notes");
        foreach (var note in notes)
        {
            builder.Append("- ").AppendLine(note);
        }
    }
}
