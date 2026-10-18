using AccEngineer.Analysis.Telemetry;
using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Tests;

/// <summary>
/// Tests for the pace profile behind the overlay's distance → time gap conversion.
/// The key properties: speed comes from the sampled profile (never a fixed constant),
/// insufficient history yields no estimate at all, and the walk wraps correctly.
/// </summary>
public sealed class TrackPaceProfileTests
{
    private const double SpaMeters = 7004;

    private static TelemetrySample Sample(double progressMeters, double speedMps) =>
        new() { TrackProgressMeters = (float)progressMeters, SpeedMetersPerSecond = (float)speedMps };

    /// <summary>Dense history at one constant speed, covering a whole lap.</summary>
    private static List<TelemetrySample> FlatHistory(double speedMps = 50, double stepMeters = 5)
    {
        var samples = new List<TelemetrySample>();
        for (var p = 0d; p < SpaMeters; p += stepMeters) samples.Add(Sample(p, speedMps));
        return samples;
    }

    /// <summary>Unwraps the nullable estimate so precision-assert overloads resolve.</summary>
    private static double Delta(TrackPaceProfile profile, double from, double delta, double? lapMeters)
    {
        var v = profile.TimeDeltaSeconds(from, delta, lapMeters);
        Assert.NotNull(v);
        return v!.Value;
    }

    // ---- Build: usable vs unusable history ------------------------------------

    [Fact]
    public void Build_WithDenseHistory_ReturnsUsableProfile()
    {
        var profile = TrackPaceProfile.Build(FlatHistory());

        Assert.NotNull(profile);
        Assert.Equal(50d, profile!.MeanSpeedMps, 6);
        Assert.True(profile.CoveredMeters >= 500, $"covered {profile.CoveredMeters}m");
    }

    [Fact]
    public void Build_WithEmptyOrNullHistory_ReturnsNull()
    {
        Assert.Null(TrackPaceProfile.Build(null));
        Assert.Null(TrackPaceProfile.Build(Array.Empty<TelemetrySample>()));
    }

    [Fact]
    public void Build_WithTooFewSamples_ReturnsNull()
    {
        var samples = Enumerable.Range(0, 10).Select(i => Sample(i * 50, 50)).ToList();

        Assert.Null(TrackPaceProfile.Build(samples));
    }

    [Fact]
    public void Build_WithTooLittleCoveredArc_ReturnsNull()
    {
        // Plenty of samples, but all inside a 200 m pocket: not a representative profile.
        var samples = Enumerable.Range(0, 100).Select(i => Sample(i * 2, 50)).ToList();

        Assert.Null(TrackPaceProfile.Build(samples));
    }

    [Fact]
    public void Build_WithOnlyStandingSpeeds_ReturnsNull()
    {
        var samples = Enumerable.Range(0, 60).Select(i => Sample(i * 20, 0.2)).ToList();

        Assert.Null(TrackPaceProfile.Build(samples));
    }

    [Fact]
    public void Build_WithoutProgressOrSpeed_SkipsUnusableSamples()
    {
        var samples = new List<TelemetrySample>
        {
            new() { SpeedMetersPerSecond = 50f },                    // no progress
            new() { TrackProgressMeters = 100f },                    // no speed
            Sample(double.NaN, 50),                                  // corrupt progress
            Sample(200, double.NaN)                                  // corrupt speed
        };

        Assert.Null(TrackPaceProfile.Build(samples));
    }

    // ---- TimeDeltaSeconds: exact integration ----------------------------------

    [Fact]
    public void TimeDeltaSeconds_AtConstantSpeed_IsDistanceOverSpeed()
    {
        var profile = TrackPaceProfile.Build(FlatHistory())!;

        Assert.Equal(2.0, Delta(profile, 1_000, 100, SpaMeters), 9);
        Assert.Equal(2.0, Delta(profile, 1_000, -100, SpaMeters), 9);
        Assert.Equal(14.008, Delta(profile, 4_202.4, 700.4, SpaMeters), 9);
        Assert.Equal(0d, Delta(profile, 500, 0, SpaMeters), 9);
    }

    [Fact]
    public void TimeDeltaSeconds_UsesProfiledSpeeds_RatherThanOneConstantSpeed()
    {
        // Slow first third (25 m/s), fast final third (100 m/s): the same 100 m gap
        // must cost very different amounts of time depending on where it is measured.
        var samples = new List<TelemetrySample>();
        for (var p = 0d; p < 2_000; p += 5) samples.Add(Sample(p, 25));
        for (var p = 2_000d; p < 5_000; p += 5) samples.Add(Sample(p, 50));
        for (var p = 5_000d; p < SpaMeters; p += 5) samples.Add(Sample(p, 100));
        var profile = TrackPaceProfile.Build(samples)!;

        Assert.Equal(4.0, Delta(profile, 0, 100, SpaMeters), 9);
        Assert.Equal(1.0, Delta(profile, 5_000, 100, SpaMeters), 9);
        Assert.Equal(1.0, Delta(profile, 5_100, -100, SpaMeters), 9);
        Assert.Equal(3.0, Delta(profile, 5_100, -200, SpaMeters), 9); // 100 m fast + 100 m mid
    }

