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

    /// <summary>Signed proximity distance in metres to the player ("0.0" on the player
    /// row, "--" when it cannot be derived).</summary>
    public string GapText { get; init; } = "--";
}
