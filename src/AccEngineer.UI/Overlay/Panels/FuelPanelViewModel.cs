using System.Globalization;
using AccEngineer.Analysis.Fuel;
using AccEngineer.Telemetry;
using AccEngineer.UI.Services;
using MarginMode = AccEngineer.Analysis.Fuel.SafetyMarginMode;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Compact fuel panel: the two figures a driver actually needs mid-stint — how much
/// fuel to add to finish the planned remaining laps, and how many laps the current
/// fuel still covers — plus current fuel, measured litres-per-lap and an honest
/// source indicator. The numbers come from the same deterministic
/// <see cref="FuelCalculator"/> engine the Fuel Calculator page uses, fed from live
/// telemetry consumption and the user's persisted planning inputs; missing data renders
/// an explicit "collecting"/unavailable state rather than a guessed value.
/// </summary>
public sealed class FuelPanelViewModel : OverlayPanelViewModel
{
    private readonly FuelCalculator _calculator = new();
    private readonly AppSettings? _options;

    public FuelPanelViewModel(IOverlayTelemetrySource telemetry, AppSettings? options = null)
        : base(telemetry, 100)
    {
        _options = options;
        // The base constructor already ran one synchronous tick before _options was
        // assigned, so refresh once more with the planning inputs in place.
        Refresh(telemetry.Latest);
    }

    public bool IsLive { get; private set; }

    /// <summary>Status dot text: LIVE / NO SIGNAL.</summary>
    public string StatusText { get; private set; } = "NO SIGNAL";

    /// <summary>Honest source label: LIVE TELEMETRY / TEST DATA / MANUAL INPUT.</summary>
    public string SourceText { get; private set; } = "MANUAL INPUT";

    /// <summary>Fuel currently in the car, in litres.</summary>
    public string CurrentFuelText { get; private set; } = Display.Missing;

    /// <summary>Measured/average fuel consumption, in litres per lap.</summary>
    public string ConsumptionText { get; private set; } = Display.Missing;

    /// <summary>Prominent result: litres to add to meet the planned requirement.</summary>
    public string FuelToAddText { get; private set; } = Display.Missing;

    /// <summary>Estimated laps the current fuel still covers.</summary>
    public string LapsPossibleText { get; private set; } = Display.Missing;

    /// <summary>Plain-language recommendation / insufficiency warning.</summary>
    public string StatusLineText { get; private set; } =
        "Collecting fuel data — enter consumption manually or complete another lap.";

    /// <summary>True when the current fuel already covers the planned requirement.</summary>
    public bool IsSufficient { get; private set; }

    /// <summary>True when a real shortfall exists (drives the amber warning colour).</summary>
    public bool HasShortfall { get; private set; }

