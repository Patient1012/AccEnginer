using AccEngineer.Analysis.Fuel;

namespace AccEngineer.Analysis.Tests;

public class FuelCalculatorTests
{
    private static readonly FuelCalculator Calculator = new();

    [Fact]
    public void Calculate_BaseRequirement_IsConsumptionTimesLaps()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20
        });

        Assert.Equal(54.0, result.BaseFuelRequiredLitres!.Value, 6);
        Assert.Equal(54.0, result.TotalFuelRequiredLitres!.Value, 6);
        Assert.Equal(44.0, result.FuelToAddLitres!.Value, 6);
        Assert.Equal(FuelPlanOutcome.Shortfall, result.Outcome);
    }

    [Fact]
    public void Calculate_CurrentFuelCoversRequirement_IsSufficient()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 60,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20
        });

        Assert.Equal(0.0, result.FuelToAddLitres!.Value, 6);
        Assert.Equal(6.0, result.SurplusOrShortfallLitres!.Value, 6);
        Assert.Equal(FuelPlanOutcome.Sufficient, result.Outcome);
    }

    [Fact]
    public void Calculate_SafetyMarginInLitres_IsAddedToTotal()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20,
            SafetyMarginMode = SafetyMarginMode.Litres,
            SafetyMarginLitres = 2
        });

        Assert.Equal(2.0, result.SafetyMarginLitres!.Value, 6);
        Assert.Equal(56.0, result.TotalFuelRequiredLitres!.Value, 6);
    }

    [Fact]
    public void Calculate_SafetyMarginAsPercentage_IsPercentOfBaseFuel()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20,
            SafetyMarginMode = SafetyMarginMode.Percentage,
            SafetyMarginPercent = 5
        });

        Assert.Equal(2.7, result.SafetyMarginLitres!.Value, 6);
        Assert.Equal(56.7, result.TotalFuelRequiredLitres!.Value, 6);
    }

    [Fact]
    public void Calculate_BothSafetyMargins_AreAddedTogether()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20,
            SafetyMarginMode = SafetyMarginMode.Both,
            SafetyMarginLitres = 2,
            SafetyMarginPercent = 5
        });

        Assert.Equal(4.7, result.SafetyMarginLitres!.Value, 6);
        Assert.Equal(58.7, result.TotalFuelRequiredLitres!.Value, 6);
    }

    [Fact]
    public void Calculate_ReserveFuel_IsIncludedInTotal()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20,
            ReserveLitres = 1.5
        });

        Assert.Equal(1.5, result.ReserveLitres!.Value, 6);
        Assert.Equal(55.5, result.TotalFuelRequiredLitres!.Value, 6);
    }

    [Fact]
    public void Calculate_ZeroRemainingLaps_RequiresOnlyReserveAndMargin()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 0,
            ReserveLitres = 1.5,
            SafetyMarginLitres = 2
        });

        Assert.Equal(0.0, result.BaseFuelRequiredLitres!.Value, 6);
        Assert.Equal(3.5, result.TotalFuelRequiredLitres!.Value, 6);
    }

    [Fact]
    public void Calculate_ZeroConsumption_YieldsZeroBaseWithoutDivisionErrors()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            ConsumptionPerLapLitres = 0,
            RemainingLaps = 20
        });

        Assert.Equal(0.0, result.BaseFuelRequiredLitres!.Value, 6);
        Assert.Null(result.EstimatedLapsWithCurrentFuel);
        Assert.Equal(FuelPlanOutcome.Sufficient, result.Outcome);
    }

    [Fact]
    public void Calculate_EstimatedLapsWithCurrentFuel_UsesDivision()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 27,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20
        });

        Assert.Equal(10.0, result.EstimatedLapsWithCurrentFuel!.Value, 6);
    }

    [Fact]
    public void Calculate_NegativeCurrentFuel_IsRejectedAndReported()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = -1,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20
        });

        Assert.Equal(FuelPlanOutcome.Incomplete, result.Outcome);
        Assert.Null(result.FuelToAddLitres);
        Assert.Contains(result.Issues, i => i.Contains("Current fuel"));
    }

    [Fact]
    public void Calculate_NegativeConsumption_IsRejectedAndReported()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            ConsumptionPerLapLitres = -2.7,
            RemainingLaps = 20
        });

        Assert.Equal(FuelPlanOutcome.Incomplete, result.Outcome);
        Assert.Null(result.BaseFuelRequiredLitres);
        Assert.Contains(result.Issues, i => i.Contains("consumption", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Calculate_NonFiniteInputs_AreRejectedAndReported(double value)
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = value,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20
        });

        Assert.Equal(FuelPlanOutcome.Incomplete, result.Outcome);
        Assert.Contains(result.Issues, i => i.Contains("finite"));
    }

    [Fact]
    public void Calculate_MissingCurrentFuel_ProducesNoAddAmount()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20
        });

        Assert.Null(result.FuelToAddLitres);
        Assert.Equal(FuelPlanOutcome.Incomplete, result.Outcome);
        Assert.Contains(result.Issues, i => i.Contains("Current fuel"));
    }

    [Fact]
    public void Calculate_MissingConsumption_ReportsIncomplete()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            RemainingLaps = 20
        });

        Assert.Null(result.BaseFuelRequiredLitres);
        Assert.Equal(FuelPlanOutcome.Incomplete, result.Outcome);
        Assert.Contains(result.Issues, i => i.Contains("consumption", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Calculate_ExceedsTankCapacity_IsFlagged()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 40,
            TankCapacityLitres = 100
        });

        Assert.True(result.ExceedsTankCapacity);
        Assert.Contains(result.Issues, i => i.Contains("tank capacity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Calculate_WithinTankCapacity_IsNotFlagged()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20,
            TankCapacityLitres = 100
        });

        Assert.False(result.ExceedsTankCapacity);
    }

    [Fact]
    public void Calculate_UnknownTankCapacity_LeavesCapacityCheckNull()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 10,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20
        });

        Assert.Null(result.ExceedsTankCapacity);
    }

    [Fact]
    public void Calculate_StintPlanning_ReportsRequiredAndReserve()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 40,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20,
            StintLaps = 12
        });

        Assert.Equal(32.4, result.StintFuelRequiredLitres!.Value, 6);
        Assert.Equal(7.6, result.StintReserveAtEndLitres!.Value, 6);
        Assert.True(result.StintFuelSufficient);
    }

    [Fact]
    public void Calculate_StintNotCovered_ReportsInsufficient()
    {
        var result = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 20,
            ConsumptionPerLapLitres = 2.7,
            RemainingLaps = 20,
            StintLaps = 12
        });

        Assert.False(result.StintFuelSufficient);
        Assert.True(result.StintReserveAtEndLitres < 0);
    }

    [Fact]
    public void Calculate_ManualValues_ProduceSameResultAsTelemetryDerivedValues()
    {
        var manual = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 30,
            ConsumptionPerLapLitres = 2.6,
            RemainingLaps = 15,
            SafetyMarginLitres = 1
        });

        var telemetry = Calculator.Calculate(new FuelPlanInputs
        {
            CurrentFuelLitres = 30.0,
            ConsumptionPerLapLitres = 2.6,
            RemainingLaps = 15.0,
            SafetyMarginLitres = 1.0
        });

        Assert.Equal(manual.TotalFuelRequiredLitres, telemetry.TotalFuelRequiredLitres);
        Assert.Equal(manual.FuelToAddLitres, telemetry.FuelToAddLitres);
        Assert.Equal(manual.Outcome, telemetry.Outcome);
    }
}
