using System;
using System.Collections.Generic;
using AccEngineer.Telemetry;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Minimal demo producer that yields a steady, plausible sample to exercise the
/// layout editor and visual styling without needing an ACC session.
/// </summary>
public sealed class DemoOverlayTelemetrySource : IOverlayTelemetrySource, IDisposable
{
    private readonly Random _rng = new(42);
    private readonly TelemetrySample[] _seed;
    private TelemetrySample _latest;
    private int _tick;
    private bool _disposed;

    public DemoOverlayTelemetrySource()
    {
        _seed = CreateSeed();
        _latest = Clone(_seed[0]);
        _latest = _latest with { ReceivedAtUtc = DateTimeOffset.UtcNow };
    }

    public TelemetrySample? Latest
    {
        get
        {
            if (_disposed) return null;
            Advance();
            return _latest;
        }
    }

    private void Advance()
    {
        var now = DateTimeOffset.UtcNow;
        var t = _tick++;
        var theta = t * 0.1;
        var speed = 120 + 60 * Math.Sin(theta);
        var throttle = 0.3 + 0.2 * Math.Sin(theta + 0.2);
        var brake = theta % 6 > 4 ? 0.4 : 0.0;
        var gear = 2 + (int)((speed / 40) % 6);
        var rpm = 4000 + (int)(speed * 40);
        var trackProgress = (_tick * 5.0) % 2500.0;
        var lastLapMs = 83000 + (int)(_rng.NextDouble() * 500);
        var bestLapMs = 82200;

        var positions = new List<CarPosition>(_seed.Length);
        for (var i = 0; i < _seed.Length; i++)
        {
            var dx = (float)(Math.Sin(theta + i) * 2.0);
            var dz = (float)(Math.Cos(theta + i) * 4.0);
            positions.Add(new CarPosition(i + 1, dx, 0, dz));
        }

        _latest = _latest with
        {
            ReceivedAtUtc = now,
            SpeedKph = (float)Math.Max(0, speed),
            ThrottleInput = (float)Math.Clamp(throttle, 0, 1),
            BrakeInput = (float)Math.Clamp(brake, 0, 1),
            Gear = gear,
            EngineRpm = rpm,
            TrackProgressMeters = (float)trackProgress,
            LastLapTimeMs = lastLapMs,
            BestLapTimeMs = bestLapMs,
            CurrentLapTimeMs = lastLapMs - 1000,
            LapNumber = 3,
            IsLapValid = true,
            SessionType = SessionType.Practice,
                SessionState = SessionState.Live,
            PlayerCarId = 1,
            PlayerPosition = 3,
            ActiveCars = _seed.Length,
            PositionXMeters = 0,
            PositionZMeters = 0,
            CarsOnTrack = positions
        };
    }

    private static TelemetrySample[] CreateSeed()
    {
        var baseTime = DateTimeOffset.UtcNow;
        var samples = new TelemetrySample[6];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = new TelemetrySample
            {
                ReceivedAtUtc = baseTime.AddSeconds(-i),
                SessionId = "demo",
                TrackName = "Demo Circuit",
                CarName = "Generic",
                SessionType = SessionType.Practice,
            SessionState = SessionState.Live,
                PlayerCarId = 1,
                PlayerPosition = 3,
                ActiveCars = 6,
                LapNumber = 2,
                SpeedKph = 150,
                Gear = 4,
                EngineRpm = 7000,
                ThrottleInput = 0.8f,
                BrakeInput = 0.0f,
                LastLapTimeMs = 83000,
                BestLapTimeMs = 82200,
                IsLapValid = true,
                TrackProgressMeters = 1200,
                PositionXMeters = 0,
                PositionZMeters = 0,
                CarsOnTrack = new List<CarPosition>
                {
                    new(1, 0, 0, 0),
                    new(2, 1, 0, 5),
                    new(3, -1, 0, 10),
                    new(4, 2, 0, -2),
                    new(5, -0.5f, 0, 3),
                    new(6, 1.2f, 0, -1)
                }
            };
        }
        return samples;
    }

    private static TelemetrySample Clone(TelemetrySample s) => s with { CarsOnTrack = new List<CarPosition>(s.CarsOnTrack ?? new List<CarPosition>()) };

    public void Dispose() => _disposed = true;
}
