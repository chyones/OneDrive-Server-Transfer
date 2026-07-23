using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;

namespace OneDriveServerTransfer.Transfer;

/// <summary>
/// Unauthenticated HTTP client for short-lived pre-authenticated download URLs
/// (D-037, docs/DOWNLOAD_AND_INTEGRITY_POLICY.md). Uses the dedicated "download"
/// <see cref="IHttpClientFactory" /> client, which carries no Graph bearer token, no
/// cookies, and no Graph middleware. The temporary URL is never logged, persisted, or
/// placed in state: only the caller-provided item reference appears in logs. The
/// client sends at most one request per call and owns no retry; the
/// <see cref="DownloadRetryCoordinator" /> is the single retry owner for this request
/// category.
/// </summary>
public sealed class TemporaryDownloadClient : ITemporaryDownloadClient
{
    internal const string HttpClientName = "download";

    private const int BufferSizeBytes = 80 * 1024;

    private static readonly HashSet<int> TransientStatuses = [408, 429, 500, 502, 503, 504];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TemporaryDownloadClient> _logger;

    public TemporaryDownloadClient(
        IHttpClientFactory httpClientFactory,
        ILogger<TemporaryDownloadClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TemporaryDownloadResult> DownloadAsync(
        Uri temporaryDownloadUrl,
        string itemReference,
        Stream destination,
        long? resumeOffsetBytes,
        IProgress<long>? bytesWritten,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(temporaryDownloadUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemReference);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        }

        var resumeOffset = resumeOffsetBytes ?? 0;
        if (resumeOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resumeOffsetBytes));
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, temporaryDownloadUrl);
        if (resumeOffset > 0)
        {
            request.Headers.Range = new RangeHeaderValue(resumeOffset, null);
        }

        HttpResponseMessage response;
        try
        {
            response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            // Connection reset, DNS, TLS, or socket interruption: transient per policy.
            throw new TemporaryDownloadException(
                TemporaryDownloadFailureKind.Transient, null, retryAfter: null, exception);
        }

        using (response)
        {
            var statusCode = (int)response.StatusCode;

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    // A 200 answer to a range request means the host ignored Range:
                    // truncate the partial safely and restart from byte zero.
                    if (resumeOffset > 0)
                    {
                        RestartFromZero(destination);
                    }

                    var okBytes = await CopyAsync(response, destination, bytesWritten, cancellationToken)
                        .ConfigureAwait(false);
                    ThrowIfTruncated(okBytes, response.Content.Headers.ContentLength, statusCode);
                    _logger.LogInformation(
                        "Download completed; item={ItemReference}; status={Status}; bytes={Bytes}; resumed={Resumed}",
                        itemReference, statusCode, okBytes, false);
                    return new TemporaryDownloadResult(okBytes, false, statusCode,
                        response.Content.Headers.ContentLength);

                case HttpStatusCode.PartialContent:
                    if (resumeOffset <= 0)
                    {
                        // 206 without a range request is contradictory range metadata.
                        throw new TemporaryDownloadException(
                            TemporaryDownloadFailureKind.InvalidRangeMetadata, statusCode);
                    }

                    ValidateContentRange(response, resumeOffset, statusCode);
                    destination.Position = resumeOffset;
                    var resumedBytes = await CopyAsync(response, destination, bytesWritten, cancellationToken)
                        .ConfigureAwait(false);
                    ThrowIfTruncated(resumedBytes, response.Content.Headers.ContentLength, statusCode);
                    _logger.LogInformation(
                        "Download resumed; item={ItemReference}; status={Status}; bytes={Bytes}; offset={Offset}",
                        itemReference, statusCode, resumedBytes, resumeOffset);
                    return new TemporaryDownloadResult(resumedBytes, true, statusCode,
                        response.Content.Headers.ContentRange?.Length
                        ?? response.Content.Headers.ContentLength);

                case HttpStatusCode.RequestedRangeNotSatisfiable:
                    // The caller must revalidate the local partial length and current
                    // source metadata before restarting or failing (policy).
                    throw new TemporaryDownloadException(
                        TemporaryDownloadFailureKind.RangeNotSatisfiable, statusCode,
                        ReadRetryAfter(response));

                default:
                    throw ClassifyFailure(response, statusCode);
            }
        }
    }

    private static void RestartFromZero(Stream destination)
    {
        if (!destination.CanSeek)
        {
            throw new TemporaryDownloadException(
                TemporaryDownloadFailureKind.Permanent, 200,
                innerException: new InvalidOperationException(
                    "The partial-file stream must be seekable to restart from byte zero."));
        }

        destination.SetLength(0);
        destination.Position = 0;
    }

    private static void ValidateContentRange(HttpResponseMessage response, long resumeOffset, int statusCode)
    {
        var contentRange = response.Content.Headers.ContentRange;
        if (contentRange is null ||
            !string.Equals(contentRange.Unit, "bytes", StringComparison.OrdinalIgnoreCase) ||
            contentRange.From != resumeOffset ||
            contentRange.To is null ||
            contentRange.To < contentRange.From ||
            contentRange.Length is null ||
            contentRange.Length <= contentRange.To)
        {
            // Absent, contradictory, or unsafe range metadata: never append.
            throw new TemporaryDownloadException(
                TemporaryDownloadFailureKind.InvalidRangeMetadata, statusCode);
        }
    }

    private static TemporaryDownloadException ClassifyFailure(HttpResponseMessage response, int statusCode)
    {
        var retryAfter = ReadRetryAfter(response);
        if (TransientStatuses.Contains(statusCode))
        {
            return new TemporaryDownloadException(
                TemporaryDownloadFailureKind.Transient, statusCode, retryAfter);
        }

        // 401/403/404/410 from the temporary host mean the short-lived preauthenticated
        // URL is no longer usable; the next attempt obtains a fresh URL through Graph.
        if (statusCode is 401 or 403 or 404 or 410)
        {
            return new TemporaryDownloadException(
                TemporaryDownloadFailureKind.Transient, statusCode, retryAfter);
        }

        return new TemporaryDownloadException(
            TemporaryDownloadFailureKind.Permanent, statusCode, retryAfter);
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    private static void ThrowIfTruncated(long bytesCopied, long? contentLength, int statusCode)
    {
        if (contentLength is { } expected && bytesCopied != expected)
        {
            // Incomplete temporary-host response: the partial and range metadata stay
            // safe, so the attempt is retryable inside the per-file budget.
            throw new TemporaryDownloadException(
                TemporaryDownloadFailureKind.Transient, statusCode,
                innerException: new IOException(
                    $"Truncated response: received {bytesCopied} of {expected} bytes."));
        }
    }

    private static async Task<long> CopyAsync(
        HttpResponseMessage response,
        Stream destination,
        IProgress<long>? bytesWritten,
        CancellationToken cancellationToken)
    {
        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var buffer = new byte[BufferSizeBytes];
        long total = 0;
        while (true)
        {
            int read;
            try
            {
                read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                throw new TemporaryDownloadException(
                    TemporaryDownloadFailureKind.Transient, (int)response.StatusCode,
                    innerException: exception);
            }

            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
            bytesWritten?.Report(total);
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        return total;
    }
}
