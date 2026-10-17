namespace AccEngineer.Telemetry;

/// <summary>
/// Deterministic synthetic telemetry model behind <see cref="SimulatedTelemetryReader"/>.
///
/// It produces a self-consistent GT3-style lap with no simulator, network or shared
/// memory involved: speed follows the (sinusoidal) curvature of a repeating corner
/// pattern, throttle and brake follow the resulting longitudinal acceleration, brake
/// and tyre temperatures and pressures lag the applied load, fuel burns down and lap
/// times are integrated from the actual speed. The same seed and the same elapsed
/// time always yield the same sample, so it is reproducible in tests.
/// </summary>
internal sealed class TestTelemetrySimulator
{
    private const double StandardGravity = 9.80665;
    private const double AcceleratingG = 1.15;
    private const double BrakingG = 1.6;
    private const double CorneringG = 1.9;
    private const double PeakCurvature = 0.052;      // 1/m at the apex
    private const double StraightCurvature = 0.0018; // 1/m on a straight
    private const double MinSpeedMps = 16.0;
    private const double MaxSpeedMps = 72.0;
    private const int CornerCount = 10;
    private const int RedlineRpm = 7600;
    private const int IdleRpm = 3000;
    private const double TrackRadiusX = 340.0;
    private const double TrackRadiusZ = 210.0;

    /// <summary>Upper speed (m/s) of each gear 1..8; the last band caps at <see cref="MaxSpeedMps"/>.</summary>
    private static readonly double[] GearBands = { 14, 21, 28, 35, 42, 49, 56, 72 };

    private readonly SimulatedTelemetryOptions _options;
    private readonly double _phase;
    private readonly List<CarPosition> _opponents = new();

    private double _distance;
    private double _speed = 40.0;
    private double _lapTime;
    private double _lastLap = -1;
    private double _bestLap = double.PositiveInfinity;
    private int _lapNumber = 1;
    private double _fuel;

    private int _lastSectorIndex = -1;
    private double _sectorElapsed;
    private double _lastSector = -1;

    private readonly double[] _tyreCore = new double[4];
    private readonly double[] _tyrePressure = new double[4];
    private readonly double[] _tyreWear = new double[4];
    private readonly double[] _brakeTemp = new double[4];
    private readonly double[] _suspension = new double[4];

    public TestTelemetrySimulator(SimulatedTelemetryOptions options)
    {
        _options = options;
        _phase = (Math.Abs(options.Seed) % 1000) / 1000.0;
        _fuel = options.StartingFuelKg;

        for (var i = 0; i < 4; i++)
        {
            _tyreCore[i] = 72.0;
            _tyrePressure[i] = 22.6;
            _brakeTemp[i] = 90.0;
            _suspension[i] = 0.03;
        }

        var count = Math.Max(0, options.OtherCars);
        for (var i = 0; i < count; i++) _opponents.Add(new CarPosition(i + 2, 0, 0, 0));
    }

