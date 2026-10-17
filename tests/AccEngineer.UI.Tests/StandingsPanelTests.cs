using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay;
using AccEngineer.UI.Overlay.Panels;
using Xunit;

namespace AccEngineer.UI.Tests;

public sealed class StandingsPanelTests
{
    private sealed class FakeSource : IOverlayTelemetrySource
    {
        public TelemetrySample? Latest { get; init; }
        public CompetitorStandings? Standings { get; init; }
        public string StandingsStatusText { get; init; } = "Broadcasting off.";

        public IReadOnlyList<TelemetrySample> Recent(int maxCount) => Array.Empty<TelemetrySample>();
        public bool IsTestData => false;
    }

    private static Competitor Car(ushort index, bool isPlayer, string name, int? number, int position, float lap, int laps, int? lastLapMs) =>
        new(index, isPlayer, name, null, number, null, null, AccCarLocation.Track, position, position,
            laps, null, lastLapMs, lastLapMs, lap, 200);

    [Fact]
    public void Refresh_WithNoBroadcast_ShowsExplicitReasonAndNoRows()
    {
        var source = new FakeSource { Standings = null, StandingsStatusText = "Broadcasting off." };
        using var vm = new StandingsPanelViewModel(source);

        Assert.False(vm.IsLive);
        Assert.Empty(vm.Rows);
        Assert.Equal("Broadcasting off.", vm.StatusText);
    }

    [Fact]
    public void Refresh_WithStandings_ProjectsPositionsNamesAndMetreGaps()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3, 100_000),
                Car(2, false, "Rival", 9, 2, 0.50f, 3, 101_000)
            },
            DateTimeOffset.UnixEpoch);

        var source = new FakeSource { Standings = standings };
        using var vm = new StandingsPanelViewModel(source);

        Assert.True(vm.IsLive);
        Assert.Equal("LIVE", vm.StatusText);
        Assert.Equal(2, vm.Rows.Count);

        Assert.Equal("1", vm.Rows[0].PositionText);
        Assert.Equal("Player", vm.Rows[0].Name);
        Assert.Equal("7", vm.Rows[0].NumberText);
        Assert.True(vm.Rows[0].IsPlayer);
        Assert.Equal("--", vm.Rows[0].GapText);

        Assert.Equal("Rival", vm.Rows[1].Name);
        Assert.Equal("700 m", vm.Rows[1].GapText);
        Assert.Equal("1:41.000", vm.Rows[1].LastLapText);
    }

    [Fact]
    public void Refresh_WithLapDeficit_ShowsLapsDownInsteadOfMetres()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3, 100_000),
                Car(2, false, "Rival", 9, 2, 0.50f, 2, 101_000)
            },
            DateTimeOffset.UnixEpoch);

        var source = new FakeSource { Standings = standings };
        using var vm = new StandingsPanelViewModel(source);

        Assert.Equal("+1L", vm.Rows[1].GapText);
    }
}
