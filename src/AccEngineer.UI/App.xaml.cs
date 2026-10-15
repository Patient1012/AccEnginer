using System.Windows;
using AccEngineer.UI.Services;

namespace AccEngineer.UI;

/// <summary>Application entry point. The shell is created explicitly so it can be disposed.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Apply the saved theme before the window is created so the first frame is
        // already painted in it rather than flashing the bootstrap palette.
        ThemeManager.Apply(AppSettings.Load().Theme);

        var window = new MainWindow { DataContext = new ViewModels.MainViewModel() };
        MainWindow = window;
        window.Show();
    }
}
