using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.SourceResolution;

public class GraphRequestChannelTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder) => _responder = responder;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(CloneForInspection(request));
            return Task.FromResult(_responder(request, Requests.Count));
        }

        private static HttpRequestMessage CloneForInspection(HttpRequestMessage request)
        {
            // Headers remain readable on the original request after disposal only if we
            // keep a reference; store the original (the channel disposes it, so copy).
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Headers.Authorization is not null)
            {
                clone.Headers.Authorization = request.Headers.Authorization;
            }

            return clone;
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "request-id", "ms-request-1" } },
        };

    private static HttpResponseMessage ErrorBody(HttpStatusCode status, string code, string message) =>
        Json("{\"error\":{\"code\":\"" + code + "\",\"message\":\"" + message + "\"}}", status);

    private GraphRequestChannel CreateChannel(
        StubHandler handler,
        FakeAuthenticationService authenticationService,
        CapturingLogger<GraphRequestChannel> logger)
    {
        var coordinator = new GraphRetryCoordinator(
            NullLogger<GraphRetryCoordinator>.Instance,
            (_, _) => Task.CompletedTask,
            () => 0.0);

        return new GraphRequestChannel(
            new StubHttpClientFactory(handler),
            authenticationService,
            coordinator,
            logger);
    }

    [Fact]
    public async Task SuccessReturnsParsedBodyWithCorrelationHeaders()
    {
        var handler = new StubHandler((_, _) => Json("""{"id":"drive-1"}"""));
        var auth = new FakeAuthenticationService();
        var logger = new CapturingLogger<GraphRequestChannel>();
        var channel = CreateChannel(handler, auth, logger);

        using var body = await channel.GetJsonAsync(
            new Uri("https://graph.microsoft.com/v1.0/users/employee%40contoso.com/drive?$select=id"),
            "/users/{upn}/drive",
            CancellationToken.None);

        Assert.Equal("drive-1", body.RootElement.GetProperty("id").GetString());

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.True(request.Headers.Contains("client-request-id"));
        Assert.Contains(logger.Messages, message => message.Contains("/users/{upn}/drive", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("ms-request-1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UrlsAndTokensNeverReachLogs()
    {
        var handler = new StubHandler((_, _) => Json("""{"id":"drive-1"}"""));
        var auth = new FakeAuthenticationService();
        var logger = new CapturingLogger<GraphRequestChannel>();
        var channel = CreateChannel(handler, auth, logger);

        using var body = await channel.GetJsonAsync(
            new Uri("https://graph.microsoft.com/v1.0/users/employee%40contoso.com/drive?$select=id"),
            "/users/{upn}/drive",
            CancellationToken.None);

        foreach (var message in logger.Messages)
        {
            Assert.DoesNotContain("employee%40contoso.com", message, StringComparison.Ordinal);
            Assert.DoesNotContain("employee@contoso.com", message, StringComparison.Ordinal);
            Assert.DoesNotContain("test-access-token", message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task FirstUnauthorizedTriggersOneSilentRenewalAndResend()
    {
        var handler = new StubHandler((_, call) =>
            call == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Json("""{"id":"drive-1"}"""));
        var auth = new FakeAuthenticationService();
        var channel = CreateChannel(handler, auth, new CapturingLogger<GraphRequestChannel>());

        using var body = await channel.GetJsonAsync(
            new Uri("https://graph.microsoft.com/v1.0/me"), "/me", CancellationToken.None);

        Assert.Equal(2, auth.AcquireTokenCallCount);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task RepeatedUnauthorizedThrowsNonTransient()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var auth = new FakeAuthenticationService();
        var channel = CreateChannel(handler, auth, new CapturingLogger<GraphRequestChannel>());

        var exception = await Assert.ThrowsAsync<GraphRequestException>(() => channel.GetJsonAsync(
            new Uri("https://graph.microsoft.com/v1.0/me"), "/me", CancellationToken.None));

        Assert.Equal(401, exception.StatusCode);
        Assert.False(exception.IsTransient);
        Assert.Equal(2, handler.Requests.Count); // one renewal resend, no blind loop
    }

    [Fact]
    public async Task TransientFailureIsRetriedThroughTheCoordinator()
    {
        var handler = new StubHandler((_, call) =>
            call == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Json("""{"id":"drive-1"}"""));
        var channel = CreateChannel(handler, new FakeAuthenticationService(), new CapturingLogger<GraphRequestChannel>());

        using var body = await channel.GetJsonAsync(
            new Uri("https://graph.microsoft.com/v1.0/me"), "/me", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ErrorBodyCodeAndHintAreClassified()
    {
        var handler = new StubHandler((_, _) =>
            ErrorBody(HttpStatusCode.NotFound, "Request_ResourceNotFound", "Resource does not exist"));
        var channel = CreateChannel(handler, new FakeAuthenticationService(), new CapturingLogger<GraphRequestChannel>());

        var exception = await Assert.ThrowsAsync<GraphRequestException>(() => channel.GetJsonAsync(
            new Uri("https://graph.microsoft.com/v1.0/me"), "/me", CancellationToken.None));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal("Request_ResourceNotFound", exception.GraphErrorCode);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task RetryAfterIsCapturedForTheCoordinator()
    {
        var handler = new StubHandler((_, _) =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)429);
            response.Headers.TryAddWithoutValidation("Retry-After", "7");
            return response;
        });
        var channel = CreateChannel(handler, new FakeAuthenticationService(), new CapturingLogger<GraphRequestChannel>());

        var exception = await Assert.ThrowsAsync<GraphRequestException>(() => channel.GetJsonAsync(
            new Uri("https://graph.microsoft.com/v1.0/me"), "/me", CancellationToken.None));

        Assert.Equal(429, exception.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), exception.RetryAfter);
        Assert.Equal(GraphRetryCoordinator.MaxAttempts, handler.Requests.Count);
    }

    [Fact]
    public async Task NetworkInterruptionIsTransient()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection reset"));
        var channel = CreateChannel(handler, new FakeAuthenticationService(), new CapturingLogger<GraphRequestChannel>());

        var exception = await Assert.ThrowsAsync<GraphRequestException>(() => channel.GetJsonAsync(
            new Uri("https://graph.microsoft.com/v1.0/me"), "/me", CancellationToken.None));

        Assert.True(exception.IsTransient);
        Assert.Equal(GraphRetryCoordinator.MaxAttempts, handler.Requests.Count);
    }
}
