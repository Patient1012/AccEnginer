using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay;
using AccEngineer.UI.Overlay.Panels;
using Xunit;

namespace AccEngineer.UI.Tests;

public sealed class TyresPanelTests
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
    public void Refresh_MapsEachCornerFromItsWheelArrays()
    {
        var sample = new TelemetrySample
        {
            SessionId = "s1",
            TyreCompound = "Dry",
            TyreCoreTempC = new[] { 80.0f, 81.0f, 78.0f, 79.0f },
            WheelPressure = new[] { 22.6f, 22.5f, 22.9f, 23.0f },
            BrakeTempC = new[] { 410.0f, 420.0f, 300.0f, 310.0f }
        };

        using var vm = new TyresPanelViewModel(new FakeSource { Latest = sample });

        Assert.True(vm.IsLive);
        Assert.Equal("Dry", vm.CompoundText);
        Assert.Equal("FL", vm.FrontLeft.Label);
        Assert.Equal("80.0 °C", vm.FrontLeft.TempText);
        Assert.Equal("22.6", vm.FrontLeft.PressureText);
        Assert.Equal("410.0 °C", vm.FrontLeft.BrakeText);
        Assert.Equal("RR", vm.RearRight.Label);
        Assert.Equal("79.0 °C", vm.RearRight.TempText);
        Assert.Equal("310.0 °C", vm.RearRight.BrakeText);
    }

    [Fact]
    public void Refresh_WithoutWheelChannels_ShowsUnavailablePerCorner()
    {
        var sample = new TelemetrySample { SessionId = "s1" };
        using var vm = new TyresPanelViewModel(new FakeSource { Latest = sample });

        Assert.True(vm.IsLive);
        Assert.Equal("--", vm.FrontLeft.TempText);
        Assert.Equal("--", vm.FrontLeft.PressureText);
        Assert.Equal("--", vm.FrontLeft.BrakeText);
        Assert.Equal("--", vm.RearRight.TempText);
    }

    [Fact]
    public void Refresh_WithNoSignal_ShowsNoSignalStatus()
    {
        using var vm = new TyresPanelViewModel(new FakeSource { Latest = null });

        Assert.False(vm.IsLive);
        Assert.Equal("NO SIGNAL", vm.StatusText);
        Assert.Equal("--", vm.FrontLeft.TempText);
    }
}
