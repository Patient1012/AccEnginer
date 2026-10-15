namespace AccEngineer.Analysis.Comparison;

/// <summary>Time difference for a single sector between two laps.</summary>
public sealed record SectorDelta(
    int SectorIndex,
    double? ReferenceSeconds,
    double? CurrentSeconds,
    double? DeltaSeconds);

/// <summary>Compares sector times between two laps by sector index.</summary>
public sealed class SectorComparator
{
    public IReadOnlyList<SectorDelta> CompareSectors(LapTimeline reference, LapTimeline current)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(current);

        var count = Math.Max(reference.Sectors.Count, current.Sectors.Count);
        var deltas = new List<SectorDelta>(count);

        for (var i = 0; i < count; i++)
        {
            var referenceTime = i < reference.Sectors.Count ? reference.Sectors[i].TimeSeconds : null;
            var currentTime = i < current.Sectors.Count ? current.Sectors[i].TimeSeconds : null;

            double? delta = referenceTime is { } r && currentTime is { } c ? c - r : null;

            deltas.Add(new SectorDelta(i, referenceTime, currentTime, delta));
        }

        return deltas;
    }
}
