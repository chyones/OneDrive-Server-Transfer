using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// Destination selection rejection matrix: UNC and network paths, non-fixed drive
/// types, relative paths, protected system/application directories, and reparse-point
/// redirection are all rejected with stable reference codes.
/// </summary>
public class DestinationValidatorTests
{
    private static string NewTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"odst-m4-{Guid.NewGuid():N}");

    private static DestinationValidator CreateValidator(
        FakeFileSystemProbe? probe = null,
        IReadOnlyList<string>? protectedRoots = null) =>
        new(probe ?? new FakeFileSystemProbe(),
            protectedRoots ?? [],
            NullLogger<DestinationValidator>.Instance);

    [Fact]
    public void RejectsNullEmptyAndWhitespace()
    {
        var validator = CreateValidator();

        foreach (var path in new[] { "", "   ", "\t" })
        {
            var exception = Assert.Throws<DestinationException>(() => validator.ValidateAndCanonicalize(path));
            Assert.Equal(DestinationErrorCodes.InvalidDestinationPath, exception.ReferenceCode);
        }
    }

    [Theory]
    [InlineData(@"\\server\share")]
    [InlineData(@"\\server\share\folder")]
    [InlineData("//server/share")]
    public void RejectsUncPaths(string path)
    {
        var validator = CreateValidator();

        var exception = Assert.Throws<DestinationException>(() => validator.ValidateAndCanonicalize(path));

        Assert.Equal(DestinationErrorCodes.NetworkDestination, exception.ReferenceCode);
    }

    [Fact]
    public void RejectsRelativePaths()
    {
        var validator = CreateValidator();

        foreach (var path in new[] { "folder", Path.Combine("folder", "subfolder"), "." + Path.DirectorySeparatorChar + "folder" })
        {
            var exception = Assert.Throws<DestinationException>(() => validator.ValidateAndCanonicalize(path));
            Assert.Equal(DestinationErrorCodes.InvalidDestinationPath, exception.ReferenceCode);
        }
    }

    [Theory]
    [InlineData(DriveType.Network, DestinationErrorCodes.NetworkDestination)]
    [InlineData(DriveType.Removable, DestinationErrorCodes.UnsupportedDriveType)]
    [InlineData(DriveType.CDRom, DestinationErrorCodes.UnsupportedDriveType)]
    [InlineData(DriveType.Ram, DestinationErrorCodes.UnsupportedDriveType)]
    [InlineData(DriveType.NoRootDirectory, DestinationErrorCodes.UnsupportedDriveType)]
    [InlineData(DriveType.Unknown, DestinationErrorCodes.UnsupportedDriveType)]
    public void RejectsUnsupportedDriveTypes(DriveType driveType, string expectedCode)
    {
        var probe = new FakeFileSystemProbe { DriveType = driveType };
        var validator = CreateValidator(probe);

        var exception = Assert.Throws<DestinationException>(() => validator.ValidateAndCanonicalize(NewTempRoot()));

        Assert.Equal(expectedCode, exception.ReferenceCode);
    }

    [Fact]
    public void AcceptsLocalFixedDriveAndCanonicalizes()
    {
        var root = NewTempRoot();
        var validator = CreateValidator();

        var canonical = validator.ValidateAndCanonicalize(root + Path.DirectorySeparatorChar);

        Assert.Equal(Path.GetFullPath(root), canonical);
        Assert.False(
            canonical.EndsWith(Path.DirectorySeparatorChar),
            "The canonical root must not retain a trailing separator.");
    }

    [Fact]
    public void RejectsProtectedSystemAndApplicationDirectories()
    {
        var root = NewTempRoot();
        var windowsDir = Path.Combine(root, "Windows");
        var programFiles = Path.Combine(root, "Program Files");
        var appDir = Path.Combine(root, "app");
        var validator = CreateValidator(protectedRoots: [windowsDir, programFiles, appDir]);

        foreach (var path in new[]
        {
            windowsDir,
            Path.Combine(windowsDir, "System32"),
            programFiles,
            Path.Combine(programFiles, "Contoso"),
            appDir,
            Path.Combine(appDir, "dest")
        })
        {
            var exception = Assert.Throws<DestinationException>(() => validator.ValidateAndCanonicalize(path));
            Assert.Equal(DestinationErrorCodes.SystemDirectory, exception.ReferenceCode);
        }
    }

    [Fact]
    public void RejectsProtectedDirectoryCaseInsensitively()
    {
        var root = NewTempRoot();
        var windowsDir = Path.Combine(root, "Windows");
        var validator = CreateValidator(protectedRoots: [windowsDir]);

        var exception = Assert.Throws<DestinationException>(
            () => validator.ValidateAndCanonicalize(windowsDir.ToUpperInvariant()));

        Assert.Equal(DestinationErrorCodes.SystemDirectory, exception.ReferenceCode);
    }

    [Fact]
    public void AllowsDirectoryWhoseNameOnlySharesPrefixWithProtectedRoot()
    {
        var root = NewTempRoot();
        var windowsDir = Path.Combine(root, "Windows");
        var validator = CreateValidator(protectedRoots: [windowsDir]);

        var canonical = validator.ValidateAndCanonicalize(Path.Combine(root, "WindowsBackup"));

        Assert.EndsWith("WindowsBackup", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsReparsePointAnywhereInDestinationChain()
    {
        var root = NewTempRoot();
        var nested = Path.Combine(root, "linked", "dest");
        var probe = new FakeFileSystemProbe();
        probe.ReparsePoints.Add(Path.Combine(root, "linked"));
        var validator = CreateValidator(probe);

        var exception = Assert.Throws<DestinationException>(() => validator.ValidateAndCanonicalize(nested));

        Assert.Equal(DestinationErrorCodes.UnsafeReparsePoint, exception.ReferenceCode);
    }

    [Fact]
    public void RejectsReparsePointAtFinalSegment()
    {
        var root = NewTempRoot();
        var probe = new FakeFileSystemProbe();
        probe.ReparsePoints.Add(root);
        var validator = CreateValidator(probe);

        var exception = Assert.Throws<DestinationException>(() => validator.ValidateAndCanonicalize(root));

        Assert.Equal(DestinationErrorCodes.UnsafeReparsePoint, exception.ReferenceCode);
    }

    [Fact]
    public void EveryErrorCarriesUserFacingFields()
    {
        var validator = CreateValidator(new FakeFileSystemProbe { DriveType = DriveType.Network });

        var exception = Assert.Throws<DestinationException>(() => validator.ValidateAndCanonicalize(NewTempRoot()));

        Assert.False(string.IsNullOrWhiteSpace(exception.Title));
        Assert.False(string.IsNullOrWhiteSpace(exception.Explanation));
        Assert.False(string.IsNullOrWhiteSpace(exception.CorrectiveAction));
    }
}
