namespace AccEngineer.Analysis.Fuel;

/// <summary>
/// Pure helpers that derive how many laps remain from the session state, used by the fuel
/// page so the remaining-laps figure is explained consistently and can be unit-tested.
/// </summary>
public static class FuelLapEstimator
{
    /// <summary>
    /// Remaining laps for a lap-limited race. <paramref name="currentLapNumber"/> is the
    /// number of the lap currently in progress (ACC reports completed laps, so the lap in
    /// progress is completed + 1). The in-progress lap is included in the result.
    /// </summary>
    public static int RemainingRaceLaps(int totalLaps, int currentLapNumber)
    {
        if (totalLaps <= 0)
        {
            return 0;
        }

        var remaining = totalLaps - (currentLapNumber - 1);
        return Math.Max(0, remaining);
    }

    /// <summary>
    /// Estimated laps remaining for a time-limited session, given the time left and a
    /// representative lap time. Returns a fractional value rather than rounding up, so a
    /// partially completed final lap is represented as its true fraction and is never
    /// counted as a whole extra lap. Returns null when the lap time is not known.
    /// </summary>
    public static double? EstimateRemainingLapsFromTime(double? sessionTimeLeftSeconds, double? averageLapSeconds)
    {
        if (sessionTimeLeftSeconds is not { } timeLeft || double.IsNaN(timeLeft) || double.IsInfinity(timeLeft))
        {
            return null;
        }

        if (timeLeft <= 0d)
        {
            return 0d;
        }

        if (averageLapSeconds is not { } lapSeconds || double.IsNaN(lapSeconds) || double.IsInfinity(lapSeconds) || lapSeconds <= 0d)
        {
            return null;
        }

        return timeLeft / lapSeconds;
    }

    /// <summary>
    /// Average lap time in seconds from a set of valid lap times in milliseconds. Returns
    /// null when no usable lap time is supplied.
    /// </summary>
    public static double? AverageLapSeconds(IEnumerable<int>? validLapTimesMs)
    {
        if (validLapTimesMs is null)
        {
            return null;
        }

        var total = 0L;
        var count = 0;
        foreach (var ms in validLapTimesMs)
        {
            if (ms > 0)
            {
                total += ms;
                count++;
            }
        }

        return count == 0 ? null : total / 1000d / count;
    }
}
