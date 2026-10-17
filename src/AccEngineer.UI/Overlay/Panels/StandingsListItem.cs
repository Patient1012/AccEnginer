namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// One entry in the standings tower. The tower is a single vertically stacked list of
/// heterogeneous items - class-group headers and driver rows - so a driver's name, gap,
/// fastest and last lap are all drawn from the same row object and can never drift onto
/// different lines. Any scrolling moves the whole table together.
/// </summary>
public abstract class StandingsListItem
{
}
