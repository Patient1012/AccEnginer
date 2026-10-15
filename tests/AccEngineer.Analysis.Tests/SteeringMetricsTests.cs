using AccEngineer.Analysis.Metrics;

namespace AccEngineer.Analysis.Tests;

public class SteeringMetricsTests
{
    private static LapTimeline SteeringLap()
    {
        return Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, steering: 0f),
            Synth.Sample(2, 1, 10, 0.5, steering: 0.3f),
            Synth.Sample(3, 1, 20, 1.0, steering: 0.5f),
            Synth.Sample(4, 1, 30, 1.5, steering: 0.2f),
            Synth.Sample(5, 1, 40, 2.0, steering: -0.3f));
    }

    [Fact]
    public void Calculate_MaxAndAverage()
    {
        var metrics = new SteeringMetricsCalculator().Calculate(SteeringLap());

        Assert.Equal(0.5, metrics.MaximumAbsoluteInput!.Value, 3);
        Assert.Equal((0.0 + 0.3 + 0.5 + 0.2 + 0.3) / 5.0, metrics.AverageAbsoluteInput!.Value, 3);
    }

    [Fact]
    public void Calculate_MaxRateAndReversal()
    {
        var metrics = new SteeringMetricsCalculator().Calculate(SteeringLap());

        Assert.Equal(1.0, metrics.MaximumRatePerSecond!.Value, 3);
        Assert.Equal(1, metrics.ReversalCount);
    }

    [Fact]
    public void Calculate_CountsCorrection()
    {
        var metrics = new SteeringMetricsCalculator().Calculate(SteeringLap());

        Assert.Equal(1, metrics.CorrectionCount);
    }

    [Fact]
    public void Calculate_NoSteeringChannel_ReturnsEmpty()
    {
        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0),
            Synth.Sample(2, 1, 10, 0.5));

        var metrics = new SteeringMetricsCalculator().Calculate(lap);

        Assert.Equal(0, metrics.SampleCount);
        Assert.Null(metrics.MaximumAbsoluteInput);
    }
}
