using AccEngineer.Analysis.Metrics;

namespace AccEngineer.Analysis.Tests;

public class BrakingMetricsTests
{
    private static LapTimeline BrakingLap()
    {
        // 0.5 s spacing, 10 m spacing.
        return Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, brake: 0f, steering: 0f),
            Synth.Sample(2, 1, 10, 0.5, brake: 0.8f, steering: 0f),
            Synth.Sample(3, 1, 20, 1.0, brake: 0.9f, steering: 0.3f),
            Synth.Sample(4, 1, 30, 1.5, brake: 0.6f, steering: 0.4f),
            Synth.Sample(5, 1, 40, 2.0, brake: 0.3f, steering: 0.2f),
            Synth.Sample(6, 1, 50, 2.5, brake: 0f, steering: 0f));
    }

    [Fact]
    public void Calculate_DetectsSingleBrakingApplication()
    {
        var zones = new BrakingMetricsCalculator().Calculate(BrakingLap());

        var zone = Assert.Single(zones);
        Assert.Equal(2, zone.FirstSampleId);
        Assert.Equal(5, zone.LastSampleId);
        Assert.Equal(0.9, zone.PeakBrakeInput!.Value, 3);
    }

    [Fact]
    public void Calculate_MeasuresDistanceAndDuration()
    {
        var zone = new BrakingMetricsCalculator().Calculate(BrakingLap())[0];

        Assert.Equal(10.0, zone.StartDistanceMeters!.Value, 3);
        Assert.Equal(40.0, zone.EndDistanceMeters!.Value, 3);
        Assert.Equal(30.0, zone.DistanceTravelledMeters!.Value, 3);
        Assert.Equal(1.5, zone.DurationSeconds!.Value, 3);
    }

    [Fact]
    public void Calculate_RecordsBrakeProgression()
    {
        var zone = new BrakingMetricsCalculator().Calculate(BrakingLap())[0];

        Assert.Equal(new[] { 0.8, 0.9, 0.6, 0.3 }, zone.BrakeInputProgression.Select(v => Math.Round(v, 3)));
    }

    [Fact]
    public void Calculate_BrakeAtTurnInIsInputAtPeakSteering()
    {
        var zone = new BrakingMetricsCalculator().Calculate(BrakingLap())[0];

        Assert.Equal(0.6, zone.BrakeInputAtTurnIn!.Value, 3);
    }

    [Fact]
    public void Calculate_TrailBrakingSecondsSumsSteeringOverlap()
    {
        var zone = new BrakingMetricsCalculator().Calculate(BrakingLap())[0];

        Assert.Equal(1.5, zone.TrailBrakingSeconds!.Value, 3);
    }

    [Fact]
    public void Calculate_ShortApplicationIgnored()
    {
        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, brake: 0f),
            Synth.Sample(2, 1, 10, 1.0, brake: 0.9f),
            Synth.Sample(3, 1, 20, 2.0, brake: 0f));

        var zones = new BrakingMetricsCalculator().Calculate(lap);

        Assert.Empty(zones);
    }
}
