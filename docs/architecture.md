# Architecture

ACC Engineer is a .NET 10 / C# solution. The `AccEngineer.sln` solution groups the
production projects under `src/` and the test projects under `tests/`.

## Projects

| Project | Path | Target | Purpose |
| --- | --- | --- | --- |
| `AccEngineer.Telemetry` | `src/AccEngineer.Telemetry` | `net10.0` | ACC shared-memory reader, validation, and the project-owned `TelemetrySample`. Windows-only at runtime. |
| `AccEngineer.Analysis` | `src/AccEngineer.Analysis` | `net10.0` | Lap reconstruction, lap comparison, and deterministic driving analysis. |
| `AccEngineer.AI` | `src/AccEngineer.AI` | `net10.0` | Provider-neutral AI interface, prompt building, response parsing, rate limiting, and an OpenAI-compatible client. |
| `AccEngineer.Reporting` | `src/AccEngineer.Reporting` | `net10.0` | Lap reports, live event reports, session summaries, and JSON/Markdown exporters. |
| `AccEngineer.Pipeline` | `src/AccEngineer.Pipeline` | `net10.0` | Live event pipeline: rolling evidence buffer, deduplication, queue and rate limits. |
| `AccEngineer.Storage` | `src/AccEngineer.Storage` | `net10.0` | JSONL session recording/replay and recording-file naming. |
| `AccEngineer.App` | `src/AccEngineer.App` | `net10.0` | Console app for live display, replay, offline analysis, and live reports. |
| `AccEngineer.UI` | `src/AccEngineer.UI` | `net10.0-windows` (WPF) | Desktop interface. |

Test projects live under `tests/` (`AccEngineer.Telemetry.Tests`,
`AccEngineer.Analysis.Tests`, `AccEngineer.AI.Tests`, `AccEngineer.Reporting.Tests`,
`AccEngineer.Pipeline.Tests`, `AccEngineer.Storage.Tests`, `AccEngineer.UI.Tests`).

## Dependency flow

Dependencies flow one way. Each edge below is a `ProjectReference` in the project file.

```
Telemetry  ──────────────► (no project dependencies)
Analysis   ──────────────► Telemetry
AI         ──────────────► Analysis
Reporting  ──────────────► Analysis
Pipeline   ──────────────► Telemetry, Analysis, AI, Reporting
Storage    ──────────────► Telemetry
App        ──────────────► Telemetry, Analysis, AI, Reporting, Pipeline, Storage
UI         ──────────────► Telemetry, Analysis, AI, Reporting, Pipeline, Storage
```

`App` and `UI` are the composition roots; nothing depends on them.

## Data flow

The deterministic pipeline from shared memory to reports:

```
ACC shared memory
  Local\acpmf_physics / Local\acpmf_graphics / Local\acpmf_static
        │
        ▼
AccSharedMemoryReader.ReadLatest()              src/AccEngineer.Telemetry/AccSharedMemoryReader.cs
        │  (AccSampleBuilder maps raw pages → one sample per advancing physics packet)
        ▼
TelemetrySample                                 src/AccEngineer.Telemetry/TelemetrySample.cs
        │
        ├─► TelemetryValidator.Validate()        merges stream-level quality flags (gaps/stale/order)
        │
        ├─► JsonlSessionRecorder.Record()        src/AccEngineer.Storage/JsonlSessionRecorder.cs
        │       (bounded producer/consumer queue → gzip JSONL on disk)
        │
        └─► LapAnalyzer.Analyze()                src/AccEngineer.Analysis/LapAnalyzer.cs
                │  (contiguous runs by session + lap number → LapTimeline)
                ▼
           ReferenceLapSelector.SelectFastestValid()   src/AccEngineer.Analysis/LapComparison.cs
                │
                ▼
           LapComparer.Compare()                 src/AccEngineer.Analysis/LapComparison.cs
                │  (common track-progress grid, gain/loss regions)
                ▼
           ReportBuilder.Build()                 src/AccEngineer.Reporting/ReportBuilder.cs
                │
                ▼
        JsonReportExporter / MarkdownReportExporter   src/AccEngineer.Reporting/ReportExporters.cs
```

Two higher-level entry points sit on top of the deterministic layer:

- **Full deterministic analysis** — `AnalysisEngine.Analyze()` (`src/AccEngineer.Analysis/AnalysisEngine.cs`)
  runs the lap analyzer, representation builder, reference/current selection, lap comparison,
  event detection, behaviour indicators, time-loss analysis, and ranked findings, and builds a
  compact AI-facing summary. `AnalysisEngineCoordinator` (`src/AccEngineer.AI/AnalysisEngineCoordinator.cs`)
  can then attach AI interpretations for the top findings, but the deterministic result is
  complete without it.
- **Live event pipeline** — `LiveEventPipeline` (`src/AccEngineer.Pipeline/LiveEventPipeline.cs`)
  buffers recent samples, accepts significant findings, deduplicates them, and produces
  `LiveEventReport`s. See [pipeline.md](pipeline.md).

## Entry points

| Entry point | File | Notes |
| --- | --- | --- |
| Console | `src/AccEngineer.App/Program.cs` | Parses arguments, then dispatches to `AnalyzeRunner`, `ReplayRunner`, or `LiveRunner`. See [cli.md](cli.md). |
| Desktop | `src/AccEngineer.UI/App.xaml.cs` | `App.OnStartup` applies the saved theme and shows `MainWindow` with a `MainViewModel`. |
| Desktop services | `src/AccEngineer.UI/ViewModels/MainViewModel.cs` | Owns the long-lived services (`LiveTelemetryService`, `ReportService`, `RecordingLibrary`, `OverlayManager`), the pages, and a 100 ms `DispatcherTimer` pump. |

`LiveTelemetryService` (`src/AccEngineer.UI/Services/LiveTelemetryService.cs`) owns the reader
on a dedicated background thread, publishes the latest sample plus bounded recent/session
buffers, manages recording, and can replay a recording through the same pipeline. The UI never
touches shared memory directly.

## Configuration

User settings are persisted as JSON at `%AppData%\AccEngineer\settings.json` by
`AppSettings` (`src/AccEngineer.UI/Services/AppSettings.cs`). See
[settings.sample.json](settings.sample.json) for the shape. Provider API keys are never
persisted; they are read from the environment (see [ai-provider.md](ai-provider.md)).
