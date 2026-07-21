using System.IO;

namespace OneDriveServerTransfer.Destination;

/// <summary>Free-space facts for one destination volume.</summary>
internal interface IDriveSpaceProvider
{
    long GetAvailableFreeSpaceBytes(string destinationRootPath);
}

internal sealed class DriveInfoSpaceProvider : IDriveSpaceProvider
{
    public long GetAvailableFreeSpaceBytes(string destinationRootPath)
    {
        var fullPath = Path.GetFullPath(destinationRootPath);
        var root = Path.GetPathRoot(fullPath) ?? fullPath;
        return new DriveInfo(root).AvailableFreeSpace;
    }
}

/// <summary>
/// Result of a destination free-space evaluation. <see cref="IsSufficient" /> is true
/// only when available free space is strictly greater than the required bytes; a
/// boundary-exact match fails.
/// </summary>
public sealed record DestinationCapacityResult(
    bool IsSufficient,
    long AvailableFreeSpaceBytes,
    long RequiredBytes);

/// <summary>
/// Destination-volume capacity checks (D-022). Free space must exceed the known
/// remaining source bytes plus the fixed 5 GiB safety reserve before downloads are
/// scheduled, and must exceed one file's expected size plus the reserve on the
/// per-file recheck. Disk-full and reserve-violation scheduling behavior is M5 scope;
/// this service only reports the numbers.
/// </summary>
public interface IDestinationCapacityService
{
    long GetAvailableFreeSpaceBytes(string destinationRootPath);

    DestinationCapacityResult CheckTotal(string destinationRootPath, long knownRemainingSourceBytes);

    DestinationCapacityResult CheckFile(string destinationRootPath, long expectedFileBytes);
}

public sealed class DestinationCapacityService : IDestinationCapacityService
{
    /// <summary>The fixed safety reserve: 5 GiB (5 * 1024^3 bytes).</summary>
    public const long ReserveBytes = 5L * 1024 * 1024 * 1024;

    private readonly IDriveSpaceProvider _spaceProvider;

    public DestinationCapacityService()
        : this(new DriveInfoSpaceProvider())
    {
    }

    internal DestinationCapacityService(IDriveSpaceProvider spaceProvider)
    {
        _spaceProvider = spaceProvider ?? throw new ArgumentNullException(nameof(spaceProvider));
    }

    public long GetAvailableFreeSpaceBytes(string destinationRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRootPath);
        return _spaceProvider.GetAvailableFreeSpaceBytes(destinationRootPath);
    }

    public DestinationCapacityResult CheckTotal(string destinationRootPath, long knownRemainingSourceBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(knownRemainingSourceBytes);
        return Evaluate(destinationRootPath, knownRemainingSourceBytes + ReserveBytes);
    }

    public DestinationCapacityResult CheckFile(string destinationRootPath, long expectedFileBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedFileBytes);
        return Evaluate(destinationRootPath, expectedFileBytes + ReserveBytes);
    }

    private DestinationCapacityResult Evaluate(string destinationRootPath, long requiredBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRootPath);

        var freeBytes = _spaceProvider.GetAvailableFreeSpaceBytes(destinationRootPath);
        return new DestinationCapacityResult(freeBytes > requiredBytes, freeBytes, requiredBytes);
    }
}