    [Fact]
    public void TimeDeltaSeconds_WrapsAcrossStartFinish_OnlyWithALapLength()
    {
        // Fast zone at the start of the lap, slow zone at its end: only a wrapped walk
        // can use the correct speed for the arc that crosses start/finish.
        var samples = new List<TelemetrySample>();
        for (var p = 0d; p < 100; p += 5) samples.Add(Sample(p, 100));
        for (var p = 100d; p < 6_900; p += 20) samples.Add(Sample(p, 50));
        for (var p = 6_900d; p < SpaMeters; p += 5) samples.Add(Sample(p, 25));
        var profile = TrackPaceProfile.Build(samples)!;

        // 2 m at 100 m/s (0..2) plus 8 m at 25 m/s (6996..7004).
        Assert.Equal(0.02 + (8d / 25d), Delta(profile, 2, -10, SpaMeters), 9);

        // Without a lap length the walk cannot wrap, so it fills with mean pace instead
        // of touching the slow zone: a different (coarser), still finite answer.
        var unwrapped = profile.TimeDeltaSeconds(2, -10, lapMeters: null);
        Assert.NotNull(unwrapped);
        Assert.NotEqual(0.02 + (8d / 25d), unwrapped!.Value, 9);
    }

    [Fact]
    public void TimeDeltaSeconds_FillsUncoveredArcWithMeanRecentPace()
    {
        // Two-speed history (half the arc at 25 m/s, half at 75 m/s) covering only the
        // first kilometre: the arc beyond coverage must cost the *mean* recent pace.
        var samples = new List<TelemetrySample>();
        for (var p = 0d; p < 500; p += 5) samples.Add(Sample(p, 25));
        for (var p = 500d; p < 1_000; p += 5) samples.Add(Sample(p, 75));
        var profile = TrackPaceProfile.Build(samples)!;
        Assert.Equal(50d, profile.MeanSpeedMps, 6);

        // 900..1000 is sampled at 75 m/s; 1000..1400 was never driven → mean 50 m/s.
        var expected = (100d / 75d) + (400d / 50d);
        Assert.Equal(expected, Delta(profile, 900, 500, SpaMeters), 9);
    }

    [Theory]
    [InlineData(double.NaN, 100)]
    [InlineData(double.PositiveInfinity, 100)]
    [InlineData(1_000, double.NaN)]
    [InlineData(1_000, double.PositiveInfinity)]
    [InlineData(1_000, 999_999)] // beyond any real half lap
    public void TimeDeltaSeconds_WithImplausibleInput_ReturnsNull(double from, double delta)
    {
        var profile = TrackPaceProfile.Build(FlatHistory())!;

        Assert.Null(profile.TimeDeltaSeconds(from, delta, SpaMeters));
    }

    [Fact]
    public void TimeDeltaSeconds_WithImplausibleLapLength_ReturnsNull()
    {
        var profile = TrackPaceProfile.Build(FlatHistory())!;

        Assert.Null(profile.TimeDeltaSeconds(1_000, 100, lapMeters: 0));
        Assert.Null(profile.TimeDeltaSeconds(1_000, 100, lapMeters: -5));
        Assert.Null(profile.TimeDeltaSeconds(1_000, double.NaN, lapMeters: null));
    }

    [Fact]
    public void TimeDeltaSeconds_RejectsGapsLargerThanHalfALap()
    {
        var profile = TrackPaceProfile.Build(FlatHistory())!;

        Assert.Null(profile.TimeDeltaSeconds(0, 4_000, SpaMeters));   // > 3502 m
        Assert.NotNull(profile.TimeDeltaSeconds(0, 3_500, SpaMeters)); // <= 3502 m
    }

    [Fact]
    public void TimeDeltaSeconds_WithoutLapLength_StillWalksRawProgress()
    {
        var profile = TrackPaceProfile.Build(FlatHistory())!;

        // Projection-fallback mode: no track length available, raw coordinates.
        Assert.Equal(2.0, Delta(profile, 1_000, 100, lapMeters: null), 9);
        Assert.Equal(2.0, Delta(profile, -50, -100, lapMeters: null), 9);
    }
}
