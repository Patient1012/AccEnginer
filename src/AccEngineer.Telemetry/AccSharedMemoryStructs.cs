using System.Runtime.InteropServices;

namespace AccEngineer.Telemetry;

// ACC shared-memory page layouts. These are ACC-specific interop types and must stay
// inside the Telemetry project; the rest of the solution works with TelemetrySample.
//
// Layouts were cross-checked against three independent community implementations:
//   - MorGuux/AssettoCorsaCompetizioneSharedMemory (MIT, ACC 1.8.x+)
//   - rsys-dev/accsharedmemory (MIT)
//   - dabde/acc_shared_mem_access_python (ctypes structs)
// All three agree field-for-field on every offset used by this project.
//
// The structures are read as a prefix of the real shared-memory region, so fields
// appended by newer ACC versions are simply not seen. Where the sources disagree
// (see AccStaticPage), the affected fields are marked and are not mapped into
// TelemetrySample until verified against a live ACC session.

internal enum AccStatus
{
    Off = 0,
    Replay = 1,
    Live = 2,
    Pause = 3
}

internal enum AccSessionType
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

[StructLayout(LayoutKind.Sequential)]
internal struct AccCoordinates
{
    public float X;
    public float Y;
    public float Z;
}

/// <summary>Raw SPageFilePhysics layout (updated at physics rate).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
internal struct AccPhysicsPage
{
    public int PacketId;
    public float Gas;
    public float Brake;
    public float Fuel; // kg
    public int Gear;   // 0 = reverse, 1 = neutral, 2+ = gear + 1
    public int Rpms;
    public float SteerAngle;  // normalized -1.0 .. 1.0 per ACC documentation
    public float SpeedKmh;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] Velocity;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] AccG;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelSlip;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelLoad; // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelsPressure; // unit not stated in official docs

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelAngularSpeed; // rad/s

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreWear; // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreDirtyLevel; // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreCoreTemperature;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] CamberRad; // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SuspensionTravel;

    public float Drs; // not used in ACC
    public float TC;
    public float Heading;
    public float Pitch;
    public float Roll;
    public float CgHeight; // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
    public float[] CarDamage;

    public int NumberOfTyresOut; // not used in ACC
    public int PitLimiterOn;
    public float Abs;
    public float KersCharge; // not used in ACC
    public float KersInput;  // not used in ACC
    public int AutoShifterOn;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public float[] RideHeight; // not used in ACC

    public float TurboBoost;
    public float Ballast;    // not used in ACC
    public float AirDensity; // not used in ACC
    public float AirTemp;
    public float RoadTemp;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] LocalAngularVelocity;

    public float FinalFF;
    public float PerformanceMeter; // not used in ACC
    public int EngineBrake;        // not used in ACC
    public int ErsRecoveryLevel;   // not used in ACC
    public int ErsPowerLevel;      // not used in ACC
    public int ErsHeatCharging;    // not used in ACC
    public int ErsIsCharging;      // not used in ACC
    public float KersCurrentKj;    // not used in ACC
    public int DrsAvailable;       // not used in ACC
    public int DrsEnabled;         // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] BrakeTemp;

    public float Clutch;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempI;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempM;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempO;

    public int IsAiControlled;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public AccCoordinates[] TyreContactPoint;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public AccCoordinates[] TyreContactNormal;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public AccCoordinates[] TyreContactHeading;

    public float BrakeBias;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] LocalVelocity;

    public int P2pActivation; // not used in ACC
    public int P2pStatus;     // not used in ACC
    public float CurrentMaxRpm;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] Mz; // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] Fx; // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] Fy; // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SlipRatio;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SlipAngle;

    public int TcInAction;  // not used in ACC
    public int AbsInAction; // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SuspensionDamage; // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTemp; // not used in ACC

    public float WaterTemp;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] BrakePressure;

    public int FrontBrakeCompound;
    public int RearBrakeCompound;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] PadLife;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] DiscLife;

    public int IgnitionOn;
    public int StarterEngineOn;
    public int IsEngineRunning;
    public float KerbVibration;
    public float SlipVibration;
    public float GVibration;
    public float AbsVibration;
}

/// <summary>
/// Raw SPageFileGraphic layout (updated at graphics rate).
/// Truncated after <see cref="MissingMandatoryPits"/>: all three source
/// implementations agree on every offset up to that point and diverge afterwards,
/// and this project does not use the later fields.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
internal struct AccGraphicsPage
{
    public int PacketId;
    public AccStatus Status;
    public AccSessionType Session;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string CurrentTime;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string LastTime;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string BestTime;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string Split;

    public int CompletedLaps;
    public int Position;
    public int ICurrentTime; // current lap time, ms
    public int ILastTime;    // last lap time, ms
    public int IBestTime;    // best lap time, ms
    public float SessionTimeLeft;
    public float DistanceTraveled;
    public int IsInPit;
    public int CurrentSectorIndex;
    public int LastSectorTime;
    public int NumberOfLaps;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string TyreCompound;

    public float ReplayTimeMultiplier; // not used in ACC
    public float NormalizedCarPosition;
    public int ActiveCars;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 180)]
    public float[] CarCoordinates;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 60)]
    public int[] CarId;

    public int PlayerCarId;
    public float PenaltyTime;
    public int Flag;
    public int Penalty;
    public int IdealLineOn;
    public int IsInPitLane;
    public float SurfaceGrip;
    public int MandatoryPitDone;
    public float WindSpeed;
    public float WindDirection;
    public int IsSetupMenuVisible;
    public int MainDisplayIndex;
    public int SecondaryDisplayIndex;
    public int TC;
    public int Tcut;
    public int EngineMap;
    public int Abs;
    public float FuelXLap;
    public int RainLights;
    public int FlashingLights;
    public int LightsStage;
    public float ExhaustTemperature;
    public int WiperLv;
    public int DriverStintTotalTimeLeft;
    public int DriverStintTimeLeft;
    public int RainTyres;
    public int SessionIndex;
    public float UsedFuel;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string DeltaLapTime;

    public int IDeltaLapTime;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string EstimatedLapTime;

    public int IEstimatedLapTime;
    public int IsDeltaPositive;
    public int IsSplit;
    public int IsValidLap;
    public float FuelEstimatedLaps;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string TrackStatus;

    public int MissingMandatoryPits;
}

/// <summary>
/// Raw SPageFileStatic layout (written once per session).
/// Only fields up to <see cref="TyreRadius"/> are defined: all source
/// implementations agree on these offsets. Later fields (maxTurboBoost onwards,
/// including pit-window and tyre-name fields) are intentionally not declared
/// because the sources disagree on their layout; they must be verified against a
/// live ACC session before use.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
internal struct AccStaticPage
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string SmVersion;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string AcVersion;

    public int NumberOfSessions;
    public int NumCars;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string CarModel;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string Track;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerSurname;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerNick;

    public int SectorCount;
    public float MaxTorque; // not used in ACC
    public float MaxPower;  // not used in ACC
    public int MaxRpm;
    public float MaxFuel;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SuspensionMaxTravel; // not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreRadius; // not used in ACC
}
