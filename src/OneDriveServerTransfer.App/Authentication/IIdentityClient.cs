namespace OneDriveServerTransfer.Authentication;

public enum InteractiveMode
{
    Broker,
    SystemBrowser
}

/// <summary>A cached Microsoft account. Carries no tokens.</summary>
public sealed record CachedAccount(string HomeAccountId, string HomeTenantId, string Username);

/// <summary>
/// Result of a token acquisition. The access token is used only in memory for approved
/// Graph calls and is never logged, persisted by application code, or displayed.
/// </summary>
public sealed record IdentityTokenResult(
    string AccessToken,
    string TokenTenantId,
    string ObjectId,
    string? UserPrincipalName,
    string? IdentityProvider,
    IReadOnlyCollection<string> GrantedScopes,
    DateTimeOffset ExpiresOn,
    CachedAccount Account);

/// <summary>
/// Boundary over the MSAL public-client application so authentication orchestration can
/// be tested without a live Microsoft sign-in. The production implementation uses MSAL
/// only; no custom HTTP OAuth calls are permitted.
/// </summary>
public interface IIdentityClient
{
    Task<bool> IsBrokerAvailableAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CachedAccount>> GetAccountsAsync(CancellationToken cancellationToken);

    Task<IdentityTokenResult> AcquireTokenSilentAsync(
        IReadOnlyCollection<string> scopes,
        CachedAccount account,
        CancellationToken cancellationToken);

    Task<IdentityTokenResult> AcquireTokenInteractiveAsync(
        IReadOnlyCollection<string> scopes,
        InteractiveMode preferredMode,
        IntPtr parentWindowHandle,
        CancellationToken cancellationToken);

    Task RemoveAccountAsync(CachedAccount account, CancellationToken cancellationToken);
}
