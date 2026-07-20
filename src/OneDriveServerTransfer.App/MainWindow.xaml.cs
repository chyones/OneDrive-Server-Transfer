using System.Windows;
using System.Windows.Interop;
using OneDriveServerTransfer.ViewModels;

namespace OneDriveServerTransfer;

/// <summary>
/// The single application window. All state and behavior live in the view model (MVVM);
/// the code-behind only wires the injected view model as the data context and provides
/// the window handle required by the interactive Microsoft sign-in flow.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
        ViewModel.WindowHandleProvider = () => new WindowInteropHelper(this).Handle;
    }

    public MainViewModel ViewModel { get; }
}
