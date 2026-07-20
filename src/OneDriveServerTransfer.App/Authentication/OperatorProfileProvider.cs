using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OneDriveServerTransfer.Authentication;

/// <summary>The validated signed-in operator profile from Microsoft Graph /me.</summary>
public sealed record OperatorProfile(string ObjectId, string UserPrincipalName, string DisplayName);

public enum OperatorProfileFailure
{
    None,
    Unauthorized,
    Forbidden,
    InvalidResponse,
    Unavailable
}

/// <summary>Carries only the failure classification and HTTP status; never the body.</summary>
public sealed class OperatorProfileException : Exception
{
    public OperatorProfileException(
        OperatorProfileFailure failure,
        int? httpStatusCode,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
        HttpStatusCode = httpStatusCode;
    }

    public OperatorProfileFailure Failure { get; }

    public int? HttpStatusCode { get; }
}

/// <summary>
/// Reads the signed-in operator through the approved endpoint GRAPH-AUTH-001
/// (GET /me with a $select of id, userPrincipalName, displayName). This is the only
/// Microsoft Graph endpoint called in M2; no employee or drive resolution exists.
/// </summary>
public interface IOperatorProfileProvider
{
    Task<OperatorProfile> GetCurrentOperatorProfileAsync(string accessToken, CancellationToken cancellationToken);
}

public sealed class OperatorProfileProvider : IOperatorProfileProvider
{
    public const string MeEndpoint =
        "https://graph.microsoft.com/v1.0/me?$select=id,userPrincipalName,displayName";

    private readonly HttpClient _httpClient;

    public OperatorProfileProvider(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<OperatorProfile> GetCurrentOperatorProfileAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, MeEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("client-request-id", Guid.NewGuid().ToString());

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new OperatorProfileException(
                OperatorProfileFailure.Unavailable,
                null,
                "The operator profile endpoint could not be reached.",
                exception);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new OperatorProfileException(
                    OperatorProfileFailure.Unauthorized, 401, "The operator profile request was rejected as unauthorized.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new OperatorProfileException(
                    OperatorProfileFailure.Forbidden, 403, "The operator profile request was denied.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new OperatorProfileException(
                    OperatorProfileFailure.Unavailable,
                    (int)response.StatusCode,
                    "The operator profile endpoint returned an unexpected status.");
            }

            return await ReadProfileAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<OperatorProfile> ReadProfileAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var root = document.RootElement;
            var id = GetString(root, "id");
            var upn = GetString(root, "userPrincipalName");
            var displayName = GetString(root, "displayName");

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(upn) || displayName is null)
            {
                throw InvalidResponse();
            }

            return new OperatorProfile(id, upn, displayName);
        }
        catch (JsonException)
        {
            throw InvalidResponse();
        }
    }

    private static string? GetString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static OperatorProfileException InvalidResponse() => new(
        OperatorProfileFailure.InvalidResponse, null, "The operator profile response was not usable.");
}
