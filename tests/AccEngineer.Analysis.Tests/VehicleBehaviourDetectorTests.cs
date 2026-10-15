using AccEngineer.Analysis.Corners;
using AccEngineer.Analysis.Indicators;

namespace AccEngineer.Analysis.Tests;

public class VehicleBehaviourDetectorTests
{
    [Fact]
    public void Detect_NoCorners_ReturnsEmpty()
    {
        var lap = Synth.Lap(1, Synth.Sample(1, 1, 0, 0, steering: 0.5f));

        var indicators = new VehicleBehaviourDetector().Detect(lap, corners: null);

        Assert.Empty(indicators);
    }

    [Fact]
    public void Detect_FlagsExcessiveSteeringCorrection()
    {
        var corner = new TrackCornerDefinition
        {
            Track = "T",
            Corners = new[]
            {
                new Corner { Number = 1, TurnInDistanceMeters = 20, ApexDistanceMeters = 40, ExitDistanceMeters = 60 }
            }
        };

        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 20, 0.0, steering: 0.3f, speedKph: 150f, throttle: 0.8f),
            Synth.Sample(2, 1, 30, 0.5, steering: 0.6f, speedKph: 140f, throttle: 0.8f),
            Synth.Sample(3, 1, 40, 1.0, steering: 0.2f, speedKph: 130f, throttle: 0.8f),
            Synth.Sample(4, 1, 50, 1.5, steering: 0.6f, speedKph: 135f, throttle: 0.8f),
            Synth.Sample(5, 1, 58, 2.0, steering: 0.2f, speedKph: 140f, throttle: 0.8f));

        var indicators = new VehicleBehaviourDetector().Detect(lap, corner);

        Assert.Contains(indicators, i => i.Kind == BehaviourIndicatorKind.ExcessiveSteeringCorrection);
    }

    [Fact]
    public void Detect_FlagsPossibleMidCornerUndersteer()
    {
        var corner = new TrackCornerDefinition
        {
            Track = "T",
            Corners = new[]
            {
                new Corner { Number = 1, TurnInDistanceMeters = 20, ApexDistanceMeters = 40, ExitDistanceMeters = 60 }
            }
        };

        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 20, 0.0, steering: 0.35f, speedKph: 150f, throttle: 0.2f),
            Synth.Sample(2, 1, 30, 0.5, steering: 0.40f, speedKph: 140f, throttle: 0.1f),
            Synth.Sample(3, 1, 40, 1.0, steering: 0.45f, speedKph: 130f, throttle: 0.0f),
            Synth.Sample(4, 1, 50, 1.5, steering: 0.40f, speedKph: 135f, throttle: 0.1f));

        var indicators = new VehicleBehaviourDetector().Detect(lap, corner);

        var understeer = Assert.Single(indicators, i => i.Kind == BehaviourIndicatorKind.PossibleMidCornerUndersteer);
        Assert.Equal(CornerPhase.MidCorner, understeer.Phase);
        Assert.NotEmpty(understeer.Limitations);
    }

    [Fact]
    public void Detect_FlagsPossibleExitUndersteer()
    {
        var corner = new TrackCornerDefinition
        {
            Track = "T",
            Corners = new[]
            {
                new Corner { Number = 1, StartDistanceMeters = 60, TurnInDistanceMeters = 60, ApexDistanceMeters = 70, ExitDistanceMeters = 80, EndDistanceMeters = 120 }
            }
        };

        var lap = Synth.Lap(1,
            Synth.Sample(1, 1, 60, 0.0, steering: 0.30f, speedKph: 130f, throttle: 0.9f),
            Synth.Sample(2, 1, 70, 0.5, steering: 0.25f, speedKph: 135f, throttle: 0.9f),
            Synth.Sample(3, 1, 80, 1.0, steering: 0.40f, speedKph: 140f, throttle: 0.9f),
            Synth.Sample(4, 1, 90, 1.5, steering: 0.35f, speedKph: 143f, throttle: 0.9f),
            Synth.Sample(5, 1, 100, 2.0, steering: 0.30f, speedKph: 146f, throttle: 0.9f));

        var indicators = new VehicleBehaviourDetector().Detect(lap, corner);

        Assert.Contains(indicators, i => i.Kind == BehaviourIndicatorKind.PossibleExitUndersteer);
    }
}
