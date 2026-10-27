# Reusable Task: Codebase Cleanup, Readability and Maintainability

You are working on my existing C# .NET 10 WPF Assetto Corsa Competizione telemetry application.

Your task is to inspect and improve the codebase's readability, consistency, maintainability and internal organisation while preserving **all existing features and behaviour**.

This prompt is intended to be reusable at any stage of development, including after implementing new features, modifying overlays, adding telemetry sources or refactoring existing systems.

Treat this as a careful maintenance and refactoring task, not an opportunity to redesign the application or rewrite working systems from scratch.

# 1. Non-negotiable rules

## Preserve every feature

**Do not remove, disable, simplify away or regress any existing functionality.**

This includes, but is not limited to:
- ACC shared-memory telemetry.
- ACC UDP Broadcasting integration.
- Telemetry models and data mappings.
- Synthetic/test-data mode.
- Timing, Relative and Standings overlays.
- Tyre, vehicle-dynamics, conditions and track-map panels.
- Overlay positioning, resizing and layout management.
- Click-through and layout-edit behaviour.
- Overlay layout presets.
- Theme selection and theme resources.
- Telemetry calculations and formatting.
- AI providers, prompts, analysis and reporting.
- Session storage, persistence and configuration.
- Text-to-speech functionality, where implemented.
- Logging, diagnostics and error handling.
- Existing commands, keyboard shortcuts and settings.
- Existing public interfaces and integration points.
- Unit tests and other automated tests.
- Any other existing or partially implemented project feature.

This list is not exhaustive. Inspect the actual repository and preserve functionality beyond the examples listed here.

Do not assume that a feature is unused simply because it is not referenced in an obvious location. Check dependency injection, reflection, XAML bindings, commands, configuration, event subscriptions, runtime registration, serialization and other indirect integration mechanisms before modifying code.

If you cannot establish whether a component is safe to remove or change, leave it intact and report the uncertainty.

## Never delete or rewrite existing comments

**Do not remove existing comments. Do not rewrite, rephrase, shorten or replace them.**

This includes:
- XML documentation comments.
- Regular line comments.
- Block comments.
- TODO and FIXME comments.
- Explanatory comments.
- Comments describing workarounds or known issues.
- Comments in XAML.
- Comments explaining telemetry formats, units, calculations or compatibility requirements.

Preserve comments in their original wording and retain their association with the code they explain.

You may add new comments when they genuinely improve clarity, but do not add unnecessary commentary to obvious code.

If a refactor moves code, move its associated comments with it where appropriate. If an existing comment becomes questionable because the code changes, preserve the original comment and report the discrepancy rather than silently deleting or rewriting it.

Do not remove comments as part of formatting, code generation, file splitting or automated cleanup.

## Avoid unnecessary rewrites

Do not rewrite entire files just to make them look different.

Do not replace working implementations with new abstractions without a clear benefit.

Prefer small, understandable, incremental changes over large architectural transformations.

Do not introduce a new framework, package or dependency solely for stylistic reasons.

Do not change the application's user interface, visual theme, feature set or user-facing behaviour unless a specific change is required to correct a verified defect and is explicitly justified.

# 2. Establish a baseline before editing

Before changing any code:

1. Inspect the repository structure and identify the solution's projects.
2. Inspect the current Git status and recent commits.
3. Identify existing uncommitted modifications and untracked files.
4. Inspect the solution configuration, project files, dependencies and target frameworks.
5. Identify the existing build and test commands.
6. Run the existing build and relevant tests where practical.
7. Record any failures that already exist before your changes.
8. Identify the areas of the codebase that are hardest to understand or maintain.

Do not discard, overwrite, reset or revert existing work to create a clean baseline.

**Preserve the user's current working tree.** Do not assume all uncommitted or untracked files belong to this task.

If the current project does not build, determine whether the failure predates your changes. Do not conceal existing failures or claim that a previously failing build passed.

Before making changes, create a short internal plan based on actual problems discovered in the repository. Do not refactor files simply to meet an arbitrary file count or line count.

# 3. Improve readability

