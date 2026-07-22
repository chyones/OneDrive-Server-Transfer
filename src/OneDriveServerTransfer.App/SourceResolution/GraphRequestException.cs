namespace OneDriveServerTransfer.SourceResolution;

/// <summary>
/// A failed Microsoft Graph request. Carries only classification data: HTTP status,
/// sanitized Graph error code, Retry-After, and transience. The raw error body hint is
/// used only for failure classification and is never logged or displayed.
/// </summary>
public sealed class GraphRequestException : Exception
{
    public GraphRequestException(
        int? statusCode,
        string? graphErrorCode,
        bool isTransient,
        TimeSpan? retryAfter,
        string? errorHintForClassification,
        Exception? innerException = null,
        Uri? resetLocation = null)
        : base($"Graph request failed with status {statusCode?.ToString() ?? "n/a"} and code {graphErrorCode ?? "n/a"}.", innerException)
    {
        StatusCode = statusCode;
        GraphErrorCode = graphErrorCode;
        IsTransient = isTransient;
        RetryAfter = retryAfter;
        ErrorHintForClassification = errorHintForClassification;
        ResetLocation = resetLocation;
    }

    public int? StatusCode { get; }

    public string? GraphErrorCode { get; }

    public bool IsTransient { get; }

    public TimeSpan? RetryAfter { get; }

    /// <summary>Classification-only hint. Never log or display this value.</summary>
    public string? ErrorHintForClassification { get; }

    /// <summary>
    /// The opaque <c>Location</c> URL of a supported HTTP 410 delta-reset response
    /// (GRAPH-DELTA-003). It is never logged or displayed; the delta inventory client
    /// surfaces it to the caller so a fresh enumeration can start from it.
    /// </summary>
    public Uri? ResetLocation { get; }
}
