using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay;
using AccEngineer.UI.Overlay.Controls;
using AccEngineer.UI.Overlay.Panels;
using Xunit;

namespace AccEngineer.UI.Tests;

public sealed class StandingsPanelTests
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
        ushort index,
        bool isPlayer,
        string? name,
        int? number,
        int position,
        float spline,
        int laps,
        int? bestLapMs = null,
        int? lastLapMs = null,
        byte? model = null,
        AccDriverCategory? category = null,
        AccCarLocation location = AccCarLocation.Track,
        int? relativeTimeGapMs = null) =>
        new(index, isPlayer, name, null, number, null, null, location,
            position, position, laps, null, bestLapMs, lastLapMs, spline, 200)
        {
            CarModelType = model,
            DriverCategory = category,
            RelativeTimeGapMs = relativeTimeGapMs
        };

    private static TelemetrySample Sample(SessionType type = SessionType.Race, string? track = "Spa") =>
        new() { SessionType = type, TrackName = track };

    /// <summary>Whole-lap pace history at a flat 50 m/s so interval expectations are
    /// exact arithmetic (distance ÷ 50).</summary>
    private static IReadOnlyList<TelemetrySample> PaceHistory()
    {
        var samples = new List<TelemetrySample>();
        for (var p = 0d; p < 7004; p += 5)
        {
            samples.Add(new TelemetrySample { TrackProgressMeters = (float)p, SpeedMetersPerSecond = 50f });
        }

        return samples;
    }

    private static IReadOnlyList<StandingsRow> Rows(StandingsPanelViewModel vm) =>
        vm.Items.OfType<StandingsRow>().ToList();

    // ---- Unavailable state ---------------------------------------------------

    [Fact]
    public void Refresh_WithNoBroadcast_ShowsExplicitReasonAndNoItems()
    {
        var source = new FakeSource { Standings = null, StandingsStatusText = "Broadcasting off." };
        using var vm = new StandingsPanelViewModel(source);

        Assert.False(vm.IsLive);
        Assert.Empty(vm.Items);
        Assert.Equal("Broadcasting off.", vm.StatusText);
        Assert.Equal("--:--:--", vm.SessionTimerText);
        Assert.Equal(string.Empty, vm.SessionContextText);
    }

    // ---- Row projection ------------------------------------------------------

    [Fact]
    public void Refresh_WithStandings_ProjectsPositionsNamesGapsAndFieldBest()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 3_725_000f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3, bestLapMs: 100_000, lastLapMs: 101_000,
                    model: 0, category: AccDriverCategory.Platinum),
                Car(2, false, "Rival", 9, 2, 0.50f, 3, bestLapMs: 101_000, lastLapMs: 102_000,
                    model: 0, category: AccDriverCategory.Silver)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(
            new FakeSource { Latest = Sample(), Standings = standings, RecentSamples = PaceHistory() });

        Assert.True(vm.IsLive);
        Assert.Equal("LIVE", vm.StatusText);
        Assert.Equal("1:02:05", vm.SessionTimerText);
        Assert.Equal("RACE  ·  Spa", vm.SessionContextText);
        Assert.Equal("P1", vm.PlayerPositionText);
        Assert.Equal("GT3", vm.PlayerClassText);

        var rows = Rows(vm);
        Assert.Equal(2, rows.Count);

        Assert.Equal("1", rows[0].PositionText);
        Assert.Equal("Player", rows[0].Name);
        Assert.Equal("7", rows[0].NumberText);
        Assert.Equal("PLT", rows[0].RatingText);
        Assert.True(rows[0].IsPlayer);
        Assert.Equal("--", rows[0].GapText);
        Assert.Null(rows[0].GapSeconds);
        Assert.Equal("1:40.000", rows[0].FastestText);
        Assert.Equal(TimingState.SessionBest, rows[0].FastestState);

        Assert.Equal("Rival", rows[1].Name);
        Assert.Equal("SLV", rows[1].RatingText);
        // 0.10 lap = 700.4 m from the car ahead at 50 m/s = 14.008 s.
        Assert.Equal(14.008, rows[1].GapSeconds!.Value, 3);
        Assert.Equal("+14.008", rows[1].GapText);
        Assert.Equal("1:41.000", rows[1].FastestText);
        Assert.Equal(TimingState.Normal, rows[1].FastestState);
        Assert.Equal("1:42.000", rows[1].LastText);
    }

    [Fact]
    public void Refresh_WithReportedTimeGaps_TakesTheIntervalFromTheSource()
    {
        // The synthetic feed reports each car's time gap to the player; the interval is
        // the difference between neighbours, never the distance divided by a constant.
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3, relativeTimeGapMs: 0),
                Car(2, false, "Second", 9, 2, 0.50f, 3, relativeTimeGapMs: 2_000),
                Car(3, false, "Third", 4, 3, 0.40f, 3, relativeTimeGapMs: 6_000)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        var rows = Rows(vm);
        Assert.Equal("--", rows[0].GapText);   // leader has no interval
        Assert.Equal("+2.000", rows[1].GapText);
        Assert.Equal(2.0, rows[1].GapSeconds!.Value, 3);
        Assert.Equal("+4.000", rows[2].GapText);
        Assert.Equal(4.0, rows[2].GapSeconds!.Value, 3);
    }

    [Fact]
    public void Refresh_WithImplausibleReportedInterval_FallsBackToThePaceEstimate()
    {
        // Two half-lap-wrapped values from opposite sides of the track read ~96 s apart;
        // that is a wrap artefact, so the estimate takes over.
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3, relativeTimeGapMs: -48_000),
                Car(2, false, "Rival", 9, 2, 0.50f, 3, relativeTimeGapMs: 48_000)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(
            new FakeSource { Latest = Sample(), Standings = standings, RecentSamples = PaceHistory() });

        Assert.Equal("+14.008", Rows(vm)[1].GapText);
    }

    [Fact]
    public void Refresh_WithoutPaceHistoryOrReportedGaps_ShowsUnavailableNotMetres()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3),
                Car(2, false, "Rival", 9, 2, 0.50f, 3)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        var rows = Rows(vm);
        Assert.Equal("--", rows[0].GapText);
        Assert.Equal("--", rows[1].GapText);
        Assert.Null(rows[1].GapSeconds);
        Assert.DoesNotContain(" m", rows[1].GapText);
    }

    [Fact]
    public void Refresh_WithLapDeficit_ShowsLapsDownInsteadOfSeconds()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3),
                Car(2, false, "Rival", 9, 2, 0.50f, 2)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(
            new FakeSource { Latest = Sample(), Standings = standings, RecentSamples = PaceHistory() });

        Assert.Equal("+1L", Rows(vm)[1].GapText);
        Assert.Null(Rows(vm)[1].GapSeconds);
    }

    [Fact]
    public void Refresh_ClassifiesByPosition_NotSourceOrder()
    {
        // The source is car-index ordered; the official position order is different.
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 2,
            new[]
            {
                Car(1, false, "Third", 1, 3, 0.50f, 3),
                Car(2, true, "Winner", 2, 1, 0.70f, 3),
                Car(3, false, "Second", 3, 2, 0.60f, 3)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(
            new FakeSource { Latest = Sample(), Standings = standings, RecentSamples = PaceHistory() });

        var rows = Rows(vm);
        Assert.Equal(new[] { "Winner", "Second", "Third" }, rows.Select(r => r.Name));
        Assert.Equal(new[] { "1", "2", "3" }, rows.Select(r => r.PositionText));
        // Gap is against the car ahead in the classification, computed before grouping.
        Assert.Equal("--", rows[0].GapText);
        Assert.Equal("+14.008", rows[1].GapText);
        Assert.Equal("+14.008", rows[2].GapText);
    }

    [Fact]
    public void Refresh_MarksTheOverallFastestLapAsSessionBest()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3, bestLapMs: 101_000),
                Car(2, false, "Rival", 9, 2, 0.50f, 3, bestLapMs: 100_000)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        var rows = Rows(vm);
        Assert.Equal(TimingState.Normal, rows[0].FastestState);
        Assert.Equal(TimingState.SessionBest, rows[1].FastestState);
    }

    // ---- Class grouping ------------------------------------------------------

    [Fact]
    public void Refresh_GroupsByCarClass_InDisplayOrder()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                // Deliberately shuffled and mixed so grouping (not source order) decides layout.
                Car(1, false, "AmOne", 1, 3, 0.50f, 3, model: 0),
                Car(2, true, "Gt4One", 2, 1, 0.70f, 3, model: 50),
                Car(3, false, "AmTwo", 3, 2, 0.60f, 3, model: 0)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        var headers = vm.Items.OfType<StandingsClassHeader>().ToList();
        Assert.Equal(new[] { "GT3", "GT4" }, headers.Select(h => h.ClassLabel));
        Assert.Equal(new[] { "2 cars", "1 car" }, headers.Select(h => h.CountText));

        // Rows appear under their own header, contiguous.
        Assert.IsType<StandingsClassHeader>(vm.Items[0]);
        // Within a group rows keep classified order (AmTwo is P2, AmOne is P3).
        Assert.Equal("AmTwo", ((StandingsRow)vm.Items[1]).Name);
        Assert.Equal("AmOne", ((StandingsRow)vm.Items[2]).Name);
        Assert.IsType<StandingsClassHeader>(vm.Items[3]);
        Assert.Equal("Gt4One", ((StandingsRow)vm.Items[4]).Name);
    }

    [Fact]
    public void Refresh_WithNoClassMetadata_FallsBackToFlatList()
    {
        // Cars with no entry-list model id all classify as Unknown → one flat list, no headers.
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3),
                Car(2, false, "Rival", 9, 2, 0.50f, 3)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Empty(vm.Items.OfType<StandingsClassHeader>());
        Assert.Equal(2, Rows(vm).Count);
    }

    // ---- Appearance ----------------------------------------------------------

    [Fact]
    public void Refresh_ShowsAFixedNineCarWindow_IgnoringAppearanceRowCount()
    {
        var cars = Enumerable.Range(1, 20)
            .Select(i => Car((ushort)i, i == 1, "Driver" + i, i, i, 0.9f - i * 0.02f, 3))
            .ToArray();
        var standings = new CompetitorStandings("Spa", 7004, 0f, 0, 1, cars, DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });
        vm.ApplyAppearance(new OverlayAppearance { StandingsRows = 3 });

        // The tower is a fixed nine-car window; the editor's row count is no longer used.
        Assert.Equal(9, Rows(vm).Count);
    }

    [Fact]
    public void Refresh_WindowsNineCarsAroundThePlayer_AlwaysIncludingThePlayer()
    {
        // 20-car field; the player is P10 (focused car index 10). The window is centred:
        // 4 ahead (P6..P9), self (P10), 4 behind (P11..P14).
        var cars = Enumerable.Range(1, 20)
            .Select(i => Car((ushort)i, i == 10, "Driver" + i, i, i, 0.9f - i * 0.02f, 3))
            .ToArray();
        var standings = new CompetitorStandings("Spa", 7004, 0f, 0, 10, cars, DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        var rows = Rows(vm);
        Assert.Equal(9, rows.Count);
        Assert.Contains(rows, r => r.IsPlayer);
        Assert.Equal(new[] { "6", "7", "8", "9", "10", "11", "12", "13", "14" },
            rows.Select(r => r.PositionText));
    }

    [Fact]
    public void Refresh_NearTheLeader_ClampsTheWindowToAFullNineCars()
    {
        // Player is P2: the window cannot show 4 cars ahead, so it clamps to P1..P9.
        var cars = Enumerable.Range(1, 20)
            .Select(i => Car((ushort)i, i == 2, "Driver" + i, i, i, 0.9f - i * 0.02f, 3))
            .ToArray();
        var standings = new CompetitorStandings("Spa", 7004, 0f, 0, 2, cars, DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        var rows = Rows(vm);
        Assert.Equal(9, rows.Count);
        Assert.Equal(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" },
            rows.Select(r => r.PositionText));
    }

    [Fact]
    public void Refresh_SmallField_ShowsEveryCar()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3),
                Car(2, false, "Rival", 9, 2, 0.50f, 3)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal(2, Rows(vm).Count);
    }

    // ---- Player pit marker / placeholder safety ------------------------------

    [Fact]
    public void Refresh_FlagsPitAndAlternatesRows()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3),
                Car(2, false, "Rival", 9, 2, 0.50f, 3, location: AccCarLocation.Pitlane)
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        var rows = Rows(vm);
        Assert.False(rows[0].Alternate);
        Assert.True(rows[1].Alternate);
        Assert.True(rows[1].InPit);
    }

    [Fact]
    public void Refresh_WithUnknownName_ShowsGenericCarLabelNotInvention()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[] { Car(4, true, null, null, 1, 0.60f, 3) },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal("CAR 04", Rows(vm)[0].Name);
        Assert.Equal("", Rows(vm)[0].NumberText);
    }

    // ---- Brand badges --------------------------------------------------------

    [Fact]
    public void Refresh_ProjectsBrandMonogramFromTheCarModelId()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[]
            {
                Car(1, true, "Player", 7, 1, 0.60f, 3, model: 0),   // Porsche
                Car(2, false, "Rival", 9, 2, 0.50f, 3, model: 2)    // Ferrari
            },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        var rows = Rows(vm);
        Assert.Equal("POR", rows[0].BrandText);
        Assert.Equal("FER", rows[1].BrandText);
        Assert.False(ReferenceEquals(rows[0].BrandAccent, rows[1].BrandAccent));
    }

    [Fact]
    public void Refresh_WithoutCarModel_LeavesBrandEmptySoNoBadgeGuessed()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[] { Car(1, true, "Player", 7, 1, 0.60f, 3) },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal(string.Empty, Rows(vm)[0].BrandText);
    }

    [Fact]
    public void Refresh_WithUndocumentedModelId_LeavesBrandEmpty()
    {
        var standings = new CompetitorStandings(
            "Spa", 7004, 0f, 0, 1,
            new[] { Car(1, true, "Player", 7, 1, 0.60f, 3, model: 200) },
            DateTimeOffset.UnixEpoch);

        using var vm = new StandingsPanelViewModel(new FakeSource { Latest = Sample(), Standings = standings });

        Assert.Equal(string.Empty, Rows(vm)[0].BrandText);
    }
}
