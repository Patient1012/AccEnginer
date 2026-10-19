using System.Windows.Media;

namespace AccEngineer.UI.Overlay.Panels;

/// <summary>
/// A class-group header inside the standings tower. Spans the full width of the combined
/// driver/timing table and carries the class accent that is also used for that group's
/// driver-row markers.
/// </summary>
public sealed class StandingsClassHeader : StandingsListItem
{
    /// <summary>Display name of the ACC cup category, for example <c>PRO-AM</c>.</summary>
    public string ClassLabel { get; init; } = string.Empty;

    /// <summary>Number of classified drivers shown in this group, labelled for display,
    /// for example <c>6 cars</c>.</summary>
    public string CountText { get; init; } = string.Empty;

    /// <summary>Class accent colour, shared with the group's driver rows.</summary>
    public Brush Accent { get; init; } = Brushes.Gray;
}
