namespace AccEngineer.Telemetry.Tests;

/// <summary>Manual clock so reader emission can be driven deterministically in tests.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public ManualTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now += delta;
}

/// <summary>
/// Tests for the built-in test-data provider. It must be deterministic, emit only
/// when the update interval has elapsed, carry the TestData quality flag, and
/// produce channel values that stay physically coherent.
/// </summary>
public class SimulatedTelemetryReaderTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    private static SimulatedTelemetryOptions Options(double lapMeters = 4300) => new()
    {
        UpdateInterval = Interval,
        Seed = 20240607,
        LapLengthMeters = lapMeters
    };

    [Fact]
    public void Reader_ImplementsInterfaceContracts()
    {
        using ITrackTelemetryReader reader = new SimulatedTelemetryReader(Options());
        using ISimulatedTelemetryReader simulated = new SimulatedTelemetryReader(Options());

        Assert.NotNull(reader);
        Assert.NotNull(simulated);
    }

    [Fact]
    public void FirstRead_ReturnsSample_FlaggedAsTestData()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        using var reader = new SimulatedTelemetryReader(Options(), time);

        var sample = reader.ReadLatest();

        Assert.NotNull(sample);
        Assert.True(sample!.Quality.HasFlag(DataQualityFlags.TestData));
        Assert.False(string.IsNullOrWhiteSpace(sample.SessionId));
        Assert.True(reader.IsConnected);
    }

    [Fact]
    public void ReadWithinUpdateInterval_ReturnsNull()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        using var reader = new SimulatedTelemetryReader(Options(), time);

        Assert.NotNull(reader.ReadLatest());
        time.Advance(Interval - TimeSpan.FromMilliseconds(10));

        Assert.Null(reader.ReadLatest());
    }

    [Fact]
    public void ReadAfterUpdateInterval_AdvancesSample()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        using var reader = new SimulatedTelemetryReader(Options(), time);

        var first = reader.ReadLatest()!;
        time.Advance(Interval);
        var second = reader.ReadLatest();

        Assert.NotNull(second);
        Assert.True(second!.SampleId > first.SampleId);
        Assert.True(second.ReceivedAtUtc > first.ReceivedAtUtc);
    }

    [Fact]
    public void Dispose_StopsEmissionAndConnection()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var reader = new SimulatedTelemetryReader(Options(), time);

        reader.Dispose();

        Assert.False(reader.IsConnected);
        Assert.Null(reader.ReadLatest());
    }

    [Fact]
    public void SameSeed_ProducesIdenticalScalarSequence()
    {
        var start = DateTimeOffset.UnixEpoch;
        var timeA = new ManualTimeProvider(start);
        var timeB = new ManualTimeProvider(start);
        using var a = new SimulatedTelemetryReader(Options(), timeA);
        using var b = new SimulatedTelemetryReader(Options(), timeB);

        for (var i = 0; i < 200; i++)
        {
            var sa = a.ReadLatest();
            var sb = b.ReadLatest();
            Assert.NotNull(sa);
            Assert.NotNull(sb);
            Assert.Equal(sa!.SampleId, sb!.SampleId);
            Assert.Equal(sa.SpeedKph, sb.SpeedKph);
            Assert.Equal(sa.EngineRpm, sb.EngineRpm);
            Assert.Equal(sa.Gear, sb.Gear);
            Assert.Equal(sa.ThrottleInput, sb.ThrottleInput);
            Assert.Equal(sa.BrakeInput, sb.BrakeInput);
            Assert.Equal(sa.LapNumber, sb.LapNumber);
            timeA.Advance(Interval);
            timeB.Advance(Interval);
        }
    }

    [Fact]
    public void GeneratedSamples_ArePhysicallyCoherent()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        using var reader = new SimulatedTelemetryReader(Options(lapMeters: 2000), time);

        var throttleHigh = false;
        var brakeUsed = false;
        double previousFuel = double.MaxValue;
        int? lastCompletedLap = null;
        var maxLapNumber = 0;

        for (var i = 0; i < 8000; i++)
        {
            time.Advance(Interval);
            var s = reader.ReadLatest();
            Assert.NotNull(s);

            Assert.True(s!.Quality.HasFlag(DataQualityFlags.TestData));
            Assert.InRange(s.SpeedKph ?? -1, 0, 280);
            Assert.InRange(s.EngineRpm ?? -1, 2500, 8000);
            Assert.InRange(s.Gear ?? -1, 2, 9);
            Assert.InRange(s.ThrottleInput ?? -1, 0, 1);
            Assert.InRange(s.BrakeInput ?? -1, 0, 1);
            Assert.InRange(s.SteeringInput ?? -2, -1, 1);

            // Coherent inputs: not on the throttle and brake hard at the same time.
            Assert.False(s.ThrottleInput > 0.05 && s.BrakeInput! > 0.05);

            if (s.ThrottleInput > 0.9) throttleHigh = true;
            if (s.BrakeInput > 0.3) brakeUsed = true;

            // Fuel only ever burns down.
            Assert.True(s.FuelLevel <= previousFuel);
            previousFuel = s.FuelLevel ?? previousFuel;

            if (s.LapNumber > maxLapNumber) maxLapNumber = s.LapNumber;
            if (s.LastLapTimeMs is { } last && s.BestLapTimeMs is { } best)
            {
                Assert.True(best <= last);
                lastCompletedLap = s.LapNumber;
            }
        }

        Assert.True(throttleHigh, "expected some full-throttle samples");
        Assert.True(brakeUsed, "expected braking into corners");
        Assert.True(maxLapNumber > 1, "expected at least one completed lap");
        Assert.NotNull(lastCompletedLap);
    }

    [Fact]
    public void GeneratedMap_PositionsTraceAClosedLoop()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        using var reader = new SimulatedTelemetryReader(Options(), time);

        var sample = reader.ReadLatest();
        Assert.NotNull(sample);
        Assert.NotNull(sample!.CarsOnTrack);
        Assert.True(sample.CarsOnTrack!.Count > 1);
        Assert.Equal(1, sample.PlayerCarId);
        Assert.NotNull(sample.PositionXMeters);
        Assert.NotNull(sample.PositionZMeters);
    }

    [Fact]
    public void GeneratedSamples_BurnAStableAmountOfFuelPerLap()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        using var reader = new SimulatedTelemetryReader(new SimulatedTelemetryOptions
        {
            UpdateInterval = Interval,
            Seed = 20240607,
            LapLengthMeters = 2000
        }, time);

        var fuelAtLapStart = new Dictionary<int, float>();
        for (var i = 0; i < 6000; i++)
        {
            time.Advance(Interval);
            var sample = reader.ReadLatest();
            Assert.NotNull(sample);
            var s = sample!;

            if (s.FuelLevel is { } fuel && !fuelAtLapStart.ContainsKey(s.LapNumber))
            {
                fuelAtLapStart[s.LapNumber] = fuel;
            }
        }

        var burns = new List<double>();
        foreach (var lap in fuelAtLapStart.Keys.OrderBy(k => k))
        {
            if (fuelAtLapStart.ContainsKey(lap + 1))
            {
                burns.Add(fuelAtLapStart[lap] - fuelAtLapStart[lap + 1]);
            }
        }

        Assert.True(burns.Count >= 2, $"expected at least two measured laps, got {burns.Count}");
        foreach (var burn in burns)
        {
            Assert.InRange(burn, 2.5, 2.9);
        }
    }

    [Fact]
    public void GeneratedSamples_ReportSessionLengthAndTankCapacity()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        using var reader = new SimulatedTelemetryReader(new SimulatedTelemetryOptions
        {
            UpdateInterval = Interval,
            Seed = 20240607,
            SessionTimeLimitSeconds = 600,
            TotalLaps = 40,
            TankCapacityLitres = 120
        }, time);

        var firstSample = reader.ReadLatest();
        Assert.NotNull(firstSample);
        var first = firstSample!;
        Assert.Equal(40, first.TotalLaps);
        Assert.Equal(120f, first.TankCapacityLitres);
        // The first sample is emitted after one update interval has elapsed.
        Assert.InRange(first.SessionTimeLeftSeconds ?? -1f, 599.9f, 600f);

        TelemetrySample? last = null;
        for (var i = 0; i < 20; i++)
        {
            time.Advance(Interval);
            last = reader.ReadLatest();
            Assert.NotNull(last);
            Assert.InRange(last!.SessionTimeLeftSeconds ?? -1f, 0f, 600f);
        }

        Assert.NotNull(last);
        Assert.True(
            first.SessionTimeLeftSeconds > last!.SessionTimeLeftSeconds,
            "expected the session time left to count down");
    }

    [Fact]
    public void GeneratedSamples_WithoutLapLimitOrTimeLimit_LeaveSessionLengthNull()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        using var reader = new SimulatedTelemetryReader(new SimulatedTelemetryOptions
        {
            UpdateInterval = Interval,
            Seed = 20240607,
            SessionTimeLimitSeconds = 0,
            TotalLaps = 0,
            TankCapacityLitres = 0
        }, time);

        var sample = reader.ReadLatest();
        Assert.NotNull(sample);

        Assert.Null(sample!.TotalLaps);
        Assert.Null(sample.SessionTimeLeftSeconds);
        Assert.Null(sample.TankCapacityLitres);
    }
}
