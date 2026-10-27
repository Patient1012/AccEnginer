# Autonomous Implementation Task: ACC AI Race Engineer

## Mission

Implement a fully integrated AI race engineer in the existing `AISimRacingAgent` application.

Work autonomously from repository inspection through implementation, testing, documentation and final review. I have other things to do, so do not stop after producing a plan, do not ask me to approve routine decisions, and do not leave straightforward implementation work for me to complete later.

Make sensible engineering decisions based on the actual repository. Prioritise correctness, reliability, preservation of existing features and a polished user experience.

**Do not just write a plan or scaffold. Implement the feature.**

If a genuinely blocking issue prevents completion, finish all safe and independent work first, then report the precise blocker and the work that remains.

---

# 1. Workspace access and initial safety checks

Start by establishing the workspace root.

1. Confirm the actual repository directory OpenCode has opened. Print the absolute path in your initial progress report.
2. Confirm this is the existing `AISimRacingAgent` project. Inspect the solution, project files, source tree, build configuration, test projects, documentation and Git state.
3. If the wrong directory is open, ask me to select the existing project directory. Do not create a replacement project.
4. Read the repository instructions, including `AGENTS.md`, relevant OpenCode instructions, and any applicable project-specific agent rules. Follow them unless they conflict with the explicit requirements in this prompt.
5. Inspect the current Git branch, remote, recent commits, staged changes, unstaged changes, untracked files and ignored files relevant to the task.
6. Establish the existing build and test commands. Run baseline tests and a baseline build when practical before making changes.
7. Record existing failures separately from failures introduced by this task.
8. Inspect existing architecture and documentation before deciding where new functionality belongs.

Existing documentation that must be preserved includes, if present:

- `docs/ai-provider.md`
- `docs/architecture.md`
- `docs/index.md`
- `docs/laps-and-analysis.md`
- `docs/pipeline.md`
- `docs/telemetry.md`

These files may contain uncommitted work. Preserve it and update documentation in place only where appropriate.

### Git safety rules

- Never discard, reset, overwrite or clean up pre-existing user changes.
- Never use destructive commands such as `git reset --hard`, `git clean -fd`, or force-push operations.
- Do not revert unrelated modifications.
- Do not overwrite files simply because they are untracked.
- Do not commit unrelated user changes.
- Never commit secrets, API keys, tokens, credentials, generated binaries or temporary diagnostic output.
- Inspect the branch and remote before any commit or push.
- Do not change branches or rewrite history without a compelling, explicitly documented reason.
- Keep implementation changes logically grouped and reviewable.
- Commit the completed implementation and push it to the existing appropriate remote branch only if all required checks pass and the repository state is safe.
- If pushing would overwrite remote work, if the branch has diverged unexpectedly, or if credentials or permissions are unavailable, do not force anything. Finish local work, explain the issue and report the appropriate next step.
- Do not expose secrets in logs, terminal output, reports or documentation.

If the repository has unrelated changes, work around them safely. Do not demand a clean working tree if the task can be completed without disturbing those changes.

---

# 2. Inspect the existing application before designing

This is an existing C#/.NET WPF application. Do not build a parallel application or introduce a second architecture unnecessarily.

Inspect and reuse existing implementations wherever appropriate:

- ACC shared-memory telemetry.
- ACC UDP Broadcasting API and session/competitor information.
- Telemetry ingestion, state and event pipelines.
- Existing deterministic calculations and analysis services.
- AI provider configuration and abstraction.
- Settings persistence and application settings UI.
- Existing session history and persistence.
- Existing text-to-speech or speech recognition integrations, if any.
- Existing keyboard/controller binding infrastructure.
- Existing theme and UI components.
- Existing overlay system.
- Existing synthetic/test-data mode.
- Existing logging, diagnostics and error handling.
- Existing dependency injection and service lifetimes.
- Existing test projects and testing conventions.

Inspect the actual available telemetry fields and the current implementation. Do not assume ACC exposes data that the application cannot obtain.

The application already contains telemetry and overlay features, including timing, standings/relative information, vehicle dynamics, tyre/brake temperatures and other existing work. Preserve all of them.

Do not remove, simplify, replace or regress existing features to make the new feature easier.

