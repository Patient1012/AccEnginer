using System.Text.Json.Serialization;

namespace AccEngineer.Telemetry;

/// <summary>World position of one car currently on track, from the graphics page.</summary>
public readonly record struct CarPosition(int CarId, float XMeters, float YMeters, float ZMeters);

/// <summary>
/// Project-owned telemetry sample, independent of the ACC reader library.
/// All numeric fields use explicit units in their names.
/// Unavailable or invalid values are represented with nullable types,
/// never silently substituted with zero.
/// A record so consumers can derive annotated copies via <c>with</c>.
/// </summary>
public sealed record TelemetrySample
{
    public long SampleId { get; init; }

    /// <summary>Utc time when the sample was received from the source.</summary>
    public DateTimeOffset ReceivedAtUtc { get; init; }

    /// <summary>Source-provided timestamp if available; otherwise null.</summary>
    public DateTimeOffset? SourceTimestampUtc { get; init; }

    /// <summary>Monotonic receipt tick (Stopwatch ticks) for ordering.</summary>
    public long ReceiptTick { get; init; }

    public string SessionId { get; init; } = string.Empty;

    public int LapNumber { get; init; }

    public bool IsLapValid { get; init; } = true;

    /// <summary>
    /// Current lap time in milliseconds as reported by ACC (<c>iCurrentTime</c>).
    /// Null when ACC reports a negative sentinel value.
    /// </summary>
    public int? CurrentLapTimeMs { get; init; }

    /// <summary>
    /// Last completed lap time in milliseconds as reported by ACC (<c>iLastTime</c>).
    /// Null when not yet set (ACC reports 0 or a negative sentinel).
    /// </summary>
    public int? LastLapTimeMs { get; init; }

    /// <summary>
    /// Best lap time in milliseconds as reported by ACC (<c>iBestTime</c>).
    /// Null when not yet set (ACC reports 0 or a negative sentinel).
    /// </summary>
    public int? BestLapTimeMs { get; init; }

    /// <summary>Current sector index (0-based) as reported by ACC (<c>currentSectorIndex</c>).</summary>
    public int? CurrentSectorIndex { get; init; }

    /// <summary>Sector count for the track, from the static page (<c>sectorCount</c>).</summary>
    public int? SectorCount { get; init; }

    /// <summary>
    /// Last completed sector time in milliseconds as reported by ACC
    /// (<c>lastSectorTime</c>). Null when not yet set (ACC reports 0 or a sentinel).
    /// </summary>
    public int? LastSectorTimeMs { get; init; }

    /// <summary>
    /// Live delta to the reference (best) lap in milliseconds, negative when ahead of
    /// the reference. Mapped from the graphics page <c>iDeltaLapTime</c> with the sign
    /// reconstructed from <c>isDeltaPositive</c>, so it is correct whether ACC stores
    /// the delta signed or as a magnitude. Null when ACC reports no active delta.
    /// </summary>
    public int? DeltaLapTimeMs { get; init; }

    /// <summary>
    /// Sign flag reported alongside the delta (graphics page <c>isDeltaPositive</c>);
    /// true when the current lap is slower than the reference at this point.
    /// </summary>
    public bool IsDeltaPositive { get; init; }

    /// <summary>Track-relative progress in metres, if provided by the source.</summary>
    public float? TrackProgressMeters { get; init; }

    public float? SpeedMetersPerSecond { get; init; }

    public float? SpeedKph { get; init; }

    /// <summary>Steering input. ACC documents this as a normalized value from -1.0 to 1.0.</summary>
    public float? SteeringInput { get; init; }

    /// <summary>Throttle pedal input, 0.0 to 1.0.</summary>
    public float? ThrottleInput { get; init; }

    /// <summary>Brake pedal input, 0.0 to 1.0.</summary>
    public float? BrakeInput { get; init; }

    /// <summary>Clutch pedal input, 0.0 to 1.0.</summary>
    public float? ClutchInput { get; init; }

    /// <summary>
    /// Gear as reported by ACC raw encoding: 0 = reverse, 1 = neutral, 2+ = gear (value - 1).
    /// </summary>
    public int? Gear { get; init; }

    public int? EngineRpm { get; init; }

    /// <summary>
    /// Lateral acceleration in G. Mapped from ACC <c>accG[0]</c> (car-fixed axes, Y-up
    /// convention). Verify sign/index against driving data before relying on it.
    /// </summary>
    public float? LateralAccelerationG { get; init; }

    /// <summary>
    /// Longitudinal acceleration in G. Mapped from ACC <c>accG[2]</c> (car-fixed axes,
    /// Y-up convention). Verify sign/index against driving data before relying on it.
    /// </summary>
    public float? LongitudinalAccelerationG { get; init; }

    /// <summary>
    /// Yaw rate in rad/s, mapped from the vertical (Y) component of ACC
    /// <c>localAngularVelocity</c> (Y-up coordinate system). Unverified mapping.
    /// </summary>
    public float? AngularVelocityYawRadPerSec { get; init; }

    public float? HeadingRad { get; init; }

    public float? PitchRad { get; init; }

    public float? RollRad { get; init; }

    public float? PositionXMeters { get; init; }

    public float? PositionYMeters { get; init; }

    public float? PositionZMeters { get; init; }

    /// <summary>Player's overall race position (1-based) as reported by ACC.</summary>
    public int? PlayerPosition { get; init; }

    /// <summary>Player's normalized position around the lap (0..1), when reported.</summary>
    public float? NormalizedCarPosition { get; init; }

    /// <summary>Number of cars currently on track, when reported.</summary>
    [JsonIgnore]
    public int? ActiveCars { get; init; }

