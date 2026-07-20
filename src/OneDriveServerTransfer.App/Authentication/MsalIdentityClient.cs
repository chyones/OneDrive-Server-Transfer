using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// MSAL-backed identity client for the single-tenant public-client desktop application.
/// Interactive sign-in uses the official Microsoft flow with the Windows broker (WAM)
/// enabled; MSAL falls back to the system browser when the broker cannot be used. Only
/// delegated interactive acquisition is used; no other flow exists in this build.
/// </summary>
public sealed class MsalIdentityClient : IIdentityClient
{
    private readonly IOptions<AuthenticationOptions> _options;
    private readonly PersistentTokenCacheBinder _cacheBinder;
    private readonly ILogger<MsalIdentityClient> _logger;
    private readonly object _appLock = new();
    private PublicClientApplication? _app;

    public MsalIdentityClient(
        IOptions<AuthenticationOptions> options,
        PersistentTokenCacheBinder cacheBinder,
        ILogger<MsalIdentityClient> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _cacheBinder = cacheBinder ?? throw new ArgumentNullException(nameof(cacheBinder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<bool> IsBrokerAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(GetApp().IsBrokerAvailable());
    }

    public async Task<IReadOnlyList<CachedAccount>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await GetApp().GetAccountsAsync().ConfigureAwait(false);
        return accounts.Select(MapAccount).ToArray();
    }

    public async Task<IdentityTokenResult> AcquireTokenSilentAsync(
        IReadOnlyCollection<string> scopes,
        CachedAccount account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        var msalAccount = (await GetApp().GetAccountsAsync().ConfigureAwait(false))
            .FirstOrDefault(a => string.Equals(a.HomeAccountId.Identifier, account.HomeAccountId, StringComparison.Ordinal));

        if (msalAccount is null)
        {
            throw new MsalUiRequiredException("no_account_found", "The account is no longer present in the cache.");
        }

        var result = await GetApp()
            .AcquireTokenSilent(scopes, msalAccount)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return MapResult(result);
    }

    public async Task<IdentityTokenResult> AcquireTokenInteractiveAsync(
        IReadOnlyCollection<string> scopes,
        InteractiveMode preferredMode,
        IntPtr parentWindowHandle,
        CancellationToken cancellationToken)
    {
        // The broker is enabled at application level; MSAL uses WAM when invokable and
        // otherwise falls back to the system browser. preferredMode records intent.
        _logger.LogDebug("Interactive acquisition requested; preferredMode={PreferredMode}", preferredMode);

        var result = await GetApp()
            .AcquireTokenInteractive(scopes)
            .WithParentActivityOrWindow(parentWindowHandle)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return MapResult(result);
    }

    public async Task RemoveAccountAsync(CachedAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        cancellationToken.ThrowIfCancellationRequested();

        var msalAccount = (await GetApp().GetAccountsAsync().ConfigureAwait(false))
            .FirstOrDefault(a => string.Equals(a.HomeAccountId.Identifier, account.HomeAccountId, StringComparison.Ordinal));

        if (msalAccount is not null)
        {
            await GetApp().RemoveAsync(msalAccount).ConfigureAwait(false);
        }
    }

    private PublicClientApplication GetApp()
    {
        if (_app is not null)
        {
            return _app;
        }

        lock (_appLock)
        {
            if (_app is not null)
            {
                return _app;
            }

            var options = _options.Value; // throws OptionsValidationException when invalid
            var builder = PublicClientApplicationBuilder
                .Create(options.ClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, options.TenantId)
                .WithRedirectUri(options.RedirectUri)
                .WithLogging(LogMsal, Microsoft.Identity.Client.LogLevel.Verbose, enablePiiLogging: false);

            if (OperatingSystem.IsWindows())
            {
                builder = builder.WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows));
            }

            _app = (PublicClientApplication)builder.Build();
            _cacheBinder.Bind(_app.UserTokenCache);
            return _app;
        }
    }

    private void LogMsal(Microsoft.Identity.Client.LogLevel level, string? message, bool containsPii)
    {
        if (containsPii || string.IsNullOrEmpty(message))
        {
            return;
        }

        var sanitized = AuthErrorSanitizer.RedactSensitiveText(message);
        _logger.Log(MapLevel(level), "MSAL: {Message}", sanitized);
    }

    private static Microsoft.Extensions.Logging.LogLevel MapLevel(Microsoft.Identity.Client.LogLevel level) => level switch
    {
        Microsoft.Identity.Client.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
        Microsoft.Identity.Client.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
        Microsoft.Identity.Client.LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Debug,
        _ => Microsoft.Extensions.Logging.LogLevel.Trace,
    };

    private static CachedAccount MapAccount(IAccount account) => new(
        account.HomeAccountId.Identifier,
        account.HomeAccountId.TenantId ?? string.Empty,
        account.Username);

    private static IdentityTokenResult MapResult(AuthenticationResult result)
    {
        var account = MapAccount(result.Account);
        var (objectId, upn, idp) = ReadIdTokenClaims(result.IdToken, account);
        return new IdentityTokenResult(
            result.AccessToken,
            result.TenantId ?? account.HomeTenantId,
            objectId,
            upn,
            idp,
            result.Scopes?.ToArray() ?? [],
            result.ExpiresOn,
            account);
    }

    private static (string ObjectId, string? Upn, string? IdentityProvider) ReadIdTokenClaims(
        string? idToken,
        CachedAccount account)
    {
        string? objectId = null;
        string? upn = null;
        string? idp = null;

        foreach (var claim in ReadJwtPayloadClaims(idToken))
        {
            switch (claim.Key)
            {
                case "oid":
                    objectId = claim.Value;
                    break;
                case "upn":
                    upn = claim.Value;
                    break;
                case "idp":
                    idp = claim.Value;
                    break;
            }
        }

        if (string.IsNullOrEmpty(objectId))
        {
            // Home account identifiers have the form "<object-id>.<tenant-id>".
            var separator = account.HomeAccountId.IndexOf('.', StringComparison.Ordinal);
            objectId = separator > 0 ? account.HomeAccountId[..separator] : account.HomeAccountId;
        }

        upn ??= account.Username;
        return (objectId, upn, idp);
    }

    /// <summary>
    /// Reads selected claim values from an MSAL-issued ID token payload. The token was
    /// already validated by MSAL; this is a read-only extraction that tolerates unknown
    /// properties and malformed input.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string>> ReadJwtPayloadClaims(string? idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            yield break;
        }

        var segments = idToken.Split('.');
        if (segments.Length != 3)
        {
            yield break;
        }

        JsonDocument? payload = null;
        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(segments[1]));
            payload = JsonDocument.Parse(json);
        }
        catch (Exception)
        {
            yield break;
        }

        using (payload)
        {
            if (payload.RootElement.ValueKind != JsonValueKind.Object)
            {
                yield break;
            }

            foreach (var property in payload.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    yield return new KeyValuePair<string, string>(property.Name, property.Value.GetString()!);
                }
            }
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
