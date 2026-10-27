# AGENTS.md (ACC Engineer)

## Overview
ACC Engineer is a local-first telemetry/analysis tool for Assetto Corsa Competizione. Deterministic analysis/reports run offline; AI interpretation is opt-in, never automatic, and always labeled as a hypothesis.

## Projects & dependencies (one-way)
| Project | Role |
| --- | --- |
| `AccEngineer.Telemetry` | ACC shared-memory reader/validation; project-owned `TelemetrySample`. |
| `AccEngineer.Analysis` | Lap reconstruction, comparison, deterministic findings. |
| `AccEngineer.AI` | Provider-neutral AI (prompt/build/parse/rate-limit), OpenAI-compatible client. |
| `AccEngineer.Reporting` | Lap/live/session reports; JSON/Markdown exporters. |
| `AccEngineer.Pipeline` | Live event pipeline (rolling buffer, dedup, queue/rate limits). |
| `AccEngineer.Storage` | JSONL session recording/replay + recording library (gzip auto-detected). |
| `AccEngineer.App` | Console (`net10.0`) live display/replay/offline analysis/live reports. |
| `AccEngineer.UI` | WPF desktop (`net10.0-windows`) with pages Overview/Live Data/Analysis/Live Events/Reports/Sessions. |

Flow: `Analysis → Telemetry`, `AI → Analysis`, `Reporting → Analysis`, `Pipeline → {Analysis, Reporting, AI, Telemetry}`, `Storage → Telemetry`. `App`/`UI` compose them.

## Build/test
```powershell
dotnet build AccEngineer.sln
dotnet test AccEngineer.sln
```
- Solution has 9 src + 7 test projects. UI is `net10.0-windows` (WPF); App is `net10.0`.

## Run
```powershell
# Desktop (WPF)
dotnet run --project src/AccEngineer.UI

# Console
dotnet run --project src/AccEngineer.App -- [options]
```
Common console options (see README): `--record`, `--record-dir <dir>`, `--live-reports`, `--report-dir <dir>`, `--replay <file>`, `--analyze <file>`, `--out <dir>`, `--poll-ms <n>`, `--once`, `--log <file>`, `--log-level <level>`. Defaults: recordings `./recordings`, reports `./reports`.

## Platform/requirements
- Windows required (ACC shared memory + WPF UI). Live telemetry needs ACC in-session; replay/offline analysis work without ACC.
- .NET 10 SDK.
- UI project uses WPF + `app.manifest` (see `src/AccEngineer.UI/AccEngineer.UI.csproj`).

## Recordings (Storage)
- JSONL, gzip-compressed by default, auto-detected on load (schema v1 readable; schema v2 current: compact per-sample, relative ms from session start, ints/enums/flags, no repeated `sessionId` in samples; header once + end record).
- `CreateNew` only (never overwritten). Missing end record = interrupted session. Streamed via bounded producer/consumer (no disk I/O blocking). Live gzip may be unreadable until closed cleanly.

## Reports
- Pairs: Lap (`*.report.md`/`*.report.json`), Live event (`*.live.md`/`*.live.json`), Session summary (`*-summary.live.md`/`*.live.json`).
- Always separate measured observation vs possible cause (AI output labeled hypothesis only).

## Configuration/AI
- Settings stored outside source at `%AppData%\AccEngineer\settings.json` (edited via UI Settings). Keys never written to settings.json.
- Env vars: `ACCENGINEER_AI_API_KEY` preferred, fallback `OPENROUTER_API_KEY`. Console also reads `ACCENGINEER_AI_PROVIDER`, `ACCENGINEER_AI_MODEL`, `ACCENGINEER_AI_ENDPOINT` for `--live-reports`.
- Providers/endpoints: `openrouter` → `https://openrouter.ai/api/v1`, `ollama` → `http://localhost:11434/v1`, `lmstudio`/`local` → `http://localhost:1234/v1` or supply full base URL.

## Gotchas
- If ACC shared memory unavailable: start ACC and enter a session; same arch as ACC on Windows.
- Partial first lap excluded from reference lap selection; invalid laps kept with `IsValid=false`.
- AI unavailable → deterministic reports still work; live event records reason (`noProvider`, `unavailable`, `invalid`, `cancelled`).
- `--log` needs a path and is off by default; logs never contain secrets.
- Live-in-progress gzip recordings may appear unreadable until cleanly closed.
