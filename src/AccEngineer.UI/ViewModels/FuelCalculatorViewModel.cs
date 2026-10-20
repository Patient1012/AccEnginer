using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using AccEngineer.Analysis.Fuel;
using AccEngineer.UI.Mvvm;
using AccEngineer.UI.Services;
using AccEngineer.Telemetry;
using MarginMode = AccEngineer.Analysis.Fuel.SafetyMarginMode;

namespace AccEngineer.UI.ViewModels;

/// <summary>
/// Fuel planning page. Combines the deterministic <see cref="FuelCalculator"/> engine
/// with the live per-lap consumption measurements from <see cref="LiveTelemetryService"/>
/// and any manual overrides the operator enters.
/// </summary>
public sealed class FuelCalculatorViewModel : PageViewModel
{
    // A compact fuel-pump glyph. The app ships no icon library, so the one navigation
    // icon in the product is an inline path; pages without an icon stay text-only.
    private static readonly Geometry FuelPumpGeometry = Geometry.Parse(
        "M6,22V4C6,2.9 6.9,2 8,2H14C15.1,2 16,2.9 16,4V13H18L20,16V22H6M8,4V13H14V4H8M8,20V17H10V20H8M14,20V17H16V20H14M17,10H19L18.5,9.5V7.5L19,7H20V10H17V10Z");

    private readonly LiveTelemetryService _telemetry;
    private readonly AppSettings _options;
    private readonly FuelCalculator _calculator = new();

    private double _currentFuelLitres;
    private bool _currentFuelOverridden;
    private double? _manualConsumptionPerLap;
    private double? _manualRemainingLaps;
    private double _reserveLitres = 1.0;
    private int _stintLaps;
    private string _safetyMarginMode = "Litres";
    private double _safetyMarginLitres;
    private double _safetyMarginPercent;
    private bool _preferTimeLimitedEstimate;
    private string _displayUnit = "Litres";

    private string _statusText = "Insufficient fuel data — enter consumption manually or complete another lap.";
    private string _dataSourceText = "MANUAL INPUT";
    private bool _isLiveData;
    private bool _isTestData;
    private string _currentFuelText = Display.Missing;
    private string _consumptionText = Display.Missing;
    private string _remainingRaceLapsText = Display.Missing;
    private string _avgLapSecondsText = Display.Missing;
    private string _validLapCountText = Display.Missing;
    private string _confidenceText = "Insufficient";
    private string _tankCapacityText = Display.Missing;
    private string _baseFuelRequiredText = Display.Missing;
    private string _safetyMarginAppliedText = Display.Missing;
    private string _reserveAppliedText = Display.Missing;
    private string _totalFuelRequiredText = Display.Missing;
    private string _fuelToAddText = Display.Missing;
    private string _surplusShortfallText = Display.Missing;
    private string _lapsPossibleText = Display.Missing;
    private string _stintFuelRequiredText = Display.Missing;
    private string _stintReserveAtEndText = Display.Missing;
    private string _stintSufficiencyText = Display.Missing;
    private string _tankCapacityWarningText = string.Empty;
    private string _breakdownText = string.Empty;

    public FuelCalculatorViewModel(LiveTelemetryService telemetry, AppSettings options)
        : base("Fuel Calculator", FuelPumpGeometry)
    {
        _telemetry = telemetry;
        _options = options;

        _reserveLitres = _options.FuelCalculatorLastReserveLitres ?? 1.0;
        _safetyMarginMode = _options.FuelCalculatorSafetyMarginMode ?? "Litres";
        _safetyMarginLitres = _options.FuelCalculatorLastSafetyMarginLitres ?? 0.0;
        _safetyMarginPercent = _options.FuelCalculatorLastSafetyMarginPercent ?? 0.0;
        _manualConsumptionPerLap = _options.FuelCalculatorManualConsumptionPerLap;
        _manualRemainingLaps = _options.FuelCalculatorManualRemainingLaps;
        _stintLaps = _options.FuelCalculatorStintLaps ?? 0;
        _preferTimeLimitedEstimate = _options.FuelCalculatorPreferTimeLimitedEstimate;
        _displayUnit = NormalizeUnit(_options.FuelCalculatorDisplayUnit);

        SaveCommand = new RelayCommand(SaveAndRecalculate);
        ResetLiveFuelCommand = new RelayCommand(() =>
        {
            CurrentFuelOverridden = false;
            Recalculate();
        });
    }

