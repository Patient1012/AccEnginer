using System.Windows.Media;
using AccEngineer.UI.Mvvm;

namespace AccEngineer.UI.ViewModels;

/// <summary>Base for navigable pages. <see cref="Refresh"/> is driven by the shell pump.</summary>
public abstract class PageViewModel : ObservableObject
{
    protected PageViewModel(string title) : this(title, null) { }

    protected PageViewModel(string title, Geometry? navIcon)
    {
        Title = title;
        NavIcon = navIcon;
    }

    public string Title { get; }

    public Geometry? NavIcon { get; }

    /// <summary>Called each UI pump tick while this page is selected.</summary>
    public virtual void Refresh()
    {
    }
}
