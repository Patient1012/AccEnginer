using System;
using System.Collections.Generic;
using AccEngineer.Telemetry;

namespace AccEngineer.UI.Overlay;

/// <summary>
/// Minimal demo producer that yields plausible, slowly evolving samples so panels
/// and the layout editor can be exercised without an ACC session. Cars are placed
/// on a circle and "drive" around it, which gives the projection logic a real line
/// to work with. Everything is synthetic and clearly intended only for UI testing.
/// </summary>
public sealed class DemoOverlayTelemetrySource : IOverlayTelemetrySource, IDisposable
{
    private const int RecentCapacity = 900;
    private const double Radius = 30.0;

    private readonly Random _rng = new(42);
    private readonly List<TelemetrySample> _recent = new(RecentCapacity);
    private TelemetrySample _latest;
    private int _tick;
    private bool _disposed;

    public DemoOverlayTelemetrySource()
    {
        _latest = Seed();
        _recent.Add(_latest);
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

    public IReadOnlyList<TelemetrySample> Recent(int maxCount)
    {
        if (_disposed) return Array.Empty<TelemetrySample>();

        // Return the most recent window in chronological order.
        var count = Math.Min(maxCount, _recent.Count);
        var result = new TelemetrySample[count];
        for (var i = 0; i < count; i++) result[i] = _recent[_recent.Count - count + i];
        return result;
    }

    /// <summary>Advances the synthetic session one step and publishes it as latest.</summary>
    private void Advance()
    {
        var now = DateTimeOffset.UtcNow;
        var t = _tick++;
        var playerAngle = t * 0.02;
        var speed = 120 + 60 * Math.Sin(t * 0.1);

        // Player on the circle; "player" is car id 1 and sits at index 0.
        var playerX = Radius * Math.Sin(playerAngle);
        var playerZ = Radius * Math.Cos(playerAngle);

        // Other cars trail behind the player by increasing angular offsets so the
        // projected line orders them consistently.
        var carCount = 6;
        var positions = new List<CarPosition>(carCount);
        for (var i = 0; i < carCount; i++)
        {
            if (i == 0)
            {
                positions.Add(new CarPosition(1, (float)playerX, 0, (float)playerZ));
                continue;
            }

            var angle = playerAngle - (i * 0.06);
            positions.Add(new CarPosition(i + 1, (float)(Radius * Math.Sin(angle)), 0, (float)(Radius * Math.Cos(angle))));
        }

        _latest = _latest with
        {
            ReceivedAtUtc = now,
            SpeedKph = (float)Math.Max(0, speed),
            ThrottleInput = (float)Math.Clamp(0.3 + 0.2 * Math.Sin(t * 0.1 + 0.2), 0, 1),
            BrakeInput = (float)Math.Clamp(t % 60 > 40 ? 0.4 : 0.0, 0, 1),
            SteeringInput = (float)Math.Sin(playerAngle * 2),
            Gear = 2 + (int)((Math.Max(0, speed) / 40) % 6),
            EngineRpm = 4000 + (int)(Math.Max(0, speed) * 40),
            TrackProgressMeters = (float)(playerAngle * Radius),
            LastLapTimeMs = 83000 + _rng.Next(0, 500),
            BestLapTimeMs = 82200,
            CurrentLapTimeMs = 83000 + _rng.Next(0, 500) - 2000,
            LapNumber = 3,
            IsLapValid = true,
            PlayerCarId = 1,
            PlayerPosition = 3,
            ActiveCars = carCount,
            PositionXMeters = (float)playerX,
            PositionZMeters = (float)playerZ,
            CarsOnTrack = positions
        };

        _recent.Add(_latest);
        if (_recent.Count > RecentCapacity) _recent.RemoveAt(0);
    }

    private static TelemetrySample Seed() => new()
    {
        ReceivedAtUtc = DateTimeOffset.UtcNow,
        SessionId = "demo",
        TrackName = "Demo Circuit",
        CarName = "Generic GT3",
        SessionType = SessionType.Practice,
        SessionState = SessionState.Live,
        PlayerCarId = 1,
        PlayerPosition = 3,
        ActiveCars = 6,
        LapNumber = 3,
        SpeedKph = 150,
        Gear = 4,
        EngineRpm = 7000,
        ThrottleInput = 0.8f,
        BrakeInput = 0.0f,
        LastLapTimeMs = 83000,
        BestLapTimeMs = 82200,
        CurrentLapTimeMs = 81000,
        IsLapValid = true,
        TrackProgressMeters = 0,
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

    public void Dispose() => _disposed = true;
}
