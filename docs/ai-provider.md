# AI provider

The AI layer is optional and strictly an **interpretation** layer. Deterministic analysis and
reports are always produced without it. Every expected failure — no provider, bad output,
timeout, cancellation — is returned as a value, never thrown, so recording and analysis continue
regardless of the AI.

## Provider interface

`IAnalysisProvider` (`src/AccEngineer.AI/IAnalysisProvider.cs`):

```csharp
string Name { get; }
bool IsConfigured { get; }
Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken);
```

`NullAnalysisProvider` is the no-AI fallback: `Name` is `"none"`, `IsConfigured` is `false`, and
`AnalyzeAsync` returns `AnalysisOutcome.NoProvider`. The factory returns it whenever no usable
provider is configured, rather than failing.

## Configuration

`AiProviderOptions` (`src/AccEngineer.AI/AiProviderOptions.cs`):

| Option | Default | Notes |
| --- | --- | --- |
| `Provider` | `null` | Provider key such as `openrouter`, `local`, `lmstudio`, `ollama`, or any custom name. |
| `Endpoint` | `null` | Base endpoint. Optional for the known providers below. |
| `Model` | `null` | Required; a blank model yields the null provider. |
| `ApiKey` | `null` | When null the environment is consulted. Never persisted to settings. |
| `TimeoutSeconds` | `60` | Clamped to 1..600. |
| `MaxRequestsPerMinute` | `20` | Rate limit enforced by `RequestLimiter`. |
| `MaxConcurrentRequests` | `1` | Concurrency limit. |

### Endpoint resolution

`AnalysisProviderFactory.ResolveEndpoint`:

1. An explicit `Endpoint` is trimmed and has any trailing `/` removed.
2. Otherwise the `Provider` key selects a built-in:

   | Provider key(s) | Endpoint |
   | --- | --- |
   | `openrouter` | `https://openrouter.ai/api/v1` |
   | `local`, `lmstudio`, `lm-studio` | `http://localhost:1234/v1` |
   | `ollama` | `http://localhost:11434/v1` |

   Anything else resolves to `null`.
3. A `null` endpoint or blank model returns `NullAnalysisProvider`.

### API keys

`AnalysisProviderFactory.ResolveApiKey`:

1. `AiProviderOptions.ApiKey` when non-blank; otherwise
2. the `ACCENGINEER_AI_API_KEY` environment variable; otherwise
3. the `OPENROUTER_API_KEY` environment variable.

Keys are never written to `settings.json`; only environment (or an explicitly supplied value)
is used.

## OpenAI-compatible client

`OpenAiCompatibleProvider` (`src/AccEngineer.AI/OpenAiCompatibleProvider.cs`) targets any
OpenAI-compatible chat-completions endpoint (OpenRouter, a local LM Studio/Ollama server, or a
custom base URL).

- It `POST`s to `{endpoint}/chat/completions`.
- Authentication uses a `Bearer` header only when a key is present (local servers often need
  none).
- The request body is `{ model, temperature = 0.2, messages: [ system, user ] }`.
- The `HttpClient.Timeout` is the configured timeout. A supplied `HttpMessageHandler` makes the
  client fully testable without network access.
- The response content is read from `choices[0].message.content`.

Failure mapping:

| Situation | Outcome |
| --- | --- |
| Caller cancelled | `Cancelled` |
| Timed out | `Unavailable` ("The request timed out.") |
| Non-success HTTP status | `Unavailable` (includes status code and reason) |
| Network error | `Unavailable` |
| Any other exception | `Unavailable` |
| Malformed/empty payload | `InvalidResponse` (via the parser) |

## Rate limiting

`RequestLimiter` (`src/AccEngineer.AI/RequestLimiter.cs`) bounds outbound requests:

- A `SemaphoreSlim` caps concurrency at `MaxConcurrentRequests` (at least 1).
- A minimum interval of `60 / MaxRequestsPerMinute` seconds is enforced between request starts
  (a non-positive rate means no spacing). Telemetry recording never waits on this; only callers
  that request an interpretation acquire a slot.

## Prompting

`AnalysisPrompts` (`src/AccEngineer.AI/AnalysisPrompts.cs`) builds the provider-neutral prompt.

- The **system prompt** pins the model to a single JSON object with this schema and instructs it
  to keep facts and hypotheses separate and to never invent values:

  ```json
  {
    "findingType": "string",
    "observation": "string",
    "evidence": ["string"],
    "interpretation": "string",
    "confidence": 0.0,
    "suggestedTest": "string",
    "limitations": ["string"]
  }
  ```

- The **user prompt** carries only prepared evidence: metadata, the measured event/observation,
  metrics, an optional sample window, an optional comparison-vs-reference summary, data-quality
  warnings, and the question. A whole telemetry stream is never forwarded.

`AnalysisPrompts.MaxSamples` (40) is the default cap on the raw sample window included. Requests
are built by one of two builders, depending on the finding source:

- `FindingAnalysisRequestBuilder` (ranked findings) forwards the measured summary and evidence
  strings only — **no raw samples**.
- `DrivingAnalysisRequestBuilder` (telemetry events) attaches a small traceable window: the
  sample range referenced by the event's evidence, or a ±50 m window around the event's progress
  point, capped at `MaxSamples` (clamped to 1..200) and downsampled to fit.

### Request and result shapes

- `AnalysisRequest` (`src/AccEngineer.AI/AnalysisModels.cs`) — context, event category and
  description, metrics, sample excerpts, quality warnings, optional comparison summary, question.
- `SampleExcerpt` — just `SampleId`, `ProgressMeters`, `SpeedKph`, `Throttle`, `Brake`, `Steering`.
- `AnalysisResult` — `Outcome`, `FindingType`, `Observation`, `Interpretation`, `Evidence`,
  `Confidence`, `SuggestedTest`, `Limitations`, `Provider`, `Message`.
- `AnalysisOutcome` — `Success`, `NoProvider`, `InvalidResponse`, `Unavailable`, `Cancelled`.

## Response parsing

`AnalysisResultParser` (`src/AccEngineer.AI/AnalysisResultParser.cs`) validates model output:

- Empty content → `InvalidResponse`.
- A leading ```` ``` ```` code fence is stripped (including a trailing one).
- JSON is parsed case-insensitively, ignoring comments and allowing trailing commas.
- A missing/blank `observation` → `InvalidResponse`.
- `confidence` outside 0..1 is clamped to the range.

## Orchestration

`AnalysisEngineCoordinator` (`src/AccEngineer.AI/AnalysisEngineCoordinator.cs`) is the integration
point:

1. Run the deterministic `AnalysisEngine` to produce a `SessionAnalysisResult`.
2. If no provider is configured (`IsConfigured == false`), return the analysis unchanged with no
   AI findings (`EngineAnalysisReport.IsAIAvailable` is `false`).
3. Otherwise, interpret each of the summary's `TopFindings` with
   `FindingAnalysisRequestBuilder` + `IAnalysisProvider`, collecting a `FindingAnalysis` per
   finding. A failing call is captured as an `Unavailable` result for that finding; cancellation
   still propagates.

The measured `Analysis` in the result is never altered by the AI, so deterministic output is
identical with or without a provider.
