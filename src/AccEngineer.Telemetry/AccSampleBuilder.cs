namespace AccEngineer.Telemetry;

/// <summary>
/// Stateful mapper from raw ACC shared-memory pages to project-owned
/// <see cref="TelemetrySample"/> instances. Owns per-session state: session
/// identity, sample numbering, and lap-relative track progress.
///
/// The builder is deliberately decoupled from the memory-mapped reader so the
/// mapping logic can be unit-tested with synthetic page data and no ACC process.
/// Not thread-safe; call from a single reader thread.
/// </summary>
internal sealed class AccSampleBuilder
{
    /// <summary>
    /// Drop in track progress larger than this without a lap-number change is
    /// treated as a position reset (respawn, teleport) rather than normal motion.
    /// </summary>
    private const float DistanceBackwardsResetMeters = 5f;

    private long _sampleId;
    private bool _hasSession;
    private string? _sessionId;
    private int _lastSessionIndex = int.MinValue;
    private int _lastCompletedLaps = -1;
    private float _lastDistance;
    private float _lapDistanceBaseline;
    private string? _lastCar;
    private string? _lastTrack;

    public TelemetrySample Build(
        AccPhysicsPage physics,
        AccGraphicsPage graphics,
        AccStaticPage statics,
        DateTimeOffset receivedAtUtc,
        long receiptTick)
    {
        var flags = DataQualityFlags.None;

        var sessionChanged = DetectSessionChange(graphics, statics);
        if (sessionChanged)
        {
            flags |= DataQualityFlags.SessionChanged;
            ResetLapState(graphics);
        }

        var trackProgress = ComputeTrackProgress(graphics);

        if (graphics.IsInPitLane != 0 || graphics.IsInPit != 0)
        {
            flags |= DataQualityFlags.PitStop;
        }

        if (graphics.Status == AccStatus.Live && graphics.IsValidLap == 0)
        {
            flags |= DataQualityFlags.InvalidLap;
        }

        return new TelemetrySample
        {
            SampleId = ++_sampleId,
            ReceivedAtUtc = receivedAtUtc,
            SourceTimestampUtc = null, // ACC shared memory exposes no source wall-clock
            ReceiptTick = receiptTick,
            SessionId = _sessionId ?? "unknown",
            LapNumber = graphics.CompletedLaps + 1,
            IsLapValid = graphics.IsValidLap != 0,
            CurrentLapTimeMs = NonNegative(graphics.ICurrentTime),
            LastLapTimeMs = Positive(graphics.ILastTime),
            BestLapTimeMs = Positive(graphics.IBestTime),
            CurrentSectorIndex = NonNegative(graphics.CurrentSectorIndex),
            SectorCount = Positive(statics.SectorCount),
            TrackProgressMeters = trackProgress,
            SpeedKph = physics.SpeedKmh,
            SpeedMetersPerSecond = physics.SpeedKmh / 3.6f,
            SteeringInput = physics.SteerAngle,
            ThrottleInput = physics.Gas,
            BrakeInput = physics.Brake,
            Gear = physics.Gear,
            EngineRpm = physics.Rpms,
            LateralAccelerationG = At(physics.AccG, 0),
            LongitudinalAccelerationG = At(physics.AccG, 2),
            AngularVelocityYawRadPerSec = At(physics.LocalAngularVelocity, 1),
            HeadingRad = physics.Heading,
            PitchRad = physics.Pitch,
            RollRad = physics.Roll,
            PositionXMeters = CarAxis(graphics, 0),
            PositionYMeters = CarAxis(graphics, 1),
            PositionZMeters = CarAxis(graphics, 2),
            WheelAngularSpeedRadPerSec = physics.WheelAngularSpeed,
            WheelSlip = physics.WheelSlip,
            WheelLoad = physics.WheelLoad,
            WheelPressure = physics.WheelsPressure,
            TyreCoreTempC = physics.TyreCoreTemperature,
            TyreTempInnerC = physics.TyreTempI,
            TyreTempMiddleC = physics.TyreTempM,
            TyreWear = physics.TyreWear,
            BrakeTempC = physics.BrakeTemp,
            FuelLevel = physics.Fuel,
            PitLimiterOn = physics.PitLimiterOn != 0,
            IsInPitLane = graphics.IsInPitLane != 0,
            PlayerPosition = graphics.Position > 0 ? graphics.Position : null,
            NormalizedCarPosition = graphics.NormalizedCarPosition,
            ActiveCars = graphics.ActiveCars > 0 ? graphics.ActiveCars : null,
            PlayerCarId = graphics.PlayerCarId,
            CarsOnTrack = BuildCarsOnTrack(graphics),
            TrackName = statics.Track,
            CarName = statics.CarModel,
            TyreCompound = graphics.TyreCompound,
            SessionType = MapSessionType(graphics.Session),
            SessionState = MapSessionState(graphics.Status),
            Quality = flags
        };
    }

