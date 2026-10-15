using AccEngineer.Analysis.Metrics;

namespace AccEngineer.Analysis.Tests;

public class SpeedMetricsTests
{
    [Fact]
    public void Calculate_ComputesMinMaxAverage()
    {
        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, speedKph: 100f),
            Synth.Sample(2, 1, 50, 1.0, speedKph: 200f),
            Synth.Sample(3, 1, 100, 2.0, speedKph: 150f));

        var metrics = new SpeedMetricsCalculator().Calculate(lap);

        Assert.Equal(100.0, metrics.MinimumKph!.Value, 3);
        Assert.Equal(200.0, metrics.MaximumKph!.Value, 3);
        Assert.Equal(150.0, metrics.AverageKph!.Value, 3);
        Assert.Equal(3, metrics.SampleCount);
    }

    [Fact]
    public void Calculate_NoSpeedChannel_ReturnsNulls()
    {
        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0),
            Synth.Sample(2, 1, 50, 1.0));

        var metrics = new SpeedMetricsCalculator().Calculate(lap);

        Assert.Equal(0, metrics.SampleCount);
        Assert.Null(metrics.AverageKph);
        Assert.Null(metrics.MaximumKph);
    }

    [Fact]
    public void Calculate_SpeedAtDistance_ReturnsNearestSample()
    {
        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, speedKph: 100f),
            Synth.Sample(2, 1, 50, 1.0, speedKph: 200f),
            Synth.Sample(3, 1, 100, 2.0, speedKph: 150f));

        var metrics = new SpeedMetricsCalculator().Calculate(lap, distanceQueryMeters: 52);

        Assert.Equal(200.0, metrics.SpeedAtDistanceKph!.Value, 3);
        Assert.Equal(100.0, metrics.DistanceMeters!.Value, 3);
    }
}
