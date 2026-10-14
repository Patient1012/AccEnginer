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

## Phase 3 - timing + relative panels with a demo data source (current)

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
- `Overlay/OverlayManager.cs` - now multi-panel (`timing`, `relative`, `test`) with
  demo-mode swap, `Panels`, `AnyVisible` / `AnyEditing`, `ToggleAll` / `ShowAll` /
  `HideAll` / `ToggleEditAll` / `TogglePanel(id)` / `ResetToDefault`.

### Data-availability note

For cars other than the player the graphics page only exposes `CarCoordinates` and
`CarId`; opponent names and official gaps/lap times are not available. The relative
panel therefore orders cars by projecting them onto the player's recent line and
labels the gaps as estimates. Ground plane is X-Z.

### Controls

- **Ctrl+Alt+O** - show / hide all panels.
- **Ctrl+Alt+E** - toggle edit (unlock) mode.
- Settings page -> **OVERLAY** section: Show / Hide, Toggle Edit Mode, Reset Layout,
  Toggle Demo Data, per-panel visibility and the live status / hotkey availability.

While locked the panels are click-through, so mouse input reaches the simulator. In
edit mode drag a header to move a panel and the bottom-right grip to resize; placement,
size and opacity are saved automatically and restored on restart.

### Manual checklist

- [ ] Panels render over the sim with no title bar and stay on top.
- [ ] Locked: clicks pass through to the simulator.
- [ ] Ctrl+Alt+O and Ctrl+Alt+E work while the sim has focus.
- [ ] Edit mode: a header drags its panel, the grip resizes it.
- [ ] Empty state shows `--` for every value and a non-live status dot.
- [ ] Placement survives an app restart.
- [ ] When values are unavailable they read `--` (never a fabricated zero).
- [ ] Demo data moves the timing + relative panels so the layout can be tuned offline.

> Note: live-over-simulator behaviour (click-through, topmost, hotkeys) can only be
> verified with ACC running; the checks above are manual.
