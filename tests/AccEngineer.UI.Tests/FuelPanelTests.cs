using AccEngineer.Analysis.Fuel;
using AccEngineer.Telemetry;
using AccEngineer.Telemetry.Broadcasting;
using AccEngineer.UI.Overlay;
using AccEngineer.UI.Overlay.Panels;
using AccEngineer.UI.Services;
using Xunit;

namespace AccEngineer.UI.Tests;

public sealed class FuelPanelTests
{
    private sealed class FakeSource : IOverlayTelemetrySource
    {
        public TelemetrySample? Latest { get; init; }
        public FuelConsumptionSnapshot? Fuel { get; init; }
        public bool IsTestData { get; init; }
        public IReadOnlyList<TelemetrySample> Recent(int maxCount) => Array.Empty<TelemetrySample>();
        public CompetitorStandings? Standings => null;
        public string StandingsStatusText => "Broadcasting off.";
        public FuelConsumptionSnapshot? FuelConsumption => Fuel;
    }

    private static TelemetrySample Sample() => new()
    {
        SessionId = "s1",
        FuelLevel = 40f,
        TankCapacityLitres = 120f,
        LapNumber = 3,
        TotalLaps = 10
    };

    private static FuelConsumptionSnapshot Consumption(double perLap, int laps = 5) => new()
    {
        AverageLitresPerLap = perLap,
        LastLapLitresPerLap = perLap,
        RecentLitresPerLap = perLap,
        RecentLapCount = Math.Min(laps, FuelConsumptionTracker.RecentWindow),
        ValidLapCount = laps,
        HasSufficientData = laps >= 2
    };

    // 10 scheduled laps, on lap 3 -> 8 remaining (the in-progress lap counts). At 2.5 L/lap
    // that is 20 L base; + 1 L reserve = 21 L required; 40 L in the car -> ~19 L spare.
    private static AppSettings Plan() => new() { FuelCalculatorLastReserveLitres = 1.0 };

    [Fact]
    public void Refresh_ComputesHeaderUsageAndFinishFromTelemetry()
    {
        var source = new FakeSource { Latest = Sample(), Fuel = Consumption(2.5) };

        using var vm = new FuelPanelViewModel(source, Plan());

        Assert.True(vm.IsLive);
        Assert.Equal("40.0 L", vm.CurrentFuelText);              // Section A current fuel
        Assert.Equal("16.0 laps", vm.LapsRemainingText);          // 40 / 2.5
        Assert.Equal("2.50 L", vm.PredictedPerLapText);           // Section B predicted
        Assert.Equal(string.Empty, vm.AveragePerLapText);         // equal to predicted -> omitted
        Assert.Equal("2.50", vm.LastLapUsageText);                // Section C
        Assert.Equal("16.0", vm.LastLapLapsText);                 // laps on current fuel at 2.5
        Assert.Equal("21.0 L", vm.ToFinishText);                  // Section D
        Assert.Equal("+19.0 L spare", vm.SpareText);
        Assert.True(vm.IsSufficient);
        Assert.False(vm.HasShortfall);
        Assert.Equal(FuelBarState.Sufficient, vm.BarState);
        Assert.True(vm.FuelBarFraction > 0d && vm.FuelBarFraction <= 1d);
        Assert.Equal("LIVE TELEMETRY", vm.SourceText);
    }

    [Fact]
    public void Refresh_WithShortfall_FlagsStateAndShowsShortMargin()
    {
        // Same plan but only 5 L in the car -> 21 - 5 = 16 L short.
        var sample = Sample() with { FuelLevel = 5f };
        var source = new FakeSource { Latest = sample, Fuel = Consumption(2.5) };

        using var vm = new FuelPanelViewModel(source, Plan());

        Assert.True(vm.HasShortfall);
        Assert.False(vm.IsSufficient);
        Assert.Equal("21.0 L", vm.ToFinishText);
        Assert.Equal("-16.0 L short", vm.SpareText);
        Assert.DoesNotContain("spare", vm.SpareText);
        Assert.Equal(FuelBarState.Shortfall, vm.BarState);
    }

    [Fact]
    public void Refresh_LowMargin_ReadsWarningNotSufficient()
    {
        // Exactly 21 L in the car -> no shortfall, but no headroom either: amber.
        var sample = Sample() with { FuelLevel = 21f };
        var source = new FakeSource { Latest = sample, Fuel = Consumption(2.5) };

        using var vm = new FuelPanelViewModel(source, Plan());

        Assert.Equal(FuelBarState.Warning, vm.BarState);
        Assert.Equal("+0.0 L spare", vm.SpareText);
    }

    [Fact]
    public void Refresh_RecentWindowLabel_UsesTheActualSampleCount()
    {
        // Only three clean laps measured: the middle row must say "3 LAP", not "5 LAP".
        var source = new FakeSource { Latest = Sample(), Fuel = Consumption(2.5, laps: 3) };

        using var vm = new FuelPanelViewModel(source, Plan());

        Assert.Equal("3 LAP", vm.RecentLapLabelText);
        Assert.Equal("2.50", vm.RecentLapUsageText);
        Assert.Equal("16.0", vm.RecentLapLapsText);
    }