Before implementing, create a concise internal task plan and a dependency-aware sequence of implementation stages. Record meaningful progress in a suitable task document or project notes if that fits the existing conventions.

Then continue automatically.

---

# 3. Required feature

Implement a race engineer that monitors the driver's ACC session, detects meaningful events, produces useful coaching and delivers concise spoken messages.

It must combine deterministic C# logic with AI interpretation.

## 3.1 Deterministic core

Implement the safety-critical and time-sensitive parts as ordinary, testable C# code.

These include:

- Event detection.
- Fuel calculations and shortage projections.
- Data-validity checks.
- Urgency and priority classification.
- Announcement cooldowns and duplicate suppression.
- Message expiration and cancellation.
- Session lifecycle handling.
- Speech-queue management.
- Confidence and evidence requirements.

Do not depend on an AI response to detect an urgent fuel shortage or other condition that can be detected deterministically.

The AI should interpret available evidence, explain likely causes and produce contextual coaching. It must not be the only mechanism for critical warnings.

Never let AI requests block telemetry processing, session tracking, the UI thread, deterministic event detection or local speech output.

Use asynchronous processing, cancellation and bounded queues where appropriate. Avoid unbounded task creation, polling loops that waste CPU, deadlocks and shared-state races.

Keep time-sensitive paths predictable.

## 3.2 AI provider integration

Inspect and reuse the existing AI provider abstraction and settings.

- Do not introduce a second provider system without a demonstrated need.
- Respect the currently configured provider and model.
- Reuse existing API-key handling and configuration.
- Do not hardcode credentials or model-specific assumptions unnecessarily.
- Add timeouts, cancellation, sensible retry limits and clear failure handling.
- Avoid sending full telemetry histories when a compact context is sufficient.
- Do not send audio recordings or speech-recognition audio to a hosted AI service.
- Do not implement hosted speech recognition or hosted text-to-speech.
- Do not silently switch to a different provider or model.
- Never treat model-generated statements as measured telemetry.

Create a compact, structured AI context containing only relevant information, such as the current session state, valid measurements, relevant lap/sector history, detected events, confidence and missing data.

Inspect the existing provider's capabilities and limitations before selecting an implementation strategy. If the provider cannot support a desired operation, degrade gracefully rather than pretending it succeeded.

AI output must be constrained to useful, concise race-engineer responses. Validate structured responses where used, handle malformed output and prevent unsupported claims from being presented as facts.

Clearly distinguish:

- Measured facts.
- Deterministic calculations.
- Estimates or projections.
- Possible explanations.
- Suggestions that need further evidence.

AI-generated coaching must not override deterministic safety warnings.

---

# 4. Race engineer announcements

Implement configurable announcement categories and meaningful event triggers.

## 4.1 Fuel engineer

Provide adaptive fuel warnings based on valid telemetry and reliable calculations.

- Track fuel usage from suitable valid samples and laps.
- Use valid lap consumption where available.
- Estimate remaining fuel requirements using the best reliable prediction available.
- Account for remaining laps, current fuel, reserve and safety margin.
- Escalate urgency when the projected finish fuel becomes insufficient or the situation deteriorates.
- Explain uncertainty when the remaining distance or consumption estimate is unreliable.
- Avoid double-counting reserves and safety margins.
- Handle refuelling, pit stops, fuel resets, invalid laps, session transitions and stale readings.
- Avoid alarming the driver because of a single bad sample.
- Never invent a remaining-laps figure or fuel estimate when the required inputs are missing.

Reuse existing fuel-calculation functionality if it exists, including any existing Fuel Calculator page, overlay or calculation service.

Do not create a second independent fuel-calculation implementation if the existing service can be extended safely.

## 4.2 Lap and pace coaching

Build a reliable pace baseline using valid laps from the current session, recent performance and personal best where appropriate.

Detect meaningful changes, including:

- A substantial pace improvement or deterioration.
- Significant sector-time changes.
- Consistency changes over several valid laps.
- Major single-lap events when evidence supports them.
- Repeated losses in a particular part of the lap, if the data supports that conclusion.

Avoid commentary on every lap or every small fluctuation.

Identify likely causes of lost time only when supported by available telemetry. Consider traffic, changing conditions, invalid laps and incomplete data when the application can actually identify them.

If several explanations are plausible, communicate uncertainty instead of confidently asserting one.

