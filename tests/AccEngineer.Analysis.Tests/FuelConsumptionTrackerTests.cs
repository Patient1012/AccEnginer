using AccEngineer.Analysis.Fuel;
using AccEngineer.Telemetry;

namespace AccEngineer.Analysis.Tests;

public class FuelConsumptionTrackerTests
{
    private static TelemetrySample Sample(
        int lap,
        float? fuel,
        string session = "session-1",
        bool valid = true,
        int? lastLapMs = null,
        DataQualityFlags quality = DataQualityFlags.None)
        => new()
        {
            SessionId = session,
            LapNumber = lap,
            FuelLevel = fuel,
            IsLapValid = valid,
            LastLapTimeMs = lastLapMs,
            Quality = quality
        };

    [Fact]
    public void Observe_SingleBoundary_DoesNotRecordThePossiblePartialLap()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f, lastLapMs: 95_000));

        // The first lap may be partial, so it must not become an observation.
        Assert.Equal(0, tracker.Snapshot().ValidLapCount);
    }

    [Fact]
    public void Observe_CompletedLaps_ReportsLastAndAverageConsumption()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f, lastLapMs: 95_000));
        tracker.Observe(Sample(3, 54.6f, lastLapMs: 95_000));
        tracker.Observe(Sample(4, 51.9f, lastLapMs: 96_000));

        var snapshot = tracker.Snapshot();

        Assert.Equal(2, snapshot.ValidLapCount);
        Assert.Equal(2.7, snapshot.LastLapLitresPerLap!.Value, 4);
        Assert.Equal(2.7, snapshot.AverageLitresPerLap!.Value, 4);
        Assert.True(snapshot.HasSufficientData);
    }

    [Fact]
    public void Observe_FewerLapsThanTheWindow_ReportsTheActualSampleCount()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f, lastLapMs: 95_000));
        tracker.Observe(Sample(3, 54.6f, lastLapMs: 95_000));
        tracker.Observe(Sample(4, 51.9f, lastLapMs: 95_000));

        var snapshot = tracker.Snapshot();

        // Only two clean laps exist, so the recent figure is honest about using two.
        Assert.Equal(2, snapshot.RecentLapCount);
        Assert.Equal(2.7, snapshot.RecentLitresPerLap!.Value, 4);
    }

    [Fact]
    public void Observe_MoreLapsThanTheWindow_AveragesOnlyTheNewest()
    {
        var tracker = new FuelConsumptionTracker();

        // Seven laps: the first two burn high (5.0 L), the newest five burn 2.0 L.
        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 55f, lastLapMs: 95_000));
        tracker.Observe(Sample(3, 50f, lastLapMs: 95_000));
        tracker.Observe(Sample(4, 48f, lastLapMs: 95_000));
        tracker.Observe(Sample(5, 46f, lastLapMs: 95_000));
        tracker.Observe(Sample(6, 44f, lastLapMs: 95_000));
        tracker.Observe(Sample(7, 42f, lastLapMs: 95_000));
        tracker.Observe(Sample(8, 40f, lastLapMs: 95_000));

        var snapshot = tracker.Snapshot();

        Assert.Equal(FuelConsumptionTracker.RecentWindow, snapshot.RecentLapCount);
        Assert.Equal(2.0, snapshot.RecentLitresPerLap!.Value, 4);

        // The session-wide average still reflects every measured lap (a mix, so > 2.0).
        Assert.True(snapshot.AverageLitresPerLap!.Value > 2.0);
    }

    [Fact]
    public void Observe_AverageLapSeconds_IsDerivedFromMeasuredLaps()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f, lastLapMs: 95_000));
        tracker.Observe(Sample(3, 54.6f, lastLapMs: 95_000));
        tracker.Observe(Sample(4, 51.9f, lastLapMs: 97_000));

        Assert.Equal(96.0, tracker.Snapshot().AverageLapSeconds!.Value, 6);
    }

    [Fact]
    public void Observe_RefuelDuringLap_SkipsThatLap()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 55f));
        tracker.Observe(Sample(2, 70f));                 // refuel mid-lap
        tracker.Observe(Sample(3, 69f, lastLapMs: 95_000));
        tracker.Observe(Sample(4, 66.3f, lastLapMs: 95_000));

        var snapshot = tracker.Snapshot();

        Assert.Equal(1, snapshot.ValidLapCount);
        Assert.Equal(2.7, snapshot.AverageLitresPerLap!.Value, 4);
    }

    [Fact]
    public void Observe_MissedLap_DoesNotBridgeTheGap()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f));
        tracker.Observe(Sample(5, 50f));                 // two laps were missed
        tracker.Observe(Sample(6, 47.3f, lastLapMs: 95_000));

        Assert.Equal(1, tracker.Snapshot().ValidLapCount);
    }

    [Fact]
    public void Observe_InvalidLap_IsExcluded()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f, valid: false));
        tracker.Observe(Sample(3, 54.6f, lastLapMs: 95_000));
        tracker.Observe(Sample(4, 51.9f, lastLapMs: 95_000));

        var snapshot = tracker.Snapshot();

        Assert.Equal(1, snapshot.ValidLapCount);
        Assert.Equal(2.7, snapshot.LastLapLitresPerLap!.Value, 4);
    }

    [Fact]
    public void Observe_InvalidLapQualityFlag_IsExcluded()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f, quality: DataQualityFlags.InvalidLap));
        tracker.Observe(Sample(3, 54.6f, lastLapMs: 95_000));
        tracker.Observe(Sample(4, 51.9f, lastLapMs: 95_000));

        Assert.Equal(1, tracker.Snapshot().ValidLapCount);
    }

    [Fact]
    public void Observe_StaleSample_IsIgnored()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(2, 57f, quality: DataQualityFlags.Stale));

        Assert.Equal(0, tracker.Snapshot().ValidLapCount);
    }

    [Fact]
    public void Observe_OutOfOrderSample_IsIgnored()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(2, 57f, quality: DataQualityFlags.OutOfOrder));

        Assert.Equal(0, tracker.Snapshot().ValidLapCount);
    }

    [Fact]
    public void Observe_ImplausibleConsumption_IsRejected()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57f));
        tracker.Observe(Sample(3, 0f));                  // 57 L burned in one lap

        Assert.Equal(0, tracker.Snapshot().ValidLapCount);
    }

    [Fact]
    public void Observe_UnknownFuelAtLapBoundary_MeasuresNothingForThatLap()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f));
        tracker.Observe(Sample(3, null));                // end-of-lap fuel unknown

        Assert.Equal(0, tracker.Snapshot().ValidLapCount);
    }

    [Fact]
    public void Observe_LapNumberGoesBackwards_RebaselinesWithoutCrashing()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f));
        tracker.Observe(Sample(1, 40f));                 // restart of the same session
        tracker.Observe(Sample(2, 37.3f));
        tracker.Observe(Sample(3, 34.6f, lastLapMs: 95_000));

        Assert.Equal(1, tracker.Snapshot().ValidLapCount);
        Assert.Equal(2.7, tracker.Snapshot().LastLapLitresPerLap!.Value, 4);
    }

    [Fact]
    public void Observe_NewSession_ClearsPreviousMeasurements()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f));
        tracker.Observe(Sample(3, 54.6f, lastLapMs: 95_000));
        Assert.Equal(1, tracker.Snapshot().ValidLapCount);

        tracker.Observe(Sample(1, 80f, session: "session-2"));

        var snapshot = tracker.Snapshot();
        Assert.Equal(0, snapshot.ValidLapCount);
        Assert.Null(snapshot.AverageLitresPerLap);
        Assert.Equal(FuelDataConfidence.Insufficient, snapshot.Confidence);
    }

    [Fact]
    public void Observe_SessionChangedFlag_ClearsMeasurements()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f));
        tracker.Observe(Sample(3, 54.6f, lastLapMs: 95_000));
        Assert.Equal(1, tracker.Snapshot().ValidLapCount);

        tracker.Observe(Sample(1, 80f, quality: DataQualityFlags.SessionChanged));

        Assert.Equal(0, tracker.Snapshot().ValidLapCount);
    }

    [Fact]
    public void Observe_NonFiniteFuel_IsRejected()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57f));
        tracker.Observe(Sample(3, float.NaN));

        Assert.Equal(0, tracker.Snapshot().ValidLapCount);
    }

    [Fact]
    public void Snapshot_WithNoData_ReportsInsufficientConfidence()
    {
        var snapshot = new FuelConsumptionTracker().Snapshot();

        Assert.Equal(0, snapshot.ValidLapCount);
        Assert.Null(snapshot.LastLapLitresPerLap);
        Assert.Null(snapshot.AverageLitresPerLap);
        Assert.False(snapshot.HasSufficientData);
        Assert.Equal(FuelDataConfidence.Insufficient, snapshot.Confidence);
        Assert.Empty(snapshot.Observations);
    }

    [Fact]
    public void Reset_ClearsMeasurements()
    {
        var tracker = new FuelConsumptionTracker();
        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f));
        tracker.Observe(Sample(3, 54.6f, lastLapMs: 95_000));

        tracker.Reset();

        Assert.Equal(0, tracker.Snapshot().ValidLapCount);
    }

    [Fact]
    public void Confidence_TenConsistentLaps_IsHigh()
    {
        var tracker = new FuelConsumptionTracker();

        for (var lap = 1; lap <= 12; lap++)
        {
            tracker.Observe(Sample(lap, (float)(60 - (2.7 * (lap - 1))), lastLapMs: 95_000));
        }

        var snapshot = tracker.Snapshot();

        Assert.Equal(10, snapshot.ValidLapCount);
        Assert.Equal(FuelDataConfidence.High, snapshot.Confidence);
    }

    [Fact]
    public void Confidence_FewConsistentLaps_IsLow()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f, lastLapMs: 95_000));
        tracker.Observe(Sample(3, 54.6f, lastLapMs: 95_000));
        tracker.Observe(Sample(4, 51.9f, lastLapMs: 95_000));

        var snapshot = tracker.Snapshot();

        Assert.Equal(2, snapshot.ValidLapCount);
        Assert.Equal(FuelDataConfidence.Low, snapshot.Confidence);
    }

    [Fact]
    public void Snapshot_ReturnsTheMeasurementHistoryOldestFirst()
    {
        var tracker = new FuelConsumptionTracker();

        tracker.Observe(Sample(1, 60f));
        tracker.Observe(Sample(2, 57.3f, lastLapMs: 95_000));
        tracker.Observe(Sample(3, 54.6f, lastLapMs: 95_000));
        tracker.Observe(Sample(4, 51.9f, lastLapMs: 95_000));

        var snapshot = tracker.Snapshot();

        Assert.Equal(2, snapshot.Observations.Count);
        Assert.Equal(2, snapshot.Observations[0].LapNumber);
        Assert.Equal(3, snapshot.Observations[1].LapNumber);
    }
}