Focus on making the code easier for another developer to understand without needing to trace an excessive number of files or mentally simulate complicated control flow.

Look for the following problems.

## Naming

- Replace genuinely ambiguous local names with descriptive alternatives.
- Use consistent terminology across related classes, methods and properties.
- Make units and meanings clear in names where ambiguity could cause errors.
- Prefer names that explain purpose rather than implementation details.
- Avoid renaming public APIs, serialized properties, configuration keys or XAML-bound properties unless compatibility is verified.

For telemetry-related code, distinguish concepts such as:
- Distance and time gaps.
- Raw and converted values.
- Missing and zero values.
- Current lap, last lap and personal best.
- Real and simulated telemetry.
- Source measurements and derived calculations.

Do not rename variables merely to satisfy personal naming preferences if their current names are already clear.

## Methods and control flow

- Break up genuinely oversized methods into smaller, logically named methods.
- Reduce unnecessary nesting where it improves understanding.
- Simplify complicated conditionals without changing their meaning.
- Remove duplicated logic by extracting a shared helper where that helper makes the code easier to understand.
- Prefer straightforward control flow over clever one-liners or deeply nested expressions.
- Avoid excessive abstraction for simple operations.
- Keep error handling explicit and appropriate to the operation.

Do not split a clear, short method into numerous trivial methods just to reduce its line count.

## Classes and responsibilities

- Identify classes that have accumulated unrelated responsibilities.
- Separate responsibilities when doing so makes ownership and dependencies clearer.
- Keep closely related functionality together.
- Avoid creating excessive numbers of tiny classes, interfaces or wrapper methods.
- Avoid circular dependencies and unnecessary cross-project coupling.
- Preserve existing dependency injection and application lifecycle behaviour.

Only refactor class boundaries when the benefit is clear and the risk is manageable.

## Duplication

Identify repeated logic, formatting, conversions, validation and error-handling patterns.

Where appropriate:
- Extract shared helpers.
- Reuse existing services.
- Centralise genuinely shared constants.
- Consolidate duplicated formatting or conversion rules.
- Keep related calculations consistent across the application.

Do not create a universal helper or generic framework for two unrelated pieces of code just because they contain similar lines.

Do not centralise unrelated constants or force every class to depend on a large shared utility class.

## Dead and redundant code

Look for:
- Unused private methods.
- Redundant conditions.
- Repeated calculations.
- Obsolete temporary variables.
- Duplicate implementations.
- Unnecessary intermediate collections.
- Redundant conversions.
- Unused imports.
- Dead branches that can be proven unreachable.
- Debug code that is not part of an intended feature.

Remove redundant code only when you can establish that it is genuinely unnecessary and that doing so does not change behaviour.

Do not remove public APIs, commands, event handlers, configuration properties, serialized members, XAML-bound properties, test hooks or dynamically invoked methods merely because a static search does not find a reference.

Do not remove a feature just because it is currently incomplete, hidden behind a setting or not frequently used.

# 4. Improve project organisation

Inspect the existing folder and namespace structure.

Make targeted organisational improvements where they make code easier to discover.

Aim for clear boundaries between responsibilities such as:
- Telemetry acquisition and source adapters.
- Telemetry models.
- Data conversion and validation.
- Calculations and analysis.
- Overlay view models.
- Overlay controls and windows.
- Shared styling and theme resources.
- AI integration.
- Persistence and configuration.
- Logging and diagnostics.
- Tests.

These are examples, not a mandatory folder structure. Preserve the repository's existing architecture where it is already reasonable.

Do not move large numbers of files merely to impose a preferred structure.

When moving or renaming files, update namespaces, project references, XAML resource references, file paths, tests and any runtime registration that depends on those locations.

Keep file names, namespaces and class responsibilities consistent.

Avoid introducing folders that contain only one trivial file unless there is a clear reason.

# 5. Improve C# consistency

Use modern, appropriate C# features where they improve clarity and remain compatible with the project's target framework and coding conventions.

Review:
- Nullable reference types and null handling.
- Async/await usage.
- Cancellation
