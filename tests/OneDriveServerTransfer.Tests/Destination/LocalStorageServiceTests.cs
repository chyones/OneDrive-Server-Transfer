using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// The M1 local-storage seam implemented in M4: validation returns reference-coded
/// results instead of throwing, lock acquisition yields the exclusive destination lock,
/// and free space comes from the destination volume.
/// </summary>
public class LocalStorageServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"odst-m4-{Guid.NewGuid():N}");

    private static LocalStorageService CreateService(FakeFileSystemProbe? probe = null) =>
        new(
            new DestinationValidator(
                probe ?? new FakeFileSystemProbe(), [], NullLogger<DestinationValidator>.Instance),
            new DestinationLayoutService(NullLogger<DestinationLayoutService>.Instance),
            new DestinationLockService(NullLogger<DestinationLockService>.Instance),
            new DestinationCapacityService(),
            NullLogger<LocalStorageService>.Instance);

    [Fact]
    public async Task ValidDestinationReturnsSuccess()
    {
        var service = CreateService();

        var result = await service.ValidateDestinationAsync(_root, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task InvalidDestinationReturnsTheReferenceCode()
    {
        var service = CreateService(new FakeFileSystemProbe { DriveType = DriveType.Network });

        var result = await service.ValidateDestinationAsync(_root, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(DestinationErrorCodes.NetworkDestination, result.FailureReason);
    }

    [Fact]
    public async Task RelativeDestinationReturnsTheReferenceCode()
    {
        var service = CreateService();

        var result = await service.ValidateDestinationAsync("relative-folder", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(DestinationErrorCodes.InvalidDestinationPath, result.FailureReason);
    }

    [Fact]
    public async Task ExclusiveLockFlowValidatesCreatesLayoutAndLocks()
    {
        var service = CreateService();

        await using var destinationLock = await service.AcquireExclusiveLockAsync(_root, CancellationToken.None);

        Assert.Equal(Path.GetFullPath(_root), destinationLock.DestinationRoot);
        Assert.True(Directory.Exists(Path.Combine(_root, "OneDriveData")));
        Assert.True(Directory.Exists(Path.Combine(_root, "_TransferReport")));

        await Assert.ThrowsAsync<DestinationException>(
            () => service.AcquireExclusiveLockAsync(_root, CancellationToken.None));
    }

    [Fact]
    public void FreeSpaceComesFromTheDestinationVolume()
    {
        var service = CreateService();

        var free = service.GetFreeSpaceBytes(_root);

        Assert.True(free > 0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
