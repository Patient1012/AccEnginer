using System.Windows;

namespace AccEngineer.UI;

/// <summary>Shell window. The view model is supplied by <see cref="App"/>.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as ViewModels.MainViewModel)?.Dispose();
        DataContext = null;
        base.OnClosed(e);
    }
}
