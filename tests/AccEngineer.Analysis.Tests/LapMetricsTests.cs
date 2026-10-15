using AccEngineer.Analysis.Metrics;

namespace AccEngineer.Analysis.Tests;

public class LapMetricsTests
{
    [Fact]
    public void Calculate_ProducesEveryMetricGroup()
    {
        var lap = Synth.Lap(4,
            Synth.Sample(1, 4, 0, 0.0, speedKph: 100f, throttle: 0f, brake: 0f, steering: 0f, gear: 3),
            Synth.Sample(2, 4, 10, 0.5, speedKph: 60f, throttle: 0.1f, brake: 0.9f, steering: 0.1f, gear: 2),
            Synth.Sample(3, 4, 20, 1.0, speedKph: 55f, throttle: 0.2f, brake: 0.8f, steering: 0.3f, gear: 2),
            Synth.Sample(4, 4, 30, 1.5, speedKph: 80f, throttle: 0.4f, brake: 0.6f, steering: 0.2f, gear: 2),
            Synth.Sample(5, 4, 40, 2.0, speedKph: 120f, throttle: 0.98f, brake: 0f, steering: 0.1f, gear: 3));

        var metrics = new LapMetricsCalculator().Calculate(lap);

        Assert.Equal(4, metrics.LapNumber);
        Assert.Equal(120.0, metrics.Speed.MaximumKph!.Value, 3);
        Assert.Single(metrics.Braking);
        Assert.NotNull(metrics.Throttle);
        Assert.Equal(0.3, metrics.Steering.MaximumAbsoluteInput!.Value, 3);
        Assert.Equal(1, metrics.Gear.UpshiftCount);
    }
}
