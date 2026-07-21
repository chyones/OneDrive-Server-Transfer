using System.IO;
using Microsoft.Extensions.Logging;

namespace OneDriveServerTransfer.Destination;

/// <summary>
/// Canonical containment and write-safety validation for employee-content paths
/// (D-011). Every create, open, replace, or rename target is resolved to a canonical
/// full path, verified to stay under the <c>OneDriveData</c> root, checked against the
/// supported path-length limit, and walked for reparse points (junctions, symlinks,
/// mount points) on every existing segment below the content root. Existing targets
/// that are reparse points or have multiple hard links are never overwritten. Nothing
/// here ever resolves a path outside the content root.
/// </summary>
public interface IDestinationPathGuard
{
    /// <summary>
    /// Resolves a mapped relative path to a canonical, contained, length-checked full
    /// path under the content root. Throws a reference-coded
    /// <see cref="DestinationException" /> when containment, length, or reparse-point
    /// checks fail.
    /// </summary>
    string ResolveContentPath(ResolvedDestination destination, string mappedRelativePath);

    /// <summary>
    /// <see cref="ResolveContentPath" /> plus the untrusted-existing-file checks used
    /// before create, open, replace, or rename operations.
    /// </summary>
    string ResolveWritableContentPath(ResolvedDestination destination, string mappedRelativePath);
}

public sealed class DestinationPathGuard : IDestinationPathGuard
{
    private readonly IFileSystemProbe _probe;
    private readonly ILogger<DestinationPathGuard> _logger;

    public DestinationPathGuard(ILogger<DestinationPathGuard> logger)
        : this(new SystemIOFileSystemProbe(), logger)
    {
    }

    internal DestinationPathGuard(IFileSystemProbe probe, ILogger<DestinationPathGuard> logger)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ResolveContentPath(ResolvedDestination destination, string mappedRelativePath)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(mappedRelativePath);

        var contentRoot = Path.GetFullPath(destination.ContentRootPath);
        var rootWithSeparator = contentRoot.EndsWith(Path.DirectorySeparatorChar)
            ? contentRoot
            : contentRoot + Path.DirectorySeparatorChar;

        string canonical;
        try
        {
            canonical = Path.GetFullPath(Path.Combine(rootWithSeparator, mappedRelativePath));
        }
        catch (PathTooLongException exception)
        {
            _logger.LogWarning(
                "Content path rejected; code={ReferenceCode}; reason=PathTooLong",
                DestinationErrorCodes.PathTooLong);
            throw DestinationErrors.PathTooLong(exception);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw DestinationErrors.ContainmentViolation(exception);
        }

        if (!canonical.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Content path rejected; code={ReferenceCode}; reason=ContainmentEscape; relativePath={RelativePath}",
                DestinationErrorCodes.ContainmentViolation, mappedRelativePath);
            throw DestinationErrors.ContainmentViolation();
        }

        if (canonical.Length > PathMapperV1.MaxCanonicalPathUtf16Units)
        {
            _logger.LogWarning(
                "Content path rejected; code={ReferenceCode}; reason=PathTooLong; length={PathLength}",
                DestinationErrorCodes.PathTooLong, canonical.Length);
            throw DestinationErrors.PathTooLong();
        }

        RejectReparsePointsBelowRoot(rootWithSeparator, canonical);
        return canonical;
    }

    public string ResolveWritableContentPath(ResolvedDestination destination, string mappedRelativePath)
    {
        var canonical = ResolveContentPath(destination, mappedRelativePath);

        if (_probe.FileOrDirectoryExists(canonical))
        {
            if (_probe.IsReparsePoint(canonical))
            {
                _logger.LogWarning(
                    "Existing content path rejected; code={ReferenceCode}; reason=ReparsePointTarget",
                    DestinationErrorCodes.UntrustedExistingFile);
                throw DestinationErrors.UntrustedExistingFile();
            }

            if (File.Exists(canonical) && _probe.GetHardLinkCount(canonical) > 1)
            {
                _logger.LogWarning(
                    "Existing content path rejected; code={ReferenceCode}; reason=MultipleHardLinks",
                    DestinationErrorCodes.UntrustedExistingFile);
                throw DestinationErrors.UntrustedExistingFile();
            }
        }

        return canonical;
    }

    private void RejectReparsePointsBelowRoot(string rootWithSeparator, string canonical)
    {
        // Walk every existing segment below the content root except the final one; the
        // final segment of a write target is checked separately so an existing reparse
        // point there is reported as an untrusted existing file.
        var relative = canonical[rootWithSeparator.Length..];
        var segments = relative.Split(
            Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var current = rootWithSeparator;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            current = Path.Combine(current, segments[index]);
            if (_probe.IsReparsePoint(current))
            {
                _logger.LogWarning(
                    "Content path rejected; code={ReferenceCode}; reason=ReparsePointInChain; segment={PathSegment}",
                    DestinationErrorCodes.UnsafeReparsePoint, current);
                throw DestinationErrors.UnsafeReparsePoint();
            }
        }
    }
}
