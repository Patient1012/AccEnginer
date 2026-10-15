namespace AccEngineer.Analysis.Corners;

/// <summary>
/// A corner defined by track-distance boundaries. <see cref="ApexDistanceMeters"/> is
/// required because it is the anchor for phase analysis; the other boundaries are
/// optional so a partial manual definition is still usable.
/// </summary>
public sealed record Corner
{
    public required int Number { get; init; }

    public string? Name { get; init; }

    public double? StartDistanceMeters { get; init; }

    public double? EntryDistanceMeters { get; init; }

    public double? TurnInDistanceMeters { get; init; }

    public required double ApexDistanceMeters { get; init; }

    public double? ExitDistanceMeters { get; init; }

    public double? EndDistanceMeters { get; init; }
}
