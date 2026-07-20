using System.Text.RegularExpressions;

namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// Redaction helpers for authentication-adjacent text. Structured MSAL error fields are
/// preferred over message text; these helpers are defense-in-depth for any free text
/// that reaches protected logs.
/// </summary>
public static partial class AuthErrorSanitizer
{
    private const string Redacted = "[redacted]";

    public static string RedactSensitiveText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = text;
        result = BearerTokenRegex().Replace(result, "Bearer " + Redacted);
        result = JsonTokenFieldRegex().Replace(result, match =>
            match.Value.Split(':', 2)[0] + ":" + Redacted);
        result = JwtRegex().Replace(result, Redacted);
        result = QueryStringRegex().Replace(result, "?[redacted-query]");
        result = SecretAssignmentRegex().Replace(result, match =>
            match.Value.Split('=', 2)[0] + "=" + Redacted);
        return result;
    }

    /// <summary>
    /// Builds a fixed-template log summary for a classified error. Contains only the
    /// reference code, MSAL error code, correlation ID, and HTTP status.
    /// </summary>
    public static string BuildLogSummary(string referenceCode, SanitizedAuthError error) =>
        $"Auth failure {referenceCode}; kind={error.Kind}; msalError={error.ErrorCode ?? "n/a"}; " +
        $"correlationId={error.CorrelationId ?? "n/a"}; httpStatus={error.HttpStatusCode?.ToString() ?? "n/a"}";

    // JSON Web Tokens always start with the base64url header "eyJ".
    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*", RegexOptions.Compiled)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.Compiled)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"""[^""]*(?:token|secret|password|assertion|credential)[^""]*""\s*:\s*""[^""]*""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex JsonTokenFieldRegex();

    [GeneratedRegex(@"\?[A-Za-z0-9_~%.-]+=[^&\s""']+(?:&[A-Za-z0-9_~%.-]+=[^&\s""']*)*", RegexOptions.Compiled)]
    private static partial Regex QueryStringRegex();

    [GeneratedRegex(@"(?i)\b(?:access_token|refresh_token|id_token|client_secret|password|passwd|pwd)\s*=\s*[^\s&;]+", RegexOptions.Compiled)]
    private static partial Regex SecretAssignmentRegex();
}
