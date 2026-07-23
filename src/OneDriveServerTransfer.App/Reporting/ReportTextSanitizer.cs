using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Reporting;

/// <summary>
/// Sanitizes free text that reaches the audit reports (docs/REPORT_SCHEMA.md, "Error
/// redaction"). Builds on the authentication sanitizer so tokens, authorization
/// headers, JSON credential fields, temporary download URL query strings, and secret
/// assignments are redacted, and additionally collapses the text to a single line so
/// multi-line payloads such as raw response bodies or stack traces can never span
/// report cells.
///
/// Source item names and paths are employee-controlled audit data and are never
/// redacted here; only error text passes through this sanitizer.
/// </summary>
internal static class ReportTextSanitizer
{
    public static string SanitizeErrorMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var redacted = AuthErrorSanitizer.RedactSensitiveText(text);
        var firstLineEnd = redacted.IndexOfAny(['\r', '\n']);
        if (firstLineEnd >= 0)
        {
            redacted = redacted[..firstLineEnd];
        }

        return redacted.Trim();
    }
}