    /// <summary>The player's car id within <see cref="CarsOnTrack"/>.</summary>
    [JsonIgnore]
    public int? PlayerCarId { get; init; }

    /// <summary>
    /// World positions of the cars currently on track. Only <c>carCoordinates</c>
    /// and <c>carID</c> describe other cars in ACC shared memory; opponent names,
    /// gaps and lap times are not exposed. Not persisted to recordings (used only
    /// for the live map).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<CarPosition>? CarsOnTrack { get; init; }

    public float[]? WheelAngularSpeedRadPerSec { get; init; }

    public float[]? WheelSlip { get; init; }

    public float[]? WheelLoad { get; init; }

    /// <summary>
    /// Raw per-wheel suspension travel in metres exactly as reported by ACC shared
    /// memory (source field <c>suspensionTravel</c>, order FL, FR, RL, RR). Larger
    /// values indicate more compression. Validate against live ACC data before using
    /// it in analysis.
    /// </summary>
    public float[]? SuspensionTravelMeters { get; init; }

    /// <summary>
    /// Raw per-wheel pressure values exactly as reported by ACC shared memory
    /// (source field <c>wheelsPressure</c>, order FL, FR, RL, RR).
    /// The official ACC shared-memory documentation does not state a unit for this
    /// channel, so no unit is asserted here. Validate against live ACC data before
    /// using these values in analysis (GT3 hot pressures are typically ~26-27 psi).
    /// </summary>
    public float[]? WheelPressure { get; init; }

    /// <summary>Tyre core temperature per wheel in degrees Celsius (FL, FR, RL, RR).</summary>
    public float[]? TyreCoreTempC { get; init; }

    public float[]? TyreTempInnerC { get; init; }

    public float[]? TyreTempMiddleC { get; init; }

    public float[]? TyreWear { get; init; }

    public float[]? BrakeTempC { get; init; }

    public float? FuelLevel { get; init; }

    /// <summary>
    /// Time remaining in the current session in seconds (graphics page
    /// <c>sessionTimeLeft</c>). Null when ACC reports no limit or a non-positive value.
    /// </summary>
    public float? SessionTimeLeftSeconds { get; init; }

    /// <summary>
    /// Total laps for the session (graphics page <c>numberOfLaps</c>). Null when the
    /// session is not lap-limited (ACC reports 0), for example a practice session.
    /// </summary>
    public int? TotalLaps { get; init; }

    /// <summary>
    /// Fuel tank capacity (static page <c>maxFuel</c>), in the same units as
    /// <see cref="FuelLevel"/>. ACC's fuel channel uses the same units as the in-game
    /// setup fuel (litres): the official documentation labels the physics <c>fuel</c>
    /// field "kg", but the numeric value matches the setup litres that fuel planning
    /// works with. Null when ACC reports a non-positive value.
    /// </summary>
    public float? TankCapacityLitres { get; init; }

    public bool? PitLimiterOn { get; init; }

    public bool? IsInPitLane { get; init; }

    /// <summary>Ambient air temperature in degrees Celsius (physics page <c>airTemp</c>).</summary>
    public float? AirTempC { get; init; }

    /// <summary>Track surface (road) temperature in degrees Celsius (physics page <c>roadTemp</c>).</summary>
    public float? TrackTempC { get; init; }

    /// <summary>
    /// Track surface grip factor (graphics page <c>surfaceGrip</c>). The scale is
    /// simulator-defined and is not asserted here; some ACC versions report a constant
    /// value, so consumers must not treat it as a precise wetness measurement.
    /// </summary>
    public float? SurfaceGrip { get; init; }

    /// <summary>Whether the player has wet-weather tyres fitted (graphics page <c>rainTyres</c>).</summary>
    public bool RainTyresOn { get; init; }

    /// <summary>Current session flag (graphics page <c>flag</c>).</summary>
    public SessionFlag Flag { get; init; } = SessionFlag.Unknown;

    public string? TrackName { get; init; }

    public string? CarName { get; init; }

    public string? TyreCompound { get; init; }

    public SessionType SessionType { get; init; } = SessionType.Unknown;

    public SessionState SessionState { get; init; } = SessionState.Unknown;

    public DataQualityFlags Quality { get; init; } = DataQualityFlags.None;
}

public enum SessionType
{
    Unknown = -1,
    Practice = 0,
    Qualify = 1,
    Race = 2,
    Hotlap = 3,
    TimeAttack = 4,
    Drift = 5,
    Drag = 6,
    HotStint = 7,
    HotStintSuperpole = 8
}

public enum SessionState
{
    Unknown = 0,
    Off = 1,
    Replay = 2,
    Live = 3,
    Pause = 4
}

/// <summary>
/// Session flag as reported by ACC (graphics page <c>flag</c>). Values follow the
/// shared-memory flag enumeration; unrecognised values map to <see cref="Unknown"/>.
/// </summary>
public enum SessionFlag
{
    Unknown = -1,
    None = 0,
    Blue = 1,
    Yellow = 2,
    Black = 3,
    White = 4,
    Checkered = 5,
    Red = 6,
    Green = 7
}

[Flags]
public enum DataQualityFlags
{
    None = 0,
    Stale = 1,
    OutOfOrder = 2,
    GapDetected = 4,
    PartialUpdate = 8,
    InvalidLap = 16,
    SessionChanged = 32,
    PitStop = 64,

    /// <summary>
    /// The sample was produced by the built-in test-data provider rather than a
    /// real simulator. Consumers must surface this clearly and never treat test
    /// data as a live session.
    /// </summary>
    TestData = 128
}