    public ICommand SaveCommand { get; }

    public ICommand ResetLiveFuelCommand { get; }

    public double CurrentFuelLitres
    {
        get => _currentFuelLitres;
        set
        {
            if (SetProperty(ref _currentFuelLitres, Math.Max(0d, value)))
            {
                CurrentFuelOverridden = true;
                Recalculate();
            }
        }
    }

    /// <summary>
    /// True once the operator has typed a current-fuel value; live telemetry then stops
    /// overwriting the box until it is released again.
    /// </summary>
    public bool CurrentFuelOverridden
    {
        get => _currentFuelOverridden;
        private set
        {
            if (SetProperty(ref _currentFuelOverridden, value))
            {
                OnPropertyChanged(nameof(CurrentFuelIsManual));
            }
        }
    }

    public bool CurrentFuelIsManual => CurrentFuelOverridden;

    public string CurrentFuelText
    {
        get => _currentFuelText;
        private set => SetProperty(ref _currentFuelText, value);
    }

    /// <summary>Manual consumption override in litres per lap; empty means "use telemetry".</summary>
    public double? ManualConsumptionPerLap
    {
        get => _manualConsumptionPerLap;
        set
        {
            if (SetProperty(ref _manualConsumptionPerLap, value))
            {
                OnPropertyChanged(nameof(ManualConsumptionText));
                Recalculate();
            }
        }
    }

