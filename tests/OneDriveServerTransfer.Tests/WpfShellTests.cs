using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Tests.TestSupport;
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
                var viewModel = new MainViewModel(
                    new FakeAuthenticationService(),
                    Options.Create(new AuthenticationOptions()),
                    new FakeEmployeeSourceResolver(),
                    new FakeDestinationSessionService(),
                    new FakeScanService(),
                    new FakeTransferOrchestrator(),
                    new FakeFolderPickerService(),
                    new FakeShellService(),
                    NullLogger<MainViewModel>.Instance);
                var window = new MainWindow(viewModel);
                Assert.Equal("OneDrive Server Transfer", window.Title);
                Assert.Same(viewModel, window.DataContext);
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
