using System.Text.RegularExpressions;
using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay;
using AccEngineer.UI.Overlay.Panels;
using Xunit;

namespace AccEngineer.UI.Tests;

/// <summary>
/// End-to-end compatibility of the built-in test-data mode with the seconds-gap
/// system: the deterministic standings feed's reported time gaps and the simulated
/// telemetry's pace history must both drive the signed-seconds columns, the two
/// providers must agree on the lap length the estimate wraps against, and nothing
/// may fall back to metres anywhere.
/// </summary>
public sealed class TestDataGapCompatibilityTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;
    private static readonly Regex Seconds = new(@"^[+-]?\d+\.\d{3}$", RegexOptions.Compiled);

    private sealed class TestModeSource : IOverlayTelemetrySource
    {
        public TelemetrySample? Latest { get; init; }
        public CompetitorStandings? Standings { get; init; }
        public IReadOnlyList<TelemetrySample> RecentSamples { get; init; } = Array.Empty<TelemetrySample>();

        public IReadOnlyList<TelemetrySample> Recent(int maxCount) => RecentSamples;
        public bool IsTestData => true;
        public string StandingsStatusText => "Test data";
    }

    /// <summary>Manual clock so the simulated reader emits deterministically.</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public ManualTimeProvider(DateTimeOffset start) => _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }

    /// <summary>Recent-window history exactly as test mode produces it: 900 samples
    /// at the simulated reader's 50 ms cadence (45 s of driving).</summary>
    private static List<TelemetrySample> SimulatedHistory(int count = 900)
    {
        var time = new ManualTimeProvider(T0);
        using var reader = new SimulatedTelemetryReader(new SimulatedTelemetryOptions(), time);

        var samples = new List<TelemetrySample>(count);
        for (var i = 0; i < count; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(50));
            var sample = reader.ReadLatest();
            if (sample is not null) samples.Add(sample);
        }

        return samples;
    }

    [Fact]
    public void TestFeed_ReportedGapsRenderAsSignedSeconds_InTheRelativePanel()
    {
        // Focused car 10 sits mid-field: the window around it must contain cars both
        // ahead (negative) and behind (positive), plus the player's own 0.000.
        var standings = new TestCompetitorFeed().Snapshot(T0, focusedCarIndex: 10);

        using var vm = new RelativePanelViewModel(new TestModeSource
        {
            Latest = new TelemetrySample { PlayerPosition = 11, ActiveCars = 20 },
            Standings = standings
        });

        Assert.Equal(8, vm.Rows.Count);
        Assert.All(vm.Rows, r => Assert.Matches(Seconds, r.GapText));
        Assert.All(vm.Rows, r => Assert.DoesNotContain("m", r.GapText));
        Assert.All(vm.Rows, r => Assert.NotNull(r.GapSeconds));

        // The feed is classified order (the CompetitorStandings contract), so the
        // window runs P7..P14 with the player's P11 row centred in it.
        Assert.Equal(new[] { 7, 8, 9, 10, 11, 12, 13, 14 }, vm.Rows.Select(r => r.Position).ToArray());

        Assert.Equal("0.000", vm.Rows.Single(r => r.CarId == 10).GapText);   // player
        Assert.Equal("+5.000", vm.Rows.Single(r => r.CarId == 9).GapText);    // 0.05 lap behind
        Assert.Equal("-5.000", vm.Rows.Single(r => r.CarId == 11).GapText);   // 0.05 lap ahead
        Assert.Contains(vm.Rows, r => r.GapSeconds!.Value > 0);
        Assert.Contains(vm.Rows, r => r.GapSeconds!.Value < 0);
    }

    [Fact]
    public void TestFeed_IntervalsRenderAsSignedSeconds_InTheStandingsPanel()
    {
        // Adjacent cars are a fifth of a lap apart on a 100 s simulated lap, so the
        // reported interval is exactly 5.000 s everywhere. The focused car is P1, so the
        // nine-car window clamps to P1..P9; the wrap straddle (car 11 vs car 12) is
        // outside the window and not exercised here.
        var standings = new TestCompetitorFeed().Snapshot(T0);
        var source = new TestModeSource
        {
            Latest = new TelemetrySample { SessionType = SessionType.Race, TrackName = "Test Circuit" },
            Standings = standings,
            RecentSamples = SimulatedHistory()
        };

        using var vm = new StandingsPanelViewModel(source);
        var rows = vm.Items.OfType<StandingsRow>().ToList();

        // The tower is a fixed nine-car, player-centred window, so the player is always
        // present regardless of their classification position.
        Assert.Equal(9, rows.Count);
        Assert.Contains(rows, r => r.IsPlayer);
        Assert.All(rows, r => Assert.True(r.GapText == "--" || Seconds.IsMatch(r.GapText), r.GapText));
        Assert.All(rows, r => Assert.DoesNotContain("m", r.GapText));

        // Adjacent cars are a fifth of a lap apart, so every interval behind the leader
        // reads an exact signed 5.000 s. The leader reads "--"; whether the leader is in
        // the window depends on where the player is classified.
        var leader = rows.SingleOrDefault(r => r.Position == 1);
        if (leader is not null)
        {
            Assert.Equal("--", leader.GapText);
            Assert.Null(leader.GapSeconds);
        }

        Assert.All(rows.Where(r => r.Position != 1), r => Assert.Equal("+5.000", r.GapText));
    }

    [Fact]
    public void TestMode_WithNoReportedGaps_EstimatesFromSimulatedPaceHistory()
    {
        // A source that reports standings but no time gaps (the real ACC case) must
        // still render seconds in test mode, because the simulated telemetry feeds
        // the same pace-profile estimator used for live data.
        var snapshot = new TestCompetitorFeed().Snapshot(T0, focusedCarIndex: 10);
        var standings = new CompetitorStandings(
            snapshot.TrackName, snapshot.TrackMeters, snapshot.SessionTimeMs,
            snapshot.SessionPhase, snapshot.FocusedCarIndex,
            snapshot.Competitors.Select(c => c with { RelativeTimeGapMs = null }).ToList(),
            snapshot.ReceivedAtUtc);

        using var vm = new RelativePanelViewModel(new TestModeSource
        {
            Latest = new TelemetrySample { PlayerPosition = 11, ActiveCars = 20 },
            Standings = standings,
            RecentSamples = SimulatedHistory()
        });

        Assert.Equal(8, vm.Rows.Count);
        Assert.All(vm.Rows, r => Assert.Matches(Seconds, r.GapText));
        Assert.All(vm.Rows, r => Assert.NotNull(r.GapSeconds));
        Assert.Equal("0.000", vm.Rows.Single(r => r.IsPlayer).GapText);
        Assert.Contains(vm.Rows, r => r.GapSeconds!.Value > 0);
        Assert.Contains(vm.Rows, r => r.GapSeconds!.Value < 0);
    }

    [Fact]
    public void TestDataLapLength_MatchesBetweenTheStandingsFeedAndTheSimulator()
    {
        // The estimate bins the simulator's progress over one lap while wrapping the
        // standings arc against the feed's track length: both must describe the same
        // track, or every test-data gap would be scaled wrongly.
        var snapshot = new TestCompetitorFeed().Snapshot(T0);

        Assert.Equal(4300d, snapshot.TrackMeters!.Value);
        Assert.Equal(4300d, new SimulatedTelemetryOptions().LapLengthMeters);
        Assert.True(RelativePanelViewModel.IsPlausibleTrackLength(snapshot.TrackMeters));
    }
}
