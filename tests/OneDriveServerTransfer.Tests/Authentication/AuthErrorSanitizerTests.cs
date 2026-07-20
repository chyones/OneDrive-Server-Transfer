using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Tests.Authentication;

public class AuthErrorSanitizerTests
{
    [Fact]
    public void JwtTokensAreRedacted()
    {
        var text = "failed with eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.s3cr3tSignature in response";

        var result = AuthErrorSanitizer.RedactSensitiveText(text);

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", result, StringComparison.Ordinal);
        Assert.Contains("[redacted]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BearerHeadersAreRedacted()
    {
        var text = "Authorization: Bearer abcDEF123._-~+/=";

        var result = AuthErrorSanitizer.RedactSensitiveText(text);

        Assert.DoesNotContain("abcDEF123", result, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryStringsAreRedacted()
    {
        var text = "GET https://download.example.test/file?tempauth=secret-value&other=1";

        var result = AuthErrorSanitizer.RedactSensitiveText(text);

        Assert.DoesNotContain("tempauth=secret-value", result, StringComparison.Ordinal);
        Assert.Contains("?[redacted-query]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonTokenFieldsAreRedacted()
    {
        var text = """{"access_token":"hidden-value","name":"visible"}""";

        var result = AuthErrorSanitizer.RedactSensitiveText(text);

        Assert.DoesNotContain("hidden-value", result, StringComparison.Ordinal);
        Assert.Contains("visible", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SecretAssignmentsAreRedacted()
    {
        var text = "access_token=hidden-value; expires=3600";

        var result = AuthErrorSanitizer.RedactSensitiveText(text);

        Assert.DoesNotContain("hidden-value", result, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryTextPassesThrough()
    {
        var result = AuthErrorSanitizer.RedactSensitiveText("Sign-in failed; reference AUTH-CANCELLED-001.");

        Assert.Equal("Sign-in failed; reference AUTH-CANCELLED-001.", result);
    }

    [Fact]
    public void NullAndEmptyProduceEmpty()
    {
        Assert.Equal(string.Empty, AuthErrorSanitizer.RedactSensitiveText(null));
        Assert.Equal(string.Empty, AuthErrorSanitizer.RedactSensitiveText(string.Empty));
    }

    [Fact]
    public void LogSummaryContainsOnlyApprovedFields()
    {
        var error = new SanitizedAuthError(AuthFailureKind.SessionExpired, "invalid_grant", null, "corr-1", 400);

        var summary = AuthErrorSanitizer.BuildLogSummary("AUTH-REAUTH-001", error);

        Assert.Contains("AUTH-REAUTH-001", summary, StringComparison.Ordinal);
        Assert.Contains("invalid_grant", summary, StringComparison.Ordinal);
        Assert.Contains("corr-1", summary, StringComparison.Ordinal);
        Assert.Contains("400", summary, StringComparison.Ordinal);
    }
}
