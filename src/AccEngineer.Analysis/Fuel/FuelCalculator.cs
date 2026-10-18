namespace AccEngineer.Analysis.Fuel;

/// <summary>
/// Pure, deterministic fuel calculations. No telemetry, clock or UI dependencies, so the
/// same engine can be unit-tested and reused by any front end.
/// </summary>
public sealed class FuelCalculator
{
    /// <summary>
    /// Computes a <see cref="FuelPlanResult"/> from the supplied <see cref="FuelPlanInputs"/>.
    /// Missing inputs are reported in <see cref="FuelPlanResult.Issues"/> rather than being
    /// silently replaced with defaults, and invalid (negative or non-finite) values are
    /// rejected and reported.
    /// </summary>
    public FuelPlanResult Calculate(FuelPlanInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var issues = new List<string>();

        double? current = Sanitize(inputs.CurrentFuelLitres, "Current fuel", issues);
        double? consumption = Sanitize(inputs.ConsumptionPerLapLitres, "Fuel consumption per lap", issues);
        double? remainingLaps = Sanitize(inputs.RemainingLaps, "Remaining laps", issues);
        double? stintLaps = Sanitize(inputs.StintLaps, "Stint laps", issues);
        double? reserve = Sanitize(inputs.ReserveLitres, "Reserve", issues);
        double? marginLitres = Sanitize(inputs.SafetyMarginLitres, "Safety margin (litres)", issues);
        double? marginPercent = Sanitize(inputs.SafetyMarginPercent, "Safety margin (percent)", issues);
        double? tankCapacity = Sanitize(inputs.TankCapacityLitres, "Tank capacity", issues);

        // Base race requirement: only meaningful when both consumption and remaining laps exist.
        double? baseFuelRequired = null;
        if (consumption is { } c && remainingLaps is { } l)
        {
            baseFuelRequired = c * l;
        }
        else
        {
            if (consumption is null)
            {
                issues.Add("Fuel consumption per lap is not available.");
            }

            if (remainingLaps is null)
            {
                issues.Add("Laps remaining is not available.");
            }
        }

        // Safety margin. A margin is optional, so absent values simply contribute nothing.
        double? safetyMargin = null;
        if (baseFuelRequired is { } baseFuel)
        {
            switch (inputs.SafetyMarginMode)
            {
                case SafetyMarginMode.Percentage:
                    safetyMargin = PercentOf(baseFuel, marginPercent);
                    break;
                case SafetyMarginMode.Both:
                    safetyMargin = (marginLitres ?? 0d) + PercentOf(baseFuel, marginPercent);
                    break;
                default:
                    safetyMargin = marginLitres ?? 0d;
                    break;
            }
        }

        double? totalFuelRequired = null;
        if (baseFuelRequired is { } b)
        {
            totalFuelRequired = b + (reserve ?? 0d) + (safetyMargin ?? 0d);
        }

        double? fuelToAdd = null;
        double? surplusOrShortfall = null;
        var outcome = FuelPlanOutcome.Incomplete;
        if (totalFuelRequired is { } total)
        {
            if (current is { } cur)
            {
                fuelToAdd = Math.Max(0d, total - cur);
                surplusOrShortfall = cur - total;
                outcome = cur + 1e-9 >= total ? FuelPlanOutcome.Sufficient : FuelPlanOutcome.Shortfall;
            }
            else
            {
                issues.Add("Current fuel is not available.");
            }
        }

        double? estimatedLapsWithCurrentFuel = null;
        if (current is { } curFuel && consumption is { } cc && cc > 0d)
        {
            estimatedLapsWithCurrentFuel = curFuel / cc;
        }

        double? stintFuelRequired = null;
        double? stintReserveAtEnd = null;
        bool? stintFuelSufficient = null;
        if (consumption is { } sc && stintLaps is { } sl)
        {
            stintFuelRequired = sc * sl;
            if (current is { } stintCurrent)
            {
                stintReserveAtEnd = stintCurrent - stintFuelRequired;
                stintFuelSufficient = stintCurrent + 1e-9 >= stintFuelRequired;
            }
        }

        bool? exceedsTankCapacity = null;
        if (tankCapacity is { } tank && totalFuelRequired is { } requiredTotal)
        {
            exceedsTankCapacity = requiredTotal > tank + 1e-9;
            if (exceedsTankCapacity == true)
            {
                issues.Add(
                    $"Required fuel ({FormatLitres(requiredTotal)}) exceeds the tank capacity ({FormatLitres(tank)}).");
            }
        }

        return new FuelPlanResult
        {
            BaseFuelRequiredLitres = baseFuelRequired,
            SafetyMarginLitres = safetyMargin,
            ReserveLitres = reserve ?? (baseFuelRequired is null ? null : 0d),
            TotalFuelRequiredLitres = totalFuelRequired,
            FuelToAddLitres = fuelToAdd,
            SurplusOrShortfallLitres = surplusOrShortfall,
            EstimatedLapsWithCurrentFuel = estimatedLapsWithCurrentFuel,
            StintFuelRequiredLitres = stintFuelRequired,
            StintReserveAtEndLitres = stintReserveAtEnd,
            StintFuelSufficient = stintFuelSufficient,
            ExceedsTankCapacity = exceedsTankCapacity,
            Outcome = outcome,
            Issues = issues
        };
    }

    private static double? PercentOf(double baseFuel, double? percent)
        => percent is { } p ? baseFuel * p / 100d : 0d;

    private static double? Sanitize(double? value, string label, List<string> issues)
    {
        if (value is not { } v)
        {
            return null;
        }

        if (double.IsNaN(v) || double.IsInfinity(v))
        {
            issues.Add($"{label} must be a finite number; the value was ignored.");
            return null;
        }

        if (v < 0d)
        {
            issues.Add($"{label} cannot be negative; the value was ignored.");
            return null;
        }

        return v;
    }

    private static string FormatLitres(double value) => $"{value:0.0} L";
}
