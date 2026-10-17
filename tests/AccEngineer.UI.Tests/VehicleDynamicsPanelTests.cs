using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay;
using AccEngineer.UI.Overlay.Panels;
using Xunit;

namespace AccEngineer.UI.Tests;

public sealed class VehicleDynamicsPanelTests
{
    private sealed class FakeSource : IOverlayTelemetrySource
    {
        public TelemetrySample? Latest { get; init; }
        public IReadOnlyList<TelemetrySample> Recent(int maxCount) => Array.Empty<TelemetrySample>();
        public bool IsTestData => false;
        public CompetitorStandings? Standings => null;
        public string StandingsStatusText => "Broadcasting off.";
    }

    [Fact]
    public void Refresh_MapsDynamicsAndPerCornerSuspension()
    {
        var sample = new TelemetrySample
        {
            SessionId = "s1",
            SpeedKph = 182f,
            LateralAccelerationG = -1.25f,
            LongitudinalAccelerationG = 0.8f,
            AngularVelocityYawRadPerSec = 0.35f,
            SuspensionTravelMeters = new[] { 0.021f, 0.019f, 0.034f, 0.033f }
        };

        using var vm = new VehicleDynamicsPanelViewModel(new FakeSource { Latest = sample });

        Assert.True(vm.IsLive);
        Assert.Equal("182 km/h", vm.SpeedText);
        Assert.Equal("-1.25 g", vm.LateralGText);
        Assert.Equal("+0.80 g", vm.LongitudinalGText);
        Assert.Equal("+0.35 rad/s", vm.YawText);
        Assert.Equal("21 mm", vm.FrontLeft.TravelText);
        Assert.Equal("19 mm", vm.FrontRight.TravelText);
        Assert.Equal("34 mm", vm.RearLeft.TravelText);
        Assert.Equal("33 mm", vm.RearRight.TravelText);
    }

    [Fact]
    public void Refresh_WithoutSuspensionChannel_ShowsUnavailablePerCorner()
    {
        var sample = new TelemetrySample { SessionId = "s1" };
        using var vm = new VehicleDynamicsPanelViewModel(new FakeSource { Latest = sample });

        Assert.True(vm.IsLive);
        Assert.Equal("--", vm.FrontLeft.TravelText);
        Assert.Null(sample.SuspensionTravelMeters);
    }

    [Fact]
    public void Refresh_WithNoSignal_ShowsNoSignalStatus()
    {
        using var vm = new VehicleDynamicsPanelViewModel(new FakeSource { Latest = null });

        Assert.False(vm.IsLive);
        Assert.Equal("NO SIGNAL", vm.StatusText);
        Assert.Equal("--", vm.SpeedText);
        Assert.Equal("--", vm.FrontLeft.TravelText);
    }
}
