using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// Destination capacity thresholds (D-022): free space must be strictly greater than
/// known remaining source bytes plus the fixed 5 GiB reserve; a boundary-exact match
/// fails. The per-file recheck uses the file's expected size plus the same reserve.
/// </summary>
public class DestinationCapacityServiceTests
{
    private const string Root = "/unused";

    private static DestinationCapacityService CreateService(long freeBytes) =>
        new(new FakeDriveSpaceProvider { AvailableFreeSpaceBytes = freeBytes });

    [Fact]
    public void ReserveIsExactlyFiveGiB()
    {
        Assert.Equal(5L * 1024 * 1024 * 1024, DestinationCapacityService.ReserveBytes);
    }

    [Fact]
    public void TotalCheckSucceedsWhenFreeSpaceExceedsRemainingPlusReserve()
    {
        var remaining = 10_000_000_000L;
        var service = CreateService(remaining + DestinationCapacityService.ReserveBytes + 1);

        var result = service.CheckTotal(Root, remaining);

        Assert.True(result.IsSufficient);
        Assert.Equal(remaining + DestinationCapacityService.ReserveBytes, result.RequiredBytes);
        Assert.Equal(remaining + DestinationCapacityService.ReserveBytes + 1, result.AvailableFreeSpaceBytes);
    }

    [Fact]
    public void TotalCheckFailsAtTheExactBoundary()
    {
        var remaining = 10_000_000_000L;
        var service = CreateService(remaining + DestinationCapacityService.ReserveBytes);

        var result = service.CheckTotal(Root, remaining);

        Assert.False(result.IsSufficient);
    }

    [Fact]
    public void TotalCheckFailsBelowTheBoundary()
    {
        var remaining = 10_000_000_000L;
        var service = CreateService(remaining + DestinationCapacityService.ReserveBytes - 1);

        Assert.False(service.CheckTotal(Root, remaining).IsSufficient);
    }

    [Fact]
    public void TotalCheckWithZeroRemainingStillRequiresMoreThanTheReserve()
    {
        Assert.False(CreateService(DestinationCapacityService.ReserveBytes).CheckTotal(Root, 0).IsSufficient);
        Assert.True(CreateService(DestinationCapacityService.ReserveBytes + 1).CheckTotal(Root, 0).IsSufficient);
    }

    [Fact]
    public void FileCheckSucceedsWhenFreeSpaceExceedsFileSizePlusReserve()
    {
        var fileSize = 2_000_000_000L;
        var service = CreateService(fileSize + DestinationCapacityService.ReserveBytes + 1);

        var result = service.CheckFile(Root, fileSize);

        Assert.True(result.IsSufficient);
        Assert.Equal(fileSize + DestinationCapacityService.ReserveBytes, result.RequiredBytes);
    }

    [Fact]
    public void FileCheckFailsAtTheExactBoundary()
    {
        var fileSize = 2_000_000_000L;
        var service = CreateService(fileSize + DestinationCapacityService.ReserveBytes);

        Assert.False(service.CheckFile(Root, fileSize).IsSufficient);
    }

    [Fact]
    public void AvailableFreeSpaceComesFromTheVolume()
    {
        var service = CreateService(123_456_789L);

        Assert.Equal(123_456_789L, service.GetAvailableFreeSpaceBytes(Root));
    }

    [Fact]
    public void NegativeSizesAreRejected()
    {
        var service = CreateService(long.MaxValue);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CheckTotal(Root, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.CheckFile(Root, -1));
    }
}
