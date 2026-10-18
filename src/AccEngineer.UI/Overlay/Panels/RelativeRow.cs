namespace AccEngineer.UI.Overlay.Panels;

/// <summary>One row in the relative panel. Plain immutable row; the list is
/// rebuilt each tick so binding needs no change notification.</summary>
public sealed class RelativeRow
{
    public int CarId { get; init; }

    public int Position { get; init; }

    public bool IsPlayer { get; init; }

    /// <summary>Car number from the broadcast entry list, prefixed with '#'.
    /// Empty when the feed did not report one — never the row index.</summary>
    public string CarNumberText { get; init; } = string.Empty;

    /// <summary>Driver name from broadcasting, or "CAR nn" for the projection fallback.</summary>
    public string Name { get; init; } = "--";

    /// <summary>Cup-class badge text ("PA"/"AM"/"SIL"/"NAT"). Empty means no badge,
    /// which keeps the column aligned without inventing ratings.</summary>
    public string ClassText { get; init; } = string.Empty;

    /// <summary>Signed time gap to the player in seconds: positive when the car is
    /// behind the player, negative when ahead, exactly 0 on the player row. Null when
    /// the source cannot support a value (never a fabricated number). The distance
    /// behind this value is intentionally not stored — the column displays time.</summary>
    public double? GapSeconds { get; init; }

    /// <summary>Display text for <see cref="GapSeconds"/> from the shared formatter
    /// (<c>+0.842</c>/<c>-1.275</c>/<c>0.000</c>/<c>--</c>).</summary>
    public string GapText { get; init; } = "--";
}
