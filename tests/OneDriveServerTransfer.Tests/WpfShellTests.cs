using OneDriveServerTransfer.ViewModels;

namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Windows-only smoke check that the single application window and its view model
/// construct and bind successfully. Full WPF startup on Windows Server 2019 remains a
/// production-acceptance check in milestone M7.
/// </summary>
public class WpfShellTests
{
    [Fact]
    public void MainWindowConstructsWithViewModelOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // WPF types can only load on Windows; CI executes this check there.
        }

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow(new MainViewModel());
                Assert.Equal("OneDrive Server Transfer", window.Title);
                Assert.IsType<MainViewModel>(window.DataContext);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }
}
