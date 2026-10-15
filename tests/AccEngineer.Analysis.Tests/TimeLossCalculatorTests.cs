using AccEngineer.Analysis.Comparison;
using AccEngineer.Analysis.Corners;

namespace AccEngineer.Analysis.Tests;

public class TimeLossCalculatorTests
{
    private static LapTimeline Reference()
    {
        return Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0, speedKph: 100f),
            Synth.Sample(2, 1, 20, 2.0, speedKph: 100f),
            Synth.Sample(3, 1, 40, 4.0, speedKph: 100f),
            Synth.Sample(4, 1, 60, 6.0, speedKph: 100f),
            Synth.Sample(5, 1, 80, 8.0, speedKph: 100f),
            Synth.Sample(6, 1, 100, 10.0, speedKph: 100f));
    }

    private static LapTimeline SlowerAfter40()
    {
        // Same trajectory until 40 m, then 0.2 s slower for the remainder.
        return Synth.Lap(2,
            Synth.Sample(7, 2, 0, 0, speedKph: 100f),
            Synth.Sample(8, 2, 20, 2.0, speedKph: 100f),
            Synth.Sample(9, 2, 40, 4.0, speedKph: 100f),
            Synth.Sample(10, 2, 60, 6.2, speedKph: 100f),
            Synth.Sample(11, 2, 80, 8.2, speedKph: 100f),
            Synth.Sample(12, 2, 100, 10.2, speedKph: 100f));
    }

    private static TrackCornerDefinition Corner() => new()
    {
        Track = "T",
        Corners = new[]
        {
            new Corner { Number = 1, StartDistanceMeters = 0, ApexDistanceMeters = 40, EndDistanceMeters = 80 }
        }
    };

    [Fact]
    public void Calculate_EstimatesTimeLossAcrossCorner()
    {
        var result = new TimeLossCalculator().Calculate(Reference(), SlowerAfter40(), Corner());

        var corner = Assert.Single(result.Corners);
        Assert.Equal(1, corner.CornerNumber);
        Assert.True(corner.HasSufficientEvidence);
        Assert.Equal(0.2, corner.EstimatedTimeLossSeconds!.Value, 2);
        Assert.Equal(0.2, result.TotalDeltaSeconds!.Value, 2);
    }

    [Fact]
    public void Calculate_NoCorners_ReturnsEmptyCornerList()
    {
        var result = new TimeLossCalculator().Calculate(Reference(), SlowerAfter40(), corners: null);

        Assert.Empty(result.Corners);
        Assert.Equal(0.2, result.TotalDeltaSeconds!.Value, 2);
    }

    [Fact]
    public void Calculate_ReportsMeasurementDeltas()
    {
        var corners = new TrackCornerDefinition
        {
            Track = "T",
            Corners = new[]
            {
                new Corner { Number = 1, StartDistanceMeters = 0, ApexDistanceMeters = 40, EndDistanceMeters = 80 }
            }
        };

        // Current lap brakes 10 m later than reference.
        var reference = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0),
            Synth.Sample(2, 1, 10, 1, brake: 0f),
            Synth.Sample(3, 1, 20, 2, brake: 0.8f),
            Synth.Sample(4, 1, 40, 4),
            Synth.Sample(5, 1, 80, 8));
        var current = Synth.Lap(2,
            Synth.Sample(6, 2, 0, 0),
            Synth.Sample(7, 2, 10, 1, brake: 0.8f),
            Synth.Sample(8, 2, 20, 2, brake: 0.8f),
            Synth.Sample(9, 2, 40, 4),
            Synth.Sample(10, 2, 80, 8));

        var result = new TimeLossCalculator().Calculate(reference, current, corners);

        var corner = Assert.Single(result.Corners);
        Assert.Equal(20.0, corner.ReferenceBrakePointMeters!.Value, 3);
        Assert.Equal(10.0, corner.CurrentBrakePointMeters!.Value, 3);
        Assert.Equal(-10.0, corner.BrakePointDeltaMeters!.Value, 3);
    }
}
