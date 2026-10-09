using AccEngineer.Analysis;
using AccEngineer.Telemetry;

namespace AccEngineer.Reporting;

public static class ReportSchema
{
    public const int Version = 1;
}

public sealed record LapReport
{
    public required int SchemaVersion { get; init; }

    public required DateTimeOffset GeneratedAtUtc { get; init; }

    public required ReportSession Session { get; init; }

    public required ReportDataQuality DataQuality { get; init; }

    public required IReadOnlyList<ReportLap> Laps { get; init; }

    public ReportComparison? Comparison { get; init; }

    public required IReadOnlyList<string> Notes { get; init; }
}

public sealed record ReportSession
{
    public string? SessionId { get; init; }

    public string? Track { get; init; }

    public string? Car { get; init; }

    public string Simulator { get; init; } = "Assetto Corsa Competizione";

    public SessionType SessionType { get; init; } = SessionType.Unknown;
}

public sealed record ReportDataQuality
{
    /// <summary>"good" or "warnings".</summary>
    public required string Status { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed record ReportLap
{
    public required int LapNumber { get; init; }

    public required bool IsValid { get; init; }

    public required bool IsComplete { get; init; }

    public double? LapTimeSeconds { get; init; }

    public required string LapTimeSource { get; init; }

    public required int SampleCount { get; init; }

    public int? SectorCount { get; init; }

    public required IReadOnlyList<ReportSector> Sectors { get; init; }
}

public sealed record ReportSector
{
    public required int SectorIndex { get; init; }

    public double? TimeSeconds { get; init; }

    public double? StartProgressMeters { get; init; }
}

public sealed record ReportComparison
{
    public required int ReferenceLapNumber { get; init; }

    public required double GridStepMeters { get; init; }

    public required IReadOnlyList<ReportLapComparison> Laps { get; init; }
}

public sealed record ReportLapComparison
{
    public required int LapNumber { get; init; }

    public double? LapTimeSeconds { get; init; }

    public double? ReferenceLapTimeSeconds { get; init; }

    public double? TotalDeltaSeconds { get; init; }

    public double? DeltaAtEndSeconds { get; init; }

    public required IReadOnlyList<ReportRegion> Regions { get; init; }
}

public sealed record ReportRegion
{
    /// <summary>"gain" or "loss".</summary>
    public required string Type { get; init; }

    public required double StartProgressMeters { get; init; }

    public required double EndProgressMeters { get; init; }

    public required double DeltaSeconds { get; init; }
}