    public TelemetrySample Advance(TimeSpan delta, long sampleId, DateTimeOffset receivedAtUtc, long receiptTick)
    {
        var dt = Math.Clamp(delta.TotalSeconds, 0.001, 1.0);
        var lapLength = Math.Max(200.0, _options.LapLengthMeters);

        var u = (_distance % lapLength) / lapLength;
        var curvature = PeakCurvature * Math.Sin((2 * Math.PI * ((CornerCount * u) + _phase)));

        var speedLimit = Math.Sqrt(CorneringG * StandardGravity / Math.Max(Math.Abs(curvature), StraightCurvature));
        speedLimit = Math.Clamp(speedLimit, MinSpeedMps, MaxSpeedMps);

        var accelStep = AcceleratingG * StandardGravity * dt;
        var brakeStep = BrakingG * StandardGravity * dt;
        var change = Math.Clamp(speedLimit - _speed, -brakeStep, accelStep);
        _speed = Math.Max(MinSpeedMps * 0.5, _speed + change);

        var longitudinalG = change / dt / StandardGravity;
        var throttle = (float)Math.Clamp(change >= 0 ? 0.32 + (0.68 * (change / accelStep)) : 0.0, 0, 1);
        var brake = (float)Math.Clamp(change < 0 ? -change / brakeStep : 0.0, 0, 1);

        var lateralG = curvature * _speed * _speed / StandardGravity;
        var steering = (float)Math.Clamp(lateralG / CorneringG, -1, 1);

        _distance += _speed * dt;
        _lapTime += dt;

        var lapRolled = _distance >= (double)_lapNumber * lapLength;
        if (lapRolled)
        {
            _lastLap = _lapTime;
            if (_lapTime < _bestLap) _bestLap = _lapTime;
            _lapNumber++;
            _lapTime = 0;
        }

        // Sector timing: the simulated track has three sectors. A new sector (or a
        // completed lap) closes the previous one, storing its duration.
        var uAfter = (_distance % lapLength) / lapLength;
        var sector = Math.Clamp((int)(uAfter * 3), 0, 2);
        _sectorElapsed += dt;
        if (_lastSectorIndex < 0)
        {
            _lastSectorIndex = sector;
            _sectorElapsed = 0;
        }
        else if (lapRolled || sector != _lastSectorIndex)
        {
            _lastSector = _sectorElapsed;
            _sectorElapsed = 0;
            _lastSectorIndex = sector;
        }

        // Live delta: elapsed time this lap versus the same fraction of the best lap.
        // Null until a best lap exists, exactly as ACC reports no delta without a reference.
        double? deltaSeconds = double.IsPositiveInfinity(_bestLap) ? null : _lapTime - (_bestLap * uAfter);

        UpdateThermal(dt, lateralG, longitudinalG, throttle, brake, steering);
        _fuel = Math.Max(2.0, _fuel - ((0.0004 + (0.006 * throttle)) * dt));

        var gear = GearForSpeed(_speed);
        var (bandLow, bandHigh) = GearBand(gear);
        var rpm = (int)Math.Round(IdleRpm + ((RedlineRpm - IdleRpm) * Math.Clamp((_speed - bandLow) / (bandHigh - bandLow), 0, 1)));

        var angle = 2 * Math.PI * u;
        var x = TrackRadiusX * Math.Cos(angle);
        var z = TrackRadiusZ * Math.Sin(angle);
        var heading = Math.Atan2(TrackRadiusZ * Math.Cos(angle), -TrackRadiusX * Math.Sin(angle));

        var cars = new List<CarPosition>(_opponents.Count + 1)
        {
            new(1, (float)x, 0f, (float)z)
        };
        for (var i = 0; i < _opponents.Count; i++)
        {
            var offset = (u - (0.012 * (i + 1))) % 1.0;
            if (offset < 0) offset += 1.0;
            var oa = 2 * Math.PI * offset;
            _opponents[i] = new CarPosition(i + 2, (float)(TrackRadiusX * Math.Cos(oa)), 0f, (float)(TrackRadiusZ * Math.Sin(oa)));
            cars.Add(_opponents[i]);
        }

        return new TelemetrySample
        {
            SampleId = sampleId,
            ReceivedAtUtc = receivedAtUtc,
            ReceiptTick = receiptTick,
            SessionId = _options.SessionId,
            LapNumber = _lapNumber,
            IsLapValid = true,
            CurrentLapTimeMs = _lapTime > 1.0 ? (int)Math.Round(_lapTime * 1000) : null,
            LastLapTimeMs = _lastLap > 0 ? (int)Math.Round(_lastLap * 1000) : null,
            BestLapTimeMs = double.IsPositiveInfinity(_bestLap) ? null : (int)Math.Round(_bestLap * 1000),
            CurrentSectorIndex = sector,
            SectorCount = 3,
            LastSectorTimeMs = _lastSector > 0 ? (int)Math.Round(_lastSector * 1000) : null,
            DeltaLapTimeMs = deltaSeconds is { } d ? (int)Math.Round(d * 1000) : null,
            IsDeltaPositive = deltaSeconds is { } dd && dd >= 0,
            TrackProgressMeters = (float)(_distance % lapLength),
            SpeedMetersPerSecond = (float)_speed,
            SpeedKph = (float)(_speed * 3.6),
            SteeringInput = steering,
            ThrottleInput = throttle,
            BrakeInput = brake,
            ClutchInput = 0f,
            Gear = gear + 1,
            EngineRpm = rpm,
            LateralAccelerationG = (float)lateralG,
            LongitudinalAccelerationG = (float)longitudinalG,
            HeadingRad = (float)heading,
            PitchRad = (float)(-longitudinalG * 0.02),
            RollRad = (float)(-lateralG * 0.03),
            PositionXMeters = (float)x,
            PositionZMeters = (float)z,
            PlayerPosition = 3,
            NormalizedCarPosition = (float)u,
            ActiveCars = cars.Count,
            PlayerCarId = 1,
            CarsOnTrack = cars,
            WheelSlip = new[] { 0f, 0f, 0f, 0f },
            WheelLoad = _suspension.Select(s => (float)(s * 100000.0)).ToArray(),
            WheelPressure = _tyrePressure.Select(p => (float)p).ToArray(),
            SuspensionTravelMeters = _suspension.Select(s => (float)s).ToArray(),
            TyreCoreTempC = _tyreCore.Select(t => (float)t).ToArray(),
            TyreWear = _tyreWear.Select(w => (float)w).ToArray(),
            BrakeTempC = _brakeTemp.Select(t => (float)t).ToArray(),
            FuelLevel = (float)_fuel,
            PitLimiterOn = false,
            IsInPitLane = false,
            AirTempC = 24.5f,
            TrackTempC = 31.0f,
            SurfaceGrip = 0.98f,
            RainTyresOn = false,
            Flag = SessionFlag.None,
            TrackName = _options.TrackName,
            CarName = _options.CarName,
            TyreCompound = _options.TyreCompound,
            SessionType = _options.SessionType,
            SessionState = _options.SessionState,
            Quality = DataQualityFlags.TestData
        };
    }

