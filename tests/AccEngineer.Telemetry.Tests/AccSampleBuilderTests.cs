namespace AccEngineer.Telemetry.Tests;

public class AccSampleBuilderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static TelemetrySample Build(
        AccSampleBuilder builder,
        int laps,
        float distance,
        int sessionIndex = 0,
        DateTimeOffset? at = null)
    {
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            Session = AccSessionType.Practice,
            SessionIndex = sessionIndex,
            CompletedLaps = laps,
            IsValidLap = 1,
            DistanceTraveled = distance
        };
        return builder.Build(default, graphics, default, at ?? T0, 0);
    }

    [Fact]
    public void Build_MapsPerWheelSuspensionTravel()
    {
        var builder = new AccSampleBuilder();
        var physics = new AccPhysicsPage
        {
            SuspensionTravel = new[] { 0.021f, 0.019f, 0.034f, 0.033f }
        };
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 1,
            DistanceTraveled = 10f
        };

        var sample = builder.Build(physics, graphics, default, T0, 0);

        Assert.NotNull(sample.SuspensionTravelMeters);
        Assert.Equal(new[] { 0.021f, 0.019f, 0.034f, 0.033f }, sample.SuspensionTravelMeters);
    }

    [Fact]
    public void Build_WithoutSuspensionChannel_LeavesTravelNull()
    {
        var builder = new AccSampleBuilder();

        var sample = Build(builder, laps: 0, distance: 10f);

        Assert.Null(sample.SuspensionTravelMeters);
    }

    [Fact]
    public void Build_MapsCorePhysicsGraphicsAndStaticFields()
    {
        var builder = new AccSampleBuilder();
        var physics = new AccPhysicsPage
        {
            SpeedKmh = 180f,
            Gas = 1f,
            Brake = 0.25f,
            Clutch = 0.4f,
            Gear = 4,
            Rpms = 7000,
            SteerAngle = -0.5f,
            Fuel = 42.5f,
            PitLimiterOn = 1,
            AccG = new[] { 0.1f, 0.2f, 0.3f },
            LocalAngularVelocity = new[] { 0f, 2.5f, 0f }
        };
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            Session = AccSessionType.Practice,
            SessionIndex = 3,
            CompletedLaps = 2,
            IsValidLap = 1,
            DistanceTraveled = 150.25f,
            TyreCompound = "Dry",
            ICurrentTime = 45_000,
            ILastTime = 92_123,
            IBestTime = 91_500,
            CurrentSectorIndex = 1
        };
        var statics = new AccStaticPage
        {
            CarModel = "porsche_991_cup",
            Track = "monza",
            SectorCount = 3
        };

        var sample = builder.Build(physics, graphics, statics, T0, 1234);

        Assert.Equal(1, sample.SampleId);
        Assert.Equal(180f, sample.SpeedKph);
        Assert.Equal(180f / 3.6f, sample.SpeedMetersPerSecond);
        Assert.Equal(4, sample.Gear);
        Assert.Equal(3, sample.LapNumber);
        Assert.True(sample.IsLapValid);
        Assert.Equal(45_000, sample.CurrentLapTimeMs);
        Assert.Equal(92_123, sample.LastLapTimeMs);
        Assert.Equal(91_500, sample.BestLapTimeMs);
        Assert.Equal(1, sample.CurrentSectorIndex);
        Assert.Equal(3, sample.SectorCount);
        Assert.Equal(150.25f, sample.TrackProgressMeters);
        Assert.Equal(1f, sample.ThrottleInput);
        Assert.Equal(0.25f, sample.BrakeInput);
        Assert.Equal(0.4f, sample.ClutchInput);
        Assert.Equal(-0.5f, sample.SteeringInput);
        Assert.Equal(42.5f, sample.FuelLevel);
        Assert.Equal(7000, sample.EngineRpm);
        Assert.Equal(0.1f, sample.LateralAccelerationG);
        Assert.Equal(0.3f, sample.LongitudinalAccelerationG);
        Assert.Equal(2.5f, sample.AngularVelocityYawRadPerSec);
        Assert.True(sample.PitLimiterOn);
        Assert.Equal(SessionType.Practice, sample.SessionType);
        Assert.Equal(SessionState.Live, sample.SessionState);
        Assert.Equal("Dry", sample.TyreCompound);
        Assert.Equal("monza", sample.TrackName);
        Assert.Equal("porsche_991_cup", sample.CarName);
        Assert.Equal(DataQualityFlags.None, sample.Quality);
        Assert.Equal(1234, sample.ReceiptTick);
        Assert.Equal(T0, sample.ReceivedAtUtc);
        Assert.Null(sample.SourceTimestampUtc);
    }

    [Fact]
    public void Build_SameSession_KeepsSessionIdAndIncrementsSampleIds()
    {
        var builder = new AccSampleBuilder();

        var first = Build(builder, laps: 0, distance: 100f);
        var second = Build(builder, laps: 0, distance: 200f);

        Assert.Equal(1, first.SampleId);
        Assert.Equal(2, second.SampleId);
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.StartsWith("sm-", first.SessionId, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_SessionIndexChange_FlagsSessionChangedAndRotatesSessionId()
    {
        var builder = new AccSampleBuilder();
        var first = Build(builder, laps: 5, distance: 100f, sessionIndex: 0);
        var second = Build(builder, laps: 0, distance: 50f, sessionIndex: 1);

        Assert.True(second.Quality.HasFlag(DataQualityFlags.SessionChanged));
        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal(1, second.LapNumber);
    }

    [Fact]
    public void Build_LapCountDecrease_TreatedAsSessionChange()
    {
        var builder = new AccSampleBuilder();
        Build(builder, laps: 5, distance: 100f, sessionIndex: 0);
        var second = Build(builder, laps: 0, distance: 100f, sessionIndex: 0);

        Assert.True(second.Quality.HasFlag(DataQualityFlags.SessionChanged));
        Assert.Equal(1, second.LapNumber);
    }

    [Fact]
    public void Build_TrackProgress_AccumulateSemantics_RebasesAtLapBoundary()
    {
        var builder = new AccSampleBuilder();

        Assert.Equal(1000f, Build(builder, laps: 0, distance: 1000f).TrackProgressMeters);
        Assert.Equal(2000f, Build(builder, laps: 0, distance: 2000f).TrackProgressMeters);
        // Lap completed: distance kept growing, so the previous distance becomes the baseline.
        Assert.Equal(50f, Build(builder, laps: 1, distance: 2050f).TrackProgressMeters);
        Assert.Equal(500f, Build(builder, laps: 1, distance: 2500f).TrackProgressMeters);
    }

    [Fact]
    public void Build_TrackProgress_ResetSemantics_WrapsToZero()
    {
        var builder = new AccSampleBuilder();

        Assert.Equal(5000f, Build(builder, laps: 0, distance: 5000f).TrackProgressMeters);
        // Distance wrapped back near zero at the lap boundary: baseline stays 0.
        Assert.Equal(50f, Build(builder, laps: 1, distance: 50f).TrackProgressMeters);
        Assert.Equal(60f, Build(builder, laps: 1, distance: 60f).TrackProgressMeters);
    }

    [Fact]
    public void Build_TrackProgress_BackwardsJumpWithoutLapChange_ResetsProgress()
    {
        var builder = new AccSampleBuilder();

        Assert.Equal(3000f, Build(builder, laps: 0, distance: 3000f).TrackProgressMeters);
        // Respawn/teleport: same lap, large backwards jump.
        Assert.Equal(100f, Build(builder, laps: 0, distance: 100f).TrackProgressMeters);
    }

    [Fact]
    public void Build_LiveSessionWithInvalidLap_SetsInvalidLapFlag()
    {
        var builder = new AccSampleBuilder();
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 0,
            DistanceTraveled = 10f
        };

        var sample = builder.Build(default, graphics, default, T0, 0);

        Assert.False(sample.IsLapValid);
        Assert.True(sample.Quality.HasFlag(DataQualityFlags.InvalidLap));
    }

    [Fact]
    public void Build_InPitLane_SetsPitStopFlagAndInPitLane()
    {
        var builder = new AccSampleBuilder();
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 1,
            DistanceTraveled = 10f,
            IsInPitLane = 1
        };

        var sample = builder.Build(default, graphics, default, T0, 0);

        Assert.True(sample.IsInPitLane);
        Assert.True(sample.Quality.HasFlag(DataQualityFlags.PitStop));
    }

    [Fact]
    public void Build_WithoutPlayerCarInGraphicsArray_ReturnsNullPosition()
    {
        var builder = new AccSampleBuilder();
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 1,
            DistanceTraveled = 10f,
            CarId = new[] { 7, 8 },
            CarCoordinates = new float[180],
            PlayerCarId = 99
        };

        var sample = builder.Build(default, graphics, default, T0, 0);

        Assert.Null(sample.PositionXMeters);
        Assert.Null(sample.PositionYMeters);
        Assert.Null(sample.PositionZMeters);
    }

    [Fact]
    public void Build_NegativeLapTimingSentinels_MapToNull()
    {
        var builder = new AccSampleBuilder();
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 1,
            DistanceTraveled = 0f,
            ICurrentTime = -1,
            ILastTime = 0,
            IBestTime = -1,
            CurrentSectorIndex = -1
        };
        var statics = new AccStaticPage { SectorCount = 0 };

        var sample = builder.Build(default, graphics, statics, T0, 0);

        Assert.Null(sample.CurrentLapTimeMs);
        Assert.Null(sample.LastLapTimeMs);
        Assert.Null(sample.BestLapTimeMs);
        Assert.Null(sample.CurrentSectorIndex);
        Assert.Null(sample.SectorCount);
    }

    [Fact]
    public void Build_PlayerCarPosition_MappedFromCarArray()
    {
        var builder = new AccSampleBuilder();
        var coords = new float[180];
        coords[3 + 0] = 111.5f; // second car, X
        coords[3 + 1] = 222.5f; // second car, Y
        coords[3 + 2] = 333.5f; // second car, Z
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 1,
            DistanceTraveled = 10f,
            CarId = new[] { 7, 42 },
            CarCoordinates = coords,
            PlayerCarId = 42
        };

        var sample = builder.Build(default, graphics, default, T0, 0);

        Assert.Equal(111.5f, sample.PositionXMeters);
        Assert.Equal(222.5f, sample.PositionYMeters);
        Assert.Equal(333.5f, sample.PositionZMeters);
    }

    [Fact]
    public void Build_CarsOnTrack_SkipsUnpopulatedSlots()
    {
        var builder = new AccSampleBuilder();
        var coords = new float[180];
        coords[0] = 1f;  // car 0, X
        coords[2] = 2f;  // car 0, Z (Y left at 0)
        // car 1 left at the origin (empty slot)
        coords[6] = 3f;  // car 2, X
        coords[8] = 4f;  // car 2, Z

        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 1,
            DistanceTraveled = 10f,
            CarId = new[] { 7, 8, 9 },
            CarCoordinates = coords,
            PlayerCarId = 42,
            ActiveCars = 3
        };

        var sample = builder.Build(default, graphics, default, T0, 0);

        Assert.NotNull(sample.CarsOnTrack);
        Assert.Equal(2, sample.CarsOnTrack!.Count);
        Assert.Equal(new CarPosition(7, 1f, 0f, 2f), sample.CarsOnTrack[0]);
        Assert.Equal(new CarPosition(9, 3f, 0f, 4f), sample.CarsOnTrack[1]);
    }

    [Fact]
    public void Build_CarsOnTrack_IsNullWhenGraphicsLacksArrays()
    {
        var builder = new AccSampleBuilder();
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 1,
            DistanceTraveled = 10f
        };

        var sample = builder.Build(default, graphics, default, T0, 0);

        Assert.Null(sample.CarsOnTrack);
    }

    [Fact]
    public void Build_MapsConditionAndFlagFields()
    {
        var builder = new AccSampleBuilder();
        var physics = new AccPhysicsPage { AirTemp = 24.5f, RoadTemp = 31.2f };
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 1,
            DistanceTraveled = 10f,
            SurfaceGrip = 0.97f,
            RainTyres = 1,
            Flag = 2 // yellow
        };

        var sample = builder.Build(physics, graphics, default, T0, 0);

        Assert.Equal(24.5f, sample.AirTempC);
        Assert.Equal(31.2f, sample.TrackTempC);
        Assert.Equal(0.97f, sample.SurfaceGrip);
        Assert.True(sample.RainTyresOn);
        Assert.Equal(SessionFlag.Yellow, sample.Flag);
    }

    [Theory]
    [InlineData(0, SessionFlag.None)]
    [InlineData(1, SessionFlag.Blue)]
    [InlineData(6, SessionFlag.Red)]
    [InlineData(7, SessionFlag.Green)]
    [InlineData(99, SessionFlag.Unknown)]
    public void Build_FlagValue_MapsToEnum(int raw, SessionFlag expected)
    {
        var builder = new AccSampleBuilder();
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 1,
            DistanceTraveled = 10f,
            Flag = raw
        };

        var sample = builder.Build(default, graphics, default, T0, 0);

        Assert.Equal(expected, sample.Flag);
    }

    [Fact]
    public void Build_PlayerPositionAndActiveCars_Mapped()
    {
        var builder = new AccSampleBuilder();
        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 1,
            DistanceTraveled = 10f,
            Position = 5,
            ActiveCars = 12,
            NormalizedCarPosition = 0.42f
        };

        var sample = builder.Build(default, graphics, default, T0, 0);

        Assert.Equal(5, sample.PlayerPosition);
        Assert.Equal(12, sample.ActiveCars);
        Assert.Equal(0.42f, sample.NormalizedCarPosition);
    }

    [Fact]
    public void Build_ZeroActiveCars_MapsToNullAndReadsAllSlots()
    {
        var builder = new AccSampleBuilder();
        var coords = new float[6];
        coords[0] = 5f;
        coords[2] = 6f;

        var graphics = new AccGraphicsPage
        {
            Status = AccStatus.Live,
            SessionIndex = 0,
            CompletedLaps = 0,
            IsValidLap = 1,
            DistanceTraveled = 10f,
            CarId = new[] { 3, 4 },
            CarCoordinates = coords,
            PlayerCarId = 3,
            ActiveCars = 0
        };

        var sample = builder.Build(default, graphics, default, T0, 0);

        Assert.Null(sample.ActiveCars);
        Assert.NotNull(sample.CarsOnTrack);
        Assert.Single(sample.CarsOnTrack!);
        Assert.Equal(new CarPosition(3, 5f, 0f, 6f), sample.CarsOnTrack![0]);
    }
}
