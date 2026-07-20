using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// File storage for the application-owned token cache. The cache file lives under the
/// current user's local application-data directory and is ACL-restricted to the
/// execution account and local administrators. Cache contents are never logged.
/// </summary>
public interface ITokenCacheStore
{
    string CacheFilePath { get; }

    Task<byte[]?> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(byte[] protectedBytes, CancellationToken cancellationToken);

    Task DeleteAsync(CancellationToken cancellationToken);
}

public sealed class TokenCacheFileStore : ITokenCacheStore
{
    public TokenCacheFileStore(string cacheFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheFilePath);
        CacheFilePath = cacheFilePath;
    }

    public static TokenCacheFileStore CreateDefault()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OneDriveServerTransfer",
            "TokenCache");

        return new TokenCacheFileStore(Path.Combine(directory, "msal-token-cache.bin"));
    }

    public string CacheFilePath { get; }

    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(CacheFilePath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(CacheFilePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAsync(byte[] protectedBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(protectedBytes);

        var directory = Path.GetDirectoryName(CacheFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = CacheFilePath + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, protectedBytes, cancellationToken).ConfigureAwait(false);
        RestrictToOwnerAndAdministrators(temporaryPath);

        File.Move(temporaryPath, CacheFilePath, overwrite: true);
        RestrictToOwnerAndAdministrators(CacheFilePath);
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(CacheFilePath))
        {
            File.Delete(CacheFilePath);
        }

        return Task.CompletedTask;
    }

    private static void RestrictToOwnerAndAdministrators(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is not null)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser, FileSystemRights.FullControl, AccessControlType.Allow));
        }

        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        new FileInfo(path).SetAccessControl(security);
    }
}