    protected override void Refresh(TelemetrySample? sample)
    {
        var consumption = Telemetry.FuelConsumption;

        if (sample is null)
        {
            IsLive = false;
            StatusText = "NO SIGNAL";
            CurrentFuelText = Display.Missing;
            ConsumptionText = Display.Missing;
            FuelToAddText = Display.Missing;
            LapsPossibleText = Display.Missing;
            StatusLineText = "No telemetry — fuel planning is unavailable until a session is live.";
            IsSufficient = false;
            HasShortfall = false;
            SourceText = Telemetry.IsTestData ? "TEST DATA" : "MANUAL INPUT";
            OnAllPropertiesChanged();
            return;
        }

        IsLive = true;
        StatusText = "LIVE";

        var currentFuel = sample.FuelLevel is { } fuel ? Math.Max(0d, fuel) : (double?)null;
        CurrentFuelText = currentFuel is { } current ? FormatL(current) : Display.Missing;

        // Consumption: manual override (from persisted prefs) wins when positive,
        // otherwise the measured average, otherwise the last measured lap.
        var manualConsumption = _options?.FuelCalculatorManualConsumptionPerLap;
        double? consumptionPerLap;
        if (manualConsumption is > 0d)
        {
            consumptionPerLap = manualConsumption;
        }
        else if (consumption?.AverageLitresPerLap is { } average)
        {
            consumptionPerLap = average;
        }
        else if (consumption?.LastLapLitresPerLap is { } lastLap)
        {
            consumptionPerLap = lastLap;
        }
        else
        {
            consumptionPerLap = null;
        }

        ConsumptionText = consumptionPerLap is { } perLap
            ? perLap.ToString("0.00", CultureInfo.InvariantCulture) + " L/lap"
            : Display.Missing;

        // Remaining laps: manual override wins, otherwise lap-limited session total,
        // otherwise (when the user opted in) the time-limited estimate.
        double? remainingLaps;
        if (_options?.FuelCalculatorManualRemainingLaps is { } manualLaps && manualLaps >= 0)
        {
            remainingLaps = manualLaps;
        }
        else if (sample.TotalLaps is { } totalLaps && totalLaps > 0 && sample.LapNumber >= 0)
        {
            remainingLaps = FuelLapEstimator.RemainingRaceLaps(totalLaps, sample.LapNumber);
        }
        else if (_options?.FuelCalculatorPreferTimeLimitedEstimate == true &&
                 sample.SessionTimeLeftSeconds is { } timeLeft &&
                 consumption?.AverageLapSeconds is { } avgLap)
        {
            remainingLaps = FuelLapEstimator.EstimateRemainingLapsFromTime(timeLeft, avgLap);
        }
        else
        {
            remainingLaps = null;
        }

        var marginMode = _options?.FuelCalculatorSafetyMarginMode switch
        {
            "Percentage" => MarginMode.Percentage,
            "Both" => MarginMode.Both,
            _ => MarginMode.Litres
        };

        var tankCapacity = sample.TankCapacityLitres is { } tank && tank > 0 ? tank : (double?)null;

        var inputs = new FuelPlanInputs
        {
            CurrentFuelLitres = currentFuel,
            ConsumptionPerLapLitres = consumptionPerLap,
            RemainingLaps = remainingLaps,
            StintLaps = _options?.FuelCalculatorStintLaps is { } stint && stint > 0 ? stint : null,
            ReserveLitres = _options?.FuelCalculatorLastReserveLitres,
            SafetyMarginMode = marginMode,
            SafetyMarginLitres = marginMode is MarginMode.Litres or MarginMode.Both
                ? _options?.FuelCalculatorLastSafetyMarginLitres
                : null,
            SafetyMarginPercent = marginMode is MarginMode.Percentage or MarginMode.Both
                ? _options?.FuelCalculatorLastSafetyMarginPercent
                : null,
            TankCapacityLitres = tankCapacity
        };

        var result = _calculator.Calculate(inputs);

        FuelToAddText = result.FuelToAddLitres is { } toAdd
            ? FormatL(toAdd)
            : Display.Missing;

        LapsPossibleText = result.EstimatedLapsWithCurrentFuel is { } possible
            ? possible.ToString("0.#", CultureInfo.InvariantCulture) + " laps"
            : Display.Missing;

        IsSufficient = result.Outcome == FuelPlanOutcome.Sufficient;
        HasShortfall = result.Outcome == FuelPlanOutcome.Shortfall;

        StatusLineText = BuildStatusLine(result, consumptionPerLap is not null, remainingLaps is not null);

        // Honest source label.
        if (Telemetry.IsTestData)
        {
            SourceText = "TEST DATA";
        }
        else if (manualConsumption is > 0d || _options?.FuelCalculatorManualRemainingLaps is not null)
        {
            SourceText = "MANUAL INPUT";
        }
        else if (IsLive)
        {
            SourceText = "LIVE TELEMETRY";
        }
        else
        {
            SourceText = "MANUAL INPUT";
        }

        OnAllPropertiesChanged();
    }

    private static string BuildStatusLine(FuelPlanResult result, bool hasConsumption, bool hasRemainingLaps)
    {
        if (!hasConsumption)
        {
            return "Collecting fuel data — no reliable litres-per-lap yet.";
        }

        if (result.Issues.Count > 0)
        {
            return string.Join(" ", result.Issues);
        }

        if (!hasRemainingLaps)
        {
            return "Remaining laps unknown — set them in the Fuel Calculator.";
        }

        if (result.ExceedsTankCapacity == true)
        {
            return "Required fuel exceeds the tank capacity — plan a shorter stint.";
        }

        if (result.Outcome == FuelPlanOutcome.Sufficient)
        {
            return result.FuelToAddLitres is { } add && add > 0
                ? $"Add {FormatL(add)} to meet the planned requirement."
                : "Fuel is sufficient for the planned requirement.";
        }

        if (result.Outcome == FuelPlanOutcome.Shortfall)
        {
            return result.FuelToAddLitres is { } add
                ? $"Short {FormatL(add)} — add to meet the planned requirement."
                : "Fuel does not cover the planned requirement.";
        }

        return "Fuel plan incomplete — check the Fuel Calculator inputs.";
    }

    private static string FormatL(double litres) =>
        litres.ToString("0.0", CultureInfo.InvariantCulture) + " L";
}
