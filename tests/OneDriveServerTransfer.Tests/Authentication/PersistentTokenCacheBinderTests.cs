using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Authentication;

public class PersistentTokenCacheBinderTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"odst-binder-{Guid.NewGuid():N}");

    private string CachePath => Path.Combine(_directory, "msal-token-cache.bin");

    private PersistentTokenCacheBinder CreateBinder(ITokenCacheProtector? protector = null) => new(
        new TokenCacheFileStore(CachePath),
        protector ?? new ReversingTokenCacheProtector(),
        NullLogger<PersistentTokenCacheBinder>.Instance);

    [Fact]
    public async Task RoundTripPersistsCacheBytes()
    {
        var binder = CreateBinder();
        var data = "cache-bytes"u8.ToArray();

        await binder.SaveCacheBytesAsync(data, CancellationToken.None);
        var loaded = await binder.LoadCacheBytesAsync(CancellationToken.None);

        Assert.Equal(data, loaded);
        Assert.False(binder.CorruptionDetected);
    }

    [Fact]
    public async Task PersistenceDisabledSkipsReadAndWrite()
    {
        var binder = CreateBinder();
        binder.PersistenceEnabled = false;

        await binder.SaveCacheBytesAsync("cache-bytes"u8.ToArray(), CancellationToken.None);

        Assert.False(File.Exists(CachePath));
        Assert.Null(await binder.LoadCacheBytesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CorruptionClearsFileSetsFlagAndFailsSafely()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllBytesAsync(CachePath, "damaged"u8.ToArray());
        var binder = CreateBinder(new CorruptingTokenCacheProtector());

        var loaded = await binder.LoadCacheBytesAsync(CancellationToken.None);

        Assert.Null(loaded);
        Assert.True(binder.CorruptionDetected);
        Assert.False(File.Exists(CachePath));
    }

    [Fact]
    public async Task AfterCorruptionFreshCacheCanBeWritten()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllBytesAsync(CachePath, "damaged"u8.ToArray());
        var binder = CreateBinder(new CorruptingTokenCacheProtector());

        Assert.Null(await binder.LoadCacheBytesAsync(CancellationToken.None));
        Assert.True(binder.CorruptionDetected);

        await binder.ClearAsync(CancellationToken.None);
        var healthyBinder = CreateBinder();
        var data = "fresh"u8.ToArray();
        await healthyBinder.SaveCacheBytesAsync(data, CancellationToken.None);

        Assert.Equal(data, await healthyBinder.LoadCacheBytesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ClearRemovesPersistedCache()
    {
        var binder = CreateBinder();

        await binder.SaveCacheBytesAsync("cache-bytes"u8.ToArray(), CancellationToken.None);
        await binder.ClearAsync(CancellationToken.None);

        Assert.False(File.Exists(CachePath));
        Assert.Null(await binder.LoadCacheBytesAsync(CancellationToken.None));
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
