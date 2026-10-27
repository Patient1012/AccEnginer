# Telemetry

`AccEngineer.Telemetry` is the only project that talks to Assetto Corsa Competizione. It
reads the ACC shared-memory pages and maps them to one project-owned sample type,
`TelemetrySample`. All other projects depend on that type and never read shared memory
directly.

## Reader

`AccSharedMemoryReader` (`src/AccEngineer.Telemetry/AccSharedMemoryReader.cs`) implements
`ITrackTelemetryReader` and is the live access point. It opens three read-only
memory-mapped regions, all under the `Local\` namespace:

- `Local\acpmf_physics`
- `Local\acpmf_graphics`
- `Local\acpmf_static`

`TryConnect` returns false on non-Windows platforms and when ACC is not running (the maps do
not exist), so the reader simply reports `IsConnected == false` and retries later. It is
designed for single-threaded polling by one consumer.

### ReadLatest behaviour

`ReadLatest()` returns `TelemetrySample?`:

- **`null`** when there is no new data — not yet connected, the physics packet id has not
  advanced since the previous read, or the packet is being throttled.
- **A sample** when the physics packet id advances, meaning ACC published a new physics
  frame.
- **One final sample flagged `DataQualityFlags.Stale`** when the packet id stops advancing
  for longer than the stale threshold. It is reported once, then `null` until data resumes.

On the first physics packet after a (re)connect the reader records a baseline and returns
`null`, so data captured before tracking started is never reported. If the packet id goes
backwards the sample is flagged `DataQualityFlags.OutOfOrder`. If packets stop advancing past
the disconnect threshold the connection is dropped and re-established automatically on a
later call. The static page is re-read on every call so session metadata stays fresh across
session changes.

### Options

`AccReaderOptions` (constructor default shown):

| Option | Default | Meaning |
| --- | --- | --- |
| `StaleThreshold` | 1000 ms | No new physics packet for this long marks the next sample `Stale`. |
| `DisconnectThreshold` | 5 s | No new physics packet for this long drops the connection; the reader reconnects later. |
| `ReconnectRetryInterval` | 2 s | Minimum delay between connection attempts while ACC is not running. |

## Sample mapping

`AccSampleBuilder` (internal, `src/AccEngineer.Telemetry/AccSampleBuilder.cs`) maps the raw
pages to `TelemetrySample`. It owns per-session state: session identity, sample numbering, and
lap-relative track progress. It is decoupled from the memory-mapped reader so the mapping can
be unit-tested with synthetic page data and no ACC process, and it is not thread-safe — call
from a single reader thread.

- **Session identity.** A new session id is generated when the session index changes, the
  completed-lap count decreases, or the car/track name changes. That change sets
  `DataQualityFlags.SessionChanged`.
- **Track progress.** `TrackProgressMeters` is derived from ACC's `distanceTraveled`, handling
  both "reset each lap" and "accumulate for the session" semantics. A drop larger than 5 m
  without a lap-number change is treated as a position reset (respawn/teleport).
- **Pit / validity.** Being in the pit lane or pit box flags `DataQualityFlags.PitStop`, and an
  invalid lap while live flags `DataQualityFlags.InvalidLap`.

## `TelemetrySample`

`TelemetrySample` (`src/AccEngineer.Telemetry/TelemetrySample.cs`) is an immutable `record`
(so consumers can derive annotated copies with `with`). All numeric fields carry explicit
units in their names, and unavailable values are nullable — never substituted with zero.

Identity and timing:

| Field | Type | Notes |
| --- | --- | --- |
| `SampleId` | `long` | Monotonic per-reader sample number. |
| `ReceivedAtUtc` | `DateTimeOffset` | When the sample was received from the source. |
| `SourceTimestampUtc` | `DateTimeOffset?` | Source timestamp when available; ACC shared memory exposes none, so live samples are `null`. |
| `ReceiptTick` | `long` | Monotonic `Stopwatch` tick for ordering. |
| `SessionId` | `string` | Stable within a session (`"unknown"` before a session is seen). |

Lap and sector data: `LapNumber`, `IsLapValid`, `CurrentLapTimeMs`, `LastLapTimeMs`,
`BestLapTimeMs`, `CurrentSectorIndex`, `SectorCount`, `TrackProgressMeters`. Times are in
milliseconds and are `null` when ACC reports a zero or negative sentinel.

Driver inputs and motion: `SpeedKph`, `SpeedMetersPerSecond`, `SteeringInput` (-1..1),
`ThrottleInput`, `BrakeInput`, `ClutchInput` (0..1), `Gear` (ACC raw encoding: 0 = reverse,
1 = neutral, 2+ = gear `value - 1`), `EngineRpm`, `LateralAccelerationG`,
`LongitudinalAccelerationG`, `AngularVelocityYawRadPerSec`, `HeadingRad`, `PitchRad`,
`RollRad`, `PositionXMeters/Y/ZMeters`.

Environment and tyres: `WheelAngularSpeedRadPerSec`, `WheelSlip`, `WheelLoad`,
`WheelPressure` (raw ACC `wheelsPressure`, order FL, FR, RL, RR; no unit is asserted by the
code), `TyreCoreTempC`, `TyreTempInnerC`, `TyreTempMiddleC`, `TyreWear`, `BrakeTempC`,
`FuelLevel`, `AirTempC`, `TrackTempC`, `SurfaceGrip`, `RainTyresOn`, `PitLimiterOn`,
`IsInPitLane`, `Flag`.

Session metadata: `TrackName`, `CarName`, `TyreCompound`, `SessionType`, `SessionState`,
`PlayerPosition`, `NormalizedCarPosition`.

> Fields marked `[JsonIgnore]` — `ActiveCars`, `PlayerCarId`, and `CarsOnTrack` (world
> positions of other cars) — are used only for the live map and are **not** persisted to
> recordings. Opponent names, gaps, and lap times are not exposed by ACC shared memory.

Quality:

| Field | Type | Notes |
| --- | --- | --- |
| `Quality` | `DataQualityFlags` | Bit flags merged from the reader/builder and the validator. |

Enumerations declared alongside `TelemetrySample`:

- `SessionType` — `Unknown = -1`, `Practice`, `Qualify`, `Race`, `Hotlap`, `TimeAttack`,
  `Drift`, `Drag`, `HotStint`, `HotStintSuperpole`.
- `SessionState` — `Unknown`, `Off`, `Replay`, `Live`, `Pause`.
- `SessionFlag` — `Unknown = -1`, `None`, `Blue`, `Yellow`, `Black`, `White`, `Checkered`,
  `Red`, `Green`.
- `DataQualityFlags` (`[Flags]`) — `None`, `Stale`, `OutOfOrder`, `GapDetected`,
  `PartialUpdate`, `InvalidLap`, `SessionChanged`, `PitStop`.

## Validation

`TelemetryValidator` (`src/AccEngineer.Telemetry/TelemetryValidator.cs`) performs stream-level
checks across consecutive samples. It is pure and stateful only over the previous sample, so
its behaviour is deterministic and testable.

- `OutOfOrder` when a sample's `ReceivedAtUtc` or `SampleId` is not greater than the previous.
- `GapDetected` when the receive gap exceeds `GapThreshold` (default 500 ms).
- `Stale` when the sample is older than `StalenessThreshold` (default 1 s) at validation time.

Flags returned by the validator are meant to be merged with flags already set by the
reader/builder (the callers OR them into `TelemetrySample.Quality`).

## Buffers

`SampleRingBuffer` (`src/AccEngineer.Telemetry/SampleRingBuffer.cs`) is a fixed-capacity ring
used by the UI's `LiveTelemetryService` for its recent-sample window. The live event pipeline
uses its own `RollingEvidenceBuffer` (see [pipeline.md](pipeline.md)).

## Platform notes

- The shared-memory reader and the desktop UI are **Windows-only**.
- The reader returns `null` (no connection) on non-Windows platforms rather than throwing.
