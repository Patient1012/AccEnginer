using AccEngineer.Analysis.Laps;
using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Tests;

public class LapRepresentationBuilderTests
{
    [Fact]
    public void Build_UsesReportedDistance()
    {
        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0.0, speedKph: 100f),
            Synth.Sample(2, 1, 50, 1.0, speedKph: 100f));

        var representation = new LapRepresentationBuilder().Build(lap);

        Assert.Equal(0.0, representation.Samples[0].DistanceMeters!.Value, 3);
        Assert.Equal(50.0, representation.Samples[1].DistanceMeters!.Value, 3);
        Assert.False(representation.Samples[1].IsEstimatedDistance);
        Assert.Empty(representation.Warnings);
    }

    [Fact]
    public void Build_EstimatesDistanceFromSpeedWhenMissing()
    {
        var lap = Synth.Lap(1,
            new TelemetrySample { SampleId = 1, SessionId = "sm-test", LapNumber = 1, ReceivedAtUtc = Synth.T0, TrackProgressMeters = 0f },
            new TelemetrySample { SampleId = 2, SessionId = "sm-test", LapNumber = 1, ReceivedAtUtc = Synth.T0.AddSeconds(1), SpeedMetersPerSecond = 10f },
            new TelemetrySample { SampleId = 3, SessionId = "sm-test", LapNumber = 1, ReceivedAtUtc = Synth.T0.AddSeconds(2), SpeedMetersPerSecond = 10f });

        var representation = new LapRepresentationBuilder().Build(lap);

        Assert.Equal(0.0, representation.Samples[0].DistanceMeters!.Value, 3);
        Assert.Equal(10.0, representation.Samples[1].DistanceMeters!.Value, 3);
        Assert.Equal(20.0, representation.Samples[2].DistanceMeters!.Value, 3);
        Assert.True(representation.Samples[1].IsEstimatedDistance);
        Assert.True(representation.Samples[2].IsEstimatedDistance);
        Assert.Single(representation.Warnings);
    }

    [Fact]
    public void Build_AttachesMetricsAndMetadata()
    {
        var lap = Synth.Lap(3,
            Synth.Sample(1, 3, 0, 0.0, speedKph: 80f),
            Synth.Sample(2, 3, 10, 1.0, speedKph: 120f));

        var representation = new LapRepresentationBuilder().Build(lap, trackName: "Spa", carName: "GT3");

        Assert.Equal(3, representation.LapNumber);
        Assert.Equal("Spa", representation.TrackName);
        Assert.Equal("GT3", representation.CarName);
        Assert.Equal(120.0, representation.Metrics.Speed.MaximumKph!.Value, 3);
        Assert.Equal(1.0, representation.TotalTimeSeconds!.Value, 3);
    }
}
