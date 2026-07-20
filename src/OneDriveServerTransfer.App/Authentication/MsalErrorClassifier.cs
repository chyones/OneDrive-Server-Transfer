using System.Net.Http;
using Microsoft.Identity.Client;

namespace OneDriveServerTransfer.Authentication;

public enum AuthFailureKind
{
    None,
    Cancelled,
    ConsentRequired,
    BrokerUnavailable,
    InteractionRequired,
    SessionExpired,
    ServiceUnavailable,
    Unknown
}

/// <summary>
/// Redacted, log-safe view of an MSAL failure. Contains only approved audit fields:
/// the MSAL error code, optional sub-error, correlation ID, HTTP status, and the
/// classified failure kind. Never contains message text, tokens, or claims.
/// </summary>
public sealed record SanitizedAuthError(
    AuthFailureKind Kind,
    string? ErrorCode,
    string? SubError,
    string? CorrelationId,
    int? HttpStatusCode);

/// <summary>
/// Classifies MSAL exceptions into stable failure kinds without exposing message
/// content. Message text is inspected only to classify, never logged or displayed.
/// </summary>
public static class MsalErrorClassifier
{
    public static SanitizedAuthError Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        switch (exception)
        {
            case MsalUiRequiredException uiRequired:
                return new SanitizedAuthError(
                    IsConsentRequired(uiRequired)
                        ? AuthFailureKind.ConsentRequired
                        : AuthFailureKind.SessionExpired,
                    uiRequired.ErrorCode,
                    TryGetSubError(uiRequired),
                    TryGetCorrelationId(uiRequired),
                    TryGetStatusCode(uiRequired));

            case MsalServiceException serviceException:
                return new SanitizedAuthError(
                    ClassifyServiceError(serviceException),
                    serviceException.ErrorCode,
                    TryGetSubError(serviceException),
                    TryGetCorrelationId(serviceException),
                    TryGetStatusCode(serviceException));

            case MsalClientException clientException:
                return new SanitizedAuthError(
                    ClassifyClientError(clientException),
                    clientException.ErrorCode,
                    null,
                    null,
                    null);

            case HttpRequestException:
                return new SanitizedAuthError(AuthFailureKind.ServiceUnavailable, null, null, null, null);

            default:
                return new SanitizedAuthError(AuthFailureKind.Unknown, null, null, null, null);
        }
    }

    public static bool IsBrokerUnavailable(SanitizedAuthError error) =>
        error.Kind == AuthFailureKind.BrokerUnavailable;

    private static AuthFailureKind ClassifyServiceError(MsalServiceException exception)
    {
        if (IsConsent(exception.ErrorCode, exception.Message))
        {
            return AuthFailureKind.ConsentRequired;
        }

        if (string.Equals(exception.ErrorCode, "invalid_grant", StringComparison.OrdinalIgnoreCase))
        {
            return AuthFailureKind.SessionExpired;
        }

        if (string.Equals(exception.ErrorCode, "interaction_required", StringComparison.OrdinalIgnoreCase))
        {
            return AuthFailureKind.InteractionRequired;
        }

        return AuthFailureKind.Unknown;
    }

    private static AuthFailureKind ClassifyClientError(MsalClientException exception)
    {
        if (string.Equals(exception.ErrorCode, MsalError.AuthenticationCanceledError, StringComparison.OrdinalIgnoreCase))
        {
            return AuthFailureKind.Cancelled;
        }

        if (exception.ErrorCode.Contains("broker", StringComparison.OrdinalIgnoreCase))
        {
            return AuthFailureKind.BrokerUnavailable;
        }

        return AuthFailureKind.Unknown;
    }

    private static bool IsConsentRequired(MsalUiRequiredException exception) =>
        exception.Classification == UiRequiredExceptionClassification.ConsentRequired ||
        IsConsent(exception.ErrorCode, exception.Message);

    private static bool IsConsent(string? errorCode, string? message)
    {
        if (string.Equals(errorCode, "consent_required", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Consent-not-granted is signalled by the stable AADSTS65001 service code.
        return message?.Contains("AADSTS65001", StringComparison.Ordinal) == true;
    }

    private static string? TryGetSubError(MsalServiceException exception)
    {
        try
        {
            return exception.SubErrorForLogging;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryGetCorrelationId(MsalServiceException exception)
    {
        try
        {
            return exception.CorrelationId;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int? TryGetStatusCode(MsalServiceException exception)
    {
        try
        {
            return exception.StatusCode;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
