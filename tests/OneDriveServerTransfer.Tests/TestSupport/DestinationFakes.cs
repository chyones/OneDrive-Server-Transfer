using OneDriveServerTransfer.Destination;

namespace OneDriveServerTransfer.Tests.TestSupport;

/// <summary>
/// Deterministic <see cref="IFileSystemProbe" /> double for destination tests. Existence
/// falls back to the real filesystem so tests can mix crafted answers with real temp
/// files; reparse points and hard-link counts are fully controlled.
/// </summary>
internal sealed class FakeFileSystemProbe : IFileSystemProbe
{
    public DriveType DriveType { get; set; } = DriveType.Fixed;

    public HashSet<string> ReparsePoints { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> HardLinkCounts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public DriveType GetDriveType(string driveRoot) => DriveType;

    public bool FileOrDirectoryExists(string fullPath) =>
        File.Exists(fullPath) || Directory.Exists(fullPath);

    public bool IsReparsePoint(string fullPath) => ReparsePoints.Contains(fullPath);

    public int GetHardLinkCount(string fullPath) =>
        HardLinkCounts.TryGetValue(fullPath, out var count) ? count : 1;
}

/// <summary>Deterministic free-space double for capacity tests.</summary>
internal sealed class FakeDriveSpaceProvider : IDriveSpaceProvider
{
    public long AvailableFreeSpaceBytes { get; set; }

    public long GetAvailableFreeSpaceBytes(string destinationRootPath) => AvailableFreeSpaceBytes;
}

/// <summary>Deterministic ACL double for destination security evaluation tests.</summary>
internal sealed class FakeDirectoryAclReader : IDirectoryAclReader
{
    public List<DestinationAclEntrySnapshot> Entries { get; } = [];

    public IReadOnlyList<DestinationAclEntrySnapshot> ReadEntries(string directoryPath) => Entries;
}
