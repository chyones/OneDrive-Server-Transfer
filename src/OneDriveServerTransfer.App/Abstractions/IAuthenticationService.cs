using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Authenticates the authorized IT transfer operator through delegated interactive
/// Microsoft sign-in (MSAL, WAM preferred with system-browser fallback). The application
/// never requests, processes, stores, or logs an employee password and never
/// authenticates as the employee. Only the approved delegated read scopes are requested.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// The current application authentication state. Only
    /// <see cref="AuthenticationState.SignedInValidated" /> permits later-phase source
    /// resolution or scan operations.
    /// </summary>
    AuthenticationState State { get; }

    bool IsSignedIn { get; }

    /// <summary>
    /// Returns the validated operator when one is signed in, attempting one silent
    /// cache-backed restore first. Returns null when interactive sign-in is required.
    /// </summary>
    Task<OperatorIdentity?> GetCurrentOperatorAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs the official Microsoft interactive sign-in flow for the authorized IT
    /// operator. <paramref name="rememberSignIn" /> controls only whether the
    /// application-owned DPAPI-protected persistent cache keeps the session for later
    /// application starts; it does not affect Microsoft browser or Windows sessions.
    /// Throws <see cref="UserFacingAuthException" /> with a stable reference code on
    /// failure.
    /// </summary>
    Task<OperatorIdentity> SignInAsync(
        IntPtr parentWindowHandle,
        bool rememberSignIn,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes the application-owned cached account state and the persistent application
    /// cache. This does not sign the operator out of Windows, WAM, browsers, or any
    /// other Microsoft session.
    /// </summary>
    Task SignOutAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Acquires a Microsoft Graph access token silently for the validated operator.
    /// Throws <see cref="UserFacingAuthException" /> requiring reauthentication when the
    /// session can no longer be renewed silently.
    /// </summary>
    Task<string> AcquireGraphAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Identity of the signed-in IT transfer operator. The Microsoft Entra object ID is the
/// durable identity; the user principal name and display name are display and audit
/// data only.
/// </summary>
public sealed record OperatorIdentity(
    string EntraObjectId,
    string UserPrincipalName,
    string DisplayName,
    string TenantId);
