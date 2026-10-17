using System.Globalization;
using AccEngineer.Telemetry;
using AccEngineer.UI.Overlay.Controls;
using AccEngineer.UI.Services;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Compact timing panel: session type/state, lap number, current lap, last lap,
/// personal best and session context. Values use tabular numerals and show "--"
/// when unavailable. The state flags best/last semantics via <see cref="TimingState"/>.
/// </summary>
public sealed class TimingPanelViewModel : OverlayPanelViewModel
{
    public TimingPanelViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 50) { }

    public bool IsLive { get; private set; }
    public string StatusText { get; private set; } = "NO SIGNAL";
    public string SessionText { get; private set; } = "--";
    public string LapText { get; private set; } = "--";
    public string CurrentLapText { get; private set; } = "--";
    public TimingState CurrentLapState { get; private set; } = TimingState.Unavailable;
    public string LastLapText { get; private set; } = "--";
    public TimingState LastLapState { get; private set; } = TimingState.Unavailable;
    public string PersonalBestText { get; private set; } = "--";
    public TimingState PersonalBestState { get; private set; } = TimingState.Unavailable;
    public string SessionTextSecondary { get; private set; } = "--";

    /// <summary>Sector position and last completed sector time, for example <c>2/3  0:27.000</c>.</summary>
    public string SectorText { get; private set; } = "--";

    /// <summary>Live delta to the reference lap, for example <c>-0.180 s</c>.</summary>
    public string DeltaText { get; private set; } = "--";

    public TimingState DeltaState { get; private set; } = TimingState.Unavailable;

    protected override void Refresh(TelemetrySample? sample)
    {
        if (sample is null)
        {
            IsLive = false;
            StatusText = "NO SIGNAL";
            SessionText = "--";
            LapText = "--";
            CurrentLapText = "--";
            CurrentLapState = TimingState.Unavailable;
            LastLapText = "--";
            LastLapState = TimingState.Unavailable;
            PersonalBestText = "--";
            PersonalBestState = TimingState.Unavailable;
            SessionTextSecondary = "--";
            SectorText = "--";
            DeltaText = "--";
            DeltaState = TimingState.Unavailable;
            OnAllPropertiesChanged();
            return;
        }

        IsLive = true;
        StatusText = "LIVE";
        SessionText = $"{sample.SessionType} · {sample.SessionState}";
        LapText = sample.LapNumber.ToString(CultureInfo.InvariantCulture);
        SessionTextSecondary = string.IsNullOrEmpty(sample.TrackName) ? "--" : sample.TrackName;

        // Current lap
        if (sample.CurrentLapTimeMs is { } currentMs and > 0)
        {
            CurrentLapText = Display.LapTimeMs(currentMs);
            // We don't know PB/SB for "current" live; just normal if valid-looking.
            CurrentLapState = TimingState.Normal;
        }
        else
        {
            CurrentLapText = "--";
            CurrentLapState = TimingState.Unavailable;
        }

        // Last lap
        if (sample.LastLapTimeMs is { } lastMs and > 0)
        {
            LastLapText = Display.LapTimeMs(lastMs);
            LastLapState = sample.IsLapValid && !sample.Quality.HasFlag(DataQualityFlags.InvalidLap)
                ? TimingState.PersonalBest // conservative; real PB/SB could be compared later
                : TimingState.Normal;
        }
        else
        {
            LastLapText = "--";
            LastLapState = TimingState.Unavailable;
        }

        // Personal best
        if (sample.BestLapTimeMs is { } bestMs and > 0)
        {
            PersonalBestText = Display.LapTimeMs(bestMs);
            PersonalBestState = TimingState.PersonalBest;
        }
        else
        {
            PersonalBestText = "--";
            PersonalBestState = TimingState.Unavailable;
        }

        // Sector: current position plus the last completed sector time.
        var sectorLabel = sample.CurrentSectorIndex is { } index && sample.SectorCount is { } count and > 0
            ? $"{index + 1}/{count}"
            : null;
        SectorText = (sectorLabel, sample.LastSectorTimeMs) switch
        {
            (not null, { } sectorMs) => $"{sectorLabel}  {Display.LapTimeMs(sectorMs)}",
            (not null, null) => sectorLabel,
            (null, { } sectorMs) => Display.LapTimeMs(sectorMs),
            _ => "--"
        };

        // Live delta: negative means ahead of the reference lap.
        if (sample.DeltaLapTimeMs is { } deltaMs)
        {
            DeltaText = Display.DeltaMs(deltaMs);
            DeltaState = deltaMs < 0 ? TimingState.DeltaAhead
                : deltaMs > 0 ? TimingState.DeltaBehind
                : TimingState.Normal;
        }
        else
        {
            DeltaText = "--";
            DeltaState = TimingState.Unavailable;
        }

        OnAllPropertiesChanged();
    }
}