    private bool DetectSessionChange(AccGraphicsPage graphics, AccStaticPage statics)
    {
        if (!_hasSession)
        {
            _hasSession = true;
            _sessionId = NewSessionId(graphics.SessionIndex);
            _lastSessionIndex = graphics.SessionIndex;
            _lastCar = statics.CarModel;
            _lastTrack = statics.Track;
            return false;
        }

        var changed =
            graphics.SessionIndex != _lastSessionIndex ||
            graphics.CompletedLaps < _lastCompletedLaps ||
            !string.Equals(statics.CarModel, _lastCar, StringComparison.Ordinal) ||
            !string.Equals(statics.Track, _lastTrack, StringComparison.Ordinal);

        if (changed)
        {
            _sessionId = NewSessionId(graphics.SessionIndex);
            _lastSessionIndex = graphics.SessionIndex;
            _lastCar = statics.CarModel;
            _lastTrack = statics.Track;
        }

        return changed;
    }

    private void ResetLapState(AccGraphicsPage graphics)
    {
        _lastCompletedLaps = graphics.CompletedLaps;
        _lastDistance = graphics.DistanceTraveled;
        _lapDistanceBaseline = 0f;
    }

    /// <summary>
    /// Lap-relative progress in metres. Handles both possible ACC semantics for
    /// <c>distanceTraveled</c> (reset each lap, or accumulate for the session):
    /// at a lap boundary the baseline becomes the previous distance when the value
    /// kept growing (accumulate) and stays zero when it wrapped (reset).
    /// On the first sample of a session the baseline is zero, so if ACC accumulates
    /// distance the first partially-recorded lap reports progress from session
    /// start; it self-corrects at the next lap boundary.
    /// </summary>
    private float ComputeTrackProgress(AccGraphicsPage graphics)
    {
        var distance = graphics.DistanceTraveled;

        if (!_hasSession || _lastCompletedLaps < 0)
        {
            _lastCompletedLaps = graphics.CompletedLaps;
            _lastDistance = distance;
            _lapDistanceBaseline = 0f;
            return Math.Max(0f, distance);
        }

        if (graphics.CompletedLaps != _lastCompletedLaps)
        {
            _lapDistanceBaseline = distance >= _lastDistance ? _lastDistance : 0f;
            _lastCompletedLaps = graphics.CompletedLaps;
        }
        else if (distance + DistanceBackwardsResetMeters < _lastDistance)
        {
            // Went backwards without a lap change: position reset, restart progress.
            _lapDistanceBaseline = 0f;
        }

        _lastDistance = distance;
        return Math.Max(0f, distance - _lapDistanceBaseline);
    }

    private static float? At(float[]? values, int index) =>
        values != null && index >= 0 && index < values.Length ? values[index] : null;

    /// <summary>Maps an ACC sentinel-friendly value to itself when non-negative, otherwise null.</summary>
    private static int? NonNegative(int value) => value >= 0 ? value : null;

    /// <summary>Maps a value that is only meaningful when positive (times, counts), otherwise null.</summary>
    private static int? Positive(int value) => value > 0 ? value : null;

    /// <summary>World position of the player car from the graphics car array.</summary>
    private static float? CarAxis(AccGraphicsPage graphics, int axis)
    {
        var coords = graphics.CarCoordinates;
        var ids = graphics.CarId;
        if (coords == null || ids == null) return null;

        var index = Array.IndexOf(ids, graphics.PlayerCarId);
        if (index < 0) return null;

        var offset = index * 3 + axis;
        return offset < coords.Length ? coords[offset] : null;
    }

    /// <summary>
    /// Reads the multi-car world coordinates from the graphics page. Only cars
    /// with a non-zero coordinate are returned; slots the game has not populated
    /// are skipped rather than reported as real cars at the origin.
    /// </summary>
    private static IReadOnlyList<CarPosition>? BuildCarsOnTrack(AccGraphicsPage graphics)
    {
        var coords = graphics.CarCoordinates;
        var ids = graphics.CarId;
        if (coords is null || ids is null) return null;

        var slots = Math.Min(coords.Length / 3, ids.Length);
        var limit = graphics.ActiveCars > 0 ? Math.Min(graphics.ActiveCars, slots) : slots;

        List<CarPosition>? cars = null;
        for (var i = 0; i < limit; i++)
        {
            var x = coords[i * 3];
            var y = coords[i * 3 + 1];
            var z = coords[i * 3 + 2];
            if (x == 0f && y == 0f && z == 0f) continue;

            (cars ??= new List<CarPosition>()).Add(new CarPosition(ids[i], x, y, z));
        }

        return cars;
    }

    private static string NewSessionId(int sessionIndex) =>
        $"sm-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{sessionIndex}";

    private static SessionType MapSessionType(AccSessionType session) => session switch
    {
        AccSessionType.Practice => SessionType.Practice,
        AccSessionType.Qualify => SessionType.Qualify,
        AccSessionType.Race => SessionType.Race,
        AccSessionType.Hotlap => SessionType.Hotlap,
        AccSessionType.TimeAttack => SessionType.TimeAttack,
        AccSessionType.Drift => SessionType.Drift,
        AccSessionType.Drag => SessionType.Drag,
        AccSessionType.HotStint => SessionType.HotStint,
        AccSessionType.HotStintSuperpole => SessionType.HotStintSuperpole,
        _ => SessionType.Unknown
    };

    private static SessionState MapSessionState(AccStatus status) => status switch
    {
        AccStatus.Off => SessionState.Off,
        AccStatus.Replay => SessionState.Replay,
        AccStatus.Live => SessionState.Live,
        AccStatus.Pause => SessionState.Pause,
        _ => SessionState.Unknown
    };
}
