using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Tests.TestSupport;
using OneDriveServerTransfer.Transfer;

namespace OneDriveServerTransfer.Tests.Transfer;

/// <summary>
/// Verifies the unauthenticated temporary download client: credential isolation,
/// streaming, range resume, restart-from-zero semantics, failure classification, and
/// the guarantee that the temporary URL is never logged.
/// </summary>
public class TemporaryDownloadClientTests
{
    private const string ItemReference = "item-ref-001";

    private static (TemporaryDownloadClient Client, RecordingHandler Handler, CapturingLogger<TemporaryDownloadClient> Logger)
        Create(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new RecordingHandler(responder);
        var logger = new CapturingLogger<TemporaryDownloadClient>();
        var client = new TemporaryDownloadClient(new StubHttpClientFactory(handler), logger);
        return (client, handler, logger);
    }

    private static HttpResponseMessage Content(HttpStatusCode status, byte[] bytes, Action<HttpResponseMessage>? configure = null)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(bytes) };
        configure?.Invoke(response);
        return response;
    }

    [Fact]
    public async Task RequestCarriesNoAuthorizationCookiesOrGraphHeaders()
    {
        var payload = "hello"u8.ToArray();
        var (client, handler, _) = Create(_ => Content(HttpStatusCode.OK, payload));

        using var destination = new MemoryStream();
        await client.DownloadAsync(
            new Uri("https://download.example.test/secret-token-url"), ItemReference,
            destination, null, null, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Null(request.Headers.Authorization);
        Assert.False(request.Headers.Contains("Cookie"));
        Assert.False(request.Headers.Contains("client-request-id"));
        Assert.DoesNotContain(request.Headers, h => h.Key.Contains("auth", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("hello", System.Text.Encoding.UTF8.GetString(destination.ToArray()));
    }

    [Fact]
    public async Task TemporaryUrlNeverAppearsInLogs()
    {
        var secretUrl = "https://download.example.test/super-secret-temporary-url";
        var (client, _, logger) = Create(_ => Content(HttpStatusCode.OK, [1, 2, 3]));

        using var destination = new MemoryStream();
        await client.DownloadAsync(new Uri(secretUrl), ItemReference, destination, null, null, CancellationToken.None);

        Assert.All(logger.Messages, message =>
            Assert.DoesNotContain("super-secret-temporary-url", message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedRequestNeverLogsTheTemporaryUrl()
    {
        var secretUrl = "https://download.example.test/another-secret";
        var (client, _, logger) = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("server exploded with url https://download.example.test/another-secret"),
        });

        using var destination = new MemoryStream();
        await Assert.ThrowsAsync<TemporaryDownloadException>(() => client.DownloadAsync(
            new Uri(secretUrl), ItemReference, destination, null, null, CancellationToken.None));

        Assert.All(logger.Messages, message =>
            Assert.DoesNotContain("another-secret", message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResumeSendsRangeHeaderAndAppendsOnValid206()
    {
        var tail = "world"u8.ToArray();
        var (client, handler, _) = Create(_ => Content(HttpStatusCode.PartialContent, tail, response =>
            response.Content.Headers.ContentRange =
                new System.Net.Http.Headers.ContentRangeHeaderValue(5, 9, 10)));

        using var destination = new MemoryStream();
        destination.Write("hello"u8);
        var result = await client.DownloadAsync(
            new Uri("https://download.example.test/x"), ItemReference,
            destination, resumeOffsetBytes: 5, null, CancellationToken.None);

        Assert.Equal("bytes=5-", Assert.Single(handler.Requests).Headers.Range?.ToString());
        Assert.True(result.ResumedFromOffset);
        Assert.Equal(5, result.BytesWritten);
        Assert.Equal(10, result.TotalLengthBytes);
        Assert.Equal("helloworld", System.Text.Encoding.UTF8.GetString(destination.ToArray()));
    }

    [Fact]
    public async Task RangeIgnoredWith200RestartsFromByteZero()
    {
        var full = "0123456789"u8.ToArray();
        var (client, _, _) = Create(_ => Content(HttpStatusCode.OK, full));

        using var destination = new MemoryStream("stale-partial-data!!"u8.ToArray());
        var result = await client.DownloadAsync(
            new Uri("https://download.example.test/x"), ItemReference,
            destination, resumeOffsetBytes: 5, null, CancellationToken.None);

        Assert.False(result.ResumedFromOffset);
        Assert.Equal(10, result.BytesWritten);
        Assert.Equal(full, destination.ToArray());
    }

    [Theory]
    [InlineData("bytes 0-9/10")]   // wrong start
    [InlineData("bytes 5-20/10")]  // end beyond total
    [InlineData("bytes 5-9/9")]    // total not greater than end
    [InlineData("items 5-9/10")]   // wrong unit
    public async Task InvalidContentRangeMetadataThrows(string contentRange)
    {
        var (client, _, _) = Create(_ => Content(HttpStatusCode.PartialContent, [1, 2, 3], response =>
            response.Content.Headers.TryAddWithoutValidation("Content-Range", contentRange)));

        using var destination = new MemoryStream("hello"u8.ToArray());
        var exception = await Assert.ThrowsAsync<TemporaryDownloadException>(() => client.DownloadAsync(
            new Uri("https://download.example.test/x"), ItemReference,
            destination, resumeOffsetBytes: 5, null, CancellationToken.None));

        Assert.Equal(TemporaryDownloadFailureKind.InvalidRangeMetadata, exception.Kind);
        Assert.Equal("hello", System.Text.Encoding.UTF8.GetString(destination.ToArray()));
    }

    [Fact]
    public async Task MissingContentRangeOn206Throws()
    {
        var (client, _, _) = Create(_ => Content(HttpStatusCode.PartialContent, [1, 2, 3]));

        using var destination = new MemoryStream("hello"u8.ToArray());
        var exception = await Assert.ThrowsAsync<TemporaryDownloadException>(() => client.DownloadAsync(
            new Uri("https://download.example.test/x"), ItemReference,
            destination, resumeOffsetBytes: 5, null, CancellationToken.None));

        Assert.Equal(TemporaryDownloadFailureKind.InvalidRangeMetadata, exception.Kind);
    }

    [Fact]
    public async Task RangeNotSatisfiableIsClassifiedForRevalidation()
    {
        var (client, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable));

        using var destination = new MemoryStream();
        var exception = await Assert.ThrowsAsync<TemporaryDownloadException>(() => client.DownloadAsync(
            new Uri("https://download.example.test/x"), ItemReference,
            destination, resumeOffsetBytes: 12, null, CancellationToken.None));

        Assert.Equal(TemporaryDownloadFailureKind.RangeNotSatisfiable, exception.Kind);
        Assert.Equal(416, exception.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task ExpiredTemporaryUrlStatusesAreRetryable(HttpStatusCode status)
    {
        var (client, _, _) = Create(_ => new HttpResponseMessage(status));

        using var destination = new MemoryStream();
        var exception = await Assert.ThrowsAsync<TemporaryDownloadException>(() => client.DownloadAsync(
            new Uri("https://download.example.test/x"), ItemReference,
            destination, null, null, CancellationToken.None));

        Assert.Equal(TemporaryDownloadFailureKind.Transient, exception.Kind);
        Assert.True(exception.IsRetryable);
    }

    [Fact]
    public async Task ThrottlingCarriesRetryAfter()
    {
        var (client, _, _) = Create(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter =
                new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
            return response;
        });

        using var destination = new MemoryStream();
        var exception = await Assert.ThrowsAsync<TemporaryDownloadException>(() => client.DownloadAsync(
            new Uri("https://download.example.test/x"), ItemReference,
            destination, null, null, CancellationToken.None));

        Assert.Equal(TimeSpan.FromSeconds(7), exception.RetryAfter);
        Assert.True(exception.IsRetryable);
    }

    [Fact]
    public async Task PermanentFailureIsNotRetryable()
    {
        var (client, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        using var destination = new MemoryStream();
        var exception = await Assert.ThrowsAsync<TemporaryDownloadException>(() => client.DownloadAsync(
            new Uri("https://download.example.test/x"), ItemReference,
            destination, null, null, CancellationToken.None));

        Assert.Equal(TemporaryDownloadFailureKind.Permanent, exception.Kind);
        Assert.False(exception.IsRetryable);
    }

    [Fact]
    public async Task TruncatedResponseIsRetryable()
    {
        var (client, _, _) = Create(_ => Content(HttpStatusCode.OK, [1, 2, 3], response =>
            response.Content.Headers.ContentLength = 10));

        using var destination = new MemoryStream();
        var exception = await Assert.ThrowsAsync<TemporaryDownloadException>(() => client.DownloadAsync(
            new Uri("https://download.example.test/x"), ItemReference,
            destination, null, null, CancellationToken.None));

        Assert.True(exception.IsRetryable);
    }

    [Fact]
    public async Task ProgressReportsCumulativeBytesWritten()
    {
        var payload = new byte[200 * 1024];
        new Random(42).NextBytes(payload);
        var (client, _, _) = Create(_ => Content(HttpStatusCode.OK, payload));

        var progress = new RecordingProgress();
        using var destination = new MemoryStream();
        var result = await client.DownloadAsync(
            new Uri("https://download.example.test/x"), ItemReference,
            destination, null, progress, CancellationToken.None);

        Assert.Equal(payload.Length, result.BytesWritten);
        Assert.Equal(payload.Length, destination.Length);
        Assert.NotEmpty(progress.Reports);
        Assert.Equal(payload.Length, progress.Reports[^1]);
        Assert.True(progress.Reports.SequenceEqual(progress.Reports.OrderBy(v => v)),
            "Progress reports must be cumulative and non-decreasing.");
    }

    /// <summary>Synchronous IProgress recorder; Progress&lt;T&gt; would marshal through the test context.</summary>
    private sealed class RecordingProgress : IProgress<long>
    {
        public List<long> Reports { get; } = [];

        public void Report(long value)
        {
            lock (Reports)
            {
                Reports.Add(value);
            }
        }
    }

    /// <summary>HttpMessageHandler double recording every outbound request.</summary>
    internal sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}
