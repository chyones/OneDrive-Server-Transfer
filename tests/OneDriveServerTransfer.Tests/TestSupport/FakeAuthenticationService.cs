using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Tests.TestSupport;

/// <summary>
/// Programmable IAuthenticationService double for view-model tests. Lives only in the
/// test assembly; production dependency injection always uses the real MSAL service.
/// </summary>
internal sealed class FakeAuthenticationService : IAuthenticationService
{
    private OperatorIdentity? _currentOperator;

    public AuthenticationState State { get; set; } = AuthenticationState.SignedOut;

    public bool IsSignedIn => _currentOperator is not null;

    public Func<CancellationToken, Task<OperatorIdentity?>>? GetCurrentOperatorHandler { get; set; }

    public Func<IntPtr, bool, CancellationToken, Task<OperatorIdentity>>? SignInHandler { get; set; }

    public IntPtr? LastSignInWindowHandle { get; private set; }

    public bool? LastRememberSignIn { get; private set; }

    public int SignOutCallCount { get; private set; }

    public Task<OperatorIdentity?> GetCurrentOperatorAsync(CancellationToken cancellationToken) =>
        GetCurrentOperatorHandler?.Invoke(cancellationToken) ?? Task.FromResult(_currentOperator);

    public Task<OperatorIdentity> SignInAsync(IntPtr parentWindowHandle, bool rememberSignIn, CancellationToken cancellationToken)
    {
        LastSignInWindowHandle = parentWindowHandle;
        LastRememberSignIn = rememberSignIn;
        return SignInHandler?.Invoke(parentWindowHandle, rememberSignIn, cancellationToken)
            ?? Task.FromResult(new OperatorIdentity(
                "33333333-3333-3333-3333-333333333333", "operator@example.test", "Test Operator", "11111111-1111-1111-1111-111111111111"));
    }

    public Task SignOutAsync(CancellationToken cancellationToken)
    {
        SignOutCallCount++;
        _currentOperator = null;
        return Task.CompletedTask;
    }

    public Task<string> AcquireGraphAccessTokenAsync(CancellationToken cancellationToken) =>
        Task.FromResult("test-access-token");

    public void SetSignedInOperator(OperatorIdentity identity) => _currentOperator = identity;
}
