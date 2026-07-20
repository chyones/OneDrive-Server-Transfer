using System.Net;
using System.Net.Http;
using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Tests.Authentication;

public class OperatorProfileProviderTests
{
    private const string ObjectId = "33333333-3333-3333-3333-333333333333";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responder(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ValidResponseParsesOperatorProfile()
    {
        var handler = new StubHandler(_ => Json(
            """{"id":"33333333-3333-3333-3333-333333333333","userPrincipalName":"operator@example.test","displayName":"Test Operator","@odata.context":"ignored"}"""));
        var provider = new OperatorProfileProvider(new HttpClient(handler));

        var profile = await provider.GetCurrentOperatorProfileAsync("token", CancellationToken.None);

        Assert.Equal(ObjectId, profile.ObjectId);
        Assert.Equal("operator@example.test", profile.UserPrincipalName);
        Assert.Equal("Test Operator", profile.DisplayName);
    }

    [Fact]
    public async Task RequestUsesApprovedMeEndpointAndHeaders()
    {
        var handler = new StubHandler(_ => Json(
            """{"id":"33333333-3333-3333-3333-333333333333","userPrincipalName":"operator@example.test","displayName":"Test Operator"}"""));
        var provider = new OperatorProfileProvider(new HttpClient(handler));

        await provider.GetCurrentOperatorProfileAsync("token", CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal(OperatorProfileProvider.MeEndpoint, handler.LastRequest.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.True(handler.LastRequest.Headers.Contains("client-request-id"));
    }

    [Fact]
    public async Task MissingIdIsInvalidResponse()
    {
        var handler = new StubHandler(_ => Json("""{"userPrincipalName":"operator@example.test","displayName":"Test Operator"}"""));
        var provider = new OperatorProfileProvider(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<OperatorProfileException>(
            () => provider.GetCurrentOperatorProfileAsync("token", CancellationToken.None));

        Assert.Equal(OperatorProfileFailure.InvalidResponse, exception.Failure);
    }

    [Fact]
    public async Task MalformedJsonIsInvalidResponse()
    {
        var handler = new StubHandler(_ => Json("not-json"));
        var provider = new OperatorProfileProvider(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<OperatorProfileException>(
            () => provider.GetCurrentOperatorProfileAsync("token", CancellationToken.None));

        Assert.Equal(OperatorProfileFailure.InvalidResponse, exception.Failure);
    }

    [Fact]
    public async Task UnauthorizedIsClassified()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var provider = new OperatorProfileProvider(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<OperatorProfileException>(
            () => provider.GetCurrentOperatorProfileAsync("token", CancellationToken.None));

        Assert.Equal(OperatorProfileFailure.Unauthorized, exception.Failure);
        Assert.Equal(401, exception.HttpStatusCode);
    }

    [Fact]
    public async Task ForbiddenIsClassified()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var provider = new OperatorProfileProvider(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<OperatorProfileException>(
            () => provider.GetCurrentOperatorProfileAsync("token", CancellationToken.None));

        Assert.Equal(OperatorProfileFailure.Forbidden, exception.Failure);
        Assert.Equal(403, exception.HttpStatusCode);
    }

    [Fact]
    public async Task OtherFailuresAreUnavailable()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var provider = new OperatorProfileProvider(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<OperatorProfileException>(
            () => provider.GetCurrentOperatorProfileAsync("token", CancellationToken.None));

        Assert.Equal(OperatorProfileFailure.Unavailable, exception.Failure);
        Assert.Equal(500, exception.HttpStatusCode);
    }
}
