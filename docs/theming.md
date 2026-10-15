# Application theme system

Plan and reference for the reusable UI theme system (main window, views, charts and the
RaceLab-style overlay windows).

## 1. What exists today

| Area | Where | Notes |
| --- | --- | --- |
| Colour palette | `src/AccEngineer.UI/Themes/Palette.xaml` | 11 `Color*` + 11 `Brush*` keys, hard-coded dark palette. |
| Control styles | `Themes/Controls.xaml` | Text, cards, buttons, inputs, nav. Already uses `{DynamicResource …}`. |
| Overlay theme | `Themes/OverlayClassic.xaml` | `Overlay*` brushes with literal colours; its styles use `{StaticResource …}`. |
| Composition | `App.xaml` | Merges Palette, Controls, OverlayClassic in that order. |
| Startup | `App.xaml.cs` | Builds `MainViewModel` then shows `MainWindow`. No theme hook. |
| Settings | `Services/AppSettings.cs` | JSON at `%AppData%\AccEngineer\settings.json`, loaded in `MainViewModel`, saved by `SettingsViewModel.Save()`. |
| MVVM | `Mvvm/ObservableObject.cs`, `Mvvm/RelayCommand.cs` | Manual composition, no DI container. |
| Tests | `tests/AccEngineer.UI.Tests` | xunit on `net10.0-windows` with `UseWPF`. |

Hard-coded colours outside the theme files: `Controls/Logo.xaml` (brand artwork, kept on
purpose). `Controls.xaml` and every view already referenced brushes through
`{DynamicResource …}`, so the only fix needed was `PrimaryButton`'s foreground, which was a
literal `#FF0B0F14` and is now the `TextOnAccent` token. `InputsOverlayPanel.xaml`'s graph
backdrop and the rest of the overlay chrome are handled by the overlay theme (stage 7). The
dead `AppSettings.AccentColor` field has been removed; an old settings file that still
contains `accentColor` is unaffected because unknown JSON members are skipped.

## 2. Design

**Themes are pure C# data.** Each theme is a `ThemeDefinition` holding one hex value per
semantic token (`ThemeCatalog.cs`). Keeping them in C# rather than ten XAML dictionaries
means the token set can be validated by tests, a new theme is one record, and there is no
XAML duplication.

**One dictionary, swapped at runtime.** `ThemeManager` builds a `ResourceDictionary` from a
definition containing both `ColorXxx` and `BrushXxx` keys and swaps the last merged
dictionary of `Application.Resources`. Every existing `{DynamicResource Brush…}` reference
updates in place, so no window or control is duplicated per theme.

**Bootstrap default.** `Themes/Palette.xaml` stays merged and first, so the app is usable
even if theme loading fails, and any token a theme omits falls back to the original palette.

**Overlay aliasing.** `Themes/OverlayClassic.xaml` stops owning colours and instead defines
its `Overlay*` keys as brushes bound to theme tokens (`Color="{DynamicResource Color…}"`),
with its style setters switched to `{DynamicResource}`. Overlay panel markup is untouched
and live theme changes reach the panels. Window backgrounds stay `Transparent`, so nothing
opaque is introduced.

### Semantic tokens

Surfaces `Background`, `Surface`, `SurfaceAlt`, `SurfaceHigh` ·
Text `TextPrimary`, `TextSecondary`, `TextMuted`, `TextOnAccent` ·
Lines `Border`, `Divider` ·
Accent `Accent`, `AccentHover`, `AccentDim` ·
Controls `ControlBg`, `ControlBgHover`, `ControlBgPressed`, `ControlDisabled`,
`InputBg`, `InputBorder`, `TooltipBg`, `TooltipText` ·
States `Selection`, `Focus` ·
Status `Success`, `Warning`, `Error`, `Info` ·
Charts `ChartGrid`, `ChartAxis`, `ChartLine`, `ChartFill`, `ChartThrottle`,
`ChartBrake`, `ChartSteer`, `ChartMapPath`, `ChartMapMarker`, `ChartMapPlayer` ·
Overlay `OverlayPanel`, `OverlayHeader`, `OverlayBorder`, `OverlaySeparator`,
`OverlayText`, `OverlayMuted`, `OverlayAccent`, `OverlaySuccess`, `OverlayWarning`,
`OverlaySelectedRow`, `OverlayPit`, `OverlayBarTrack`, `OverlayGearBadge`,
`OverlayGraphBg` ·
Flags `FlagBlue`, `FlagYellow`, `FlagRed`, `FlagGreen`, `FlagWhite`, `FlagBlack`,
`FlagCheckered` ·
Classes `ClassPro`, `ClassAm`, `ClassSilver`.

Each token produces `Color{Token}` and `Brush{Token}`. Input channels (throttle/brake/steer)
are given values distinct from the status colours so the two roles never read as each other.

### Themes

`Default` (the existing palette), `Gruvbox Dark`, `Gruvbox Light`, `Dracula`,
`Catppuccin Mocha`, `Catppuccin Macchiato`, `Catppuccin Latte`, `Nord`, `Tokyo Night`,
`One Dark`.

## 3. Switching and persistence

`AppSettings.Theme` stores the theme id. Unknown or missing ids resolve to `Default`.
`App.OnStartup` applies the saved theme before the main window is shown. The Settings
**Appearance** selector applies immediately and saves the id without touching any other
setting; `SettingsViewModel` exposes the list, the current selection and live swatches.

## 4. Stages

1. Plan (this document).
2. Theme architecture: `AppTheme`, `ThemePalette`, `ThemeCatalog`, `ThemeManager`,
   `AppSettings.Theme`, startup wiring. `Default` only.
3. All ten theme definitions in `ThemeCatalog`.
4. Runtime switching.
5. Settings selector + persistence.
6. Migrate application screens, controls and charts.
7. Migrate overlay windows and telemetry visualisations.
8. Tests, documentation, final validation.

## 5. Progress

- [x] 1. Plan.
- [x] 2. Architecture (`AppTheme`, `ThemePalette`, `ThemeCatalog`, `ThemeManager`,
      `AppSettings.Theme`, `App.OnStartup`).
- [x] 3. Ten themes registered in `ThemeCatalog.All`, with per-theme palettes in
      `ThemePalettes`. Checked against WCAG contrast for text, on-accent and overlay text.
- [x] 4. Runtime switching via `ThemeManager.Apply`, exercised for all ten themes.
- [x] 5. Settings **Appearance** selector: applies on selection, persists immediately,
      live preview built only from `{DynamicResource …}`, themed `ComboBox`.
- [x] 6. Application screens — already theme-aware; `PrimaryButton` foreground and the
      dead `AccentColor` field fixed.
- [ ] 7. Overlay windows and telemetry visualisations.
- [ ] 8. Tests and final validation.
