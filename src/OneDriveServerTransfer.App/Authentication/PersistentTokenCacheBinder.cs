using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// Wires the application-owned DPAPI-protected persistent cache into the MSAL token
/// cache. Persistence is active only when remember-sign-in is enabled. Corruption fails
/// safely: the damaged file is cleared, no cache content is read or logged, and
/// reauthentication is required. Remember sign-in controls only this application-owned
/// persistent cache.
/// </summary>
public sealed class PersistentTokenCacheBinder
{
    private readonly ITokenCacheStore _store;
    private readonly ITokenCacheProtector _protector;
    private readonly ILogger<PersistentTokenCacheBinder> _logger;

    public PersistentTokenCacheBinder(
        ITokenCacheStore store,
        ITokenCacheProtector protector,
        ILogger<PersistentTokenCacheBinder> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Whether persistent-cache reads and writes are active for this session.</summary>
    public bool PersistenceEnabled { get; set; } = true;

    /// <summary>Set when a damaged cache file was detected and safely cleared.</summary>
    public bool CorruptionDetected { get; private set; }

    public void Bind(ITokenCache tokenCache)
    {
        ArgumentNullException.ThrowIfNull(tokenCache);

        tokenCache.SetBeforeAccessAsync(async args =>
        {
            var data = await LoadCacheBytesAsync(args.CancellationToken).ConfigureAwait(false);
            if (data is not null)
            {
                args.TokenCache.DeserializeMsalV3(data);
            }
        });

        tokenCache.SetAfterAccessAsync(async args =>
        {
            if (args.HasStateChanged)
            {
                await SaveCacheBytesAsync(args.TokenCache.SerializeMsalV3(), args.CancellationToken)
                    .ConfigureAwait(false);
            }
        });
    }

    /// <summary>
    /// Reads and unprotects persisted cache bytes. Returns null when persistence is
    /// disabled or no cache exists. On corruption the damaged file is deleted, the
    /// corruption flag is set, and null is returned so reauthentication is required.
    /// </summary>
    public async Task<byte[]?> LoadCacheBytesAsync(CancellationToken cancellationToken)
    {
        if (!PersistenceEnabled)
        {
            return null;
        }

        var protectedBytes = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (protectedBytes is null)
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedBytes);
        }
        catch (TokenCacheCorruptionException)
        {
            CorruptionDetected = true;
            await _store.DeleteAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("The application token cache was unreadable and has been cleared.");
            return null;
        }
    }

    public async Task SaveCacheBytesAsync(byte[] cacheBytes, CancellationToken cancellationToken)
    {
        if (!PersistenceEnabled)
        {
            return;
        }

        await _store.WriteAsync(_protector.Protect(cacheBytes), cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Application token cache persisted.");
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await _store.DeleteAsync(cancellationToken).ConfigureAwait(false);
        CorruptionDetected = false;
    }
}
