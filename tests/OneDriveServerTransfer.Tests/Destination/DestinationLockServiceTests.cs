using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Destination;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// OS-backed exclusive destination locking: a second acquisition fails with
/// <c>DST-LOCK-001</c> while the lock is held, and disposal releases it so the
/// destination can be opened again.
/// </summary>
public class DestinationLockServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"odst-m4-{Guid.NewGuid():N}");

    private ResolvedDestination CreateDestination()
    {
        var layout = new DestinationLayoutService(NullLogger<DestinationLayoutService>.Instance);
        return layout.EnsureLayout(_root);
    }

    [Fact]
    public void SecondAcquisitionFailsWithReferenceCodeWhileLockIsHeld()
    {
        var destination = CreateDestination();
        var service = new DestinationLockService(NullLogger<DestinationLockService>.Instance);

        using var first = service.Acquire(destination);

        var exception = Assert.Throws<DestinationException>(() => service.Acquire(destination));
        Assert.Equal(DestinationErrorCodes.DestinationLocked, exception.ReferenceCode);
    }

    [Fact]
    public void LockIsHeldAtTheExpectedStateLocation()
    {
        var destination = CreateDestination();
        var service = new DestinationLockService(NullLogger<DestinationLockService>.Instance);

        using var held = service.Acquire(destination);

        Assert.True(File.Exists(destination.LockFilePath));
        Assert.Equal(destination.RootPath, held.DestinationRoot);
        Assert.Same(destination, held.Destination);
    }

    [Fact]
    public void DisposeReleasesTheLock()
    {
        var destination = CreateDestination();
        var service = new DestinationLockService(NullLogger<DestinationLockService>.Instance);

        service.Acquire(destination).Dispose();

        using var reacquired = service.Acquire(destination);
        Assert.NotNull(reacquired);
    }

    [Fact]
    public async Task DisposeAsyncReleasesTheLock()
    {
        var destination = CreateDestination();
        var service = new DestinationLockService(NullLogger<DestinationLockService>.Instance);

        await service.Acquire(destination).DisposeAsync();

        using var reacquired = service.Acquire(destination);
        Assert.NotNull(reacquired);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
