using System.Windows.Media;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>One row in the standings tower. Plain immutable row; the list is rebuilt
/// each tick so binding needs no change notification.</summary>
public sealed class StandingsRow
{
    public int CarIndex { get; init; }

    /// <summary>Official position, or 0 when the source has not classified the car.</summary>
    public int Position { get; init; }

    public bool IsPlayer { get; init; }

    public bool InPit { get; init; }

    /// <summary>Official race number, or null when unavailable (rendered as blank).</summary>
    public int? RaceNumber { get; init; }

    public string NumberText => RaceNumber is { } number ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

    public string PositionText => Position > 0 ? Position.ToString(System.Globalization.CultureInfo.InvariantCulture) : "--";

    /// <summary>Driver name, or an explicit unavailable placeholder. Never invented.</summary>
    public string Name { get; init; } = "--";

    public string GapText { get; init; } = "--";

    public string LastLapText { get; init; } = "--";

    public Brush Stripe { get; init; } = Brushes.Gray;
}
