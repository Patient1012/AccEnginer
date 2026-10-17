using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay;
using AccEngineer.UI.Overlay.Panels;
using Xunit;

namespace AccEngineer.UI.Tests;

public sealed class RelativePanelTests
{
    private sealed class FakeSource : IOverlayTelemetrySource
    {
        public TelemetrySample? Latest { get; init; }
        public CompetitorStandings? Standings { get; init; }
        public bool TestMode { get; init; }
        public string StandingsStatusText { get; init; } = "Broadcasting off.";

        public IReadOnlyList<TelemetrySample> Recent(int maxCount) => Array.Empty<TelemetrySample>();
        public bool IsTestData => TestMode;
    }

    private static Competitor Car(
        ushort index, bool isPlayer, string? fullName, int? number, int position,
        float spline, int laps, AccCupCategory? cup = null) =>
        new(index, isPlayer, null, fullName, number, null, cup, AccCarLocation.Track,
            position, position, laps, null, null, null, spline, 200);

    private static TelemetrySample Sample(
        int? position = null, int? activeCars = null, float? airTempC = null, int playerId = 1) =>
        new() { PlayerPosition = position, ActiveCars = activeCars, AirTempC = airTempC, PlayerCarId = playerId };

    // ---- Gap correctness (the -115,601,700 m class of bug) -------------------

