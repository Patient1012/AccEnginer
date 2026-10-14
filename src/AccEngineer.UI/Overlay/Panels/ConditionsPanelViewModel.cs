using System.Globalization;
using AccEngineer.Telemetry;
using AccEngineer.UI.Services;
using System.Windows.Media;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// Compact conditions panel: ambient and track temperatures, track grip, tyre type
/// and current session flag. Values use honest empty states ("--") when unavailable.
/// </summary>
public sealed class ConditionsPanelViewModel : OverlayPanelViewModel
{
    public ConditionsPanelViewModel(IOverlayTelemetrySource telemetry) : base(telemetry, 100) { }

    public bool IsLive { get; private set; }

    public string StatusText { get; private set; } = "NO SIGNAL";

    public string SessionText { get; private set; } = "--";

    public string TrackText { get; private set; } = "--";

    public string AirTempText { get; private set; } = "--";

    public string TrackTempText { get; private set; } = "--";

    public string GripText { get; private set; } = "--";

    public string TyresText { get; private set; } = "--";

    public string FlagText { get; private set; } = "--";

    public Brush FlagBrush { get; private set; } = Brushes.Transparent;

    protected override void OnSessionChanged()
    {
        // Session transition: keep temperatures/grip/tyres if still valid, but the
        // flag is explicitly reset to a sensible default to avoid showing stale colours.
        FlagText = "--";
        FlagBrush = Brushes.Transparent;
        OnPropertyChanged(nameof(FlagText));
        OnPropertyChanged(nameof(FlagBrush));
    }

    protected override void Refresh(TelemetrySample? sample)
    {
        if (sample is null)
        {
            IsLive = false;
            StatusText = "NO SIGNAL";
            SessionText = "--";
            TrackText = "--";
            AirTempText = "--";
            TrackTempText = "--";
            GripText = "--";
            TyresText = "--";
            FlagText = "--";
            FlagBrush = Brushes.Transparent;
            OnAllPropertiesChanged();
            return;
        }

        IsLive = true;
        StatusText = "LIVE";
        SessionText = $"{sample.SessionType} · {sample.SessionState}";
        TrackText = string.IsNullOrEmpty(sample.TrackName) ? "--" : sample.TrackName;
        AirTempText = sample.AirTempC is { } air ? air.ToString("0.0", CultureInfo.InvariantCulture) + " °C" : "--";
        TrackTempText = sample.TrackTempC is { } road ? road.ToString("0.0", CultureInfo.InvariantCulture) + " °C" : "--";
        GripText = sample.SurfaceGrip is { } grip ? grip.ToString("0.000", CultureInfo.InvariantCulture) : "--";
        TyresText = sample.RainTyresOn ? "WET" : "DRY";
        (FlagText, FlagBrush) = FlagDisplay(sample.Flag);
        OnAllPropertiesChanged();
    }

    private static (string text, Brush brush) FlagDisplay(SessionFlag flag) => flag switch
    {
        SessionFlag.None => ("NONE", Brushes.Transparent),
        SessionFlag.Blue => ("BLUE", (Brush)System.Windows.Application.Current?.Resources["OverlayFlagBlueBrush"] ?? Brushes.DodgerBlue),
        SessionFlag.Yellow => ("YELLOW", (Brush)System.Windows.Application.Current?.Resources["OverlayFlagYellowBrush"] ?? Brushes.Gold),
        SessionFlag.Black => ("BLACK", (Brush)System.Windows.Application.Current?.Resources["OverlayFlagBlackBrush"] ?? Brushes.Gray),
        SessionFlag.White => ("WHITE", (Brush)System.Windows.Application.Current?.Resources["OverlayFlagWhiteBrush"] ?? Brushes.White),
        SessionFlag.Checkered => ("CHECKERED", (Brush)System.Windows.Application.Current?.Resources["OverlayFlagCheckeredBrush"] ?? Brushes.Goldenrod),
        SessionFlag.Red => ("RED", (Brush)System.Windows.Application.Current?.Resources["OverlayFlagRedBrush"] ?? Brushes.IndianRed),
        SessionFlag.Green => ("GREEN", (Brush)System.Windows.Application.Current?.Resources["OverlayFlagGreenBrush"] ?? Brushes.SeaGreen),
        _ => ("--", Brushes.Transparent)
    };
}
