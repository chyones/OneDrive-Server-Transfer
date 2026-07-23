using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Tests.TestSupport;
using OneDriveServerTransfer.ViewModels;

namespace OneDriveServerTransfer.Tests.ViewModels;

public class MainViewModelTests
{
    private static readonly OperatorIdentity Identity = new(
        "33333333-3333-3333-3333-333333333333", "operator@example.test", "Test Operator", "11111111-1111-1111-1111-111111111111");

    private static MainViewModel CreateViewModel(
        FakeAuthenticationService authenticationService,
        bool rememberSignInDefault = true)
    {
        return new MainViewModel(
            authenticationService,
            Options.Create(new AuthenticationOptions { RememberSignInDefault = rememberSignInDefault }),
            new FakeEmployeeSourceResolver(),
            new FakeDestinationSessionService(),
            new FakeScanService(),
            new FakeTransferOrchestrator(),
            new FakeFolderPickerService(),
            new FakeShellService(),
            NullLogger<MainViewModel>.Instance);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition(), "The expected view-model state was not reached in time.");
    }

    [Fact]
    public void InitialStateIsSignedOut()
    {
        var viewModel = CreateViewModel(new FakeAuthenticationService());

        Assert.False(viewModel.IsSignedIn);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.HasError);
        Assert.True(viewModel.SignInCommand.CanExecute(null));
        Assert.False(viewModel.SignOutCommand.CanExecute(null));
    }

    [Fact]
    public void RememberSignInDefaultsFromConfiguration()
    {
        Assert.True(CreateViewModel(new FakeAuthenticationService(), rememberSignInDefault: true).RememberSignIn);
        Assert.False(CreateViewModel(new FakeAuthenticationService(), rememberSignInDefault: false).RememberSignIn);
    }

    [Fact]
    public async Task SignInSuccessShowsOperator()
    {
        var service = new FakeAuthenticationService();
        var viewModel = CreateViewModel(service);

        viewModel.SignInCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsSignedIn);

        Assert.False(viewModel.IsBusy);
        Assert.Equal("Test Operator", viewModel.OperatorDisplayName);
        Assert.Equal("operator@example.test", viewModel.OperatorUpn);
        Assert.False(viewModel.HasError);
        Assert.False(viewModel.SignInCommand.CanExecute(null));
        Assert.True(viewModel.SignOutCommand.CanExecute(null));
    }

    [Fact]
    public async Task SignInPassesRememberChoiceAndWindowHandle()
    {
        var service = new FakeAuthenticationService();
        var viewModel = CreateViewModel(service);
        var handle = new IntPtr(12345);
        viewModel.WindowHandleProvider = () => handle;
        viewModel.RememberSignIn = false;

        viewModel.SignInCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsSignedIn);

        Assert.Equal(handle, service.LastSignInWindowHandle);
        Assert.False(service.LastRememberSignIn);
    }

    [Fact]
    public async Task SignInErrorShowsReferenceCodedErrorState()
    {
        var service = new FakeAuthenticationService
        {
            SignInHandler = (_, _, _) => Task.FromException<OperatorIdentity>(AuthenticationErrors.ConsentRequired()),
        };
        var viewModel = CreateViewModel(service);

        viewModel.SignInCommand.Execute(null);
        await WaitForAsync(() => viewModel.HasError);

        Assert.False(viewModel.IsSignedIn);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(AuthenticationErrorCodes.ConsentRequired, viewModel.ErrorReferenceCode);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorTitle));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorAction));
    }

    [Fact]
    public async Task SignInShowsLoadingStateDuringOperation()
    {
        var gate = new TaskCompletionSource<OperatorIdentity>();
        var service = new FakeAuthenticationService
        {
            SignInHandler = (_, _, _) => gate.Task,
        };
        var viewModel = CreateViewModel(service);

        viewModel.SignInCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsBusy);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.SignInCommand.CanExecute(null));

        gate.SetResult(Identity);
        await WaitForAsync(() => !viewModel.IsBusy);
        Assert.True(viewModel.IsSignedIn);
    }

    [Fact]
    public async Task SignOutReturnsToSignedOutState()
    {
        var service = new FakeAuthenticationService();
        var viewModel = CreateViewModel(service);

        viewModel.SignInCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsSignedIn);

        viewModel.SignOutCommand.Execute(null);
        await WaitForAsync(() => !viewModel.IsSignedIn && !viewModel.IsBusy);

        Assert.Equal(1, service.SignOutCallCount);
        Assert.Null(viewModel.OperatorDisplayName);
        Assert.Null(viewModel.OperatorUpn);
        Assert.Equal(AuthenticationErrors.SignOutDescription, viewModel.StatusMessage);
        Assert.True(viewModel.SignInCommand.CanExecute(null));
    }

    [Fact]
    public async Task InitializeRestoresRememberedSession()
    {
        var service = new FakeAuthenticationService();
        service.SetSignedInOperator(Identity);
        var viewModel = CreateViewModel(service);

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsSignedIn);
        Assert.Equal("Test Operator", viewModel.OperatorDisplayName);
    }

    [Fact]
    public async Task InitializeShowsCacheCorruptionError()
    {
        var service = new FakeAuthenticationService
        {
            GetCurrentOperatorHandler = _ => Task.FromException<OperatorIdentity?>(AuthenticationErrors.CacheCorrupted()),
        };
        var viewModel = CreateViewModel(service);

        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsSignedIn);
        Assert.True(viewModel.HasError);
        Assert.Equal(AuthenticationErrorCodes.CacheCorrupted, viewModel.ErrorReferenceCode);
    }
}
