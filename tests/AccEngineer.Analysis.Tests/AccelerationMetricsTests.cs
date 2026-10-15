using AccEngineer.Analysis.Metrics;

namespace AccEngineer.Analysis.Tests;

public class AccelerationMetricsTests
{
    [Fact]
    public void Calculate_PrefersMeasuredChannel()
    {
        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, longitudinalG: 0.5f),
            Synth.Sample(2, 1, 10, 1.0, longitudinalG: -1.2f),
            Synth.Sample(3, 1, 20, 2.0, longitudinalG: 0.3f));

        var metrics = new AccelerationMetricsCalculator().Calculate(lap);

        Assert.Equal(3, metrics.MeasuredSampleCount);
        Assert.Equal(0.5, metrics.MeasuredPeakG!.Value, 3);
        Assert.Equal(-1.2, metrics.MeasuredMinimumG!.Value, 3);
    }

    [Fact]
    public void Calculate_EstimatesFromSpeedDeltasWhenChannelAbsent()
    {
        // 0 -> 10 m/s in 1 s = 10 m/s^2 ~= 1.0197 g
        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, speedMs: 0f),
            Synth.Sample(2, 1, 10, 1.0, speedMs: 10f));

        var metrics = new AccelerationMetricsCalculator().Calculate(lap);

        Assert.Equal(0, metrics.MeasuredSampleCount);
        Assert.Null(metrics.MeasuredPeakG);
        Assert.Equal(10.0 / 9.80665, metrics.EstimatedPeakG!.Value, 3);
    }

    [Fact]
    public void Calculate_EstimateSkipsZeroTimeDelta()
    {
        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, speedMs: 0f),
            Synth.Sample(2, 1, 10, 0.0, speedMs: 10f));

        var metrics = new AccelerationMetricsCalculator().Calculate(lap);

        Assert.Null(metrics.EstimatedPeakG);
    }
}
