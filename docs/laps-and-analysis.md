# Laps and analysis

`AccEngineer.Analysis` turns an ordered stream of `TelemetrySample`s into reconstructed laps,
comparisons, and deterministic driving findings. It has no ACC, I/O, UI, or AI dependency, so
the same code runs on a recording, a live session, or synthetic test data.

## Lap reconstruction

`LapAnalyzer` (`src/AccEngineer.Analysis/LapAnalyzer.cs`) reconstructs laps from the sample
stream.

- **Runs.** Samples are grouped into contiguous runs by a change in `(SessionId, LapNumber)`.
  A run is the raw material for one lap.
- **Completeness.** A run is `IsComplete` only when a following run with the same session id
  was observed. The final run of a session is therefore incomplete, so a recording that stops
  mid-lap never reports a partial lap as a complete lap.
- **Skipping.** Runs shorter than `LapAnalysisOptions.MinimumSamplesPerLap` (default 3) are
  skipped and counted in `SessionLapAnalysis.SkippedLapCount`.
- **Ordering.** If sample ids are not strictly increasing, a warning is added; reconstruction
  assumes recorded order.

### Lap validity

A lap `IsValid` only when it is complete **and** every sample reports `IsLapValid` **and** no
sample carries the `DataQualityFlags.InvalidLap` flag. Invalid laps are never silently dropped:
they are carried with `IsValid = false` and reported as warnings. This matters because ACC
flags invalid laps (for example track-limit cuts) and the reference-lap selection must ignore
them.

### Lap time source

`LapTimeline.LapTimeSource` (`LapTimeSource` enum) records how the time was determined:

| Source | Meaning |
| --- | --- |
| `AccReported` | ACC's reported last-lap time (`iLastTime`) on the first sample of the following lap. Authoritative when present. |
| `ComputedBoundary` | Computed from the wall-clock boundary between this lap's first sample and the next lap's first sample. |
| `PartialSpan` | The span of an incomplete lap (partial). |
| `Unavailable` | No usable lap time. |

`LapTimeline` (`src/AccEngineer.Analysis/LapTimeline.cs`) carries the lap number, completeness,
validity, start/end times, lap time and source, sector count, a list of `LapSample`s, and a list
of `SectorTimeline`s. `LapSample` positions each sample within the lap (seconds since the lap's
first sample plus lap-relative `ProgressMeters`).

### Sectors

`SectorTimeline`s are derived from changes in `CurrentSectorIndex`. The final sector runs to the
known lap time (or the last known lap time when the lap time is unknown).

## Reference-lap selection

`ReferenceLapSelector.SelectFastestValid` (`src/AccEngineer.Analysis/LapComparison.cs`) is the
documented policy:

> The fastest **valid**, complete lap by lap time; ties broken by the lowest lap number.
> Returns `null` when no lap qualifies.

`AnalysisEngine` accepts an optional requested reference lap number. If the requested lap is
not found it adds a warning and falls back to the fastest valid lap. When no reference is
requested, the fastest valid lap is used.

The **current** lap used for comparison is chosen as:

- the explicitly requested lap number, if any; otherwise
- the most recent valid, timed lap other than the reference; otherwise
- the reference lap itself.

## Lap comparison

`LapComparer.Compare(reference, laps)` (`src/AccEngineer.Analysis/LapComparison.cs`) compares
each lap against the reference.

- **Alignment.** Laps are aligned by track progress, not by sample index or timestamp. Lap time
  is interpolated at each progress point, so differing sample rates or timing do not matter.
- **Grid.** Comparison points are sampled on a common grid with `LapComparisonOptions.GridStepMeters`
  (default 10 m), up to the smaller of the two laps' maximum progress.
- **Deltas.** `TotalDeltaSeconds` is the whole-lap difference (positive = slower than the
  reference). `DeltaAtEndSeconds` is the delta at the last common grid point.
- **Gain/loss regions.** `BuildRegions` groups runs where the delta changes monotonically. A
  region is reported only when it is both at least `RegionThresholdSeconds` (default 0.05 s) in
  magnitude and at least `MinimumRegionLengthMeters` (default 20 m) long.

The comparison only uses laps that have track-progress data. If the reference lap has fewer than
two progress control points, `Compare` throws `InvalidOperationException`; callers guard against
this by selecting a reference with valid data first.

## Driving analysis

`DrivingAnalyzer` (`src/AccEngineer.Analysis/DrivingAnalyzer.cs`) produces measured facts and
traceable `TelemetryEvent`s per lap and per session. It reports braking zones, corner shape,
throttle pickup, and tyre summaries, and never diagnoses.

- **Braking zones** are detected from `BrakeInput` with hysteresis: `BrakeOnThreshold`
  (default 0.10) starts a zone and `BrakeOffThreshold` (default 0.05) ends it; a zone shorter
  than `MinimumBrakingSamples` (default 3) is ignored.
- **Corners** are approximated from a contiguous run of steering input with
  `|SteeringInput| >= CornerSteeringThreshold` (default 0.15), bridging gaps up to
  `CornerMergeGapSamples` (default 3) and ignoring runs shorter than `MinimumCornerSamples`
  (default 3). Apex is taken as the minimum-speed point, the best available proxy because ACC
  exposes no corner geometry. Throttle pickup (`ThrottlePickupThreshold`, default 0.5) and full
  throttle (`FullThrottleThreshold`, default 0.95) are searched past the exit.