Provide useful actions rather than merely reading numbers aloud.

For live coaching, favour one useful observation and one actionable suggestion. Save more detailed breakdowns for explicit questions or post-session analysis.

## 4.3 Tyre and vehicle condition

Use only telemetry the application actually receives and can validate.

Detect meaningful tyre trends, out-of-range readings and relevant vehicle-condition changes when supported by reliable measurements.

Use sensible filtering, sample validation and cooldowns.

Do not assume tyre pressure, wear, temperature or other vehicle-condition fields are available unless confirmed by the existing implementation or reliable ACC data source.

Do not diagnose a mechanical problem solely from an ambiguous symptom.

## 4.4 Session events

Recognise relevant lifecycle and session events where reliable information exists, including:

- ACC connection and disconnection.
- Session start and end.
- Session changes and transitions.
- Pit entry/exit and refuelling where detectable.
- Flags and other significant session events where available.
- Important milestones or meaningful changes in the race situation.

Handle menus, pauses, missing samples and reconnects without falsely interpreting missing telemetry as a vehicle failure.

Do not announce every routine state change.

## 4.5 Setup and general advice

Setup recommendations should primarily be available after a session or when explicitly requested.

During a live session, provide only urgent and well-supported advice.

Do not fabricate setup settings, temperatures, pressures, telemetry channels or causal explanations.

---

# 5. Voice input and local speech recognition

Implement push-to-talk voice input.

Requirements:

- No always-on microphone.
- No wake-word listener.
- Local speech recognition only.
- Do not upload speech recordings or audio for transcription.
- Reuse existing speech-recognition infrastructure if suitable.
- If no suitable implementation exists, investigate and compare appropriate offline engines before selecting one.
- Prefer a reliable solution that fits the existing Windows/.NET application and is practical to install and maintain.
- Document installation requirements, model downloads, licensing and limitations.
- Do not automatically download large models or executables from unverified sources.
- Do not add an expensive or complicated dependency without a clear benefit.
- Provide visible recording/listening status.
- Provide clear feedback when recognition fails, times out or produces no usable result.
- Support a configurable push-to-talk keyboard binding.
- Support a controller binding if the existing input infrastructure makes it practical.
- Provide an in-app control to ask a question manually.
- Ensure holding a push-to-talk control does not create duplicate recordings or overlapping recognition requests.
- Handle cancellation, disposal, session transitions and application shutdown correctly.

Questions should support both quick telemetry queries and broader questions that can be answered using the available session data.

Examples include:

- How much fuel do I need?
- Why was that lap slower?
- Where am I losing time?
- How has my pace changed?
- Summarise my session.
- What should I focus on next?

These are examples, not a hardcoded list of supported questions. Use the existing AI provider for contextual answers when appropriate.

Never claim that the engineer has inspected information that it has not received.

---

# 6. Local spoken output

Inspect the current project for existing speech-output functionality.

If an appropriate local TTS implementation exists, reuse it. Otherwise, compare suitable local Windows/.NET-compatible options and choose a reasonable solution based on reliability, speech quality, offline operation, licensing, installation complexity and maintainability.

All spoken output must be generated locally.

The default delivery style is terse motorsport radio: direct, technical and concise.

Speech length should adapt to context:

- Critical warning: immediate, minimal wording.
- Routine live update: short and useful.
- Direct question: answer the question concisely.
- Post-session report: more detailed when requested.

Use asynchronous playback. Keep speech generation and playback off the UI thread.

### Speech priority and queue

Implement explicit priorities and lifecycle rules.

1. Critical deterministic warnings have the highest priority.
2. Important contextual warnings and major events follow.
3. Routine coaching and commentary have lower priority.
4. Post-session reports are not allowed to interfere with live urgent warnings.

Critical warnings can interrupt routine speech immediately. Routine speech must not interrupt another routine message.

Discard stale routine messages instead of reading old commentary after the situation has changed.

Suppress repeated messages about the same unresolved event until the condition changes, meaningfully worsens or becomes more urgent.

At session end, discard stale live commentary and create the post-session summary separately.

Do not allow the queue to grow without bounds.

Handle speech-engine errors, device failures, cancellation and application shutdown without crashing the application.

### Mute and emergency override

Provide an engineer enable/disable control and a mute control.

