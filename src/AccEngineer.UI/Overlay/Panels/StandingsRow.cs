using System.Globalization;
using System.Windows.Media;
using AccEngineer.UI.Overlay.Controls;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// One driver row in the standings tower. Plain immutable row; the whole list is rebuilt
/// each tick so binding needs no change notification. The driver-list half (position, car
/// number, name) and the timing half (gap, fastest, last) live on the same object and are
/// drawn in one row grid, so the two halves are always aligned line-for-line.
/// </summary>
public sealed class StandingsRow : StandingsListItem
{
    public int CarIndex { get; init; }

    /// <summary>Official classified position, or 0 when the source has not classified the car.</summary>
    public int Position { get; init; }

    public bool IsPlayer { get; init; }

    public bool InPit { get; init; }

    /// <summary>Alternate-row shading flag, computed over the visible driver rows.</summary>
    public bool Alternate { get; init; }

    /// <summary>Official race number, or null when unavailable (rendered blank).</summary>
    public int? RaceNumber { get; init; }

    public string NumberText => RaceNumber is { } number
        ? number.ToString(CultureInfo.InvariantCulture)
        : string.Empty;

    public string PositionText => Position > 0
        ? Position.ToString(CultureInfo.InvariantCulture)
        : "--";

    /// <summary>Driver name, or an explicit unavailable placeholder. Never invented.</summary>
    public string Name { get; init; } = "--";

    /// <summary>Driver-category badge text ("PLT"/"GLD"/"SLV"/"BRO"); the driver's
    /// Bronze..Platinum tier. Empty when the entry list has not reported it.</summary>
    public string RatingText { get; init; } = string.Empty;

    /// <summary>Signed interval to the car ahead in seconds (positive: the car trails
    /// its predecessor), null for the leader, for laps-down markers and when the source
    /// cannot support a value. The distance behind this value is intentionally not stored.</summary>
    public double? GapSeconds { get; init; }

    /// <summary>Display text for <see cref="GapSeconds"/> from the shared formatter
    /// (<c>+1.408</c>/<c>--</c>), or a <c>+1L</c> laps-down marker.</summary>
    public string GapText { get; init; } = "--";

    /// <summary>Best lap for this driver, or "--".</summary>
    public string FastestText { get; init; } = "--";

    /// <summary>Colour convention for the fastest-lap cell (magenta for the overall best).</summary>
    public TimingState FastestState { get; init; } = TimingState.Unavailable;

    /// <summary>Last completed lap, or "--".</summary>
    public string LastText { get; init; } = "--";

    public TimingState LastState { get; init; } = TimingState.Unavailable;

    /// <summary>Class accent colour for the car-number marker.</summary>
    public Brush ClassAccent { get; init; } = Brushes.Gray;

    /// <summary>Car manufacturer monogram ("POR", "FER", ...); empty when the entry
    /// list has not reported the car model, so the badge can be hidden.</summary>
    public string BrandText { get; init; } = string.Empty;

    /// <summary>Brand accent colour for the monogram badge (tinted per manufacturer).</summary>
    public Brush BrandAccent { get; init; } = Brushes.Gray;
}