- **Tyre summary** reports per-wheel pressure and core-temperature min/max/average over a lap
  (wheels ordered FL, FR, RL, RR); null statistics mean the channel was absent.
- **Session events** include a consistency event (lap-time spread across valid laps) and a
  tyre-channel-missing event when no pressure or core-temperature data is present.

All thresholds live in `DrivingAnalysisOptions` (`src/AccEngineer.Analysis/DrivingModels.cs`).

Every `TelemetryEvent` (`src/AccEngineer.Analysis/DrivingModels.cs`) carries an id, category,
severity, a measured `Observation`, optionally an `Interpretation`, a confidence, an optional
lap/progress location, `EvidenceReference`s pointing back to real sample ids, an optional
suggested test, and `Limitations`.

Event categories (`EventCategory`): `Braking`, `CornerEntry`, `MidCorner`, `CornerExit`,
`Traction`, `Tyres`, `Consistency`, `DataQuality`. Severity: `Info`, `Low`, `Medium`, `High`.
Confidence (`EventConfidence`): `Verified` (measured fact), `Probable`, `Uncertain`.

## Findings

`Finding` (`src/AccEngineer.Analysis/Findings/Finding.cs`) is the standard structure for
everything the engine discovers. It deliberately separates:

- `Observation` — what the telemetry definitely shows (a measured fact).
- `PossibleCauses` — explicitly **unproven** explanations, which must never be presented as
  diagnoses.
- `Evidence` — a list of `FindingEvidence` with optional measured values and sample-id ranges.
- `Recommendation` / `ValidationMethod` — what to investigate and how to tell whether a change
  helped.
- `EstimatedTimeLossSeconds`, `Frequency`, `Severity`, `Confidence`/`ConfidenceScore`, and a
  location (`TrackLocation`).

`FindingBuilder` (`src/AccEngineer.Analysis/Findings/FindingBuilder.cs`) builds findings from
time-loss analysis and from behaviour indicators:

- Time-loss findings are emitted only when the estimated loss's magnitude reaches
  `FindingBuilderOptions.MinimumTimeLossSeconds` (default 0.05 s). Their category is chosen from
  the dominant deltas (brake point, minimum speed, throttle pickup). Severity is derived from
  loss magnitude (`>= 0.30 s` High, `>= 0.15 s` Medium, else Low).
- Confidence comes from `ConfidenceCalculator` using channel coverage, sample adequacy, and
  repeatability.

`FindingRanker` (`src/AccEngineer.Analysis/Findings/FindingRanker.cs`) orders findings by a
weighted score of time loss, frequency, confidence, and severity (ties break on id) so the
highest-value items come first. `FindingRankerOptions` exposes those weights.

## Behaviour indicators

`VehicleBehaviourDetector` (`src/AccEngineer.Analysis/Indicators/VehicleBehaviourDetector.cs`)
produces weighted `BehaviourIndicator`s backed by measured patterns. It requires corner
definitions: if no `TrackCornerDefinition` is supplied (or it has no corners), `Detect` returns
an empty list. Indicators are only produced for corners that can be analysed.

The detector currently emits three kinds:

- `PossibleMidCornerUndersteer` — steering stays high (`>= HighSteeringInput`, default 0.30)
  through the rotation while speed falls and throttle stays low (`< LowThrottle`, default 0.5).
- `PossibleExitUndersteer` — throttle is applied on exit (`>= LowThrottle`) while steering stays
  high and the exit speed gain is below `MinimumExitSpeedGainKph` (default 10 km/h).
- `ExcessiveSteeringCorrection` — steering-rate sign reversals in the rotation reach
  `CorrectionCountThreshold` (default 3).

`BehaviourIndicator` (`src/AccEngineer.Analysis/Indicators/BehaviourIndicator.cs`) carries the
kind, corner phase, distance, a measured observation, evidence strings, a heuristic confidence
(**not** a probability), and limitations. The `BehaviourIndicatorKind` enum also declares
entry/oversteer/lock-up/traction-loss kinds that are part of the shared vocabulary but are not
yet emitted by this detector. These are signals for interpretation, not conclusions.

## Corner definitions (optional)

Because ACC shared memory exposes no corner geometry, corner boundaries can be supplied manually.
`CornerDefinitionLoader` (`src/AccEngineer.Analysis/Corners/CornerDefinitionLoader.cs`) reads JSON
into a `TrackCornerDefinition`:

```json
{
  "track": "monza",
  "corners": [
    { "number": 1, "name": "Variante del Rettifilo", "start": 300, "entry": 340, "turnIn": 360, "apex": 375, "exit": 400, "end": 430 }
  ]
}
```

`apex` is required (it anchors phase analysis); the other boundaries are optional. The loader
performs no file I/O unless `LoadFromFile` is called, and no corner definition files ship with
the repository — corner data is supplied by the caller.

## Full engine orchestration

`AnalysisEngine.Analyze` (`src/AccEngineer.Analysis/AnalysisEngine.cs`) runs the whole
deterministic pipeline: lap analysis, distance-based `LapRepresentation` building, reference and
current selection, comparison, event detection, behaviour indicators, time-loss analysis, finding
building and ranking, and a compact `AiSessionSummary`. `AnalysisEngineOptions` exposes the
sub-options and `MaxFindings` (default 5). The result is a `SessionAnalysisResult` with laps,
events, indicators, time loss, ranked findings, the AI summary, and warnings.

When not enough valid laps are available to compare, the engine returns an empty result with the
warning `"Not enough valid laps were available to compare."` and no findings, rather than failing.
