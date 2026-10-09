using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccEngineer.UI.Mvvm;

/// <summary>Minimal INotifyPropertyChanged base. Kept dependency-free on purpose.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Signals that every property may have changed (WPF re-reads all bindings).</summary>
    protected void OnAllPropertiesChanged() => OnPropertyChanged(string.Empty);
}