By default, critical emergency warnings can break through mute. This override must be configurable in settings so the user can disable it.

Do not make the override impossible to disable.

Persist the relevant settings.

---

# 7. Coaching and session reports

## 7.1 Current-session coaching

Use valid laps, recent pace and personal best as the initial pace baseline.

Use deterministic analysis to calculate available metrics and AI to interpret relevant evidence.

Do not use an arbitrary AI-generated numerical analysis when a deterministic calculation is possible.

Avoid overwhelming the driver. Prefer timely, relevant observations.

## 7.2 Cross-session comparison

Allow comparisons with saved sessions where the application already has suitable persistence.

Do not silently build a permanent or hidden driver profile.

Use only explicitly available, relevant saved session information.

Handle differences in track, car, conditions, session type and data completeness before comparing sessions.

Avoid misleading comparisons between incompatible sessions.

## 7.3 Post-session analysis

Generate a structured session report covering the information supported by available data:

- Overall pace and consistency.
- Lap and sector performance.
- Meaningful changes over the session.
- Fuel usage and relevant projections.
- Tyre and vehicle trends when valid telemetry supports them.
- Potential areas for improvement.
- Supported recommendations.
- Confidence and limitations.
- Comparisons with compatible saved sessions when available.

Save the report using the existing persistence mechanism and make it accessible after the application restarts.

Offer to read the summary aloud, but do not automatically begin speaking a long report when the session ends.

The report must remain useful when AI is unavailable. Generate the deterministic portions locally and clearly indicate any AI-dependent sections that could not be produced.

---

# 8. Persistence and settings

Inspect the current persistence implementation first.

Reuse the existing system to save engineer preferences and session reports across application restarts.

Do not introduce a separate database or storage system unless the existing system genuinely cannot meet the requirements.

Persist settings such as:

- Engineer enabled/disabled.
- Mute state or preference, consistent with the existing settings conventions.
- Emergency-warning override.
- Speech volume.
- Push-to-talk binding.
- Announcement-category toggles.
- Any necessary advanced configuration.

Use existing settings migration/versioning conventions if present.

Do not overwrite existing settings during migration. Provide safe defaults for newly introduced values.

Keep sensitive information, particularly API keys and credentials, within the application's existing secure configuration approach.

### Settings UI

Add a dedicated Race Engineer settings page that fits the existing WPF UI and theme system.

Include accessible, understandable controls for:

- Enable/disable.
- Mute.
- Voice volume.
- Push-to-talk binding.
- Announcement categories.
- Emergency-warning override.

Place less commonly used options under an advanced section rather than overwhelming the main page.

Show useful state information, including whether speech recognition and TTS are ready and whether the AI provider is available, where those states can be determined reliably.

Provide useful validation and error messages.

Reuse existing styling, navigation, theme resources and controls. Do not introduce a separate visual design system.

---

# 9. Reliability and graceful degradation

The application must remain useful when one of its components fails.

### If the AI provider fails

- Continue ingesting telemetry.
- Continue deterministic calculations and event detection.
- Continue immediate deterministic warnings.
- Continue local TTS.
- Continue saving telemetry and reports where possible.
- Report AI failure clearly.
- Apply reasonable request timeouts and bounded retries.
- Do not endlessly retry, flood the UI or fill the speech queue with failures.

### If speech recognition fails

- Keep telemetry and other engineer functions running.
- Report the problem visibly.
- Allow manual text questions when AI is available.
- Permit retrying without restarting the entire application.

### If TTS fails

- Keep telemetry, analysis and AI processing running.
- Surface a useful error and provide a way to retry or reinitialise the speech engine where practical.
- Do not repeatedly attempt playback in a tight loop.

### If telemetry becomes invalid or stale

- Mark affected data unavailable or stale.
- Suppress warnings requiring invalid inputs.
- Do not interpret an ACC disconnect as zero fuel, zero speed, tyre failure or another physical event.
- Reinitialise appropriate state when a valid session resumes.

### If the application closes

Cancel outstanding operations, stop speech, release audio devices and dispose of resources cleanly.

Prevent event subscriptions, background tasks and timers from leaking across session transitions.

---

# 10. Privacy and data minimisation

Speech recognition and TTS must remain local.

