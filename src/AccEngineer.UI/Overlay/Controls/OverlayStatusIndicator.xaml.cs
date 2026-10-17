using System.Windows;
using System.Windows.Controls;

namespace AccEngineer.UI.Overlay.Controls;

/// <summary>
/// Status dot plus short label (for example "LIVE" or "NO SIGNAL"). The dot turns
/// green when <see cref="IsActive"/> is true and amber otherwise, so panels share
/// one connection indicator.
/// </summary>
public partial class OverlayStatusIndicator : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(OverlayStatusIndicator), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(OverlayStatusIndicator), new PropertyMetadata(false));

    public static readonly DependencyProperty TestDataProperty = DependencyProperty.Register(
        nameof(TestData), typeof(bool), typeof(OverlayStatusIndicator), new PropertyMetadata(false));

    public OverlayStatusIndicator()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>When true, a small "TEST DATA" badge is shown next to the status label.</summary>
    public bool TestData
    {
        get => (bool)GetValue(TestDataProperty);
        set => SetValue(TestDataProperty, value);
    }
}
