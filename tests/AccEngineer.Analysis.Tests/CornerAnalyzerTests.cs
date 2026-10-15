using AccEngineer.Analysis.Corners;

namespace AccEngineer.Analysis.Tests;

public class CornerAnalyzerTests
{
    private static readonly Corner Corner = new()
    {
        Number = 1,
        StartDistanceMeters = 0,
        TurnInDistanceMeters = 20,
        ApexDistanceMeters = 40,
        ExitDistanceMeters = 60,
        EndDistanceMeters = 80
    };

    private static LapTimeline SampleLap()
    {
        return Synth.Lap(1,
            Synth.Sample(1, 1, 0, 0, speedKph: 200f, brake: 0f, throttle: 1f, steering: 0f, gear: 5),
            Synth.Sample(2, 1, 10, 1, speedKph: 180f, brake: 0.2f, throttle: 0f, steering: 0.05f, gear: 4),
            Synth.Sample(3, 1, 20, 2, speedKph: 150f, brake: 0.8f, throttle: 0f, steering: 0.2f, gear: 4),
            Synth.Sample(4, 1, 30, 3, speedKph: 130f, brake: 0.5f, throttle: 0.2f, steering: 0.35f, gear: 3),
            Synth.Sample(5, 1, 40, 4, speedKph: 120f, brake: 0.2f, throttle: 0.4f, steering: 0.4f, gear: 3),
            Synth.Sample(6, 1, 50, 5, speedKph: 140f, brake: 0f, throttle: 0.6f, steering: 0.3f, gear: 3),
            Synth.Sample(7, 1, 60, 6, speedKph: 160f, brake: 0f, throttle: 0.9f, steering: 0.15f, gear: 4),
            Synth.Sample(8, 1, 70, 7, speedKph: 180f, brake: 0f, throttle: 1f, steering: 0.05f, gear: 4),
            Synth.Sample(9, 1, 80, 8, speedKph: 200f, brake: 0f, throttle: 1f, steering: 0f, gear: 5));
    }

    [Fact]
    public void Analyze_EntryPhase()
    {
        var metrics = new CornerAnalyzer().Analyze(SampleLap(), Corner);

        Assert.Equal(200.0, metrics.EntrySpeedKph!.Value, 3);
        Assert.Equal(10.0, metrics.BrakePointMeters!.Value, 3);
        Assert.Equal(0.8, metrics.PeakBrakeInput!.Value, 3);
        Assert.Equal(10.0, metrics.BrakeReleaseMeters!.Value, 3);
        Assert.Equal(2, metrics.DownshiftCount);
    }

    [Fact]
    public void Analyze_RotationPhase()
    {
        var metrics = new CornerAnalyzer().Analyze(SampleLap(), Corner);

        Assert.Equal(20.0, metrics.TurnInMeters!.Value, 3);
        Assert.Equal(0.2, metrics.TurnInSteeringInput!.Value, 3);
        Assert.Equal(120.0, metrics.MinimumSpeedKph!.Value, 3);
        Assert.Equal(120.0, metrics.ApexSpeedKph!.Value, 3);
        Assert.Equal(40.0, metrics.ApexDistanceMeters!.Value, 3);
        Assert.Equal(2.0, metrics.BrakeOverlapSeconds!.Value, 3);
    }

    [Fact]
    public void Analyze_ExitPhase()
    {
        var metrics = new CornerAnalyzer().Analyze(SampleLap(), Corner);

        Assert.Equal(60.0, metrics.ThrottlePickupMeters!.Value, 3);
        Assert.Equal(70.0, metrics.FullThrottleMeters!.Value, 3);
        Assert.Equal(160.0, metrics.ExitSpeedKph!.Value, 3);
        Assert.Equal(200.0, metrics.SpeedAfterApexKph!.Value, 3);
    }

    [Fact]
    public void Analyze_DetectsTurnInWhenOmitted()
    {
        var partial = new Corner { Number = 2, ApexDistanceMeters = 40 };

        var metrics = new CornerAnalyzer().Analyze(SampleLap(), partial);

        // Window defaults to apex +/- 150, so all samples are present; turn-in is the
        // first steering sample at or above the threshold before the apex (p=20).
        Assert.Equal(20.0, metrics.TurnInMeters!.Value, 3);
    }

    [Fact]
    public void AnalyzeAll_ReturnsOnePerCorner()
    {
        var definition = new TrackCornerDefinition
        {
            Track = "T",
            Corners = new[]
            {
                Corner,
                new Corner { Number = 2, ApexDistanceMeters = 40 }
            }
        };

        var results = new CornerAnalyzer().AnalyzeAll(SampleLap(), definition);

        Assert.Equal(2, results.Count);
    }
}
