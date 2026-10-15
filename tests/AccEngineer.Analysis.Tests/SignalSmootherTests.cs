using AccEngineer.Analysis.Telemetry;
using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Tests;

public class SignalSmootherTests
{
    private static TelemetrySample Sample(long id, float? throttle = null, float? brake = null, float? steering = null) =>
        new()
        {
            SampleId = id,
            SessionId = "sm-test",
            LapNumber = 1,
            ThrottleInput = throttle,
            BrakeInput = brake,
            SteeringInput = steering
        };

    [Fact]
    public void Smooth_AveragesWindow()
    {
        var smoother = new SignalSmoother();
        var window = new[] { Sample(1, throttle: 0f), Sample(2, throttle: 1f), Sample(3, throttle: 1f) };

        var result = smoother.Smooth(window[1], window);

        Assert.Equal(0.6667f, result.ThrottleInput!.Value, 3);
    }

    [Fact]
    public void Smooth_EmptyWindow_ReturnsOriginal()
    {
        var smoother = new SignalSmoother();
        var sample = Sample(1, throttle: 0.5f);

        var result = smoother.Smooth(sample, Array.Empty<TelemetrySample>());

        Assert.Same(sample, result);
    }

    [Fact]
    public void Smooth_PreservesNullChannels()
    {
        var smoother = new SignalSmoother();
        var window = new[] { Sample(1), Sample(2), Sample(3) };

        var result = smoother.Smooth(window[0], window);

        Assert.Null(result.ThrottleInput);
        Assert.Null(result.BrakeInput);
        Assert.Null(result.SteeringInput);
    }
}
