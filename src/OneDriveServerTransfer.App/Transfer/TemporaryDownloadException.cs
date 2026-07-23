namespace OneDriveServerTransfer.Transfer;

/// <summary>Classification of a failed temporary-host download request.</summary>
public enum TemporaryDownloadFailureKind
{
    /// <summary>A transient condition (throttling, 5xx, interruption, expired URL).</summary>
    Transient,

    /// <summary>200-level protocol violation: 206 without valid matching Content-Range.</summary>
    InvalidRangeMetadata,

    /// <summary>416 Range Not Satisfiable; the caller must revalidate the partial.</summary>
    RangeNotSatisfiable,

    /// <summary>A permanent failure that must not be retried automatically.</summary>
    Permanent
}

/// <summary>
/// A failed request against a temporary download host. Carries only classification
/// data: HTTP status, failure kind, Retry-After, and transience. The temporary URL is
/// never carried, logged, or displayed.
/// </summary>
public sealed class TemporaryDownloadException : Exception
{
    public TemporaryDownloadException(
        TemporaryDownloadFailureKind kind,
        int? statusCode,
        TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base($"Temporary download request failed ({kind}, status {statusCode?.ToString() ?? "n/a"}).", innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    public TemporaryDownloadFailureKind Kind { get; }

    public int? StatusCode { get; }

    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// True when the failure is safe to retry inside the per-file attempt budget:
    /// transient faults, invalid range metadata after a safe restart from zero, and
    /// 416 after partial revalidation. Permanent failures are never retried.
    /// </summary>
    public bool IsRetryable => Kind is not TemporaryDownloadFailureKind.Permanent;
}
