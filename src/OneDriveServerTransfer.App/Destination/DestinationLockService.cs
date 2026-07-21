using System.IO;
using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;

namespace OneDriveServerTransfer.Destination;

/// <summary>
/// Operating-system-backed exclusive lock on a destination, preventing two processes
/// or Windows sessions from using the same destination concurrently. Held for the
/// whole session and released on disposal.
/// </summary>
public interface IDestinationLock : IDestinationExclusiveLock, IDisposable
{
    ResolvedDestination Destination { get; }
}

/// <summary>
/// Acquires the destination lock. A held lock fails with a reference-coded
/// <see cref="DestinationException" /> (<c>DST-LOCK-001</c>).
/// </summary>
public interface IDestinationLockService
{
    IDestinationLock Acquire(ResolvedDestination destination);
}

public sealed class DestinationLockService : IDestinationLockService
{
    private readonly ILogger<DestinationLockService> _logger;

    public DestinationLockService(ILogger<DestinationLockService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IDestinationLock Acquire(ResolvedDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        try
        {
            var stream = new FileStream(
                destination.LockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new DestinationFileLock(destination, stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "Destination lock acquisition failed; code={ReferenceCode}; path={DestinationPath}",
                DestinationErrorCodes.DestinationLocked, destination.RootPath);
            throw DestinationErrors.DestinationLocked(exception);
        }
    }
}

internal sealed class DestinationFileLock : IDestinationLock
{
    private readonly FileStream _stream;

    internal DestinationFileLock(ResolvedDestination destination, FileStream stream)
    {
        Destination = destination;
        _stream = stream;
    }

    public ResolvedDestination Destination { get; }

    public string DestinationRoot => Destination.RootPath;

    public void Dispose() => _stream.Dispose();

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync().ConfigureAwait(false);
    }
}
