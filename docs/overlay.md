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

## Phase 1 - single classic-style test panel (current)

Scope: one transparent test panel with lock/edit behaviour, global hotkeys and
persisted placement. No telemetry-adapter changes, no AI/TTS, no other panels yet.

### Files

- `src/AccEngineer.UI/Overlay/OverlayLayout.cs` / `OverlayLayoutStore.cs` - versioned
  layout model persisted as `%AppData%\AccEngineer\overlay.json`.
- `src/AccEngineer.UI/Overlay/OverlayWindowBase.cs` - transparent, topmost,
  non-activating (`WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`), click-through
  (`WS_EX_TRANSPARENT`) while locked, drag/resize while editing, desktop-clamped.
- `src/AccEngineer.UI/Overlay/OverlayHotkeys.cs` - `RegisterHotKey` on a hidden
  message-only window.
- `src/AccEngineer.UI/Overlay/OverlayManager.cs` - owns panels + persistence; wired
  into `MainViewModel.Overlays`.
- `src/AccEngineer.UI/Overlay/TestPanelViewModel.cs` - reads `Latest` at 50 ms; missing
  values render as `--`.
- `src/AccEngineer.UI/Overlay/Panels/TestOverlayPanel.xaml(.cs)` - the panel.
- `src/AccEngineer.UI/Themes/OverlayClassic.xaml` - classic broadcast colours/styles.

### Controls

- **Ctrl+Alt+O** - show / hide the overlay.
- **Ctrl+Alt+E** - toggle edit (unlock) mode.
- Settings page -> **OVERLAY** section has Show / Hide, Toggle Edit Mode and Reset
  Position buttons, plus the live status and hotkey availability.

While locked the panel is click-through, so mouse input reaches the simulator. In edit
mode drag the header to move and the bottom-right grip to resize; placement, size and
opacity are saved automatically and restored on restart.

### Manual checklist

- [ ] Panel renders over the sim with no title bar and stays on top.
- [ ] Locked: clicks pass through to the simulator.
- [ ] Ctrl+Alt+O and Ctrl+Alt+E work while the sim has focus.
- [ ] Edit mode: header drags the panel, grip resizes it.
- [ ] Empty state shows `--` for every value and a non-live status dot.
- [ ] Placement survives an app restart.
- [ ] When values are unavailable they read `--` (never a fabricated zero).

> Note: live-over-simulator behaviour (click-through, topmost, hotkeys) can only be
> verified with ACC running; the checks above are manual.
