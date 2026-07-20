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
        Exception? innerException = null)
        : base($"Graph request failed with status {statusCode?.ToString() ?? "n/a"} and code {graphErrorCode ?? "n/a"}.", innerException)
    {
        StatusCode = statusCode;
        GraphErrorCode = graphErrorCode;
        IsTransient = isTransient;
        RetryAfter = retryAfter;
        ErrorHintForClassification = errorHintForClassification;
    }

    public int? StatusCode { get; }

    public string? GraphErrorCode { get; }

    public bool IsTransient { get; }

    public TimeSpan? RetryAfter { get; }

    /// <summary>Classification-only hint. Never log or display this value.</summary>
    public string? ErrorHintForClassification { get; }
}
