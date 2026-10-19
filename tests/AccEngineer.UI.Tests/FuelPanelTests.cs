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
        ValidLapCount = laps,
        HasSufficientData = laps >= 2
    };

    [Fact]
    public void Refresh_ComputesFuelToAddAndLapsFromTelemetry()
    {
        // 10 scheduled laps, on lap 3 -> 8 remaining laps (the in-progress lap counts).
        // 2.5 L/lap x 8 = 20 L base, + 1 L reserve (default) = 21 L required; 40 L in the
        // car -> nothing to add.
        var options = new AppSettings { FuelCalculatorLastReserveLitres = 1.0 };
        var source = new FakeSource { Latest = Sample(), Fuel = Consumption(2.5) };

        using var vm = new FuelPanelViewModel(source, options);

        Assert.True(vm.IsLive);
        Assert.Equal("40.0 L", vm.CurrentFuelText);
        Assert.Equal("2.50 L/lap", vm.ConsumptionText);
        Assert.Equal("0.0 L", vm.FuelToAddText);
        Assert.True(vm.IsSufficient);
        Assert.False(vm.HasShortfall);
        Assert.Equal("LIVE TELEMETRY", vm.SourceText);
    }

    [Fact]
    public void Refresh_WithShortfall_FlagsAndReportsFuelToAdd()
    {
        // Same plan but only 5 L in the car -> 21 - 5 = 16.0 L to add.
        var options = new AppSettings { FuelCalculatorLastReserveLitres = 1.0 };
        var sample = Sample() with { FuelLevel = 5f };
        var source = new FakeSource { Latest = sample, Fuel = Consumption(2.5) };

        using var vm = new FuelPanelViewModel(source, options);

        Assert.True(vm.HasShortfall);
        Assert.False(vm.IsSufficient);
        Assert.Equal("16.0 L", vm.FuelToAddText);
    }

    [Fact]
    public void Refresh_WithoutConsumption_ShowsCollectingState()
    {
        var options = new AppSettings();
        var source = new FakeSource { Latest = Sample(), Fuel = null };

        using var vm = new FuelPanelViewModel(source, options);

        Assert.Equal("—", vm.ConsumptionText);
        Assert.Equal("—", vm.FuelToAddText);
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

        Assert.Equal("3.00 L/lap", vm.ConsumptionText);
        Assert.Equal("MANUAL INPUT", vm.SourceText);
        Assert.Equal("0.0 L", vm.FuelToAddText); // 12 L required, 40 L in the car
    }

    [Fact]
    public void Refresh_NoSignal_ShowsNoSignal()
    {
        using var vm = new FuelPanelViewModel(new FakeSource { Latest = null }, new AppSettings());

        Assert.False(vm.IsLive);
        Assert.Equal("NO SIGNAL", vm.StatusText);
        Assert.Equal("—", vm.CurrentFuelText);
    }
}
