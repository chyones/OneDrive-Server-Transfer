using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;

namespace OneDriveServerTransfer.SourceResolution;

/// <summary>
/// Authenticated Microsoft Graph v1.0 GET channel. Every request carries a unique
/// client-request-id; protected logs record only the endpoint template, status,
/// sanitized error code, and request correlation IDs. URLs with identifiers, tokens,
/// and raw responses are never logged. Retries are owned exclusively by
/// <see cref="IRetryCoordinator" />; this channel adds none.
/// </summary>
public interface IGraphRequestChannel
{
    Task<JsonDocument> GetJsonAsync(Uri requestUri, string endpointTemplate, CancellationToken cancellationToken);
}

public sealed class GraphRequestChannel : IGraphRequestChannel
{
    private static readonly HashSet<int> TransientStatuses = [408, 429, 500, 502, 503, 504];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthenticationService _authenticationService;
    private readonly IRetryCoordinator _retryCoordinator;
    private readonly ILogger<GraphRequestChannel> _logger;

    public GraphRequestChannel(
        IHttpClientFactory httpClientFactory,
        IAuthenticationService authenticationService,
        IRetryCoordinator retryCoordinator,
        ILogger<GraphRequestChannel> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _retryCoordinator = retryCoordinator ?? throw new ArgumentNullException(nameof(retryCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<JsonDocument> GetJsonAsync(
        Uri requestUri,
        string endpointTemplate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointTemplate);

        return await _retryCoordinator.ExecuteAsync(
            RetryCategory.GraphMetadata,
            async attemptCt =>
            {
                var clientRequestId = Guid.NewGuid().ToString();
                var outcome = await SendAsync(requestUri, clientRequestId, attemptCt).ConfigureAwait(false);

                if (outcome.Body is not null)
                {
                    _logger.LogInformation(
                        "Graph request succeeded; endpoint={Endpoint}; status={Status}; clientRequestId={ClientRequestId}; msRequestId={MsRequestId}; responseDate={ResponseDate}",
                        endpointTemplate, outcome.StatusCode, clientRequestId,
                        outcome.MicrosoftRequestId ?? "n/a", outcome.ResponseDateUtc ?? "n/a");
                    return outcome.Body;
                }

                _logger.LogWarning(
                    "Graph request failed; endpoint={Endpoint}; status={Status}; code={Code}; clientRequestId={ClientRequestId}; msRequestId={MsRequestId}",
                    endpointTemplate, outcome.StatusCode, outcome.GraphErrorCode ?? "n/a",
                    clientRequestId, outcome.MicrosoftRequestId ?? "n/a");

                throw outcome.Exception!;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<SendOutcome> SendAsync(Uri requestUri, string clientRequestId, CancellationToken cancellationToken)
    {
        var token = await _authenticationService.AcquireGraphAccessTokenAsync(cancellationToken)
            .ConfigureAwait(false);

        var outcome = await SendOnceAsync(requestUri, token, clientRequestId, cancellationToken)
            .ConfigureAwait(false);

        if (outcome.StatusCode == 401)
        {
            // One controlled silent renewal attempt, per policy; never a blind loop.
            token = await _authenticationService.AcquireGraphAccessTokenAsync(cancellationToken)
                .ConfigureAwait(false);
            outcome = await SendOnceAsync(requestUri, token, clientRequestId, cancellationToken)
                .ConfigureAwait(false);
        }

        return outcome;
    }

    private async Task<SendOutcome> SendOnceAsync(
        Uri requestUri,
        string accessToken,
        string clientRequestId,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("graph");

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("client-request-id", clientRequestId);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            return SendOutcome.Failure(new GraphRequestException(
                null, null, isTransient: true, retryAfter: null, errorHintForClassification: null, exception));
        }

        using (response)
        {
            var statusCode = (int)response.StatusCode;
            var msRequestId = TryGetHeader(response, "request-id");
            var responseDate = response.Headers.Date?.ToString("O");

            if (response.IsSuccessStatusCode)
            {
                var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
                return body is null
                    ? SendOutcome.Failure(new GraphRequestException(
                        statusCode, null, isTransient: false, retryAfter: null, errorHintForClassification: null))
                    : SendOutcome.Success(statusCode, body, msRequestId, responseDate);
            }

            var (graphErrorCode, errorHint) = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
            var retryAfter = ReadRetryAfter(response);
            var isTransient = TransientStatuses.Contains(statusCode);
            // A 410 delta reset carries an opaque Location that starts a fresh
            // enumeration; capture it for the delta client without logging it.
            var resetLocation = statusCode == 410 ? response.Headers.Location : null;

            return SendOutcome.Failure(new GraphRequestException(
                statusCode, graphErrorCode, isTransient, retryAfter, errorHint, resetLocation: resetLocation),
                msRequestId, responseDate);
        }
    }

    private static async Task<JsonDocument?> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<(string? Code, string? Hint)> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (document.RootElement.TryGetProperty("error", out var error))
            {
                var code = error.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;
                var hint = error.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null;
                return (code, hint);
            }
        }
        catch (JsonException)
        {
            // Error body was not usable; classification continues with the status code.
        }

        return (null, null);
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

    private static string? TryGetHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    private sealed record SendOutcome(
        int StatusCode,
        JsonDocument? Body,
        string? MicrosoftRequestId,
        string? ResponseDateUtc,
        string? GraphErrorCode,
        GraphRequestException? Exception)
    {
        public static SendOutcome Success(int statusCode, JsonDocument body, string? msRequestId, string? responseDate) =>
            new(statusCode, body, msRequestId, responseDate, null, null);

        public static SendOutcome Failure(GraphRequestException exception, string? msRequestId = null, string? responseDate = null) =>
            new(exception.StatusCode ?? 0, null, msRequestId, responseDate, exception.GraphErrorCode, exception);
    }
}
