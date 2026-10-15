# Overlay

The overlay is a transparent, always-on-top HUD that sits over the simulator. It is
built in phases (see `ACC_Telemetry_AI_Racelab_Legacy_Overlay_Plan.md`); this document
covers the current state.

## Phase 0 - inspection (done)

- WPF entry point: `App.xaml.cs` -> `MainWindow` -> `ViewModels.MainViewModel`.
- `MainViewModel` owns the long-lived services (`LiveTelemetryService`,
  `ReportService`, `RecordingLibrary`), a 100 ms `DispatcherTimer` pump, and the page
  view models. It is disposed from `MainWindow.OnClosed`.
- Telemetry contract: `AccEngineer.Telemetry.TelemetrySample` (immutable record); the
  live access point is `LiveTelemetryService.Latest` / `SnapshotRecent()`.
- Settings persist to `%AppData%\AccEngineer\settings.json` (`AppSettings`); the
  overlay reuses that directory.
- `App.xaml` merges `Themes/Palette.xaml`, `Themes/Controls.xaml` and
  `Themes/OverlayClassic.xaml`. MVVM base is `AccEngineer.UI.Mvvm.ObservableObject`.
- No overlay infrastructure existed before this work (greenfield).

## Phase 1 - overlay infrastructure (done)

Transparent, always-on-top, click-through panel host with lock/edit behaviour, global
hotkeys and persisted placement.

- `OverlayLayout.cs` / `OverlayLayoutStore.cs` - versioned layout model persisted as
  `%AppData%\AccEngineer\overlay.json`.
- `OverlayWindowBase.cs` - transparent, topmost, non-activating (`WS_EX_NOACTIVATE` +
  `WS_EX_TOOLWINDOW`), click-through (`WS_EX_TRANSPARENT`) while locked, drag/resize
  while editing, desktop-clamped.
- `OverlayHotkeys.cs` - `RegisterHotKey` on a hidden message-only window.
- `OverlayManager.cs` - owns the panels + persistence; wired into `MainViewModel.Overlays`.
- `Themes/OverlayClassic.xaml` - classic broadcast colours/styles.

## Phase 2 - visual primitives (done)

- `Overlay/Controls/OverlayTimingValue.cs` - a `TextBlock` with `TimingState`
  (`Normal`, `PersonalBest`, `SessionBest`, `Unavailable`); session best = purple,
  personal best = green, unavailable = `--`.
- `Overlay/Controls/OverlayBar.cs` - a lightweight 0..1 bar element.
- `Overlay/Controls/OverlayStatusIndicator.xaml(.cs)` - dot + label.
- `Themes/OverlayClassic.xaml` - dense table/row/cell styles, separators, bar track,
  gear badge, class-colour brushes.

## Phase 3 - timing + relative panels with a demo data source

Scope: the telemetry-source abstraction, two real panels (timing and relative/nearby)
and synthetic demo data so the layout can be tuned without ACC.

### Files

- `Overlay/IOverlayTelemetrySource.cs` - `Latest` + `Recent(maxCount)`; live impl
  `LiveOverlayTelemetrySource` wraps `LiveTelemetryService`.
- `Overlay/DemoOverlayTelemetrySource.cs` - synthetic cars driving a circle (player =
  car 1), used when demo mode is on.
- `Overlay/OverlayPanelViewModelBase.cs` - polls the source, exposes `Telemetry` and
  handles disposal.
- `Overlay/Panels/TimingPanelViewModel.cs` + `TimingOverlayPanel.xaml(.cs)` - speed,
  gear, RPM bar, current/last/best lap.
- `Overlay/Panels/RelativePanelViewModel.cs` + `RelativeRow.cs` +
  `RelativeOverlayPanel.xaml(.cs)` - nearby cars ordered by projecting each car onto the
  player's recent racing line (gaps in **metres, estimates**).
- `Overlay/OverlayPanelWindow.cs` - generic host wrapping a panel control with the
  shared edit outline, drag and resize grip.
- `Overlay/OverlayManager.cs` - now multi-panel (`timing`, `relative`, `inputs`, `map`,
  `test`) with demo-mode swap, `Panels`, `AnyVisible` / `AnyEditing`, `ToggleAll` /
  `ShowAll` / `HideAll` / `ToggleEditAll` / `TogglePanel(id)` / `ResetToDefault`.

