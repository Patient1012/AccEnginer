using System.Text.Json.Serialization;
using AccEngineer.Telemetry;

namespace AccEngineer.Storage;

/// <summary>
/// Compact on-disk encoding of a <see cref="TelemetrySample"/> for schema version 2
/// recordings. Timestamps are stored as elapsed milliseconds from the recording's
/// <see cref="SessionMetadata.StartedAtUtc"/> (floating point, so sub-millisecond
/// receipt time is preserved to ~0.1 µs and round-trips losslessly). Enums and
/// data-quality flags are stored as integers instead of names, and the session
/// identifier is intentionally absent: it lives once in the header and is
/// re-injected when the recording is loaded.
/// </summary>
internal sealed record CompactSample
{
    public long SampleId { get; init; }

    /// <summary>Elapsed milliseconds from the session start time (replaces the ISO timestamp).</summary>
    public double ElapsedMs { get; init; }

    /// <summary>Elapsed milliseconds of the source timestamp, when the source provides one.</summary>
    public double? SourceElapsedMs { get; init; }

    public long ReceiptTick { get; init; }

    public int LapNumber { get; init; }

    public bool IsLapValid { get; init; } = true;

    public int? CurrentLapTimeMs { get; init; }

    public int? LastLapTimeMs { get; init; }

    public int? BestLapTimeMs { get; init; }

    public int? CurrentSectorIndex { get; init; }

    public int? SectorCount { get; init; }

    public float? TrackProgressMeters { get; init; }

    public float? SpeedMetersPerSecond { get; init; }

    public float? SpeedKph { get; init; }

    public float? SteeringInput { get; init; }

    public float? ThrottleInput { get; init; }

    public float? BrakeInput { get; init; }

    public int? Gear { get; init; }

    public int? EngineRpm { get; init; }

    public float? LateralAccelerationG { get; init; }

    public float? LongitudinalAccelerationG { get; init; }

    public float? AngularVelocityYawRadPerSec { get; init; }

    public float? HeadingRad { get; init; }

    public float? PitchRad { get; init; }

    public float? RollRad { get; init; }

    public float? PositionXMeters { get; init; }

    public float? PositionYMeters { get; init; }

    public float? PositionZMeters { get; init; }

    public int? PlayerPosition { get; init; }

    public float? NormalizedCarPosition { get; init; }

    public float[]? WheelAngularSpeedRadPerSec { get; init; }

    public float[]? WheelSlip { get; init; }

    public float[]? WheelLoad { get; init; }

    public float[]? WheelPressure { get; init; }

    public float[]? TyreCoreTempC { get; init; }

    public float[]? TyreTempInnerC { get; init; }

    public float[]? TyreTempMiddleC { get; init; }

    public float[]? TyreWear { get; init; }

    public float[]? BrakeTempC { get; init; }

    public float? FuelLevel { get; init; }

    public bool? PitLimiterOn { get; init; }

    public bool? IsInPitLane { get; init; }

    public string? TrackName { get; init; }

    public string? CarName { get; init; }

    public string? TyreCompound { get; init; }

    [JsonConverter(typeof(JsonNumberEnumConverter<SessionType>))]
    public SessionType SessionType { get; init; } = SessionType.Unknown;

    [JsonConverter(typeof(JsonNumberEnumConverter<SessionState>))]
    public SessionState SessionState { get; init; } = SessionState.Unknown;

    [JsonConverter(typeof(JsonNumberEnumConverter<DataQualityFlags>))]
    public DataQualityFlags Quality { get; init; } = DataQualityFlags.None;

