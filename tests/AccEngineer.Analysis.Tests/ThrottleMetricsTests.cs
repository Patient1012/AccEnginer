using AccEngineer.Analysis.Metrics;

namespace AccEngineer.Analysis.Tests;

public class ThrottleMetricsTests
{
    private static LapTimeline ThrottleLap()
    {
        return Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, throttle: 0f),
            Synth.Sample(2, 1, 10, 0.5, throttle: 0.2f),
            Synth.Sample(3, 1, 20, 1.0, throttle: 0.6f),
            Synth.Sample(4, 1, 30, 1.5, throttle: 0.98f),
            Synth.Sample(5, 1, 40, 2.0, throttle: 0.5f),
            Synth.Sample(6, 1, 50, 2.5, throttle: 0.97f));
    }

    [Fact]
    public void Calculate_DetectsSingleApplicationWithPickupRate()
    {
        var metrics = new ThrottleMetricsCalculator().Calculate(ThrottleLap());

        var application = Assert.Single(metrics.Applications);
        Assert.Equal(2, application.SampleId);
        Assert.Equal(0.4, application.PickupRatePerSecond!.Value, 3);
    }

    [Fact]
    public void Calculate_DetectsLiftAndInterruption()
    {
        var metrics = new ThrottleMetricsCalculator().Calculate(ThrottleLap());

        var lift = Assert.Single(metrics.Lifts);
        Assert.Equal(5, lift.SampleId);
        Assert.Equal(1, metrics.InterruptionsAfterApplication);
    }

    [Fact]
    public void Calculate_FullThrottleFractionAndSeconds()
    {
        var metrics = new ThrottleMetricsCalculator().Calculate(ThrottleLap());

        Assert.Equal(2.0 / 6.0, metrics.FullThrottleFraction!.Value, 3);
        Assert.Equal(1.0, metrics.FullThrottleSeconds!.Value, 3);
        Assert.Equal(1.5, metrics.PartialThrottleSeconds!.Value, 3);
    }

    [Fact]
    public void Calculate_NoThrottleChannel_ReturnsEmpty()
    {
        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0),
            Synth.Sample(2, 1, 10, 0.5));

        var metrics = new ThrottleMetricsCalculator().Calculate(lap);

        Assert.Equal(0, metrics.SampleCount);
        Assert.Empty(metrics.Applications);
    }
}
