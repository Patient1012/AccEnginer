# ACC Engineer

ACC Engineer is a local-first telemetry and driving-analysis tool for **Assetto
Corsa Competizione**. It reads the ACC shared-memory feed, records sessions,
reconstructs laps, detects driving findings, and produces deterministic JSON and
Markdown reports. An optional AI provider can add a clearly-labelled
*possible cause* interpretation on demand — it is never required, never runs
automatically, and never blocks the deterministic analysis.

Everything runs offline by default. Reports are always available with no AI
provider configured.

## Projects

| Project | Purpose |
| --- | --- |
| `AccEngineer.Telemetry` | ACC shared-memory reader, validation, and the project-owned `TelemetrySample`. |
| `AccEngineer.Analysis` | Lap reconstruction, lap comparison, and deterministic driving analysis. |
| `AccEngineer.AI` | Provider-neutral AI interface, prompt building, response parsing, rate limiting, and an OpenAI-compatible client. |
| `AccEngineer.Reporting` | Lap reports, live event reports, session summaries, and JSON/Markdown exporters. |
| `AccEngineer.Pipeline` | Live event pipeline: rolling evidence buffer, deduplication, queue and rate limits. |
| `AccEngineer.Storage` | JSONL session recording/replay and the recording library. |
| `AccEngineer.App` | Console app for live display, replay, offline analysis, and live reports. |
| `AccEngineer.UI` | WPF desktop interface (`net10.0-windows`). |

Dependencies flow one way: `Analysis → Telemetry`, `AI → Analysis`,
`Reporting → Analysis`, `Pipeline → {Analysis, Reporting, AI, Telemetry}`,
`Storage → Telemetry`; `App` and `UI` compose them.

## Requirements

- **Windows** (the ACC shared-memory reader and the desktop UI are Windows-only).
- **.NET 10 SDK**.
- **Assetto Corsa Competizione**, running and in a session, for live telemetry.
  Replay and offline analysis work without ACC.

## Build and test

```powershell
dotnet build AccEngineer.sln
dotnet test AccEngineer.sln
```

## Desktop app

```powershell
dotnet run --project src/AccEngineer.UI
```

Pages: **Overview**, **Live Data** (map, standings, pedal inputs), **Analysis**
(lap comparison + driving findings, with optional per-finding AI interpretation),
**Live Events** (the live event queue and saved reports), **Reports**, and
**Sessions**.

## Console app

```powershell
dotnet run --project src/AccEngineer.App -- [options]
```

| Option | Description |
| --- | --- |
| *(none)* | Live ACC telemetry display. |
| `--record` | Also record the session to a JSONL file per session. |
| `--record-dir <dir>` | Recording output directory (default `./recordings`). |
| `--live-reports` | Also write live event reports and a session summary. |
| `--report-dir <dir>` | Live report output directory (default `./reports`). |
| `--replay <file>` | Replay a recorded session offline (no ACC needed). |
| `--analyze <file>` | Analyse a recording offline, write JSON + Markdown reports. |
| `--out <dir>` | Report output directory for `--analyze` (default `./reports`). |
| `--poll-ms <n>` | Polling interval in milliseconds (default `50`). |
| `--once` | Read once, print the result, exit (`0` = sample, `1` = no data). |
| `--log <file>` | Append status/diagnostics to a log file (off by default). |
| `--log-level <level>` | `quiet`, `normal`, or `verbose` (default `normal`). |
| `--help` | Show help. |

Examples:

```powershell
# Record a session with live reports
dotnet run --project src/AccEngineer.App -- --record --live-reports

# Analyse an existing recording
dotnet run --project src/AccEngineer.App -- --analyze recordings\20260101-120000-abc.jsonl --out reports

# Replay a recording offline
dotnet run --project src/AccEngineer.App -- --replay recordings\20260101-120000-abc.jsonl
```

## Reports

Reports are written as a matching `.md` + `.json` pair:

- **Lap report** — `*.report.md` / `*.report.json`: per-lap times, validity, and
  gain/loss regions against a reference lap.
- **Live event report** — `*.live.md` / `*.live.json`: one significant finding
  with its evidence window and an optional AI interpretation.
- **Session summary** — `*-summary.live.md` / `*.live.json`: consolidated
  findings and best valid lap.

Every finding separates the **measured observation** (a fact) from any
**possible cause** (an interpretation). AI output is always labelled as a
hypothesis, never as a diagnosis.

## Configuration

Settings live outside the source tree at
`%AppData%\AccEngineer\settings.json` and are edited on the **Settings** page.
See [`docs/settings.sample.json`](docs/settings.sample.json) for the shape.

- **Poll interval**, **recordings directory**, **reports directory**.
- **AI provider**, **model**, **endpoint** (optional).

### AI provider

Set these values on the Settings page, then provide the API key through an
environment variable (keys are **never** written to `settings.json`):

```
ACCENGINEER_AI_API_KEY       # preferred
OPENROUTER_API_KEY           # fallback
```

Provider keys:

- `openrouter` → `https://openrouter.ai/api/v1`
- `ollama` → `http://localhost:11434/v1`
- `lmstudio` / `local` → `http://localhost:1234/v1`
- or supply a full base URL as the endpoint.

The console app reads `ACCENGINEER_AI_PROVIDER`, `ACCENGINEER_AI_MODEL`, and
`ACCENGINEER_AI_ENDPOINT` from the environment for `--live-reports`.

## Troubleshooting

- **"WAITING FOR ACC" / "ACC shared memory not available"** — start ACC and
  enter a session (shared memory is only published while in-session). Confirm you
  are on Windows and running the same architecture as ACC.
- **Telemetry is stale** — ACC has stopped publishing new frames (paused, in a
  menu, or the session ended). The reader keeps the last sample but flags it.
- **No laps / empty reports** — complete at least one full lap after the reader
  connects. Partial first laps are reported as incomplete and are excluded from
  the reference-lap selection.
- **Invalid laps** — ACC flags these; they are carried with `IsValid = false`
  rather than dropped, and are listed as data-quality warnings.
- **AI interpretation unavailable** — either no provider is configured or the
  request failed. Deterministic findings, reports, and copy still work; the live
  event report records the reason (`noProvider`, `unavailable`, `invalid`,
  `cancelled`).
- **`--log` gives no output** — pass a path, e.g.
  `--log logs\accengineer.log`; use `--log-level verbose` for more detail. Logs
  are off by default and never contain secrets.
- **Recordings/reports not visible** — check the directories on the Settings
  page and use the **Folder** buttons to open them.

## Roadmap status

Milestones 1–7 of the ACC Telemetry AI roadmap are implemented: telemetry
capture and validation, lap reconstruction and comparison, deterministic driving
analysis, the AI provider layer, live event reports with queue/dedup/rate limits,
and workflow polish (configuration, CLI options and logging, session/report
browsing, and documentation). See the roadmap for details.
