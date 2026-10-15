using AccEngineer.Analysis.Telemetry;
using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Tests;

public class SampleInterpolatorTests
{
    private static TelemetrySample Sample(float? throttle, float? brake, float? speed) =>
        new()
        {
            SampleId = 1,
            SessionId = "sm-test",
            LapNumber = 1,
            ThrottleInput = throttle,
            BrakeInput = brake,
            SpeedKph = speed
        };

    [Fact]
    public void Interpolate_Midpoint_AveragesContinuousChannels()
    {
        var interpolator = new SampleInterpolator();
        var a = Sample(throttle: 0f, brake: 1f, speed: 100f);
        var b = Sample(throttle: 1f, brake: 0f, speed: 200f);

        var mid = interpolator.Interpolate(a, b, 0.5);

        Assert.Equal(0.5f, mid.ThrottleInput!.Value, 3);
        Assert.Equal(0.5f, mid.BrakeInput!.Value, 3);
        Assert.Equal(150f, mid.SpeedKph!.Value, 3);
    }

    [Fact]
    public void Interpolate_ClampsFraction()
    {
        var interpolator = new SampleInterpolator();
        var a = Sample(throttle: 0f, brake: null, speed: null);
        var b = Sample(throttle: 1f, brake: null, speed: null);

        var result = interpolator.Interpolate(a, b, 5.0);

        Assert.Equal(1f, result.ThrottleInput!.Value, 3);
    }

    [Fact]
    public void Interpolate_OneSideNull_UsesAvailableValue()
    {
        var interpolator = new SampleInterpolator();
        var a = Sample(throttle: 0.4f, brake: null, speed: null);
        var b = Sample(throttle: null, brake: null, speed: null);

        var result = interpolator.Interpolate(a, b, 0.5);

        Assert.Equal(0.4f, result.ThrottleInput!.Value, 3);
    }

    [Fact]
    public void Interpolate_BothNull_StaysNull()
    {
        var interpolator = new SampleInterpolator();
        var a = Sample(throttle: null, brake: null, speed: null);
        var b = Sample(throttle: null, brake: null, speed: null);

        var result = interpolator.Interpolate(a, b, 0.5);

        Assert.Null(result.ThrottleInput);
    }
}
