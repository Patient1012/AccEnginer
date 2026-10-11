using AccEngineer.Analysis;

namespace AccEngineer.Reporting;

/// <summary>
/// Converts deterministic analysis results into the report model. No AI or
/// network access is involved, so a report can always be produced offline.
/// Reference-lap selection is not performed here; the comparison passed in
/// already carries the chosen reference lap number.
/// </summary>
public sealed class ReportBuilder
{
    public const int SchemaVersion = ReportSchema.Version;

    public LapReport Build(SessionLapAnalysis analysis, LapComparison? comparison = null, DateTimeOffset? generatedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var warnings = analysis.Warnings.ToList();
        var invalidLaps = analysis.Laps.Where(l => !l.IsValid).Select(l => l.LapNumber).ToArray();
        if (invalidLaps.Length > 0)
        {
            warnings.Add($"Invalid or incomplete laps: {string.Join(", ", invalidLaps)}.");
        }

        var notes = new List<string>
        {
            "Lap times come from ACC's reported last-lap time when available; otherwise they are computed from the boundary between consecutive laps.",
            "Laps are aligned by track progress (interpolated lap time), not by sample index or timestamp."
        };

        return new LapReport
        {
            SchemaVersion = SchemaVersion,
            GeneratedAtUtc = generatedAtUtc ?? DateTimeOffset.UtcNow,
            Session = new ReportSession
            {
                SessionId = analysis.SessionId,
                Track = analysis.TrackName,
                Car = analysis.CarName,
                SessionType = analysis.SessionType
            },
            DataQuality = new ReportDataQuality
            {
                Status = warnings.Count == 0 ? "good" : "warnings",
                Warnings = warnings
            },
            Laps = analysis.Laps.Select(ToReportLap).ToArray(),
            Comparison = comparison is null ? null : ToReportComparison(comparison),
            Notes = notes
        };
    }

    internal static ReportLap ToReportLap(LapTimeline lap) => new()
    {
        LapNumber = lap.LapNumber,
        IsValid = lap.IsValid,
        IsComplete = lap.IsComplete,
        LapTimeSeconds = lap.LapTimeSeconds,
        LapTimeSource = lap.LapTimeSource switch
        {
            LapTimeSource.AccReported => "acc",
            LapTimeSource.ComputedBoundary => "computed",
            LapTimeSource.PartialSpan => "partial",
            _ => "unavailable"
        },
        SampleCount = lap.Samples.Count,
        SectorCount = lap.SectorCount,
        Sectors = lap.Sectors
            .Select(s => new ReportSector
            {
                SectorIndex = s.SectorIndex,
                TimeSeconds = s.TimeSeconds,
                StartProgressMeters = s.StartProgressMeters
            })
            .ToArray()
    };

    private static ReportComparison ToReportComparison(LapComparison comparison) => new()
    {
        ReferenceLapNumber = comparison.ReferenceLapNumber,
        GridStepMeters = comparison.GridStepMeters,
        Laps = comparison.Laps
            .Where(l => l.LapNumber != comparison.ReferenceLapNumber)
            .Select(l => new ReportLapComparison
            {
                LapNumber = l.LapNumber,
                LapTimeSeconds = l.LapTimeSeconds,
                ReferenceLapTimeSeconds = l.ReferenceLapTimeSeconds,
                TotalDeltaSeconds = l.TotalDeltaSeconds,
                DeltaAtEndSeconds = l.DeltaAtEndSeconds,
                Regions = l.Regions
                    .Select(r => new ReportRegion
                    {
                        Type = r.IsGain ? "gain" : "loss",
                        StartProgressMeters = r.StartProgressMeters,
                        EndProgressMeters = r.EndProgressMeters,
                        DeltaSeconds = r.DeltaSeconds
                    })
                    .ToArray()
            })
            .ToArray()
    };
}
