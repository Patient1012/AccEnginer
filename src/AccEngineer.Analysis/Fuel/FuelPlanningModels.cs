namespace AccEngineer.Analysis.Fuel;

/// <summary>
/// Where the effective fuel figures (current fuel and per-lap consumption) come
/// from, so the UI can label them honestly. <see cref="LiveTelemetry"/> is only
/// used when the values actually originate from a live ACC session.
/// </summary>
public enum FuelDataSource
{
    /// <summary>No source yet: the user has not entered anything and there is no telemetry.</summary>
    None,

    /// <summary>Consumption and/or current fuel were entered by the user.</summary>
    Manual,

    /// <summary>Values came from a live ACC session.</summary>
    LiveTelemetry,

    /// <summary>Values came from the built-in generated test-data provider.</summary>
    TestData
}

/// <summary>How the additional safety margin is applied to the base requirement.</summary>
public enum SafetyMarginMode
{
    /// <summary>A fixed additional amount of fuel in litres.</summary>
    Litres,

    /// <summary>A percentage of the base race fuel requirement.</summary>
    Percentage,

    /// <summary>
    /// Both a fixed amount and a percentage, added together. Only used when the user
    /// explicitly selects it, so an equivalent margin is never applied twice by accident.
    /// </summary>
    Both
}

/// <summary>High-level outcome used to drive the plain-language recommendation.</summary>
public enum FuelPlanOutcome
{
    /// <summary>Required inputs are missing, so no recommendation can be made.</summary>
    Incomplete,

    /// <summary>The current fuel already covers the total requirement.</summary>
    Sufficient,

    /// <summary>The current fuel does not cover the total requirement.</summary>
    Shortfall
}

/// <summary>
/// Inputs for one fuel plan. All values are optional so the calculator can run with
/// whatever the telemetry, session and user actually provide, and report what is
/// missing instead of inventing defaults.
/// </summary>
public sealed record FuelPlanInputs
{
    /// <summary>Fuel currently in the car, in litres.</summary>
    public double? CurrentFuelLitres { get; init; }

    /// <summary>Expected fuel consumption per lap, in litres per lap.</summary>
    public double? ConsumptionPerLapLitres { get; init; }

    /// <summary>
    /// Laps remaining to complete the race. May be fractional for a time-limited
    /// session estimate; integer for a lap-limited race.
    /// </summary>
    public double? RemainingLaps { get; init; }

    /// <summary>Planned stint length in laps, when the user is planning a stint.</summary>
    public double? StintLaps { get; init; }

    /// <summary>Required reserve fuel at the finish, in litres.</summary>
    public double? ReserveLitres { get; init; }

    /// <summary>How the additional safety margin is interpreted.</summary>
    public SafetyMarginMode SafetyMarginMode { get; init; } = SafetyMarginMode.Litres;

    /// <summary>Additional safety margin in litres (used by <see cref="SafetyMarginMode.Litres"/> and <see cref="SafetyMarginMode.Both"/>).</summary>
    public double? SafetyMarginLitres { get; init; }

    /// <summary>Additional safety margin as a percentage of the base race fuel (used by <see cref="SafetyMarginMode.Percentage"/> and <see cref="SafetyMarginMode.Both"/>).</summary>
    public double? SafetyMarginPercent { get; init; }

    /// <summary>Fuel tank capacity in litres, when known for the current car. Enables the capacity check.</summary>
    public double? TankCapacityLitres { get; init; }
}

/// <summary>
/// The deterministic result of a fuel plan. Every field is nullable so the result can
/// express "unknown" without substituting a fabricated zero, and <see cref="Issues"/>
/// explains exactly which inputs were rejected or missing.
/// </summary>
public sealed record FuelPlanResult
{
    /// <summary>Base fuel required for the remaining laps: <c>consumption x remaining laps</c>.</summary>
    public double? BaseFuelRequiredLitres { get; init; }

    /// <summary>The safety margin actually applied, in litres.</summary>
    public double? SafetyMarginLitres { get; init; }

    /// <summary>The reserve included in the total, in litres.</summary>
    public double? ReserveLitres { get; init; }

    /// <summary>Total fuel required: base + reserve + safety margin.</summary>
    public double? TotalFuelRequiredLitres { get; init; }

    /// <summary>Fuel to add to meet the total requirement: <c>max(0, total - current)</c>.</summary>
    public double? FuelToAddLitres { get; init; }

    /// <summary>Current fuel minus total requirement. Positive means a surplus.</summary>
    public double? SurplusOrShortfallLitres { get; init; }

    /// <summary>Laps the current fuel is expected to cover at the given consumption.</summary>
    public double? EstimatedLapsWithCurrentFuel { get; init; }

    /// <summary>Fuel required for the planned stint alone (base only), in litres.</summary>
    public double? StintFuelRequiredLitres { get; init; }

    /// <summary>Fuel expected to remain at the end of the planned stint, in litres.</summary>
    public double? StintReserveAtEndLitres { get; init; }

    /// <summary>Whether the current fuel covers the planned stint. Null when it cannot be determined.</summary>
    public bool? StintFuelSufficient { get; init; }

    /// <summary>True when the total requirement exceeds the known tank capacity. Null when the capacity is unknown.</summary>
    public bool? ExceedsTankCapacity { get; init; }

    /// <summary>Drives the plain-language recommendation.</summary>
    public FuelPlanOutcome Outcome { get; init; } = FuelPlanOutcome.Incomplete;

    /// <summary>Human-readable explanations of missing or rejected inputs, in the order they were found.</summary>
    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();
}