    private void UpdateThermal(double dt, double lateralG, double longitudinalG, float throttle, float brake, float steering)
    {
        var lateralLoad = Math.Abs(lateralG);
        var longitudinalLoad = Math.Abs(longitudinalG);

        for (var i = 0; i < 4; i++)
        {
            var front = i < 2;
            var load = (front ? 1.0 + (0.35 * Math.Abs(steering)) : 0.85) + (0.15 * longitudinalLoad);

            var coreTarget = 82.0 + (26.0 * load) + (12.0 * lateralLoad);
            _tyreCore[i] += (coreTarget - _tyreCore[i]) * dt / 6.0;
            _tyrePressure[i] += ((23.0 + ((_tyreCore[i] - 72.0) * 0.03)) - _tyrePressure[i]) * dt / 10.0;

            var brakeTarget = 110.0 + (520.0 * brake) + (front ? 120.0 * brake : 0.0);
            _brakeTemp[i] += (brakeTarget - _brakeTemp[i]) * dt / 3.0;

            var wearRate = 0.0004 * (0.5 + Math.Abs(steering) + brake + (0.5 * throttle));
            _tyreWear[i] = Math.Min(0.25, _tyreWear[i] + (wearRate * dt));

            var travel = 0.02 + (0.02 * load) + (0.01 * lateralLoad);
            _suspension[i] += (travel - _suspension[i]) * dt / 2.0;
        }
    }

    private static int GearForSpeed(double speed)
    {
        for (var i = 0; i < GearBands.Length; i++)
        {
            if (speed <= GearBands[i]) return i + 1;
        }

        return GearBands.Length;
    }

    private static (double Low, double High) GearBand(int gear)
    {
        var low = gear <= 1 ? 0.0 : GearBands[gear - 2];
        var high = GearBands[Math.Min(gear - 1, GearBands.Length - 1)];
        return (low, high);
    }
}