    /// <summary>
    /// Text view of <see cref="ManualConsumptionPerLap"/> for the page's input box. Binding the
    /// box to a string (rather than to the double) is what lets the operator type a fractional
    /// value such as <c>2.14</c>; parsing a partially typed "2." into a double would otherwise
    /// reject the decimal separator as it is pressed. An unparseable or empty entry maps to a
    /// null override so the engine falls back to the measured average.
    /// </summary>
    public string ManualConsumptionText
    {
        get => _manualConsumptionPerLap is { } value
            ? value.ToString(CultureInfo.CurrentCulture)
            : string.Empty;
        set
        {
            double? parsed = null;
            var text = value?.Trim();
            if (!string.IsNullOrEmpty(text)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var typed))
            {
                parsed = typed;
            }

            if (Nullable.Equals(_manualConsumptionPerLap, parsed))
            {
                return;
            }

            _manualConsumptionPerLap = parsed;
            OnPropertyChanged(nameof(ManualConsumptionPerLap));
            Recalculate();
        }
    }

    /// <summary>Manual remaining-laps override; empty means "use session information".</summary>
    public double? ManualRemainingLaps
    {
        get => _manualRemainingLaps;
        set
        {
            if (SetProperty(ref _manualRemainingLaps, value))
            {
                Recalculate();
            }
        }
    }

    public int StintLaps
    {
        get => _stintLaps;
        set
        {
            if (SetProperty(ref _stintLaps, Math.Max(0, value)))
            {
                Recalculate();
            }
        }
    }

    public double ReserveLitres
    {
        get => _reserveLitres;
        set
        {
            if (SetProperty(ref _reserveLitres, Math.Max(0d, value)))
            {
                Recalculate();
            }
        }
    }

    public string SafetyMarginMode
    {
        get => _safetyMarginMode;
        set
        {
            if (SetProperty(ref _safetyMarginMode, value ?? "Litres"))
            {
                OnPropertyChanged(nameof(SafetyMarginLitresEnabled));
                OnPropertyChanged(nameof(SafetyMarginPercentEnabled));
                Recalculate();
            }
        }
    }

    public bool SafetyMarginLitresEnabled => SafetyMarginMode is "Litres" or "Both";

    public bool SafetyMarginPercentEnabled => SafetyMarginMode is "Percentage" or "Both";

    public double SafetyMarginLitres
    {
        get => _safetyMarginLitres;
        set
        {
            if (SetProperty(ref _safetyMarginLitres, Math.Max(0d, value)))
            {
                Recalculate();
            }
        }
    }

    public double SafetyMarginPercent
    {
        get => _safetyMarginPercent;
        set
        {
            if (SetProperty(ref _safetyMarginPercent, Math.Max(0d, value)))
            {
                Recalculate();
            }
        }
    }

    public bool PreferTimeLimitedEstimate
    {
        get => _preferTimeLimitedEstimate;
        set
        {
            if (SetProperty(ref _preferTimeLimitedEstimate, value))
            {
                Recalculate();
            }
        }
    }

    /// <summary>
    /// Display unit for fuel quantities: "Litres" or "Laps". The engine always computes in
    /// litres; in laps mode the results are divided by the effective litres-per-lap.
    /// </summary>
    public string DisplayUnit
    {
        get => _displayUnit;
        set
        {
            if (SetProperty(ref _displayUnit, NormalizeUnit(value)))
            {
                OnPropertyChanged(nameof(LapsMode));
                Recalculate();
            }
        }
    }

    /// <summary>True when results are shown as a lap count rather than litres.</summary>
    public bool LapsMode => _displayUnit == "Laps";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string DataSourceText
    {
        get => _dataSourceText;
        private set => SetProperty(ref _dataSourceText, value);
    }

    public bool IsLiveData
    {
        get => _isLiveData;
        private set => SetProperty(ref _isLiveData, value);
    }

    public bool IsTestData
    {
        get => _isTestData;
        private set => SetProperty(ref _isTestData, value);
    }

    public string ConsumptionText
    {
        get => _consumptionText;
        private set => SetProperty(ref _consumptionText, value);
    }

    public string RemainingRaceLapsText
    {
        get => _remainingRaceLapsText;
        private set => SetProperty(ref _remainingRaceLapsText, value);
    }

    public string AvgLapSecondsText
    {
        get => _avgLapSecondsText;
        private set => SetProperty(ref _avgLapSecondsText, value);
    }

    public string ValidLapCountText
    {
        get => _validLapCountText;
        private set => SetProperty(ref _validLapCountText, value);
    }

    public string ConfidenceText
    {
        get => _confidenceText;
        private set => SetProperty(ref _confidenceText, value);
    }

    public string TankCapacityText
    {
        get => _tankCapacityText;
        private set => SetProperty(ref _tankCapacityText, value);
    }

    public string BaseFuelRequiredText
    {
        get => _baseFuelRequiredText;
        private set => SetProperty(ref _baseFuelRequiredText, value);
    }

    public string SafetyMarginAppliedText
    {
        get => _safetyMarginAppliedText;
        private set => SetProperty(ref _safetyMarginAppliedText, value);
    }

    public string ReserveAppliedText
    {
        get => _reserveAppliedText;
        private set => SetProperty(ref _reserveAppliedText, value);
    }

    public string TotalFuelRequiredText
    {
        get => _totalFuelRequiredText;
        private set => SetProperty(ref _totalFuelRequiredText, value);
    }

    public string FuelToAddText
    {
        get => _fuelToAddText;
        private set => SetProperty(ref _fuelToAddText, value);
    }

    public string SurplusShortfallText
    {
        get => _surplusShortfallText;
        private set => SetProperty(ref _surplusShortfallText, value);
    }

    public string LapsPossibleText
    {
        get => _lapsPossibleText;
        private set => SetProperty(ref _lapsPossibleText, value);
    }

    public string StintFuelRequiredText
    {
        get => _stintFuelRequiredText;
        private set => SetProperty(ref _stintFuelRequiredText, value);
    }

    public string StintReserveAtEndText
    {
        get => _stintReserveAtEndText;
        private set => SetProperty(ref _stintReserveAtEndText, value);
    }

    public string StintSufficiencyText
    {
        get => _stintSufficiencyText;
        private set => SetProperty(ref _stintSufficiencyText, value);
    }

    public string TankCapacityWarningText
    {
        get => _tankCapacityWarningText;
        private set => SetProperty(ref _tankCapacityWarningText, value);
    }

    public string BreakdownText
    {
        get => _breakdownText;
        private set => SetProperty(ref _breakdownText, value);
    }

    public override void Refresh() => Recalculate();

    private void SaveAndRecalculate()
    {
        PersistPreferences();
        _options.Save();
        Recalculate();
    }

    private void PersistPreferences()
    {
        _options.FuelCalculatorLastReserveLitres = _reserveLitres;
        _options.FuelCalculatorSafetyMarginMode = _safetyMarginMode;
        _options.FuelCalculatorLastSafetyMarginLitres = _safetyMarginLitres;
        _options.FuelCalculatorLastSafetyMarginPercent = _safetyMarginPercent;
        _options.FuelCalculatorManualConsumptionPerLap = _manualConsumptionPerLap;
        _options.FuelCalculatorManualRemainingLaps = _manualRemainingLaps;
        _options.FuelCalculatorStintLaps = _stintLaps;
        _options.FuelCalculatorPreferTimeLimitedEstimate = _preferTimeLimitedEstimate;
        _options.FuelCalculatorDisplayUnit = _displayUnit;
    }

    private static string NormalizeUnit(string? unit) =>
        string.Equals(unit, "Laps", StringComparison.OrdinalIgnoreCase) ? "Laps" : "Litres";

    private void Recalculate()
    {
        var sample = _telemetry.Latest;
        var consumption = _telemetry.FuelConsumption;

        IsTestData = _telemetry.IsTestData;
        IsLiveData = !IsTestData && _telemetry.Connection == ConnectionState.Connected && sample is not null;

        // Current fuel: live telemetry fills the box unless the operator has typed over it.
        if (sample?.FuelLevel is { } liveFuel && !CurrentFuelOverridden)
        {
            _currentFuelLitres = Math.Max(0d, liveFuel);
            OnPropertyChanged(nameof(CurrentFuelLitres));
        }

        // Consumption: a manual override always wins when > 0; zero is treated as "no
        // override" and falls through to telemetry; negative values are passed to
        // the engine so its validation can reject them with a clear message.
        double? consumptionPerLap;
        string consumptionSource;
        if (_manualConsumptionPerLap is { } manual)
        {
            if (manual > 0d)
            {
                consumptionPerLap = manual;
                consumptionSource = "manual";
            }
            else if (manual < 0d)
            {
                consumptionPerLap = manual;
                consumptionSource = "manual (invalid)";
            }
            else
            {
                consumptionPerLap = null;
                consumptionSource = "unavailable";
            }
        }
        else if (consumption.AverageLitresPerLap is { } average)
        {
            consumptionPerLap = average;
            consumptionSource = "measured average";
        }
        else if (consumption.LastLapLitresPerLap is { } lastLap)
        {
            consumptionPerLap = lastLap;
            consumptionSource = "last lap";
        }
        else
        {
            consumptionPerLap = null;
            consumptionSource = "unavailable";
        }

        ConsumptionText = consumptionPerLap is { } c
            ? $"{c.ToString("0.00", CultureInfo.InvariantCulture)} L/lap ({consumptionSource})"
            : Display.Missing;

        // Current fuel now that the effective consumption is known (needed for laps mode).
        var enteredSuffix = CurrentFuelOverridden ? " (entered)" : null;
        CurrentFuelText = LapsMode
            ? Display.FuelQuantity(_currentFuelLitres, true, consumptionPerLap) + (enteredSuffix ?? string.Empty)
            : FormatL(_currentFuelLitres, enteredSuffix);
        ValidLapCountText = consumption.ValidLapCount.ToString(CultureInfo.InvariantCulture);
        ConfidenceText = consumption.Confidence switch
        {
            FuelDataConfidence.High => "High",
            FuelDataConfidence.Medium => "Medium",
            FuelDataConfidence.Low => "Low",
            _ => "Collecting fuel data"
        };
        AvgLapSecondsText = consumption.AverageLapSeconds is { } lapSeconds
            ? lapSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s"
            : Display.Missing;

        double? tankCapacity = sample?.TankCapacityLitres is { } tank && tank > 0 ? tank : null;
        TankCapacityText = tankCapacity is { } t
            ? Display.FuelQuantity(t, LapsMode, consumptionPerLap)
            : Display.Missing;

        // Remaining laps. A manual override wins when >= 0; negative values are passed
        // to the engine so its validation can reject them with a clear message.
        double? remainingLaps;
        string remainingSource;
        if (_manualRemainingLaps is { } manualLaps)
        {
            if (manualLaps >= 0)
            {
                remainingLaps = manualLaps;
                remainingSource = "manual";
            }
            else
            {
                remainingLaps = manualLaps;
                remainingSource = "manual (invalid)";
            }
        }
        else if (sample?.TotalLaps is { } totalLaps && totalLaps > 0 && sample.LapNumber >= 0)
        {
            remainingLaps = FuelLapEstimator.RemainingRaceLaps(totalLaps, sample.LapNumber);
            remainingSource = "session";
        }
        else if (_preferTimeLimitedEstimate &&
                 sample?.SessionTimeLeftSeconds is { } timeLeft &&
                 consumption.AverageLapSeconds is { } avgLap)
        {
            remainingLaps = FuelLapEstimator.EstimateRemainingLapsFromTime(timeLeft, avgLap);
            remainingSource = "estimate";
        }
        else
        {
            remainingLaps = null;
            remainingSource = "unavailable";
        }

        RemainingRaceLapsText = remainingLaps is { } laps
            ? $"{laps.ToString("0.#", CultureInfo.InvariantCulture)} laps ({remainingSource})"
            : Display.Missing;

        // Run the deterministic engine with exactly what we have.
        var marginMode = _safetyMarginMode switch
        {
            "Percentage" => MarginMode.Percentage,
            "Both" => MarginMode.Both,
            _ => MarginMode.Litres
        };

        var inputs = new FuelPlanInputs
        {
            CurrentFuelLitres = CurrentFuelOverridden || sample?.FuelLevel is not null ? _currentFuelLitres : null,
            ConsumptionPerLapLitres = consumptionPerLap,
            RemainingLaps = remainingLaps,
            StintLaps = _stintLaps > 0 ? _stintLaps : null,
            ReserveLitres = _reserveLitres,
            SafetyMarginMode = marginMode,
            SafetyMarginLitres = marginMode is MarginMode.Litres or MarginMode.Both ? _safetyMarginLitres : null,
            SafetyMarginPercent = marginMode is MarginMode.Percentage or MarginMode.Both ? _safetyMarginPercent : null,
            TankCapacityLitres = tankCapacity
        };

        var result = _calculator.Calculate(inputs);

        BaseFuelRequiredText = FormatFuelResult(result.BaseFuelRequiredLitres, consumptionPerLap);
        SafetyMarginAppliedText = FormatFuelResult(result.SafetyMarginLitres, consumptionPerLap);
        ReserveAppliedText = FormatFuelResult(result.ReserveLitres, consumptionPerLap);
        TotalFuelRequiredText = FormatFuelResult(result.TotalFuelRequiredLitres, consumptionPerLap);
        FuelToAddText = result.FuelToAddLitres is { } toAdd
            ? Display.FuelQuantity(toAdd, LapsMode, consumptionPerLap)
            : Display.Missing;
        SurplusShortfallText = Display.FuelSigned(result.SurplusOrShortfallLitres, LapsMode, consumptionPerLap);
        LapsPossibleText = result.EstimatedLapsWithCurrentFuel is { } possible
            ? $"{possible.ToString("0.#", CultureInfo.InvariantCulture)} laps"
            : Display.Missing;
        StintFuelRequiredText = FormatFuelResult(result.StintFuelRequiredLitres, consumptionPerLap);
        StintReserveAtEndText = FormatFuelResult(result.StintReserveAtEndLitres, consumptionPerLap);
        StintSufficiencyText = result.StintFuelSufficient switch
        {
            true => "Sufficient",
            false => "Short",
            null => Display.Missing
        };
        TankCapacityWarningText = result.ExceedsTankCapacity == true
            ? "Required fuel exceeds the tank capacity; the stint cannot be loaded in one stop."
            : string.Empty;

        BreakdownText = BuildBreakdown(result);

        // Plain-language recommendation, driven only by the actual result. Missing
        // inputs never produce a reassuring message.
        StatusText = BuildStatus(result, consumptionPerLap, remainingLaps);

        // Honest source label: LIVE TELEMETRY only when the figures really are live.
        if (IsLiveData && !CurrentFuelOverridden && _manualConsumptionPerLap is null && consumption.HasSufficientData)
        {
            DataSourceText = "LIVE TELEMETRY";
        }
        else if (IsTestData && !CurrentFuelOverridden && _manualConsumptionPerLap is null)
        {
            DataSourceText = "TEST DATA";
        }
        else if (CurrentFuelOverridden || _manualConsumptionPerLap is not null || _manualRemainingLaps is not null)
        {
            DataSourceText = "MANUAL INPUT";
        }
        else
        {
            DataSourceText = IsLiveData ? "LIVE TELEMETRY" : IsTestData ? "TEST DATA" : "MANUAL INPUT";
        }
    }

    private static string BuildBreakdown(FuelPlanResult result)
    {
        if (result.BaseFuelRequiredLitres is not { } baseFuel)
        {
            return string.Empty;
        }

        var parts = new List<string>(4)
        {
            $"{baseFuel.ToString("0.0", CultureInfo.InvariantCulture)} L base"
        };

        if (result.SafetyMarginLitres is { } margin && margin > 0)
        {
            parts.Add($"{margin.ToString("0.0", CultureInfo.InvariantCulture)} L margin");
        }

        if (result.ReserveLitres is { } reserve && reserve > 0)
        {
            parts.Add($"{reserve.ToString("0.0", CultureInfo.InvariantCulture)} L reserve");
        }

        if (result.TotalFuelRequiredLitres is { } total)
        {
            parts.Add($"= {total.ToString("0.0", CultureInfo.InvariantCulture)} L total");
        }

        return string.Join(" + ", parts.Take(parts.Count - 1)) + (parts.Count > 1 ? parts[^1] : string.Empty);
    }

    private static string BuildStatus(FuelPlanResult result, double? consumptionPerLap, double? remainingLaps)
    {
        if (result.Issues.Count > 0)
        {
            return string.Join(" ", result.Issues);
        }

        if (consumptionPerLap is null)
        {
            return "Insufficient valid fuel data — enter consumption manually or complete another lap.";
        }

        if (remainingLaps is null)
        {
            return "Remaining laps are unknown — enter them manually or enable the time-limited estimate.";
        }

        if (result.ExceedsTankCapacity == true)
        {
            return "Required fuel exceeds the tank capacity; plan a shorter stint.";
        }

        if (result.Outcome == FuelPlanOutcome.Sufficient)
        {
            return result.FuelToAddLitres is { } add && add > 0
                ? $"Add approximately {add.ToString("0.0", CultureInfo.InvariantCulture)} L to meet the planned requirement."
                : "Current fuel is sufficient for the planned requirement.";
        }

        if (result.Outcome == FuelPlanOutcome.Shortfall)
        {
            return result.FuelToAddLitres is { } shortAdd
                ? $"Add approximately {shortAdd.ToString("0.0", CultureInfo.InvariantCulture)} L to meet the planned requirement."
                : "Current fuel does not cover the planned requirement.";
        }

        return "Insufficient fuel data — enter consumption manually or complete another lap.";
    }

    private static string FormatL(double litres, string? suffix = null) =>
        $"{litres.ToString("0.0", CultureInfo.InvariantCulture)} L{suffix}";

    private static string FormatNullableL(double? litres) =>
        litres is { } value ? FormatL(value) : Display.Missing;

    /// <summary>
    /// Formats a litre result in the active display unit. In litres mode it reads
    /// "<c>x.x L</c>"; in laps mode it is converted using the effective litres-per-lap and
    /// reads "<c>x.x laps</c>" (or the missing placeholder when the rate is unknown).
    /// </summary>
    private string FormatFuelResult(double? litres, double? litresPerLap) =>
        LapsMode ? Display.FuelQuantity(litres, true, litresPerLap) : FormatNullableL(litres);
}
