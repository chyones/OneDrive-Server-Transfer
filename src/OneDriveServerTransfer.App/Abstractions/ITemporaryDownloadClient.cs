using System.IO;

namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Separate unauthenticated HTTP client for short-lived pre-authenticated download URLs.
/// Implemented in milestone M5. This client must never receive Microsoft Graph bearer
/// tokens, cookies, or Graph-specific authorization headers, and temporary URLs must
/// never be logged or persisted: log statements use only the caller-provided item
/// reference. Resume proceeds only when the host returns a valid 206 Partial Content
/// response with matching range metadata; a 200 OK answer to a range request restarts
/// the download from byte zero by truncating the (seekable) destination. The client
/// performs a single request per call and never retries: the download retry
/// coordinator is the only retry owner for this request category.
/// </summary>
public interface ITemporaryDownloadClient
{
    /// <summary>
    /// Streams content from the temporary URL into <paramref name="destination" />.
    /// When <paramref name="resumeOffsetBytes" /> is positive a Range request is sent
    /// and the response must be 206 with a Content-Range whose start matches the
    /// offset; otherwise a plain GET is sent. Throws a TemporaryDownloadException with
    /// classification data on any failure.
    /// </summary>
    Task<TemporaryDownloadResult> DownloadAsync(
        Uri temporaryDownloadUrl,
        string itemReference,
        Stream destination,
        long? resumeOffsetBytes,
        IProgress<long>? bytesWritten,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of one download request. <see cref="ResumedFromOffset" /> is true only when
/// the host honored the range request (valid 206); false means the destination was
/// (re)written from byte zero. <see cref="TotalLengthBytes" /> carries the response
/// Content-Length when present so the caller can detect truncated transfers.
/// </summary>
public sealed record TemporaryDownloadResult(
    long BytesWritten,
    bool ResumedFromOffset,
    int StatusCode,
    long? TotalLengthBytes);
