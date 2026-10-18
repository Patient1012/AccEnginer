using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay;
using AccEngineer.UI.Overlay.Panels;
using AccEngineer.UI.Services;
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
        public IReadOnlyList<TelemetrySample> RecentSamples { get; init; } = Array.Empty<TelemetrySample>();

        public IReadOnlyList<TelemetrySample> Recent(int maxCount) => RecentSamples;
        public bool IsTestData => TestMode;
    }

    private static Competitor Car(
        ushort index, bool isPlayer, string? fullName, int? number, int position,
        float spline, int laps, byte? model = null, AccDriverCategory? category = null) =>
        new(index, isPlayer, null, fullName, number, null, null, AccCarLocation.Track,
            position, position, laps, null, null, null, spline, 200)
        {
            CarModelType = model,
            DriverCategory = category
        };

    private static TelemetrySample Sample(
        int? position = null, int? activeCars = null, float? airTempC = null, int playerId = 1) =>
        new() { PlayerPosition = position, ActiveCars = activeCars, AirTempC = airTempC, PlayerCarId = playerId };

    /// <summary>
    /// Whole-lap pace history at a flat 50 m/s (180 km/h): dense enough for the profile
    /// to be usable and slow enough that gap expectations are exact arithmetic.
    /// </summary>
    private static IReadOnlyList<TelemetrySample> PaceHistory()
    {
        var samples = new List<TelemetrySample>();
        for (var p = 0d; p < 7004; p += 5)
        {
            samples.Add(new TelemetrySample { TrackProgressMeters = (float)p, SpeedMetersPerSecond = 50f });
        }

        return samples;
    }

    // ---- Gap correctness (the -115,601,700 m class of bug) -------------------

    [Fact]
    public void Refresh_WithSaneStandings_ShowsSignedSecondGapsFromThePaceProfile()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player Name", 7, 1, 0.60f, 3),
                Car(2, false, "Rival Ahead", 9, 2, 0.61f, 3),
                Car(3, false, "Rival Behind", 4, 3, 0.55f, 3)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(
            new FakeSource { Latest = Sample(), Standings = standings, RecentSamples = PaceHistory() });

        Assert.Equal(3, vm.Rows.Count);

        // 0.01 lap = 70.04 m ahead at 50 m/s = 1.4008 s → negative (ahead).
        Assert.Equal("0.000", vm.Rows[0].GapText);
        Assert.Equal(-1.4008, vm.Rows[1].GapSeconds!.Value, 3);
        Assert.Equal("-1.401", vm.Rows[1].GapText);
        // 0.05 lap = 350.2 m behind = 7.004 s → positive (behind).
        Assert.Equal(7.004, vm.Rows[2].GapSeconds!.Value, 3);
        Assert.Equal("+7.004", vm.Rows[2].GapText);
    }

    [Fact]
    public void Refresh_WithReportedTimeGapFromTheSource_UsesItInsteadOfTheEstimate()
    {
        // Sources that genuinely know time gaps (the synthetic test feed) win over the
        // estimate; real ACC never sets this, so it stays null there.
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3),
                Car(2, false, "Ahead", 9, 2, 0.61f, 3) with { RelativeTimeGapMs = -2_500 },
                Car(3, false, "Behind", 4, 3, 0.55f, 3) with { RelativeTimeGapMs = 4_000 }
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal("0.000", vm.Rows[0].GapText);
        Assert.Equal("-2.500", vm.Rows[1].GapText);
        Assert.Equal("+4.000", vm.Rows[2].GapText);
    }

    [Fact]
    public void Refresh_WithImplausibleReportedTimeGap_TreatsItAsUnavailable()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3),
                Car(2, false, "Garbage", 9, 2, 0.61f, 3) with { RelativeTimeGapMs = 10_000_000 }
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal("--", vm.Rows[1].GapText);
    }

    [Fact]
    public void Refresh_WithoutPaceHistory_ShowsUnavailableNotMetres()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3),
                Car(2, false, "Rival", 9, 2, 0.61f, 3)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal("0.000", vm.Rows[0].GapText);
        Assert.Equal("--", vm.Rows[1].GapText);
        Assert.Null(vm.Rows[1].GapSeconds);
        Assert.DoesNotContain(" m", vm.Rows[1].GapText);
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

        using var vm = new RelativePanelViewModel(
            new FakeSource { Latest = Sample(), Standings = standings, RecentSamples = PaceHistory() });

        Assert.Equal("0.000", vm.Rows[0].GapText);
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

        using var vm = new RelativePanelViewModel(
            new FakeSource { Latest = Sample(), Standings = standings, RecentSamples = PaceHistory() });

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

        using var vm = new RelativePanelViewModel(
            new FakeSource { Latest = Sample(), Standings = standings, RecentSamples = PaceHistory() });

        Assert.Equal("0.000", vm.Rows[1].GapText);
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

        using var vm = new RelativePanelViewModel(
            new FakeSource { Latest = Sample(), Standings = standings, RecentSamples = PaceHistory() });

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
    public void Refresh_WithStandings_UsesRealNamesNumbersClassesAndRatings()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 5,
            new[]
            {
                Car(5, true, "Player Name", 7, 6, 0.60f, 3, model: 0, category: AccDriverCategory.Platinum),
                Car(8, false, "Cam William", 14, 5, 0.61f, 3, model: 50, category: AccDriverCategory.Silver),
                Car(2, false, "Wade Warren", 1, 7, 0.55f, 3)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new RelativePanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal(3, vm.Rows.Count);

        Assert.Equal("Player Name", vm.Rows[0].Name);
        Assert.Equal("#7", vm.Rows[0].CarNumberText);
        Assert.Equal("GT3", vm.Rows[0].CarClassText);
        Assert.Equal("PLT", vm.Rows[0].DriverCategoryText);
        Assert.True(vm.Rows[0].IsPlayer);
        Assert.Equal(6, vm.Rows[0].Position);

        Assert.Equal("Cam William", vm.Rows[1].Name);
        Assert.Equal("#14", vm.Rows[1].CarNumberText);
        Assert.Equal("GT4", vm.Rows[1].CarClassText);
        Assert.Equal("SLV", vm.Rows[1].DriverCategoryText);

        Assert.Equal("#1", vm.Rows[2].CarNumberText);
        // No entry-list data for this car: both badges stay empty, never invented.
        Assert.Equal("", vm.Rows[2].CarClassText);
        Assert.Equal("", vm.Rows[2].DriverCategoryText);
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
    [InlineData(AccCarClass.Gt3, "GT3")]
    [InlineData(AccCarClass.Gt4, "GT4")]
    [InlineData(AccCarClass.Gt2, "GT2")]
    [InlineData(AccCarClass.Gtc, "GTC")]
    [InlineData(AccCarClass.Tcx, "TCX")]
    [InlineData(AccCarClass.Unknown, "")]
    public void CarClassCode_MapsVehicleClassToBadge(AccCarClass carClass, string expected) =>
        Assert.Equal(expected, Display.CarClassCode(carClass));

    [Theory]
    [InlineData(AccDriverCategory.Platinum, "PLT")]
    [InlineData(AccDriverCategory.Gold, "GLD")]
    [InlineData(AccDriverCategory.Silver, "SLV")]
    [InlineData(AccDriverCategory.Bronze, "BRZ")]
    [InlineData(null, "")]
    public void DriverCategoryCode_MapsCategoryToBadge(AccDriverCategory? category, string expected) =>
        Assert.Equal(expected, Display.DriverCategoryCode(category));

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
