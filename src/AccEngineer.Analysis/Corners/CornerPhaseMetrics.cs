namespace AccEngineer.Analysis.Corners;

/// <summary>
/// Measured driving phases for one corner. Every value is measured from telemetry;
/// null means the relevant channel or boundary was unavailable. Distances are in
/// metres, speeds in km/h, times in seconds.
/// </summary>
public sealed record CornerPhaseMetrics
{
    public required int CornerNumber { get; init; }

    public string? CornerName { get; init; }

    public double? WindowStartMeters { get; init; }

    public double? WindowEndMeters { get; init; }

    // Entry
    public double? EntrySpeedKph { get; init; }

    public double? BrakePointMeters { get; init; }

    public double? PeakBrakeInput { get; init; }

    public double? BrakeReleaseMeters { get; init; }

    public int DownshiftCount { get; init; }

    // Rotation
    public double? TurnInMeters { get; init; }

    public double? TurnInSteeringInput { get; init; }

    public double? MinimumSpeedKph { get; init; }

    public double? ApexSpeedKph { get; init; }

    public double? ApexDistanceMeters { get; init; }

    public int SteeringCorrectionCount { get; init; }

    public double? BrakeOverlapSeconds { get; init; }

    // Exit
    public double? ThrottlePickupMeters { get; init; }

    public double? FullThrottleMeters { get; init; }

    public double? ExitSpeedKph { get; init; }

    public double? SpeedAfterApexKph { get; init; }
}

/// <summary>A corner paired with its measured phases.</summary>
public sealed record AnalyzedCorner
{
    public required Corner Corner { get; init; }

    public required CornerPhaseMetrics Metrics { get; init; }
}
