using AccEngineer.Analysis.Metrics;

namespace AccEngineer.Analysis.Tests;

public class GearMetricsTests
{
    private static LapTimeline GearLap()
    {
        return Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, gear: 2),
            Synth.Sample(2, 1, 10, 0.5, gear: 2),
            Synth.Sample(3, 1, 20, 1.0, gear: 3),
            Synth.Sample(4, 1, 30, 1.5, gear: 4),
            Synth.Sample(5, 1, 40, 2.0, gear: 3),
            Synth.Sample(6, 1, 50, 2.5, gear: 2));
    }

    [Fact]
    public void Calculate_CountsShifts()
    {
        var metrics = new GearMetricsCalculator().Calculate(GearLap());

        Assert.Equal(2, metrics.UpshiftCount);
        Assert.Equal(2, metrics.DownshiftCount);
        Assert.Equal(4, metrics.Shifts.Count);
    }

    [Fact]
    public void Calculate_ListsGearsUsed()
    {
        var metrics = new GearMetricsCalculator().Calculate(GearLap());

        Assert.Equal(new[] { 2, 3, 4 }, metrics.GearsUsed);
    }

    [Fact]
    public void Calculate_ShiftPointsCarryNormalisedGear()
    {
        var metrics = new GearMetricsCalculator().Calculate(GearLap());

        var second = metrics.Shifts[1];
        Assert.Equal(3, second.FromGearRaw);
        Assert.Equal(4, second.ToGearRaw);
        Assert.Equal(3, second.ToGear);
        Assert.True(second.IsUpshift);
    }

    [Fact]
    public void Calculate_GearAtDistance()
    {
        var metrics = new GearMetricsCalculator().Calculate(GearLap(), distanceQueryMeters: 22);

        Assert.Equal(3, metrics.GearAtDistanceRaw);
    }
}