    [Fact]
    public void Refresh_WithSaneStandings_ShowsSaneSignedMetreGaps()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player Name", 7, 1, 0.60f, 3, AccCupCategory.Overall),
                Car(2, false, "Rival Ahead", 9, 2, 0.61f, 3, AccCupCategory.Am),
                Car(3, false, "Rival Behind", 4, 3, 0.55f, 3, AccCupCategory.ProAm)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal(3, vm.Rows.Count);
        Assert.Equal("0.0", vm.Rows[0].GapText);
        Assert.Equal("-70 m", vm.Rows[1].GapText);
        Assert.Equal("+350 m", vm.Rows[2].GapText);
    }

    [Fact]
    public void Refresh_WithAbsurdTrackLength_ShowsUnavailableNotGarbage()
    {
        // The historical defect: TrackMeters arrived implausible (e.g. 231,203,200)
        // and was multiplied straight into the displayed gap.
        var standings = new CompetitorStandings(
            "Spa", 231_203_200, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3),
                Car(2, false, "Rival", 9, 2, 0.61f, 3)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal("0.0", vm.Rows[0].GapText);
        Assert.Equal("--", vm.Rows[1].GapText);
    }

    [Fact]
    public void Refresh_WithMissingLapCount_ShowsUnavailableGap()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, laps: 3),
                Car(2, false, "Rival", 9, 2, 0.61f, laps: 0) with { LapCount = null }
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal("--", vm.Rows[1].GapText);
    }

    [Fact]
    public void Refresh_WithCarExactlyOneLapAhead_WrapsToShortProximity()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3),
                Car(2, false, "Lapped Traffic", 9, 12, 0.60f, 2)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal("0.0", vm.Rows[1].GapText);
    }

    [Fact]
    public void Refresh_WithOutlierSplinePosition_ShowsUnavailableGap()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3),
                Car(2, false, "Glitched", 9, 2, 1.8f, 3)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal("--", vm.Rows[1].GapText);
    }

    [Theory]
    [InlineData(7004, true)]
    [InlineData(50_000, true)]
    [InlineData(50_001, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(null, false)]
    public void IsPlausibleTrackLength_RejectsUnrealisticLengths(int? meters, bool expected) =>
        Assert.Equal(expected, RelativePanelViewModel.IsPlausibleTrackLength(meters));

    // ---- Rows: identity, numbers, classes ------------------------------------

    [Fact]
    public void Refresh_WithStandings_UsesRealNamesNumbersAndClasses()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 5,
            new[]
            {
                Car(5, true, "Player Name", 7, 6, 0.60f, 3, AccCupCategory.Overall),
                Car(8, false, "Cam William", 14, 5, 0.61f, 3, AccCupCategory.Silver),
                Car(2, false, "Wade Warren", 1, 7, 0.55f, 3, AccCupCategory.National)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal(3, vm.Rows.Count);

        Assert.Equal("Player Name", vm.Rows[0].Name);
        Assert.Equal("#7", vm.Rows[0].CarNumberText);
        Assert.Equal("", vm.Rows[0].ClassText);
        Assert.True(vm.Rows[0].IsPlayer);
        Assert.Equal(6, vm.Rows[0].Position);

        Assert.Equal("Cam William", vm.Rows[1].Name);
        Assert.Equal("#14", vm.Rows[1].CarNumberText);
        Assert.Equal("SIL", vm.Rows[1].ClassText);

        Assert.Equal("#1", vm.Rows[2].CarNumberText);
        Assert.Equal("NAT", vm.Rows[2].ClassText);
    }

    [Fact]
    public void Refresh_WithUnknownRaceNumber_OmitsNumberInsteadOfRowIndex()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", null, 1, 0.60f, 3)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal(string.Empty, vm.Rows[0].CarNumberText);
    }

    [Theory]
    [InlineData(AccCupCategory.Overall, "")]
    [InlineData(null, "")]
    [InlineData(AccCupCategory.ProAm, "PA")]
    [InlineData(AccCupCategory.Am, "AM")]
    [InlineData(AccCupCategory.Silver, "SIL")]
    [InlineData(AccCupCategory.National, "NAT")]
    public void ClassCode_MapsCupCategoryToBadge(AccCupCategory? cup, string expected) =>
        Assert.Equal(expected, RelativePanelViewModel.ClassCode(cup));

    // ---- Header ----------------------------------------------------------------

    [Fact]
    public void Refresh_WithSample_ShowsAirTempAndPositionCount()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[] { Car(1, true, "Player", 7, 6, 0.60f, 3) },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(
            new FakeSource { Latest = Sample(position: 6, activeCars: 25, airTempC: 27.4f), Standings = standings });

        Assert.Equal("27c", vm.HeaderLeftText);
        Assert.Equal("6/25", vm.HeaderRightText);
        Assert.Equal(string.Empty, vm.HeaderCentreText);
    }

    [Fact]
    public void Refresh_InTestMode_ShowsCompactTestIndicator()
    {
        using var vm = new RelativePanelViewModel(
            new FakeSource { Latest = Sample(), TestMode = true, Standings = null });

        Assert.Equal("TEST", vm.HeaderCentreText);
    }

    [Fact]
    public void Refresh_WithNoSample_ShowsNoSignal()
    {
        using var vm = new RelativePanelViewModel(new FakeSource { Latest = null });

        Assert.False(vm.IsLive);
        Assert.Equal("NO SIGNAL", vm.StatusText);
        Assert.Equal("NO SIGNAL", vm.HeaderCentreText);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public void Refresh_WithoutPlayerInStandings_FallsBackToProjectionNames()
    {
        // Focused car index points at nobody: panel must fall back, not crash.
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 99,
            new[] { Car(1, false, "Ghost", 7, 1, 0.60f, 3) },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.True(vm.IsLive);
    }

    // ---- Appearance -------------------------------------------------------------

    [Fact]
    public void ApplyAppearance_HonoursColumnVisibilityAndRowClamp()
    {
        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample() });

        vm.ApplyAppearance(new OverlayAppearance { RelativeRows = 99, RelativeShowGap = false, RelativeShowName = true });

        Assert.False(vm.ShowGapColumn);
        Assert.True(vm.ShowNameColumn);

        vm.ApplyAppearance(new OverlayAppearance { RelativeRows = 1, RelativeShowGap = true, RelativeShowName = false });

        Assert.True(vm.ShowGapColumn);
        Assert.False(vm.ShowNameColumn);
    }
}
