using AccEngineer.Analysis;
using AccEngineer.Reporting;
using AccEngineer.Storage;
using AccEngineer.Telemetry;

namespace AccEngineer.App;

/// <summary>
/// Offline lap analysis: loads a JSONL recording, reconstructs laps, aligns them
/// against a reference lap, and writes a machine-readable JSON report plus a
/// human-readable Markdown report. Runs entirely offline (no ACC, no network).
/// </summary>
internal static class AnalyzeRunner
{
    public static int Run(string path, string? outputDir, ConsoleDisplay display)
    {
        SessionRecording recording;
        try
        {
            recording = new SessionRepository().Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            display.StatusError($"Failed to load recording: {ex.Message}");
            return 1;
        }

        var metadata = recording.Metadata;
        display.Status($"Analysing {recording.Samples.Count} sample(s) from {Path.GetFullPath(path)}.");

        var analysis = new LapAnalyzer().Analyze(recording.Samples);
        analysis = analysis with
        {
            TrackName = metadata.TrackName ?? analysis.TrackName,
            CarName = metadata.CarName ?? analysis.CarName,
            SessionType = metadata.SessionType != SessionType.Unknown ? metadata.SessionType : analysis.SessionType
        };

        LapComparison? comparison = null;
        var reference = ReferenceLapSelector.SelectFastestValid(analysis.Laps);
        if (reference is not null)
        {
            display.Status(
                $"Reference lap: {reference.LapNumber} ({reference.LapTimeSeconds:0.000} s, {reference.LapTimeSource}).");
            comparison = new LapComparer().Compare(reference, analysis.Laps);
        }
        else
        {
            display.Status("No valid complete lap available; report has no comparison section.");
        }

        var report = new ReportBuilder().Build(analysis, comparison);

        var directory = string.IsNullOrWhiteSpace(outputDir) ? "reports" : outputDir;
        Directory.CreateDirectory(directory);
        var baseName = Path.GetFileNameWithoutExtension(path);
        var jsonPath = Path.Combine(directory, baseName + ".report.json");
        var markdownPath = Path.Combine(directory, baseName + ".report.md");

        var markdownExporter = new MarkdownReportExporter();
        new JsonReportExporter().Write(report, jsonPath);
        markdownExporter.Write(report, markdownPath);

        Console.WriteLine(markdownExporter.ToMarkdown(report));

        display.Status($"Wrote {Path.GetFullPath(jsonPath)}");
        display.Status($"Wrote {Path.GetFullPath(markdownPath)}");
        display.Status(
            $"Laps: {analysis.Laps.Count} ({analysis.Laps.Count(l => l.IsValid)} valid). Data quality: {report.DataQuality.Status}.");

        return 0;
    }
}
