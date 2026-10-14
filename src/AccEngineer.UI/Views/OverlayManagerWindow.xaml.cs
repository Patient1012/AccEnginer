using System.Windows;
using AccEngineer.UI.ViewModels;

namespace AccEngineer.UI.Views;

/// <summary>Non-modal editor for overlay profiles, named layouts and appearance.</summary>
public partial class OverlayManagerWindow : Window
{
    private readonly OverlayManagerViewModel _viewModel;

    public OverlayManagerWindow(OverlayManagerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }
}
