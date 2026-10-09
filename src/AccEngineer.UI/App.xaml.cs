using System.Windows;

namespace AccEngineer.UI;

/// <summary>Application entry point. The shell is created explicitly so it can be disposed.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var window = new MainWindow { DataContext = new ViewModels.MainViewModel() };
        MainWindow = window;
        window.Show();
    }
}
