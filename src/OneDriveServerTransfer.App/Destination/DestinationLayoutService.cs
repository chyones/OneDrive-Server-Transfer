using System.IO;
using Microsoft.Extensions.Logging;

namespace OneDriveServerTransfer.Destination;

/// <summary>
/// Creates the fixed destination layout (<c>OneDriveData</c> and <c>_TransferReport</c>)
/// under a validated destination root and reports whether a destination already
/// contains content the application did not create.
/// </summary>
public interface IDestinationLayoutService
{
    ResolvedDestination EnsureLayout(string canonicalRootPath);

    /// <summary>
    /// True when the content directory holds anything, or the state directory holds
    /// anything other than the application-owned state database and lock file. Used to
    /// reject silently adopting a non-empty destination without valid application state.
    /// </summary>
    bool HasForeignContent(ResolvedDestination destination);
}

public sealed class DestinationLayoutService : IDestinationLayoutService
{
    private static readonly string[] AllowedStateEntries =
    [
        ResolvedDestination.StateDatabaseFileName,
        ResolvedDestination.LockFileName
    ];

    private readonly ILogger<DestinationLayoutService> _logger;

    public DestinationLayoutService(ILogger<DestinationLayoutService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ResolvedDestination EnsureLayout(string canonicalRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalRootPath);

        var destination = new ResolvedDestination(canonicalRootPath);

        try
        {
            Directory.CreateDirectory(destination.ContentRootPath);
            Directory.CreateDirectory(destination.StateRootPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "Destination layout creation failed; code={ReferenceCode}; path={DestinationPath}",
                DestinationErrorCodes.LayoutCreationFailed, canonicalRootPath);
            throw DestinationErrors.LayoutCreationFailed(exception);
        }

        return destination;
    }

    public bool HasForeignContent(ResolvedDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (Directory.Exists(destination.ContentRootPath) &&
            Directory.EnumerateFileSystemEntries(destination.ContentRootPath).Any())
        {
            return true;
        }

        if (!Directory.Exists(destination.StateRootPath))
        {
            return false;
        }

        return Directory
            .EnumerateFileSystemEntries(destination.StateRootPath)
            .Any(entry => !AllowedStateEntries.Contains(
                Path.GetFileName(entry), StringComparer.OrdinalIgnoreCase));
    }
}