### Data-availability note

For cars other than the player the graphics page only exposes `CarCoordinates` and
`CarId`; opponent names and official gaps/lap times are not available. The relative
panel therefore orders cars by projecting them onto the player's recent line and
labels the gaps as estimates. Ground plane is X-Z.

## Phase 4 - inputs / gear panel

- `Overlay/Panels/InputsPanelViewModel.cs` + `InputsOverlayPanel.xaml(.cs)` - a compact
  horizontal strip: a throttle (green) / brake (red) trace graph on the left over a faint
  grid, then vertical clutch / brake / throttle pedal columns, the gear and speed (kph)
  block, and a circular steering wheel at the right that rotates with the steering angle.
- Each input channel degrades independently: a missing channel shows `--` and its trace
  stops while the others keep rendering. Traces are bounded ring buffers (120 samples)
  rebuilt at the poll cadence and cleared on signal loss so a stale line is never drawn
  as if live.
- Clutch input is sourced from the shared-memory physics page (`ClutchInput`). Steering
  exposes both a magnitude (`SteeringBar`) and a signed `SteeringAngle` (-1..+1) for the
  wheel graphic.
- `Overlay/Controls/SteeringWheel.cs` - rim, three spokes and hub that rotate to the
  current angle, with a fixed red reference marker on the upper-right rim.
- `Overlay/Controls/OverlayBar.cs` gained an `Orientation` (horizontal or vertical) for the
  pedal columns. `Controls/TraceChart.cs` gained optional gridlines (`ShowGrid`).
- Uses the central `Services/Display` formatter for units (no hard-coded assumptions).
- `Themes/OverlayClassic.xaml` gains throttle/brake/steering brushes.
- Registered in `OverlayManager` as panel `inputs` (default 40,40 420x104, hidden).

## Phase 5 - track map

There is no official track-outline source in the shared-memory API, so the static
outline is **built from the player's own driven line** and cached separately from the
live car markers (rebuilt every tick). All positions go through one calibration
transform so markers stay aligned to the outline.

- `Services/TrackMapCalibration.cs` - pure (WPF-free) world-to-map fit returning a
  uniform scale + offset; a `FitPath` on the control keeps the transform fixed while
  markers move (no fit jitter). Unit tested in `tests/AccEngineer.UI.Tests`.
- `Services/CarColour.cs` - shared stable per-car colour, now used by both the relative
  panel and the map.
- `Controls/TrackMap.cs` - gained optional per-marker brushes and `FitPath`; existing
  binding shape is unchanged.
- `Overlay/Panels/TrackMapViewModel.cs` + `TrackMapOverlayPanel.xaml(.cs)` - cached
  outline (downsampled to <= 600 points, refreshed ~1 s) plus per-tick markers; the
  player marker uses the distinct player brush, opponents their stable colour.
- Honest empty states: unknown track / waiting for movement ("outline unavailable"),
  no other cars, and no-signal.
- Registered in `OverlayManager` as panel `map` (default 560,40 250x250, hidden).

> Note: ACC does not flag pit lane in the exposed data, so the map does not attempt to
> distinguish pit state; it would be shown as a normal position.

## Phase 6 - layout editor, profiles and appearance (current)

Adds a normal (non-overlay) dialog for managing how the overlay looks, named layouts and
per-simulator profiles.

- **Document v2** (`OverlayLayout.cs`) - `OverlayLayoutDocument` holds `ActiveProfile` and
  a `Profiles` map; each `OverlayLayoutProfile` has a `Simulator`, an `ActiveLayout`, a
  `Layouts` map and optional `ResolutionLayouts` (`"1920x1080"` -> layout name) overrides.
  Each `OverlayLayout` now also carries an `OverlayAppearance` (font scale, trace length,
  relative rows, relative gap/name columns). **v1 files are migrated** into the default
  `acc` profile / `Default` layout on load.
