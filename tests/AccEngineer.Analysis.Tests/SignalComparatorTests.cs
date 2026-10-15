using AccEngineer.Analysis.Comparison;

namespace AccEngineer.Analysis.Tests;

public class SignalComparatorTests
{
    [Fact]
    public void Compare_SpeedByDistance()
    {
        var reference = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, speedKph: 100f),
            Synth.Sample(2, 1, 10, 1.0, speedKph: 120f),
            Synth.Sample(3, 1, 20, 2.0, speedKph: 140f));

        var current = Synth.Lap(2,
            Synth.Sample(4, 2, 0, 0.0, speedKph: 100f),
            Synth.Sample(5, 2, 10, 1.0, speedKph: 110f),
            Synth.Sample(6, 2, 20, 2.0, speedKph: 130f));

        var deltas = new SignalComparator().Compare(
            reference, current, s => s.Source.SpeedKph is { } k ? (double)k : null, gridStepMeters: 10);

        Assert.Equal(0.0, deltas[0].Delta!.Value, 3);
        Assert.Equal(-10.0, deltas[1].Delta!.Value, 3);
        Assert.Equal(-10.0, deltas[2].Delta!.Value, 3);
    }

    [Fact]
    public void Compare_InterpolatesAcrossSamples()
    {
        var reference = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, speedKph: 100f),
            Synth.Sample(2, 1, 20, 1.0, speedKph: 200f));

        var current = Synth.Lap(2,
            Synth.Sample(3, 2, 0, 0.0, speedKph: 100f),
            Synth.Sample(4, 2, 20, 1.0, speedKph: 100f));

        var deltas = new SignalComparator().Compare(
            reference, current, s => s.Source.SpeedKph is { } k ? (double)k : null, gridStepMeters: 10);

        Assert.Equal(3, deltas.Count);
        Assert.Equal(-50.0, deltas[1].Delta!.Value, 3);
    }

    [Fact]
    public void Compare_MissingSignalYieldsNull()
    {
        var reference = Synth.Lap(1, Synth.Sample(1, 1, 0, 0.0, speedKph: 100f));
        var current = Synth.Lap(2, Synth.Sample(2, 2, 0, 0.0));

        var deltas = new SignalComparator().Compare(
            reference, current, s => s.Source.SpeedKph is { } k ? (double)k : null, gridStepMeters: 10);

        Assert.Null(deltas[0].Delta);
    }
}
