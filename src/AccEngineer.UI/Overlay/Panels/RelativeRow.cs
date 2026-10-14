using System.Windows.Media;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>One row in the relative/nearby panel. Plain immutable row; the list is
/// rebuilt each tick so binding needs no change notification.</summary>
public sealed class RelativeRow
{
    public int CarId { get; init; }

    public int Position { get; init; }

    public bool IsPlayer { get; init; }

    public string GapText { get; init; } = "--";

    public string Name => $"CAR {CarId}";

    public Brush Stripe { get; init; } = Brushes.Gray;
}
