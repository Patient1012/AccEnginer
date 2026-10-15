using AccEngineer.Analysis.Corners;
using AccEngineer.Analysis.Events;

namespace AccEngineer.Analysis.Tests;

public class DrivingEventDetectorTests
{
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
    public void Detect_FindsBrakingEvent()
    {
        var events = new DrivingEventDetector().Detect(SampleLap());

        var braking = Assert.Single(events, e => e.Kind == DrivingEventKind.Braking);
        Assert.Equal(10.0, braking.StartDistanceMeters!.Value, 3);
        Assert.Equal(0.8, braking.Values["peakBrakeInput"], 3);
    }

    [Fact]
    public void Detect_FindsGearChanges()
    {
        var events = new DrivingEventDetector().Detect(SampleLap());

        var gear = events.Where(e => e.Kind == DrivingEventKind.GearChange).ToList();
        Assert.Equal(4, gear.Count);
        Assert.Contains(gear, e => e.Values["fromGear"] == 5 && e.Values["toGear"] == 4);
    }

    [Fact]
    public void Detect_FindsThrottleLift()
    {
        var events = new DrivingEventDetector().Detect(SampleLap());

        Assert.Contains(events, e => e.Kind == DrivingEventKind.ThrottleLift);
    }

    [Fact]
    public void Detect_WithoutCorners_OmitsCornerEvents()
    {
        var events = new DrivingEventDetector().Detect(SampleLap());

        Assert.DoesNotContain(events, e => e.Kind == DrivingEventKind.TurnIn);
        Assert.DoesNotContain(events, e => e.Kind == DrivingEventKind.Apex);
    }

    [Fact]
    public void Detect_WithCorners_EmitsCornerEvents()
    {
        var corners = new TrackCornerDefinition
        {
            Track = "T",
            Corners = new[]
            {
                new Corner { Number = 1, StartDistanceMeters = 0, TurnInDistanceMeters = 20, ApexDistanceMeters = 40, ExitDistanceMeters = 60, EndDistanceMeters = 80 }
            }
        };

        var events = new DrivingEventDetector().Detect(SampleLap(), corners);

        Assert.Contains(events, e => e.Kind == DrivingEventKind.TurnIn && e.CornerNumber == 1);
        var apex = Assert.Single(events, e => e.Kind == DrivingEventKind.Apex);
        Assert.Equal(120.0, apex.Values["apexSpeedKph"], 3);
        Assert.Contains(events, e => e.Kind == DrivingEventKind.CornerExit);
    }
}
