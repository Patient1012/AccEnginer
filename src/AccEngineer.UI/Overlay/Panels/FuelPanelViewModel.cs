using System.Globalization;
using AccEngineer.Analysis.Fuel;
using AccEngineer.Telemetry;
using AccEngineer.UI.Services;
using MarginMode = AccEngineer.Analysis.Fuel.SafetyMarginMode;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>Colour state for the fuel-status bar (Section A of the reference HUD).</summary>
public enum FuelBarState
{
    /// <summary>No usable estimate yet — draw a neutral, empty bar rather than implying sufficiency.</summary>
    Unknown,

    /// <summary>Current fuel comfortably covers the plan.</summary>
    Sufficient,

    /// <summary>Current fuel covers the plan but with little margin.</summary>
    Warning,

    /// <summary>Current fuel does not cover the plan.</summary>
    Shortfall
}

/// <summary>
/// Compact fuel panel modelled on a dense sim-racing fuel HUD. Four sections: the current
/// fuel and laps-remaining header with a coloured sufficiency bar; the predicted litres per
/// lap (with the session average as a muted comparison); the historical usage table
/// (last lap, recent window and session average, each with the laps it would sustain on the
/// current fuel); and the fuel required to finish with its surplus or shortfall. Every number
/// comes from the same deterministic <see cref="FuelCalculator"/> engine the Fuel Calculator
/// page uses, fed from live telemetry consumption and the user's persisted planning inputs;
/// missing data renders an explicit "collecting"/unavailable state rather than a guess.
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

    // ---- Section A: current fuel header -------------------------------------

    /// <summary>Current fuel in the car, in litres (the panel's most prominent value).</summary>
    public string CurrentFuelText { get; private set; } = Display.Missing;

    /// <summary>Estimated laps the current fuel still covers, to one decimal.</summary>
    public string LapsRemainingText { get; private set; } = Display.Missing;

    /// <summary>Fill fraction (0..1) of the status bar; 0 while the estimate is unknown.</summary>
    public double FuelBarFraction { get; private set; }

    /// <summary>Colour state driving the status bar's green → amber → red progression.</summary>
    public FuelBarState BarState { get; private set; } = FuelBarState.Unknown;

    // ---- Section B: predicted fuel per lap ----------------------------------

    /// <summary>The litres-per-lap figure the plan is built on (manual override or measured).</summary>
    public string PredictedPerLapText { get; private set; } = Display.Missing;

    /// <summary>Session average litres per lap, shown as the muted comparison. Empty when unknown.</summary>
    public string AveragePerLapText { get; private set; } = string.Empty;

    // ---- Section C: historical usage ----------------------------------------

    /// <summary>Most recent completed clean lap's consumption, "<c>x.xx</c>".</summary>
    public string LastLapUsageText { get; private set; } = Display.Missing;

    /// <summary>Laps the current fuel would cover at the last lap's rate.</summary>
    public string LastLapLapsText { get; private set; } = Display.Missing;

    /// <summary>Window label for the recent average, e.g. "<c>5 LAP</c>" or "<c>3 LAP</c>" when short.</summary>
    public string RecentLapLabelText { get; private set; } = "5 LAP";

    /// <summary>Recent-window average consumption, "<c>x.xx</c>".</summary>
    public string RecentLapUsageText { get; private set; } = Display.Missing;

    /// <summary>Laps the current fuel would cover at the recent-window rate.</summary>
    public string RecentLapLapsText { get; private set; } = Display.Missing;

    /// <summary>Session average consumption, "<c>x.xx</c>".</summary>
    public string AverageUsageText { get; private set; } = Display.Missing;

    /// <summary>Laps the current fuel would cover at the session-average rate.</summary>
    public string AverageLapsText { get; private set; } = Display.Missing;

    // ---- Section D: fuel required to finish ---------------------------------

    /// <summary>Total fuel required to finish the planned remaining laps, in litres.</summary>
    public string ToFinishText { get; private set; } = Display.Missing;

    /// <summary>
    /// Signed spare/short text ("<c>+6.4 L spare</c>" or "<c>-1.2 L short</c>"). Never says
    /// "spare" for a negative result; reads <see cref="Display.Missing"/> when unavailable.
    /// </summary>
    public string SpareText { get; private set; } = Display.Missing;

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
            LapsRemainingText = Display.Missing;
            FuelBarFraction = 0d;
            BarState = FuelBarState.Unknown;
            PredictedPerLapText = Display.Missing;
            AveragePerLapText = string.Empty;
            ResetUsageRows();
            ToFinishText = Display.Missing;
            SpareText = Display.Missing;
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

        // Predicted consumption: manual override (from persisted prefs) wins when positive,
        // otherwise the measured session average, otherwise the last measured lap.
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

        CurrentFuelText = currentFuel is { } current
            ? FormatFuel(current, consumptionPerLap)
            : Display.Missing;

        PredictedPerLapText = consumptionPerLap is { } rate
            ? FormatRateValue(rate) + " L"
            : Display.Missing;
        // A muted comparison only when it is a genuinely different, measured figure: the
        // session average. Omitted (empty) when unknown or when it is the predicted value.
        AveragePerLapText = consumption?.AverageLitresPerLap is { } avg
            && (consumptionPerLap is not { } predicted || Math.Abs(avg - predicted) > 1e-9)
            ? FormatRateValue(avg) + " L"
            : string.Empty;

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

        // Section A: laps the current fuel covers, and the sufficiency bar.
        LapsRemainingText = result.EstimatedLapsWithCurrentFuel is { } possible
            ? possible.ToString("0.0", CultureInfo.InvariantCulture) + " laps"
            : Display.Missing;

        BuildUsageRows(currentFuel, consumption);
        BuildFinishSection(result);

        IsSufficient = result.Outcome == FuelPlanOutcome.Sufficient;
        HasShortfall = result.Outcome == FuelPlanOutcome.Shortfall;

        StatusLineText = BuildStatusLine(result, consumptionPerLap is not null, remainingLaps is not null, consumptionPerLap);

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

    /// <summary>
    /// Section A's bar: how full it reads and whether it is green, amber or red. The bar
    /// reflects the ratio of fuel still needed to fuel held, so it never shows a full green
    /// bar when the calculation says the car is short. Unknown when either value is missing.
    /// </summary>
    private void BuildBar(FuelPlanResult result)
    {
        if (result.TotalFuelRequiredLitres is not { } required ||
            result.SurplusOrShortfallLitres is not { } margin)
        {
            FuelBarFraction = 0d;
            BarState = FuelBarState.Unknown;
            return;
        }

        if (required <= 0d)
        {
            FuelBarFraction = 1d;
            BarState = FuelBarState.Sufficient;
            return;
        }

        // Spare fraction: how much of the requirement is already held, beyond the
        // requirement itself, mapped onto 0..1 across a half-tank of headroom.
        var held = margin + required;
        var ratio = held / required;                       // 1.0 == exactly enough
        FuelBarFraction = Math.Clamp((ratio - 0.5) / 1.0, 0d, 1d);

        BarState = result.Outcome switch
        {
            FuelPlanOutcome.Shortfall => FuelBarState.Shortfall,
            FuelPlanOutcome.Sufficient => ratio >= 1.1 ? FuelBarState.Sufficient : FuelBarState.Warning,
            _ => FuelBarState.Unknown
        };
    }

    private void BuildUsageRows(double? currentFuel, FuelConsumptionSnapshot? consumption)
    {
        if (consumption is null)
        {
            ResetUsageRows();
            return;
        }

        LastLapUsageText = FormatRateValue(consumption.LastLapLitresPerLap);
        LastLapLapsText = FormatLapsOnFuel(currentFuel, consumption.LastLapLitresPerLap);

        var recentCount = consumption.RecentLapCount;
        RecentLapLabelText = recentCount > 0
            ? recentCount.ToString(CultureInfo.InvariantCulture) + " LAP"
            : FuelConsumptionTracker.RecentWindow.ToString(CultureInfo.InvariantCulture) + " LAP";
        RecentLapUsageText = FormatRateValue(consumption.RecentLitresPerLap);
        RecentLapLapsText = FormatLapsOnFuel(currentFuel, consumption.RecentLitresPerLap);

        AverageUsageText = FormatRateValue(consumption.AverageLitresPerLap);
        AverageLapsText = FormatLapsOnFuel(currentFuel, consumption.AverageLitresPerLap);
    }

    private void ResetUsageRows()
    {
        LastLapUsageText = Display.Missing;
        LastLapLapsText = Display.Missing;
        RecentLapLabelText = FuelConsumptionTracker.RecentWindow.ToString(CultureInfo.InvariantCulture) + " LAP";
        RecentLapUsageText = Display.Missing;
        RecentLapLapsText = Display.Missing;
        AverageUsageText = Display.Missing;
        AverageLapsText = Display.Missing;
    }

    /// <summary>
    /// Section D: fuel required to finish the planned laps, and the surplus/shortfall. The
    /// margin never reads "spare" when it is negative — it reads "short" instead, and an
    /// unavailable plan reads the explicit missing glyph.
    /// </summary>
    private void BuildFinishSection(FuelPlanResult result)
    {
        ToFinishText = result.TotalFuelRequiredLitres is { } required
            ? FormatLitresValue(required)
            : Display.Missing;

        if (result.SurplusOrShortfallLitres is not { } margin)
        {
            SpareText = Display.Missing;
        }
        else if (margin >= 0d)
        {
            SpareText = "+" + FormatLitresValue(margin) + " spare";
        }
        else
        {
            SpareText = FormatLitresValue(margin) + " short";
        }

        BuildBar(result);
    }

    private string BuildStatusLine(
        FuelPlanResult result, bool hasConsumption, bool hasRemainingLaps, double? litresPerLap)
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
                ? $"Add {FormatFuel(add, litresPerLap)} to meet the planned requirement."
                : "Fuel is sufficient for the planned requirement.";
        }

        if (result.Outcome == FuelPlanOutcome.Shortfall)
        {
            return result.FuelToAddLitres is { } add
                ? $"Short {FormatFuel(add, litresPerLap)} — add to meet the planned requirement."
                : "Fuel does not cover the planned requirement.";
        }

        return "Fuel plan incomplete — check the Fuel Calculator inputs.";
    }

    /// <summary>A litre quantity as "<c>x.x L</c>", or the missing glyph.</summary>
    private static string FormatLitresValue(double? litres) =>
        litres is { } value ? value.ToString("0.0", CultureInfo.InvariantCulture) + " L" : Display.Missing;

    /// <summary>
    /// Formats a litre quantity in the active display unit: "<c>x.x L</c>" in litres mode,
    /// or a converted "<c>x.x laps</c>" in laps mode (missing when the rate is unknown).
    /// </summary>
    private string FormatFuel(double litres, double? litresPerLap) =>
        Display.FuelQuantity(litres, LapsMode, litresPerLap);

    /// <summary>A bare consumption figure as "<c>x.xx</c>", or the missing glyph.</summary>
    private static string FormatRateValue(double? litresPerLap) =>
        litresPerLap is { } value ? value.ToString("0.00", CultureInfo.InvariantCulture) : Display.Missing;

    /// <summary>
    /// How many whole-tenths of a lap the held fuel covers at a given rate — the usage
    /// table's right-hand column. Missing when either value is unusable.
    /// </summary>
    private static string FormatLapsOnFuel(double? currentFuel, double? litresPerLap)
    {
        if (currentFuel is not { } fuel || litresPerLap is not { } perLap ||
            !double.IsFinite(perLap) || perLap <= 0d)
        {
            return Display.Missing;
        }

        return (fuel / perLap).ToString("0.0", CultureInfo.InvariantCulture);
    }

    private bool LapsMode =>
        string.Equals(_options?.FuelCalculatorDisplayUnit, "Laps", StringComparison.OrdinalIgnoreCase);
}
