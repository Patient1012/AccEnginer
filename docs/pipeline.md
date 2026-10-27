# Live event pipeline

`AccEngineer.Pipeline` turns live findings into reports while a session is running. It exists so
that a recurring pattern does not flood the queue, the model, or the reports, and so that
reporting keeps working even when the AI provider is slow, failing, or absent.

The pipeline is **not** a background thread. The caller feeds it samples and findings and drives
draining (the UI uses a `Task.Run` pump; the console uses `GetAwaiter().GetResult()`). The
collector side never blocks waiting on the AI.

## Components

- `RollingEvidenceBuffer` (`src/AccEngineer.Pipeline/RollingEvidenceBuffer.cs`) — a bounded,
  thread-safe window of the most recent samples, used to attach a small evidence window without
  retaining the whole session.
- `EventDeduplicator` (`src/AccEngineer.Pipeline/EventDeduplicator.cs`) — coalesces repeats.
- `LiveEventReportBuilder` (in `AccEngineer.Reporting`) — builds the deterministic report.

## Feeding the pipeline

- `Observe(sample)` / `ObserveRange(samples)` append to the rolling evidence buffer.
- `Submit(finding, session, warnings, nowUtc?)` offers a finding to the queue and returns `true`
  when it was queued.

`Submit` returns `false` (and does not queue) when the finding is coalesced into an existing item.
Either way, counters are maintained:

| Counter | Meaning |
| --- | --- |
| `AcceptedCount` | Findings queued. |
| `CoalescedCount` | Findings folded into an existing item by deduplication. |
| `DroppedCount` | Oldest queued items dropped because the queue was full. |
| `PendingCount` | Items currently queued. |
| `History` | Processed items (capped at 500, oldest removed first). |

## Deduplication

`EventDeduplicator` builds a key of **category + lap + progress bucket** (bucket size
`DedupBucketMeters`, default 100 m). A repeat of the same key within `DedupCooldown`
(default 15 s) is folded into the existing item and counted instead of queued. The running
coalesced count is carried onto the item and its evidence, and the model still sees only one
report for the pattern.

## Queue and rate limits

`LiveEventPipelineOptions` (`src/AccEngineer.Pipeline/LiveEventPipelineOptions.cs`):

| Option | Default | Meaning |
| --- | --- | --- |
| `EvidenceBufferCapacity` | 4000 | Samples retained by the rolling buffer. |
| `EvidenceFallbackToleranceMeters` | 50 | Progress tolerance when selecting evidence by location. |
| `DedupBucketMeters` | 100 | Dedup bucket size along the lap. |
| `DedupCooldown` | 15 s | Window in which repeats are coalesced. |
| `MaxQueue` | 100 | Queued items; the oldest is dropped when full. |
| `MaxRequestsPerMinute` | 12 | Interpretation requests started per minute. |
| `RequestTimeout` | 45 s | Timeout for a single interpretation. |
| `Interpret` | `true` | Whether to call the AI provider. Reports are emitted either way. |
| `MaxRequestSamples` | 40 | Raw samples attached to one request. |

`MaxQueue` is enforced on `Submit`: when full, the oldest item is dequeued, marked `Dropped`, and
`DroppedCount` is incremented. A separate internal rate gate spaces AI requests by
`60 / MaxRequestsPerMinute` (a non-positive rate disables spacing). This is independent of the
provider's own `RequestLimiter` (see [ai-provider.md](ai-provider.md)).

## Processing

`ProcessNextAsync(provider, cancellationToken)`:

1. Dequeues the next item and marks it `Interpreting`.
2. If `Interpret` is enabled and the provider is configured, calls the provider; otherwise
   produces a `noProvider` interpretation.
3. Attaches the shared coalesced count to the evidence and builds the `LiveEventReport` via
   `LiveEventReportBuilder`.
4. Marks the item `Completed`, adds it to `History`, and raises `ReportReady`.

`DrainAsync` loops `ProcessNextAsync` until the queue is empty or cancellation is requested, and
returns the number processed.

AI failures never block reporting. Interpretation statuses (`LiveEventInterpretation.Status`)
are mapped from the provider outcome:

| Status | Source |
| --- | --- |
| `ok` | `AnalysisOutcome.Success` |
| `noProvider` | `AnalysisOutcome.NoProvider`, or `Interpret` disabled |
| `invalid` | `AnalysisOutcome.InvalidResponse` |
| `cancelled` | `AnalysisOutcome.Cancelled` (including request timeout) |
| `unavailable` | Any other outcome or an exception |

### Evidence selection

When interpreting, a small window is chosen from the rolling buffer:

1. The sample range referenced by the finding's evidence (`FirstSampleId`..`LastSampleId`), when
   present; otherwise
2. a window around the finding's `ProgressMeters` within `EvidenceFallbackToleranceMeters`;
   otherwise
3. no samples.

The window is capped at `MaxRequestSamples` (clamped to 1..200) and downsampled to fit, so the
model never receives a full-rate stream.

## Session summary

`BuildSessionSummary(session, laps, warnings, generatedAtUtc?)` consolidates every reported
finding in `History` plus the lap timelines into a `SessionSummaryReport` (via
`SessionSummaryReportBuilder`). `Reset()` clears the buffer, deduplicator, queue, history, and key
index for a new session.

## Lifecycle states

`LiveEventState`: `Queued` → `Interpreting` → `Completed`, or `Dropped` when evicted by the queue
limit.
