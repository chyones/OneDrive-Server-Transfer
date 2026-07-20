namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Local destination storage operations. Implemented in milestone M4. Destinations are
/// limited to local fixed or directly attached storage on the same Windows Server;
/// network, UNC, mapped-drive, NAS, system-directory, and application-installation
/// locations are rejected. Every file operation revalidates containment under the bound
/// destination root and refuses unsafe reparse-point redirection.
/// </summary>
public interface ILocalStorageService
{
    Task<DestinationValidationResult> ValidateDestinationAsync(
        string destinationRoot,
        CancellationToken cancellationToken);

    Task<IDestinationExclusiveLock> AcquireExclusiveLockAsync(
        string destinationRoot,
        CancellationToken cancellationToken);

    long GetFreeSpaceBytes(string destinationRoot);
}

public sealed record DestinationValidationResult(bool IsValid, string? FailureReason);

/// <summary>
/// Operating-system-backed exclusive lock preventing two processes or Windows sessions
/// from using the same destination concurrently.
/// </summary>
public interface IDestinationExclusiveLock : IAsyncDisposable
{
    string DestinationRoot { get; }
}
