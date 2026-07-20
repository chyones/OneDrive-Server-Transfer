namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// The single owner of automatic retries per request category (decision D-036).
/// Implemented in milestone M5. Honors Retry-After, uses bounded exponential backoff
/// with jitter, allows at most five attempts per file, and observes cancellation during
/// the delay. No second layer may add its own automatic retry for the same request.
/// </summary>
public interface IRetryCoordinator
{
    Task<T> ExecuteAsync<T>(
        RetryCategory category,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}

public enum RetryCategory
{
    GraphMetadata,
    TemporaryDownload
}
