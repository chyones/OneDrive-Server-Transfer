using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Destination;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// Fixed destination layout creation (<c>OneDriveData</c> and <c>_TransferReport</c>)
/// and the foreign-content detection used to refuse adopting non-empty destinations.
/// </summary>
public class DestinationLayoutServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"odst-m4-{Guid.NewGuid():N}");

    private static DestinationLayoutService CreateService() =>
        new(NullLogger<DestinationLayoutService>.Instance);

    [Fact]
    public void EnsureLayoutCreatesExactlyOneDriveDataAndTransferReport()
    {
        var service = CreateService();

        var destination = service.EnsureLayout(_root);

        Assert.True(Directory.Exists(destination.ContentRootPath));
        Assert.True(Directory.Exists(destination.StateRootPath));
        Assert.Equal(Path.Combine(_root, "OneDriveData"), destination.ContentRootPath);
        Assert.Equal(Path.Combine(_root, "_TransferReport"), destination.StateRootPath);
        Assert.Equal(
            Path.Combine(destination.StateRootPath, "TransferState.db"), destination.StateDatabasePath);
        Assert.Equal(
            Path.Combine(destination.StateRootPath, "destination.lock"), destination.LockFilePath);
    }

    [Fact]
    public void EnsureLayoutIsIdempotent()
    {
        var service = CreateService();

        var first = service.EnsureLayout(_root);
        var second = service.EnsureLayout(_root);

        Assert.Equal(first, second);
    }

    [Fact]
    public void HasForeignContentIsFalseForFreshLayout()
    {
        var service = CreateService();
        var destination = service.EnsureLayout(_root);

        Assert.False(service.HasForeignContent(destination));
    }

    [Fact]
    public void HasForeignContentIsFalseWithOnlyApplicationStateFiles()
    {
        var service = CreateService();
        var destination = service.EnsureLayout(_root);
        File.WriteAllText(destination.StateDatabasePath, "state");
        File.WriteAllText(destination.LockFilePath, "lock");

        Assert.False(service.HasForeignContent(destination));
    }

    [Fact]
    public void HasForeignContentIsTrueWhenContentDirectoryIsNotEmpty()
    {
        var service = CreateService();
        var destination = service.EnsureLayout(_root);
        File.WriteAllText(Path.Combine(destination.ContentRootPath, "stray.txt"), "data");

        Assert.True(service.HasForeignContent(destination));
    }

    [Fact]
    public void HasForeignContentIsTrueWhenStateDirectoryHasForeignEntries()
    {
        var service = CreateService();
        var destination = service.EnsureLayout(_root);
        File.WriteAllText(Path.Combine(destination.StateRootPath, "notes.txt"), "data");

        Assert.True(service.HasForeignContent(destination));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
