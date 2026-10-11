using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccEngineer.Reporting;

/// <summary>JSON is the canonical machine-readable report. Markdown is the human companion.</summary>
public sealed class JsonReportExporter
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    public string ToJson(LapReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, Options);
    }

    /// <summary>Serializes any report model with the canonical report options.</summary>
    public string Serialize<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, Options);
    }

    public void Write<T>(T value, string path)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, Serialize(value));
    }

    public void Write(LapReport report, string path)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, ToJson(report));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>Deterministic Markdown companion report (no AI required).</summary>
public sealed class MarkdownReportExporter
{
    public string ToMarkdown(LapReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();
        builder.Append("# Lap report — ").Append(report.Session.Track ?? "unknown track")
            .Append(" (").Append(report.Session.SessionType).AppendLine(")");
        builder.AppendLine();
        builder.Append("- Session: ").AppendLine(report.Session.SessionId ?? "?");
        builder.Append("- Car: ").AppendLine(report.Session.Car ?? "?");
        builder.Append("- Simulator: ").AppendLine(report.Session.Simulator);
        builder.Append("- Generated: ").Append(report.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture)).AppendLine(" UTC");
        builder.Append("- Schema: v").AppendLine(report.SchemaVersion.ToString(CultureInfo.InvariantCulture));
        builder.Append("- Data quality: ").AppendLine(report.DataQuality.Status);

        if (report.DataQuality.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("### Warnings");
            foreach (var warning in report.DataQuality.Warnings)
            {
                builder.Append("- ").AppendLine(warning);
            }
        }

        AppendLaps(builder, report);
        AppendComparison(builder, report);

        if (report.Notes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("### Notes");
            foreach (var note in report.Notes)
            {
                builder.Append("- ").AppendLine(note);
            }
        }

        return builder.ToString();
    }

    public void Write(LapReport report, string path)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, ToMarkdown(report));
    }

    private static void AppendLaps(StringBuilder builder, LapReport report)
    {
        builder.AppendLine();
        builder.AppendLine("## Laps");
        builder.AppendLine();
        builder.AppendLine("| Lap | Valid | Complete | Lap time | Source | Samples | Sectors |");
        builder.AppendLine("| ---: | :---: | :---: | ---: | :--- | ---: | :--- |");

        foreach (var lap in report.Laps)
        {
            var sectors = lap.Sectors.Count == 0
                ? "-"
                : string.Join(" / ", lap.Sectors.Select(s => FormatSeconds(s.TimeSeconds)));

            builder.Append("| ").Append(lap.LapNumber.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(lap.IsValid ? "yes" : "no")
                .Append(" | ").Append(lap.IsComplete ? "yes" : "no")
                .Append(" | ").Append(FormatSeconds(lap.LapTimeSeconds))
                .Append(" | ").Append(lap.LapTimeSource)
                .Append(" | ").Append(lap.SampleCount.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(sectors)
                .AppendLine(" |");
        }
    }

    private static void AppendComparison(StringBuilder builder, LapReport report)
    {
        if (report.Comparison is not { } comparison) return;

        builder.AppendLine();
        builder.Append("## Comparison vs lap ").Append(comparison.ReferenceLapNumber)
            .Append(" (").Append(comparison.GridStepMeters.ToString("0.#", CultureInfo.InvariantCulture))
            .AppendLine(" m grid)");
        builder.AppendLine();
        builder.AppendLine("Positive delta = slower than the reference lap.");
        builder.AppendLine();
        builder.AppendLine("| Lap | Lap time | Total delta | Delta at end |");
        builder.AppendLine("| ---: | ---: | ---: | ---: |");

        foreach (var lap in comparison.Laps)
        {
            builder.Append("| ").Append(lap.LapNumber.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(FormatSeconds(lap.LapTimeSeconds))
                .Append(" | ").Append(FormatDelta(lap.TotalDeltaSeconds))
                .Append(" | ").Append(FormatDelta(lap.DeltaAtEndSeconds))
                .AppendLine(" |");
        }

        foreach (var lap in comparison.Laps)
        {
            if (lap.Regions.Count == 0) continue;

            builder.AppendLine();
            builder.Append("### Lap ").Append(lap.LapNumber)
                .Append(": ").Append(FormatDelta(lap.TotalDeltaSeconds)).AppendLine();
            foreach (var region in lap.Regions)
            {
                builder.Append("- ").Append(region.Type == "gain" ? "Gain" : "Loss")
                    .Append(' ').Append(region.StartProgressMeters.ToString("0", CultureInfo.InvariantCulture))
                    .Append('–').Append(region.EndProgressMeters.ToString("0", CultureInfo.InvariantCulture))
                    .Append(" m: ").Append(FormatDelta(region.DeltaSeconds))
                    .AppendLine();
            }
        }
    }

    private static string FormatSeconds(double? seconds)
    {
        if (seconds is not { } value) return "?";

        var timeSpan = TimeSpan.FromSeconds(value);
        return value >= 60
            ? $"{(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:00}.{timeSpan.Milliseconds:000}"
            : $"{value.ToString("0.000", CultureInfo.InvariantCulture)} s";
    }

    private static string FormatDelta(double? seconds) =>
        seconds is not { } value ? "?" : $"{value.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)} s";
}