    public static CompactSample From(TelemetrySample sample, DateTimeOffset startedAtUtc)
    {
        double? sourceElapsedMs = sample.SourceTimestampUtc is { } source
            ? (source - startedAtUtc).TotalMilliseconds
            : null;

        return new CompactSample
        {
            SampleId = sample.SampleId,
            ElapsedMs = (sample.ReceivedAtUtc - startedAtUtc).TotalMilliseconds,
            SourceElapsedMs = sourceElapsedMs,
            ReceiptTick = sample.ReceiptTick,
            LapNumber = sample.LapNumber,
            IsLapValid = sample.IsLapValid,
            CurrentLapTimeMs = sample.CurrentLapTimeMs,
            LastLapTimeMs = sample.LastLapTimeMs,
            BestLapTimeMs = sample.BestLapTimeMs,
            CurrentSectorIndex = sample.CurrentSectorIndex,
            SectorCount = sample.SectorCount,
            TrackProgressMeters = sample.TrackProgressMeters,
            SpeedMetersPerSecond = sample.SpeedMetersPerSecond,
            SpeedKph = sample.SpeedKph,
            SteeringInput = sample.SteeringInput,
            ThrottleInput = sample.ThrottleInput,
            BrakeInput = sample.BrakeInput,
            Gear = sample.Gear,
            EngineRpm = sample.EngineRpm,
            LateralAccelerationG = sample.LateralAccelerationG,
            LongitudinalAccelerationG = sample.LongitudinalAccelerationG,
            AngularVelocityYawRadPerSec = sample.AngularVelocityYawRadPerSec,
            HeadingRad = sample.HeadingRad,
            PitchRad = sample.PitchRad,
            RollRad = sample.RollRad,
            PositionXMeters = sample.PositionXMeters,
            PositionYMeters = sample.PositionYMeters,
            PositionZMeters = sample.PositionZMeters,
            PlayerPosition = sample.PlayerPosition,
            NormalizedCarPosition = sample.NormalizedCarPosition,
            WheelAngularSpeedRadPerSec = sample.WheelAngularSpeedRadPerSec,
            WheelSlip = sample.WheelSlip,
            WheelLoad = sample.WheelLoad,
            WheelPressure = sample.WheelPressure,
            TyreCoreTempC = sample.TyreCoreTempC,
            TyreTempInnerC = sample.TyreTempInnerC,
            TyreTempMiddleC = sample.TyreTempMiddleC,
            TyreWear = sample.TyreWear,
            BrakeTempC = sample.BrakeTempC,
            FuelLevel = sample.FuelLevel,
            PitLimiterOn = sample.PitLimiterOn,
            IsInPitLane = sample.IsInPitLane,
            TrackName = sample.TrackName,
            CarName = sample.CarName,
            TyreCompound = sample.TyreCompound,
            SessionType = sample.SessionType,
            SessionState = sample.SessionState,
            Quality = sample.Quality
        };
    }

    public TelemetrySample ToFull(SessionMetadata metadata)
    {
        var startedAtUtc = metadata.StartedAtUtc;
        return new TelemetrySample
        {
            SampleId = SampleId,
            ReceivedAtUtc = Reconstruct(startedAtUtc, ElapsedMs),
            SourceTimestampUtc = SourceElapsedMs is { } sourceElapsedMs
                ? Reconstruct(startedAtUtc, sourceElapsedMs)
                : null,
            ReceiptTick = ReceiptTick,
            SessionId = metadata.SessionId,
            LapNumber = LapNumber,
            IsLapValid = IsLapValid,
            CurrentLapTimeMs = CurrentLapTimeMs,
            LastLapTimeMs = LastLapTimeMs,
            BestLapTimeMs = BestLapTimeMs,
            CurrentSectorIndex = CurrentSectorIndex,
            SectorCount = SectorCount,
            TrackProgressMeters = TrackProgressMeters,
            SpeedMetersPerSecond = SpeedMetersPerSecond,
            SpeedKph = SpeedKph,
            SteeringInput = SteeringInput,
            ThrottleInput = ThrottleInput,
            BrakeInput = BrakeInput,
            Gear = Gear,
            EngineRpm = EngineRpm,
            LateralAccelerationG = LateralAccelerationG,
            LongitudinalAccelerationG = LongitudinalAccelerationG,
            AngularVelocityYawRadPerSec = AngularVelocityYawRadPerSec,
            HeadingRad = HeadingRad,
            PitchRad = PitchRad,
            RollRad = RollRad,
            PositionXMeters = PositionXMeters,
            PositionYMeters = PositionYMeters,
            PositionZMeters = PositionZMeters,
            PlayerPosition = PlayerPosition,
            NormalizedCarPosition = NormalizedCarPosition,
            WheelAngularSpeedRadPerSec = WheelAngularSpeedRadPerSec,
            WheelSlip = WheelSlip,
            WheelLoad = WheelLoad,
            WheelPressure = WheelPressure,
            TyreCoreTempC = TyreCoreTempC,
            TyreTempInnerC = TyreTempInnerC,
            TyreTempMiddleC = TyreTempMiddleC,
            TyreWear = TyreWear,
            BrakeTempC = BrakeTempC,
            FuelLevel = FuelLevel,
            PitLimiterOn = PitLimiterOn,
            IsInPitLane = IsInPitLane,
            TrackName = TrackName,
            CarName = CarName,
            TyreCompound = TyreCompound,
            SessionType = SessionType,
            SessionState = SessionState,
            Quality = Quality
        };
    }

    private static DateTimeOffset Reconstruct(DateTimeOffset startedAtUtc, double elapsedMs) =>
        startedAtUtc.AddTicks((long)Math.Round(elapsedMs * TimeSpan.TicksPerMillisecond));
}