The existing configured AI provider may be hosted. Respect the user's existing provider settings; do not force all AI inference to be local or silently change the provider.

Minimise the information sent to AI. Send only the compact telemetry context and question needed for the current task.

Do not send raw audio, full telemetry recordings or unnecessary session history to the AI provider.

Do not include credentials, API keys or sensitive local file paths in AI prompts.

Document clearly:

- Which operations run locally.
- Which operations may contact the configured AI provider.
- What telemetry or session context may be sent.
- What speech and audio data are retained, if any.
- Where reports and preferences are stored.
- Which offline functions continue working without AI.

Do not introduce telemetry collection, analytics, cloud logging or other external services.

---

# 11. Test and diagnostic infrastructure

Inspect and reuse the existing test-data mode.

If synthetic telemetry already exists, extend it rather than creating a competing simulation system.

Provide predefined scenarios that exercise the same ingestion, analysis, event and announcement pipeline as real telemetry. Clearly label all synthetic data and simulated events in the UI and diagnostics.

Create reusable scenarios covering at least:

1. Normal session startup and shutdown.
2. Valid laps and changing pace.
3. Fuel consumption and an approaching fuel shortage.
4. Invalid or missing telemetry.
5. Tyre trends and unreliable tyre readings.
6. A significant lap or sector-time change.
7. Overlapping urgent and routine announcements.
8. Duplicate warning suppression.
9. Stale-message dropping.
10. Emergency speech interruption.
11. AI provider outage and recovery.
12. Speech-recognition failure.
13. TTS failure.
14. Session transitions and ACC disconnect/reconnect.
15. Post-session report generation and persistence.
16. Application shutdown while asynchronous operations are active.

Use deterministic fake services and controllable clocks where necessary so automated tests do not rely on real-time timing, a working microphone, a speaker or a live AI provider.

### Diagnostic panel

Integrate a diagnostic view appropriate to the existing application.

Expose relevant information such as:

- Current engineer state.
- Telemetry validity and freshness.
- Detected events and their evidence.
- Event priorities.
- Suppression and cooldown decisions.
- AI request status and failures.
- Speech-recognition state.
- Speech queue and current priority.
- Stale-message cancellation.
- TTS state and errors.
- Test-mode status.

Avoid displaying secrets or excessive raw telemetry.

The diagnostic view should help identify why an announcement did or did not occur.

---

# 12. Engineering quality

Follow the repository's existing architecture, conventions and formatting.

- Use the existing .NET target framework and supported dependencies.
- Do not upgrade the framework or unrelated packages without a compelling need.
- Do not introduce unnecessary dependencies.
- Do not create giant classes containing the entire feature.
- Keep event detection, coaching, speech recognition, speech playback, queue management and persistence responsibilities appropriately separated.
- Use dependency injection where the application already does so.
- Keep logic testable without WPF, real audio hardware, a running ACC session or a hosted AI service.
- Marshal UI updates through the existing dispatcher patterns.
- Use cancellation tokens and correct resource disposal.
- Validate all external inputs and AI output.
- Avoid fire-and-forget tasks without exception handling.
- Avoid unbounded queues, retries and polling.
- Use clear logging without exposing secrets.
- Preserve existing comments and update misleading documentation only where needed.
- Do not suppress compiler warnings simply to get a clean build.
- Do not weaken existing tests or remove assertions to make tests pass.

Use established naming conventions. Prefer extending existing abstractions to creating duplicate ones.

If a required telemetry field or capability is unavailable, implement the best honest fallback and document the limitation. Do not fabricate support.

---

# 13. Autonomous execution plan

Work through the following stages without asking me to approve each stage.

## Stage A — Discovery and baseline

Inspect the repository, architecture, telemetry availability, AI provider, speech capabilities, settings, persistence, tests and Git state.

Run the baseline build and tests when practical.

Identify existing functionality that can be reused. Record any pre-existing failures.

## Stage B — Design and contracts

Determine the smallest coherent architecture that integrates with the existing application.

Define testable interfaces and data contracts for event detection, coaching context, speech input, speech output, message priorities, queue management and session reporting as needed.

Do not build duplicate infrastructure.

## Stage C — Deterministic engine

Implement the data-validity checks, event detection, urgency rules, fuel calculations or integration, suppression rules and lifecycle handling.

