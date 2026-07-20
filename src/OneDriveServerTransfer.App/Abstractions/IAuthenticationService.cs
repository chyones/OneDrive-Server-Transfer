namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Authenticates the authorized IT transfer operator through delegated interactive
/// Microsoft sign-in. Implemented in milestone M2 with MSAL; no implementation exists in
/// M1 and none may be faked. The application never requests, processes, stores, or logs
/// an employee password and never authenticates as the employee. Only the approved
/// delegated read scopes may be requested.
/// </summary>
public interface IAuthenticationService
{
    bool IsSignedIn { get; }

    Task<OperatorIdentity?> GetCurrentOperatorAsync(CancellationToken cancellationToken);

    Task<OperatorIdentity> SignInAsync(CancellationToken cancellationToken);

    Task SignOutAsync(CancellationToken cancellationToken);

    Task<string> AcquireGraphAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Identity of the signed-in IT transfer operator. The Microsoft Entra object ID is the
/// durable identity; the user principal name is display and audit data only.
/// </summary>
public sealed record OperatorIdentity(string EntraObjectId, string UserPrincipalName, string TenantId);
