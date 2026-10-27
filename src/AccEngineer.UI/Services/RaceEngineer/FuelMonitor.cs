using AccEngineer.Telemetry;

namespace AccEngineer.UI.Services.RaceEngineer;

public class FuelMonitor
{
    private readonly Dictionary<string, List<double>> _fuelHistory = new();
    private DateTimeOffset _lastWarningUtc = DateTimeOffset.MinValue;
    private double _lastFuel = double.NaN;
    private int _lastLap = -1;

    public RaceEngineerAnnouncement? CheckFuel(TelemetrySample sample)
    {
        if (sample.FuelLevel is null || sample.TankCapacityLitres is null)
            return null;

        var fuel = (double)sample.FuelLevel;
        var tank = (double)sample.TankCapacityLitres;

        // Simple consumption tracking - basic implementation
        if (!double.IsNaN(_lastFuel) && sample.LapNumber != _lastLap)
        {
            var consumed = _lastFuel - fuel;
            if (consumed > 0.01)
            {
                var key = sample.SessionId + "|" + (sample.LapNumber - 1);
                if (!_fuelHistory.ContainsKey(key))
                    _fuelHistory[key] = new();
                _fuelHistory[key].Add(consumed);
            }
            _lastLap = sample.LapNumber;
        }
        else if (_lastLap == -1)
        {
            _lastLap = sample.LapNumber;
        }
        _lastFuel = fuel;

        // Estimate laps remaining
        var avgConsumption = 0.0;
        var count = 0;
        foreach (var hist in _fuelHistory.Values)
        {
            foreach (var c in hist) { avgConsumption += c; count++; }
        }
        if (count > 0) avgConsumption /= count;
        if (avgConsumption <= 0) return null;

        var lapsLeft = fuel / avgConsumption;
        var totalLaps = sample.TotalLaps;
        if (totalLaps is not null && sample.LapNumber > 0)
        {
            var remainingLaps = totalLaps.Value - sample.LapNumber;
            if (remainingLaps < 0) remainingLaps = 0;

            // Critical if won't finish
            if (lapsLeft < remainingLaps + 0.5)
            {
                var now = DateTimeOffset.UtcNow;
                if ((now - _lastWarningUtc).TotalSeconds > 60)
                {
                    _lastWarningUtc = now;
                    var needed = remainingLaps - lapsLeft + 0.5;
                    return new RaceEngineerAnnouncement
                    {
                        Message = $"Fuel critical. Need {needed:F1} more litres to finish. Current {fuel:F1}L for ~{lapsLeft:F1} laps.",
                        Priority = AnnouncementPriority.Critical,
                        Category = "Fuel"
                    };
                }
            }
            else if (lapsLeft < remainingLaps + 1.5)
            {
                var now = DateTimeOffset.UtcNow;
                if ((now - _lastWarningUtc).TotalSeconds > 90)
                {
                    _lastWarningUtc = now;
                    return new RaceEngineerAnnouncement
                    {
                        Message = $"Fuel low. About {lapsLeft:F1} laps left on {fuel:F1} litres.",
                        Priority = AnnouncementPriority.Important,
                        Category = "Fuel"
                    };
                }
            }
        }
        return null;
    }
}