Write automated tests for these components before building AI-dependent behaviour on top of them.

## Stage D — Speech infrastructure

Inspect existing local speech engines. If needed, compare viable offline recognition and TTS options.

Implement push-to-talk, local recognition, local speech output, priorities, interruption, cancellation, bounded queues and settings integration.

Provide graceful degradation and actionable diagnostics.

Do not claim that physical audio behaviour has been verified unless it has actually been tested.

## Stage E — AI coaching

Integrate the configured AI provider with compact telemetry context, validated output, timeouts and graceful failure handling.

Implement live coaching, direct questions and supported post-session analysis.

Keep deterministic urgent warnings independent of AI.

## Stage F — UI, settings and persistence

Integrate the engineer into the existing WPF navigation and theme system.

Implement the settings page, diagnostics, report access and persistence.

Reuse the existing test-data mode and route predefined scenarios through the same processing pipeline.

## Stage G — Integration and reliability

Test interactions between telemetry, event detection, AI, speech, settings and reports.

Exercise reconnects, session transitions, cancellation, shutdown and failure recovery.

Fix regressions instead of weakening tests.

## Stage H — Documentation and final validation

Update relevant documentation, including architecture and configuration instructions.

Document speech engine setup, local dependencies, AI data flow, privacy boundaries, testing instructions and known limitations.

Run the full relevant test suite, solution build and any repository-defined quality checks.

Perform a final code and Git diff review.

## Stage I — Commit and push

When implementation and required checks are complete:

1. Review all changed, staged and untracked files.
2. Ensure no unrelated user changes are included.
3. Ensure no secrets or generated junk are included.
4. Check the branch and remote state again.
5. Commit the implementation with a clear message if safe.
6. Push to the existing appropriate remote branch if safe and authorised by the available environment permissions.
7. Never force-push or overwrite remote work.

If a commit or push is unsafe or unavailable, retain the completed local work and explain why the operation was not performed.

---

# 14. Definition of done

The task is complete only when the following have been addressed and honestly reported.

### Functionality

- Race engineer integrates with the existing application.
- Deterministic warnings work without AI.
- AI coaching uses real, valid, available telemetry.
- Push-to-talk uses local speech recognition.
- Spoken output uses local TTS.
- Urgent warnings interrupt routine speech correctly.
- Duplicate suppression, priorities and stale-message handling work.
- Settings and reports persist across restarts.
- Session reports and saved-session comparisons work within actual data limitations.
- Synthetic scenarios and diagnostics are integrated.
- Existing features remain intact.

### Validation

- Automated tests pass, or every failure is investigated and explicitly classified.
- Existing baseline failures are distinguished from new regressions.
- The clean build has no new warnings or errors, where the repository's build setup permits this.
- Manual audio and input checks are documented with exact steps and expected results.
- Any check requiring real hardware or a live ACC session is marked as actually performed or not performed.
- Documentation reflects the implementation rather than intended future behaviour.
- The final diff has been reviewed for regressions, accidental changes, secrets and unnecessary complexity.

### Reporting

At completion, provide a concise but useful report covering:

1. What was implemented.
2. Important architectural decisions and reused components.
3. New or modified files.
4. Tests and build commands run, with actual results.
5. Manual audio/input checks performed and any remaining checks.
6. Known limitations or incomplete requirements.
7. Documentation updated.
8. Commit hash and push status, if applicable.
9. Any action that genuinely requires me.

Never claim a test, manual check, commit or push succeeded unless the tool output confirms it.

---

# 15. Rules for autonomous decisions

- Prefer inspecting code over asking me questions.
- Prefer the smallest justified implementation over a broad refactor.
- Prefer local deterministic logic for calculations and urgent decisions.
- Prefer existing infrastructure over new dependencies.
- Do not wait for approval on routine implementation details.
- Do not stop after creating scaffolding or documentation.
- Do not silently omit difficult requirements.
- If a requirement cannot be implemented safely, finish the independent work, document the limitation and continue elsewhere.
- Do not repeatedly ask me to make choices already resolved in this specification.
- Do not claim full completion while essential functionality is missing.
- Do not compromise existing project functionality to make the new feature appear complete.

**Begin with workspace verification and repository inspection. Then continue through implementation, testing, documentation and final review autonomously.**