    [Fact]
    public void Refresh_WithoutConsumption_ShowsCollectingStateAndUnknownBar()
    {
        var source = new FakeSource { Latest = Sample(), Fuel = null };

        using var vm = new FuelPanelViewModel(source, new AppSettings());

        Assert.Equal("—", vm.PredictedPerLapText);
        Assert.Equal("—", vm.ToFinishText);
        Assert.Equal("—", vm.SpareText);
        Assert.Equal("—", vm.LastLapUsageText);
        Assert.Equal("—", vm.RecentLapUsageText);
        Assert.Equal("—", vm.AverageUsageText);
        Assert.Equal(string.Empty, vm.AveragePerLapText);
        Assert.Equal(FuelBarState.Unknown, vm.BarState);
        Assert.Equal(0d, vm.FuelBarFraction);
        Assert.Contains("Collecting", vm.StatusLineText);
    }

    [Fact]
    public void Refresh_TestData_LabelsSourceHonestly()
    {
        var source = new FakeSource { Latest = Sample(), Fuel = Consumption(2.5), IsTestData = true };

        using var vm = new FuelPanelViewModel(source, new AppSettings());

        Assert.Equal("TEST DATA", vm.SourceText);
    }

    [Fact]
    public void Refresh_ManualConsumptionAndLaps_OverrideTelemetry()
    {
        var options = new AppSettings
        {
            FuelCalculatorManualConsumptionPerLap = 3.0,
            FuelCalculatorManualRemainingLaps = 4,
            FuelCalculatorLastReserveLitres = 0.0
        };
        // Telemetry says 2.5 L/lap; the manual 3.0 L/lap x 4 laps = 12 L must win.
        var source = new FakeSource { Latest = Sample(), Fuel = Consumption(2.5) };

        using var vm = new FuelPanelViewModel(source, options);

        Assert.Equal("3.00 L", vm.PredictedPerLapText);
        Assert.Equal("MANUAL INPUT", vm.SourceText);
        Assert.Equal("12.0 L", vm.ToFinishText);   // 3.0 x 4 laps, no reserve
        Assert.Equal("+28.0 L spare", vm.SpareText);
    }

    [Fact]
    public void Refresh_ManualConsumption_KeepsMeasuredAverageAsComparison()
    {
        // Manual 3.0 wins, but the measured 2.5 stays as the muted session-average figure.
        var options = new AppSettings { FuelCalculatorManualConsumptionPerLap = 3.0 };
        var source = new FakeSource { Latest = Sample(), Fuel = Consumption(2.5) };

        using var vm = new FuelPanelViewModel(source, options);

        Assert.Equal("3.00 L", vm.PredictedPerLapText);
        Assert.Equal("2.50 L", vm.AveragePerLapText);
    }

    [Fact]
    public void Refresh_NoSignal_ShowsNoSignal()
    {
        using var vm = new FuelPanelViewModel(new FakeSource { Latest = null }, new AppSettings());

        Assert.False(vm.IsLive);
        Assert.Equal("NO SIGNAL", vm.StatusText);
        Assert.Equal("—", vm.CurrentFuelText);
        Assert.Equal(FuelBarState.Unknown, vm.BarState);
    }

    [Fact]
    public void Refresh_LapsMode_ConvertsCurrentFuelToLaps()
    {
        // 40 L in the car at 2.5 L/lap = 16.0 laps.
        var options = new AppSettings
        {
            FuelCalculatorLastReserveLitres = 1.0,
            FuelCalculatorDisplayUnit = "Laps"
        };
        var source = new FakeSource { Latest = Sample(), Fuel = Consumption(2.5) };

        using var vm = new FuelPanelViewModel(source, options);

        Assert.Equal("16.0 laps", vm.CurrentFuelText);
        Assert.True(vm.IsSufficient);
    }

    [Fact]
    public void Refresh_LapsMode_ShortfallReportsLapsInStatusLine()
    {
        // 21 L required, 5 L (2.0 laps) available -> 16 L to add = 6.4 laps.
        var options = new AppSettings
        {
            FuelCalculatorLastReserveLitres = 1.0,
            FuelCalculatorDisplayUnit = "Laps"
        };
        var sample = Sample() with { FuelLevel = 5f };
        var source = new FakeSource { Latest = sample, Fuel = Consumption(2.5) };

        using var vm = new FuelPanelViewModel(source, options);

        Assert.Equal("2.0 laps", vm.CurrentFuelText);
        Assert.Contains("6.4 laps", vm.StatusLineText);
    }

    [Fact]
    public void Refresh_LapsModeWithoutConsumption_ShowsMissing()
    {
        var options = new AppSettings { FuelCalculatorDisplayUnit = "Laps" };
        var source = new FakeSource { Latest = Sample(), Fuel = null };

        using var vm = new FuelPanelViewModel(source, options);

        Assert.Equal("—", vm.CurrentFuelText);
        Assert.Equal("—", vm.SpareText);
    }
}
