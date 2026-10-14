using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AccEngineer.UI.Overlay.Controls;

/// <summary>
/// Visual state of a timing readout. Purple marks the overall session best, green
/// marks the driver's own best, and Unavailable dims the value (the caller renders
/// it as "--"). Keeping this as an enum means panels never hard-code the colours.
/// </summary>
public enum TimingState
{
    Normal,
    PersonalBest,
    SessionBest,
    Unavailable
}

/// <summary>
/// Tabular-numeral text block for lap times, deltas and gaps. Foreground is driven
/// by <see cref="State"/> so the "personal best" / "session best" colour convention
/// lives in exactly one place.
/// </summary>
public sealed class OverlayTimingValue : TextBlock
{
    // iRacing-style convention: purple = session best, green = personal best.
    private static readonly Brush NormalBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)));
    private static readonly Brush PersonalBestBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x6F, 0xE3, 0x9A)));
    private static readonly Brush SessionBestBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xC0, 0x8C, 0xFF)));
    private static readonly Brush UnavailableBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xA6, 0xAC, 0xB4)));

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(TimingState), typeof(OverlayTimingValue),
        new PropertyMetadata(TimingState.Normal, OnStateChanged));

    public OverlayTimingValue()
    {
        FontFamily = new FontFamily("Consolas");
        // Align digits column-to-column across rows.
        Typography.SetNumeralAlignment(this, FontNumeralAlignment.Tabular);
    }

    public TimingState State
    {
        get => (TimingState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OverlayTimingValue)d).Foreground = BrushFor((TimingState)e.NewValue);

    private static Brush BrushFor(TimingState state) => state switch
    {
        TimingState.PersonalBest => PersonalBestBrush,
        TimingState.SessionBest => SessionBestBrush,
        TimingState.Unavailable => UnavailableBrush,
        _ => NormalBrush
    };

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
