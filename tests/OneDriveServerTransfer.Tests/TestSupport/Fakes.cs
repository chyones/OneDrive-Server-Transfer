using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Tests.TestSupport;

/// <summary>
/// Programmable IIdentityClient double for unit tests. Lives only in the test assembly;
/// production dependency injection always uses the real MSAL client.
/// </summary>
internal sealed class FakeIdentityClient : IIdentityClient
{
    public bool BrokerAvailable { get; set; } = true;

    public List<CachedAccount> Accounts { get; } = [];

    public List<InteractiveMode> RequestedInteractiveModes { get; } = [];

    public List<CachedAccount> RemovedAccounts { get; } = [];

    public Func<IReadOnlyCollection<string>, CachedAccount, Task<IdentityTokenResult>>? SilentHandler { get; set; }

    public Func<IReadOnlyCollection<string>, InteractiveMode, Task<IdentityTokenResult>>? InteractiveHandler { get; set; }

    public Task<bool> IsBrokerAvailableAsync(CancellationToken cancellationToken) =>
        Task.FromResult(BrokerAvailable);

    public Task<IReadOnlyList<CachedAccount>> GetAccountsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CachedAccount>>(Accounts.ToArray());

    public Task<IdentityTokenResult> AcquireTokenSilentAsync(
        IReadOnlyCollection<string> scopes,
        CachedAccount account,
        CancellationToken cancellationToken) =>
        SilentHandler?.Invoke(scopes, account)
        ?? throw new InvalidOperationException("SilentHandler not configured.");

    public Task<IdentityTokenResult> AcquireTokenInteractiveAsync(
        IReadOnlyCollection<string> scopes,
        InteractiveMode preferredMode,
        IntPtr parentWindowHandle,
        CancellationToken cancellationToken)
    {
        RequestedInteractiveModes.Add(preferredMode);
        return InteractiveHandler?.Invoke(scopes, preferredMode)
            ?? throw new InvalidOperationException("InteractiveHandler not configured.");
    }

    public Task RemoveAccountAsync(CachedAccount account, CancellationToken cancellationToken)
    {
        RemovedAccounts.Add(account);
        Accounts.RemoveAll(a => a.HomeAccountId == account.HomeAccountId);
        return Task.CompletedTask;
    }

    public static CachedAccount AccountFor(string tenantId, string objectId, string upn = "operator@example.test") =>
        new($"{objectId}.{tenantId}", tenantId, upn);

    public static IdentityTokenResult TokenFor(
        string tenantId,
        string objectId,
        string upn = "operator@example.test",
        IReadOnlyCollection<string>? scopes = null,
        string? homeTenantId = null) =>
        new(
            "test-access-token",
            tenantId,
            objectId,
            upn,
            null,
            scopes ?? ["User.Read", "Files.Read.All", "Sites.Read.All", "offline_access", "openid", "profile"],
            DateTimeOffset.UtcNow.AddHours(1),
            AccountFor(homeTenantId ?? tenantId, objectId, upn));
}

/// <summary>Programmable IOperatorProfileProvider double for unit tests.</summary>
internal sealed class FakeOperatorProfileProvider : IOperatorProfileProvider
{
    public OperatorProfile? Profile { get; set; }

    public Exception? Failure { get; set; }

    public Task<OperatorProfile> GetCurrentOperatorProfileAsync(string accessToken, CancellationToken cancellationToken) =>
        Failure is not null
            ? Task.FromException<OperatorProfile>(Failure)
            : Task.FromResult(Profile ?? new OperatorProfile("33333333-3333-3333-3333-333333333333", "operator@example.test", "Test Operator"));
}

/// <summary>Simple reversible protector for portable cache-binder tests.</summary>
internal sealed class ReversingTokenCacheProtector : ITokenCacheProtector
{
    public byte[] Protect(byte[] plaintext)
    {
        var copy = (byte[])plaintext.Clone();
        Array.Reverse(copy);
        return copy;
    }

    public byte[] Unprotect(byte[] protectedBytes)
    {
        var copy = (byte[])protectedBytes.Clone();
        Array.Reverse(copy);
        return copy;
    }
}

/// <summary>Protector that always fails unprotection, simulating corruption.</summary>
internal sealed class CorruptingTokenCacheProtector : ITokenCacheProtector
{
    public byte[] Protect(byte[] plaintext) => plaintext;

    public byte[] Unprotect(byte[] protectedBytes) =>
        throw new TokenCacheCorruptionException("Simulated corruption.");
}
