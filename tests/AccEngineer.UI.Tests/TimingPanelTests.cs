using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay;
using AccEngineer.UI.Overlay.Controls;
using AccEngineer.UI.Overlay.Panels;
using Xunit;

namespace AccEngineer.UI.Tests;

public sealed class TimingPanelTests
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
    public void Refresh_WithSectorAndDelta_FormatsSectorAndColoursDelta()
    {
        var sample = new TelemetrySample
        {
            SessionId = "s1",
            CurrentSectorIndex = 1,
            SectorCount = 3,
            LastSectorTimeMs = 27_000,
            DeltaLapTimeMs = -180,
            IsDeltaPositive = false
        };
        using var vm = new TimingPanelViewModel(new FakeSource { Latest = sample });

        Assert.Equal("2/3  0:27.000", vm.SectorText);
        Assert.Equal("-0.180 s", vm.DeltaText);
        Assert.Equal(TimingState.DeltaAhead, vm.DeltaState);
    }

    [Fact]
    public void Refresh_WithPositiveDelta_UsesBehindState()
    {
        var sample = new TelemetrySample
        {
            SessionId = "s1",
            DeltaLapTimeMs = 240,
            IsDeltaPositive = true
        };
        using var vm = new TimingPanelViewModel(new FakeSource { Latest = sample });

        Assert.Equal("+0.240 s", vm.DeltaText);
        Assert.Equal(TimingState.DeltaBehind, vm.DeltaState);
    }

    [Fact]
    public void Refresh_WithNoSignal_ShowsUnavailable()
    {
        using var vm = new TimingPanelViewModel(new FakeSource { Latest = null });

        Assert.Equal("--", vm.SectorText);
        Assert.Equal("--", vm.DeltaText);
        Assert.Equal(TimingState.Unavailable, vm.DeltaState);
    }
}
