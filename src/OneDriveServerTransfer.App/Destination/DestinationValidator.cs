using System.IO;
using Microsoft.Extensions.Logging;

namespace OneDriveServerTransfer.Destination;

/// <summary>
/// Validates an operator-selected destination path. Only a canonical full path on a
/// local fixed drive of this server is accepted. UNC and mapped/network drives,
/// removable, optical, RAM, and unknown drives, relative paths, Windows system
/// directories, the application installation directory, and any path that passes
/// through a reparse point (junction, symlink, or mount point) are rejected. Failures
/// throw a reference-coded <see cref="DestinationException" />.
/// </summary>
public interface IDestinationValidator
{
    /// <summary>Validates the selection and returns the canonical destination root path.</summary>
    string ValidateAndCanonicalize(string selectedPath);
}

public sealed class DestinationValidator : IDestinationValidator
{
    private readonly IFileSystemProbe _probe;
    private readonly IReadOnlyList<string> _protectedRoots;
    private readonly ILogger<DestinationValidator> _logger;

    public DestinationValidator(ILogger<DestinationValidator> logger)
        : this(new SystemIOFileSystemProbe(), DefaultProtectedRoots(), logger)
    {
    }

    internal DestinationValidator(
        IFileSystemProbe probe,
        IReadOnlyList<string> protectedRoots,
        ILogger<DestinationValidator> logger)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _protectedRoots = protectedRoots ?? throw new ArgumentNullException(nameof(protectedRoots));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ValidateAndCanonicalize(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            throw DestinationErrors.InvalidDestinationPath();
        }

        var trimmed = selectedPath.Trim();

        if (trimmed.StartsWith(@"\\", StringComparison.Ordinal) ||
            trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Destination rejected; code={ReferenceCode}; reason=UncPath", DestinationErrorCodes.NetworkDestination);
            throw DestinationErrors.NetworkDestination();
        }

        if (!Path.IsPathRooted(trimmed) || !Path.IsPathFullyQualified(trimmed))
        {
            _logger.LogWarning(
                "Destination rejected; code={ReferenceCode}; reason=RelativeOrPartialPath",
                DestinationErrorCodes.InvalidDestinationPath);
            throw DestinationErrors.InvalidDestinationPath();
        }

        string canonical;
        try
        {
            canonical = Path.GetFullPath(trimmed);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw DestinationErrors.InvalidDestinationPath(exception);
        }

        canonical = TrimTrailingSeparators(canonical);

        var driveRoot = Path.GetPathRoot(canonical);
        if (string.IsNullOrEmpty(driveRoot))
        {
            throw DestinationErrors.InvalidDestinationPath();
        }

        DriveType driveType;
        try
        {
            driveType = _probe.GetDriveType(driveRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            throw DestinationErrors.InvalidDestinationPath(exception);
        }

        if (driveType == DriveType.Network)
        {
            _logger.LogWarning(
                "Destination rejected; code={ReferenceCode}; reason=NetworkDrive; path={DestinationPath}",
                DestinationErrorCodes.NetworkDestination, canonical);
            throw DestinationErrors.NetworkDestination();
        }

        if (driveType != DriveType.Fixed)
        {
            _logger.LogWarning(
                "Destination rejected; code={ReferenceCode}; reason=DriveType; driveType={DriveType}; path={DestinationPath}",
                DestinationErrorCodes.UnsupportedDriveType, driveType, canonical);
            throw DestinationErrors.UnsupportedDriveType();
        }

        foreach (var protectedRoot in _protectedRoots)
        {
            if (IsSameOrDescendant(canonical, protectedRoot))
            {
                _logger.LogWarning(
                    "Destination rejected; code={ReferenceCode}; reason=ProtectedSystemDirectory; path={DestinationPath}",
                    DestinationErrorCodes.SystemDirectory, canonical);
                throw DestinationErrors.SystemDirectory();
            }
        }

        RejectReparsePoints(driveRoot, canonical);

        return canonical;
    }

    private void RejectReparsePoints(string driveRoot, string canonical)
    {
        var relative = canonical[driveRoot.Length..];
        var current = driveRoot;

        foreach (var segment in relative.Split(
                     Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (_probe.IsReparsePoint(current))
            {
                _logger.LogWarning(
                    "Destination rejected; code={ReferenceCode}; reason=ReparsePointInChain; segment={PathSegment}",
                    DestinationErrorCodes.UnsafeReparsePoint, current);
                throw DestinationErrors.UnsafeReparsePoint();
            }
        }
    }

    private static bool IsSameOrDescendant(string canonical, string protectedRoot)
    {
        if (string.IsNullOrWhiteSpace(protectedRoot))
        {
            return false;
        }

        var normalizedProtected = TrimTrailingSeparators(protectedRoot);
        if (canonical.Equals(normalizedProtected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return canonical.StartsWith(
            normalizedProtected + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimTrailingSeparators(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length < root.Length ? root : trimmed;
    }

    private static IReadOnlyList<string> DefaultProtectedRoots()
    {
        var roots = new List<string>();

        void Add(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                roots.Add(Path.GetFullPath(path));
            }
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        Add(AppContext.BaseDirectory);

        return roots;
    }
}
