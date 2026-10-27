# ACC Engineer documentation

ACC Engineer is a local-first telemetry and driving-analysis tool for **Assetto Corsa
Competizione**. It reads the ACC shared-memory feed, records sessions, reconstructs laps,
detects driving findings, and produces deterministic JSON and Markdown reports. An optional
AI provider can add a clearly-labelled *possible cause* interpretation.

Everything runs offline by default. Reports are always available with no AI provider
configured. See the project [README](../README.md) for build, run, and troubleshooting
instructions.

## Contents

| Document | Covers |
| --- | --- |
| [architecture.md](architecture.md) | Projects, dependency flow, and the end-to-end data flow from shared memory to reports. |
| [telemetry.md](telemetry.md) | The shared-memory reader, validation, staleness handling, and the `TelemetrySample` shape. |
| [laps-and-analysis.md](laps-and-analysis.md) | Lap reconstruction, lap comparison, driving findings, reference-lap selection, and validity. |
| [ai-provider.md](ai-provider.md) | The provider-neutral AI interface, configuration, prompt/parse behaviour, rate limiting, and failure modes. |
| [pipeline.md](pipeline.md) | The live event pipeline: rolling evidence buffer, deduplication, queue and rate limits. |
| [reporting.md](reporting.md) | Report types, file naming, and JSON/Markdown output structure. |
| [recordings.md](recordings.md) | The JSONL recording format, schema v1 vs v2, gzip, and the streaming writer. |
| [cli.md](cli.md) | The `AccEngineer.App` console options and exit codes. |
| [overlay.md](overlay.md) | The transparent always-on-top HUD overlay and its layout editor. |
| [theming.md](theming.md) | The reusable UI theme system. |
| [settings.sample.json](settings.sample.json) | The shape of the user settings file. |

## Design principles

These are consistent across the codebase:

- **Deterministic first.** Analysis, findings, and reports are computed without any AI or
  network access. AI is an optional interpretation layer only.
- **Measured fact vs. possible cause.** A finding always separates the measured
  `Observation` from any unproven `PossibleCause`/`Interpretation`, which is never presented
  as a diagnosis.
- **Unknown is `null`, never zero.** Unavailable channels are represented with nullable
  types and omitted from JSON, so a missing value is never silently substituted.
- **Honest empty states.** Missing or invalid data is reported rather than inferred.
