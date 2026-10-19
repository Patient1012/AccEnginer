using System.Diagnostics;
using System.Text.Json;
using AccEngineer.Telemetry;
using AccEngineer.UI.Services;
using AccEngineer.UI.ViewModels;

namespace AccEngineer.UI.Tests;

/// <summary>
/// Fuel Calculator page behaviour: manual fallback works with ACC disconnected,
/// manual overrides stick and are not overwritten by telemetry, preferences round-trip
/// through the settings JSON, and no reassuring recommendation is shown while inputs
/// are missing.
/// </summary>
public class FuelCalculatorViewModelTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static (FuelCalculatorViewModel Vm, LiveTelemetryService Telemetry) CreateVm(AppSettings? settings = null, bool startTelemetry = false)
    {
        settings ??= new AppSettings { PollIntervalMs = 5, TestDataMode = true };
        var service = new LiveTelemetryService(new SessionEventLog(), settings);
        if (startTelemetry) service.Start();
        return (new FuelCalculatorViewModel(service, settings), service);
    }

    [Fact]
    public void ManualFallback_Works_WhenTelemetryIsDisconnected()
    {
        using var telemetry = new LiveTelemetryService(new SessionEventLog(), new AppSettings { PollIntervalMs = 5 });
        var vm = new FuelCalculatorViewModel(telemetry, new AppSettings());

        Assert.Equal(ConnectionState.Starting, telemetry.Connection);

        vm.ManualConsumptionPerLap = 2.5;
        vm.ManualRemainingLaps = 20;
        vm.CurrentFuelLitres = 40;

        // 2.5 x 20 = 50 L base, no margin or reserve by default in a fresh AppSettings
        // (reserve defaults to 1 L). 50 + 1 = 51 L required; 40 L in the car -> add 11 L.
        Assert.Equal("11.0 L", vm.FuelToAddText);
        Assert.Equal("MANUAL INPUT", vm.DataSourceText);
        Assert.False(vm.IsLiveData);
        Assert.False(vm.IsTestData);
    }

    [Fact]
    public void ManualOverride_Wins_OverTelemetry_AndIsNotOverwritten()
    {
        var (vm, telemetry) = CreateVm(startTelemetry: true);
        using (telemetry)
        {
            // Wait for test-data telemetry to flow into the service (like existing tests).
            Assert.True(WaitForTestData(telemetry, TimeSpan.FromSeconds(10)));

            vm.ManualConsumptionPerLap = 3.0;
            vm.CurrentFuelLitres = 10;
            var before = vm.CurrentFuelText;

            // Recalculate again (as the pump would) and the typed value must survive.
            vm.Refresh();

            Assert.Equal(10, vm.CurrentFuelLitres);
            Assert.Equal(before, vm.CurrentFuelText);
            Assert.Equal("MANUAL INPUT", vm.DataSourceText);
            Assert.Contains("manual", vm.ConsumptionText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CurrentFuel_IsFilledFromLiveTelemetry_UntilOverridden()
    {
        var (vm, telemetry) = CreateVm(startTelemetry: true);
        using (telemetry)
        {
            Assert.True(WaitForTestData(telemetry, TimeSpan.FromSeconds(10)));
            vm.Refresh();

            Assert.False(vm.CurrentFuelOverridden);

            vm.CurrentFuelLitres = 5;
            Assert.True(vm.CurrentFuelOverridden);
            Assert.True(vm.CurrentFuelIsManual);

            vm.Refresh();
            Assert.Equal(5, vm.CurrentFuelLitres);
        }
    }

    [Fact]
    public void ZeroConsumption_ProducesNoDivisionError_AndExplainsMissingData()
    {
        using var telemetry = new LiveTelemetryService(new SessionEventLog(), new AppSettings { PollIntervalMs = 5 });
        var vm = new FuelCalculatorViewModel(telemetry, new AppSettings());

        vm.ManualConsumptionPerLap = 0;
        vm.ManualRemainingLaps = 10;
        vm.CurrentFuelLitres = 40;

        // Zero consumption is treated as "no override" - the engine receives null
        // and reports the specific missing-input issue.
        Assert.Contains("Fuel consumption per lap is not available", vm.StatusText);
        Assert.Equal(Display.Missing, vm.FuelToAddText);
    }

    [Fact]
    public void NegativeInputs_AreRejected_AndNoReassuringMessageIsShown()
    {
        using var telemetry = new LiveTelemetryService(new SessionEventLog(), new AppSettings { PollIntervalMs = 5 });
        var vm = new FuelCalculatorViewModel(telemetry, new AppSettings());

        vm.CurrentFuelLitres = -10;
        vm.ManualConsumptionPerLap = -1;
        vm.ManualRemainingLaps = -5;

        // The current-fuel box clamps at zero; the engine rejects the negative
        // consumption and remaining-laps values with explicit issues.
        Assert.Equal(0, vm.CurrentFuelLitres);
        Assert.Contains("cannot be negative", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(Display.Missing, vm.FuelToAddText);
    }

    [Fact]
    public void TestDataSource_LabelsTestdataHonestly()
    {
        var (vm, telemetry) = CreateVm(startTelemetry: true);
        using (telemetry)
        {
            Assert.True(WaitForTestData(telemetry, TimeSpan.FromSeconds(10)));
            vm.Refresh();

            Assert.Equal("TEST DATA", vm.DataSourceText);
            Assert.False(vm.IsLiveData);
        }
    }

    [Fact]
    public void Preferences_RoundTripThroughSettingsJson()
    {
        var settings = new AppSettings
        {
            FuelCalculatorLastReserveLitres = 2.5,
            FuelCalculatorLastSafetyMarginLitres = 3,
            FuelCalculatorLastSafetyMarginPercent = 10,
            FuelCalculatorSafetyMarginMode = "Percentage",
            FuelCalculatorManualConsumptionPerLap = 2.75,
            FuelCalculatorManualRemainingLaps = 18,
            FuelCalculatorStintLaps = 12,
            FuelCalculatorPreferTimeLimitedEstimate = true,
            FuelCalculatorDisplayUnit = "Laps"
        };

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        Assert.Contains("fuelCalculatorManualConsumptionPerLap", json, StringComparison.Ordinal);

        var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)!;
        Assert.Equal(2.5, loaded.FuelCalculatorLastReserveLitres);
        Assert.Equal("Percentage", loaded.FuelCalculatorSafetyMarginMode);
        Assert.Equal(2.75, loaded.FuelCalculatorManualConsumptionPerLap);
        Assert.Equal(18, loaded.FuelCalculatorManualRemainingLaps);
        Assert.Equal(12, loaded.FuelCalculatorStintLaps);
        Assert.True(loaded.FuelCalculatorPreferTimeLimitedEstimate);
        Assert.Equal("Laps", loaded.FuelCalculatorDisplayUnit);

        // A settings file written before this feature must simply default to empty.
        var legacy = JsonSerializer.Deserialize<AppSettings>("{}", JsonOptions)!;
        Assert.Null(legacy.FuelCalculatorManualConsumptionPerLap);
        Assert.Equal("Litres", legacy.FuelCalculatorSafetyMarginMode);
        Assert.Equal("Litres", legacy.FuelCalculatorDisplayUnit);
    }

    [Fact]
    public void ViewModel_LoadsPersistedPreferences_OnConstruction()
    {
        var settings = new AppSettings
        {
            FuelCalculatorLastReserveLitres = 4,
            FuelCalculatorStintLaps = 9,
            FuelCalculatorManualConsumptionPerLap = 2.2
        };

        using var telemetry = new LiveTelemetryService(new SessionEventLog(), settings);
        var vm = new FuelCalculatorViewModel(telemetry, settings);

        Assert.Equal(4, vm.ReserveLitres);
        Assert.Equal(9, vm.StintLaps);
        Assert.Equal(2.2, vm.ManualConsumptionPerLap);
    }

    [Fact]
    public void LapsMode_ConvertsResults_UsingEffectiveConsumption()
    {
        using var telemetry = new LiveTelemetryService(new SessionEventLog(), new AppSettings { PollIntervalMs = 5 });
        var vm = new FuelCalculatorViewModel(telemetry, new AppSettings());

        vm.DisplayUnit = "Laps";
        vm.ManualConsumptionPerLap = 2.5;
        vm.ManualRemainingLaps = 20;
        vm.CurrentFuelLitres = 40;

        // Engine: 2.5 x 20 = 50 L base + 1 L reserve = 51 L required; add 11 L.
        // In laps mode everything is divided by 2.5 L/lap.
        Assert.True(vm.LapsMode);
        Assert.Equal("4.4 laps", vm.FuelToAddText);
        Assert.Equal("16.0 laps (entered)", vm.CurrentFuelText);
        Assert.Equal("20.4 laps", vm.TotalFuelRequiredText);
    }

    [Fact]
    public void LapsMode_WithoutConsumption_ShowsMissingNotAFabricatedConversion()
    {
        using var telemetry = new LiveTelemetryService(new SessionEventLog(), new AppSettings { PollIntervalMs = 5 });
        var vm = new FuelCalculatorViewModel(telemetry, new AppSettings { FuelCalculatorDisplayUnit = "Laps" });

        vm.ManualRemainingLaps = 20;
        vm.CurrentFuelLitres = 40;

        Assert.True(vm.LapsMode);
        Assert.Equal(Display.Missing, vm.FuelToAddText);
        Assert.StartsWith(Display.Missing, vm.CurrentFuelText, StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayUnit_DefaultsToLitres_AndPersistsThroughSave()
    {
        var settings = new AppSettings();
        using var telemetry = new LiveTelemetryService(new SessionEventLog(), settings);
        var vm = new FuelCalculatorViewModel(telemetry, settings);

        Assert.Equal("Litres", vm.DisplayUnit);
        Assert.False(vm.LapsMode);

        vm.DisplayUnit = "Laps";
        vm.SaveCommand.Execute(null);

        Assert.Equal("Laps", settings.FuelCalculatorDisplayUnit);
    }

    private static bool WaitForTestData(LiveTelemetryService service, TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < timeout)
        {
            if (service.IsTestData
                && service.Connection == ConnectionState.TestData
                && service.Latest?.Quality.HasFlag(DataQualityFlags.TestData) == true)
            {
                return true;
            }
            Thread.Sleep(20);
        }

        return service.IsTestData
            && service.Connection == ConnectionState.TestData
            && service.Latest?.Quality.HasFlag(DataQualityFlags.TestData) == true;
    }
}