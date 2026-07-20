using System.IO;

namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Separate unauthenticated HTTP client for short-lived pre-authenticated download URLs.
/// Implemented in milestone M5. This client must never receive Microsoft Graph bearer
/// tokens, cookies, or Graph-specific authorization headers, and temporary URLs must
/// never be logged or persisted. Resume proceeds only when the host returns a valid
/// 206 Partial Content response with matching range metadata; any other success response
/// restarts the download from byte zero.
/// </summary>
public interface ITemporaryDownloadClient
{
    Task<TemporaryDownloadResult> DownloadAsync(
        Uri temporaryDownloadUrl,
        Stream destination,
        long? resumeOffsetBytes,
        IProgress<long>? bytesWritten,
        CancellationToken cancellationToken);
}

public sealed record TemporaryDownloadResult(long BytesWritten, bool ResumedFromOffset);
