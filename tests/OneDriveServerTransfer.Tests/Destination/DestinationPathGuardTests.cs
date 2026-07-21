using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// Canonical containment and write safety (D-011): paths are resolved under the
/// <c>OneDriveData</c> root only, traversal is rejected, reparse points on any existing
/// segment are refused, overlong canonical paths fail with the stable path-length
/// error, and existing reparse-point or multi-hard-link files are never overwritten.
/// </summary>
public class DestinationPathGuardTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"odst-m4-{Guid.NewGuid():N}");

    private readonly ResolvedDestination _destination;
    private readonly FakeFileSystemProbe _probe = new();

    public DestinationPathGuardTests()
    {
        var layout = new DestinationLayoutService(NullLogger<DestinationLayoutService>.Instance);
        _destination = layout.EnsureLayout(_root);
    }

    private DestinationPathGuard CreateGuard() =>
        new(_probe, NullLogger<DestinationPathGuard>.Instance);

    [Fact]
    public void ResolvesContentPathsUnderTheContentRoot()
    {
        var guard = CreateGuard();

        var resolved = guard.ResolveContentPath(_destination, Path.Combine("sub", "file.txt"));

        Assert.StartsWith(
            Path.GetFullPath(_destination.ContentRootPath) + Path.DirectorySeparatorChar,
            resolved,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("../../outside/file.txt")]
    [InlineData("sub/../../../evil.txt")]
    public void TraversalOutsideTheContentRootIsRejected(string relativePath)
    {
        var guard = CreateGuard();

        var exception = Assert.Throws<DestinationException>(
            () => guard.ResolveContentPath(_destination, relativePath));

        Assert.Equal(DestinationErrorCodes.ContainmentViolation, exception.ReferenceCode);
    }

    [Fact]
    public void RootedRelativePathsAreRejected()
    {
        var guard = CreateGuard();
        var rooted = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "elsewhere.txt"));

        var exception = Assert.Throws<DestinationException>(
            () => guard.ResolveContentPath(_destination, rooted));

        Assert.Equal(DestinationErrorCodes.ContainmentViolation, exception.ReferenceCode);
    }

    [Fact]
    public void CanonicalPathsBeyondTheSupportedLimitFailWithPathLengthError()
    {
        var guard = CreateGuard();
        var longName = new string('a', PathMapperV1.MaxComponentUtf16Units);
        var segments = Enumerable
            .Range(0, (PathMapperV1.MaxCanonicalPathUtf16Units / PathMapperV1.MaxComponentUtf16Units) + 1)
            .Select(_ => longName);
        var relativePath = string.Join(Path.DirectorySeparatorChar, segments);

        var exception = Assert.Throws<DestinationException>(
            () => guard.ResolveContentPath(_destination, relativePath));

        Assert.Equal(DestinationErrorCodes.PathTooLong, exception.ReferenceCode);
    }

    [Fact]
    public void ReparsePointOnAnExistingSegmentIsRefused()
    {
        var guard = CreateGuard();
        Directory.CreateDirectory(Path.Combine(_destination.ContentRootPath, "linked"));
        _probe.ReparsePoints.Add(Path.Combine(_destination.ContentRootPath, "linked"));

        var exception = Assert.Throws<DestinationException>(
            () => guard.ResolveContentPath(_destination, Path.Combine("linked", "file.txt")));

        Assert.Equal(DestinationErrorCodes.UnsafeReparsePoint, exception.ReferenceCode);
    }

    [Fact]
    public void ReparsePointTargetIsNeverOverwritten()
    {
        var guard = CreateGuard();
        var target = Path.Combine(_destination.ContentRootPath, "link.txt");
        File.WriteAllText(target, "data");
        _probe.ReparsePoints.Add(target);

        var exception = Assert.Throws<DestinationException>(
            () => guard.ResolveWritableContentPath(_destination, "link.txt"));

        Assert.Equal(DestinationErrorCodes.UntrustedExistingFile, exception.ReferenceCode);
    }

    [Fact]
    public void MultiHardLinkExistingFileIsNeverOverwritten()
    {
        var guard = CreateGuard();
        var target = Path.Combine(_destination.ContentRootPath, "shared.bin");
        File.WriteAllText(target, "data");
        _probe.HardLinkCounts[target] = 2;

        var exception = Assert.Throws<DestinationException>(
            () => guard.ResolveWritableContentPath(_destination, "shared.bin"));

        Assert.Equal(DestinationErrorCodes.UntrustedExistingFile, exception.ReferenceCode);
    }

    [Fact]
    public void OrdinaryExistingFileIsWritable()
    {
        var guard = CreateGuard();
        var target = Path.Combine(_destination.ContentRootPath, "normal.bin");
        File.WriteAllText(target, "data");

        var resolved = guard.ResolveWritableContentPath(_destination, "normal.bin");

        Assert.Equal(Path.GetFullPath(target), resolved);
    }

    [Fact]
    public void NewFileInNewSubdirectoryIsWritable()
    {
        var guard = CreateGuard();

        var resolved = guard.ResolveWritableContentPath(
            _destination, Path.Combine("newdir", "newfile.txt"));

        Assert.EndsWith(Path.Combine("newdir", "newfile.txt"), resolved, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
