using System.Security.AccessControl;
using System.Security.Principal;
using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Tests.Authentication;

public class TokenCacheFileStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"odst-cache-{Guid.NewGuid():N}");

    private string CachePath => Path.Combine(_directory, "msal-token-cache.bin");

    [Fact]
    public async Task ReadReturnsNullWhenNoCacheExists()
    {
        var store = new TokenCacheFileStore(CachePath);

        Assert.Null(await store.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task WriteReadRoundTrip()
    {
        var store = new TokenCacheFileStore(CachePath);
        var data = "protected-bytes"u8.ToArray();

        await store.WriteAsync(data, CancellationToken.None);
        var read = await store.ReadAsync(CancellationToken.None);

        Assert.Equal(data, read);
    }

    [Fact]
    public async Task WriteOverwritesExistingCache()
    {
        var store = new TokenCacheFileStore(CachePath);

        await store.WriteAsync("first"u8.ToArray(), CancellationToken.None);
        await store.WriteAsync("second"u8.ToArray(), CancellationToken.None);

        Assert.Equal("second"u8.ToArray(), await store.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeleteRemovesCache()
    {
        var store = new TokenCacheFileStore(CachePath);

        await store.WriteAsync("data"u8.ToArray(), CancellationToken.None);
        await store.DeleteAsync(CancellationToken.None);

        Assert.False(File.Exists(CachePath));
        Assert.Null(await store.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CacheFileIsNotBroadlyReadableOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // NTFS ACL checks apply on Windows; CI executes this check there.
        }

        var store = new TokenCacheFileStore(CachePath);
        await store.WriteAsync("data"u8.ToArray(), CancellationToken.None);

        var security = new FileInfo(CachePath).GetAccessControl();
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToArray();

        Assert.NotEmpty(rules);

        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null).Value;
        var builtinUsers = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null).Value;
        var allowedSids = rules.Select(rule => rule.IdentityReference.Value).ToArray();

        Assert.DoesNotContain(everyone, allowedSids);
        Assert.DoesNotContain(builtinUsers, allowedSids);
        Assert.All(rules, rule => Assert.Equal(AccessControlType.Allow, rule.AccessControlType));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temp directory.
        }
    }
}
