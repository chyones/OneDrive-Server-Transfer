using System.Security.Cryptography;

namespace OneDriveServerTransfer.Authentication;

/// <summary>Raised when protected token-cache bytes cannot be unprotected or read.</summary>
public sealed class TokenCacheCorruptionException : Exception
{
    public TokenCacheCorruptionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Protects application-owned token-cache bytes for the current Windows user.
/// </summary>
public interface ITokenCacheProtector
{
    byte[] Protect(byte[] plaintext);

    /// <summary>Throws <see cref="TokenCacheCorruptionException" /> on unreadable input.</summary>
    byte[] Unprotect(byte[] protectedBytes);
}

/// <summary>
/// Windows DPAPI protection scoped to the current Windows user. Cache contents are never
/// logged; any failure to unprotect is treated as corruption and fails safely.
/// </summary>
public sealed class DpapiTokenCacheProtector : ITokenCacheProtector
{
    private static readonly byte[] Entropy = "OneDriveServerTransfer.TokenCache.v1"u8.ToArray();

    public byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The DPAPI token cache is supported on Windows only.");
        }

        return ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] protectedBytes)
    {
        ArgumentNullException.ThrowIfNull(protectedBytes);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The DPAPI token cache is supported on Windows only.");
        }

        try
        {
            return ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException exception)
        {
            throw new TokenCacheCorruptionException("The protected token cache could not be read.", exception);
        }
    }
}
