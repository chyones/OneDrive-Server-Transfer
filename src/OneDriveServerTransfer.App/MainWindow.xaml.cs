using System.Windows;
using OneDriveServerTransfer.ViewModels;

namespace OneDriveServerTransfer;

/// <summary>
/// The single application window. All state and behavior live in the view model (MVVM);
/// the code-behind only wires the injected view model as the data context.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }
}
