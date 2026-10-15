using AccEngineer.Analysis.Comparison;

namespace AccEngineer.Analysis.Tests;

public class SectorComparatorTests
{
    [Fact]
    public void CompareSectors_ReturnsDeltasByIndex()
    {
        var reference = BuildLapWithSectors(1, 30.0, 35.0);
        var current = BuildLapWithSectors(2, 31.0, 34.0);

        var deltas = new SectorComparator().CompareSectors(reference, current);

        Assert.Equal(2, deltas.Count);
        Assert.Equal(1.0, deltas[0].DeltaSeconds!.Value, 3);
        Assert.Equal(-1.0, deltas[1].DeltaSeconds!.Value, 3);
    }

    [Fact]
    public void CompareSectors_MissingSectorYieldsNull()
    {
        var reference = BuildLapWithSectors(1, 30.0, 35.0);
        var current = BuildLapWithSectors(2, 31.0);

        var deltas = new SectorComparator().CompareSectors(reference, current);

        Assert.Equal(2, deltas.Count);
        Assert.NotNull(deltas[0].DeltaSeconds);
        Assert.Null(deltas[1].DeltaSeconds);
    }

    private static LapTimeline BuildLapWithSectors(int lapNumber, params double[] sectorSeconds)
    {
        var sectors = sectorSeconds
            .Select((seconds, index) => new SectorTimeline { SectorIndex = index, TimeSeconds = seconds })
            .ToArray();

        return new LapTimeline
        {
            SessionId = "sm-test",
            LapNumber = lapNumber,
            IsComplete = true,
            IsValid = true,
            StartedAtUtc = Synth.T0,
            EndedAtUtc = Synth.T0,
            LapTimeSeconds = sectorSeconds.Sum(),
            Samples = Array.Empty<LapSample>(),
            Sectors = sectors
        };
    }
}