- **Document v3** (`OverlayLayoutSchema`) - the inputs panel changed from a portrait stack
  to a landscape strip, so on load any document older than v3 has its saved `inputs`
  geometry re-based to the new landscape default (`OverlayLayoutStore.MigrateToV3`). Other
  panels keep their placement. v1/v2 files still load and are stamped as v3.
- **Sanitisation** - `OverlayPanelState.Sanitize()` / `OverlayAppearance.Sanitize()` clamp
  and coerce values (including non-finite `NaN`/`Infinity` from un-arranged windows) before
  serialization, because `System.Text.Json` refuses non-finite doubles. Called from
  `CaptureState`, `Save` and the store, so a bad value can never drop the whole layout.
- **`OverlayManager`** - now exposes profiles/layouts/appearance (`ProfileNames`,
  `SetProfile`, `AddProfile`, `DeleteProfile`, `LayoutNames`, `SetLayout`, `SaveLayoutAs`,
  `DeleteLayout`, `Appearance`, `SetAppearance`, `SetPanelVisible`, `SetPanelOpacity`,
  `PinCurrentResolution` / `UnpinCurrentResolution`, `CurrentResolution`, `IsResolutionPinned`,
  `ResetToDefault`) plus `Changed` (state) and `EditorRequested` (hotkey/UI) events.
- **Appearance plumbing** - `OverlayPanelViewModelBase.ApplyAppearance` (overridden by the
  inputs + relative panels) and `OverlayWindowBase.SetContentScale` (uniform
  `LayoutTransform` on the panel content, clamped 0.6-2.0).
- **Content-driven minimum** - after an appearance change (and on first render) each
  `OverlayPanelWindow` measures its content and raises the window minimum to fit, so a
  larger font scale grows the panel instead of clipping its columns. The window keeps the
  size the user chose (`OverlayWindowBase.UserWidth`/`UserHeight`) separately from the
  content minimum: the visible frame is `max(user, content)`, and only the user frame is
  persisted - scaling back down shrinks the panel to the user's size, so a font-scale
  bump can never permanently inflate the saved layout.
- **`OverlayManagerViewModel`** + **`Views/OverlayManagerWindow.xaml(.cs)`** - the editor:
  profile/layout selection (add/save-as/delete), per-resolution pinning, appearance sliders,
  a panel list with visibility + opacity, and Show/Hide/Edit/Reset actions with a status
  footer.
- Opened from Settings -> **Configure Overlay...** or the **Ctrl+Alt+M** hotkey
  (`MainViewModel.OpenOverlayManager`, subscribed to `Overlays.EditorRequested`).

## Controls

- **Ctrl+Alt+O** - show / hide all panels.
- **Ctrl+Alt+E** - toggle edit (unlock) mode.
- **Ctrl+Alt+M** - open the layout editor.
- Settings page -> **OVERLAY** section: Show / Hide, Toggle Edit Mode, Reset Layout,
  Toggle Demo Data, per-panel visibility, **Configure Overlay...** and the live status /
  hotkey availability.

While locked the panels are click-through, so mouse input reaches the simulator. In
edit mode drag a header to move a panel and the bottom-right grip to resize; placement,
size and opacity are saved automatically and restored on restart.

## Manual checklist

- [ ] Panels render over the sim with no title bar and stay on top.
- [ ] Locked: clicks pass through to the simulator.
- [ ] Ctrl+Alt+O and Ctrl+Alt+E work while the sim has focus.
- [ ] Edit mode: a header drags its panel, the grip resizes it.
- [ ] Empty state shows `--` for every value and a non-live status dot.
- [ ] Placement survives an app restart.
- [ ] When values are unavailable they read `--` (never a fabricated zero).
- [ ] Demo data moves the timing + relative panels and drives the inputs panel so the
      layout can be tuned offline.
- [ ] The layout editor opens (Ctrl+Alt+M / Configure Overlay...) and profile, layout,
      appearance and per-panel visibility/opacity changes apply live.
- [ ] Switching profile/layout (or toggling demo) keeps the panels that should be visible.
- [ ] A manually edited / older (v1) `overlay.json` still loads and is migrated.

> Note: live-over-simulator behaviour (click-through, topmost, hotkeys) can only be
> verified with ACC running; the checks above are manual.
