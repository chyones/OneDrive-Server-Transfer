using Microsoft.Identity.Client;
using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Tests.Authentication;

public class MsalErrorClassifierTests
{
    [Fact]
    public void AuthenticationCanceledIsClassifiedAsCancelled()
    {
        var exception = new MsalClientException(MsalError.AuthenticationCanceledError, "hidden message");

        var error = MsalErrorClassifier.Classify(exception);

        Assert.Equal(AuthFailureKind.Cancelled, error.Kind);
        Assert.Equal(MsalError.AuthenticationCanceledError, error.ErrorCode);
    }

    [Fact]
    public void SilentInvalidGrantIsClassifiedAsSessionExpired()
    {
        var exception = new MsalUiRequiredException("invalid_grant", "hidden session details");

        var error = MsalErrorClassifier.Classify(exception);

        Assert.Equal(AuthFailureKind.SessionExpired, error.Kind);
    }

    [Fact]
    public void ConsentClassificationIsClassifiedAsConsentRequired()
    {
        var exception = new MsalUiRequiredException(
            "invalid_grant",
            "hidden details",
            null,
            UiRequiredExceptionClassification.ConsentRequired);

        var error = MsalErrorClassifier.Classify(exception);

        Assert.Equal(AuthFailureKind.ConsentRequired, error.Kind);
    }

    [Fact]
    public void ConsentErrorCodeIsClassifiedAsConsentRequired()
    {
        var exception = new MsalServiceException("consent_required", "hidden details");

        var error = MsalErrorClassifier.Classify(exception);

        Assert.Equal(AuthFailureKind.ConsentRequired, error.Kind);
    }

    [Fact]
    public void ConsentServiceCodeIsClassifiedAsConsentRequired()
    {
        var exception = new MsalServiceException("invalid_grant", "AADSTS65001: hidden details");

        var error = MsalErrorClassifier.Classify(exception);

        Assert.Equal(AuthFailureKind.ConsentRequired, error.Kind);
    }

    [Fact]
    public void BrokerErrorIsClassifiedAsBrokerUnavailable()
    {
        var exception = new MsalClientException("broker_unavailable", "hidden details");

        var error = MsalErrorClassifier.Classify(exception);

        Assert.Equal(AuthFailureKind.BrokerUnavailable, error.Kind);
        Assert.True(MsalErrorClassifier.IsBrokerUnavailable(error));
    }

    [Fact]
    public void NetworkFailureIsClassifiedAsServiceUnavailable()
    {
        var error = MsalErrorClassifier.Classify(new HttpRequestException("hidden"));

        Assert.Equal(AuthFailureKind.ServiceUnavailable, error.Kind);
    }

    [Fact]
    public void UnknownExceptionIsClassifiedAsUnknown()
    {
        var error = MsalErrorClassifier.Classify(new InvalidOperationException("hidden"));

        Assert.Equal(AuthFailureKind.Unknown, error.Kind);
    }

    [Fact]
    public void ClassificationDoesNotCarryMessageText()
    {
        var exception = new MsalServiceException("invalid_grant", "sensitive protocol detail", 400);

        var error = MsalErrorClassifier.Classify(exception);

        // The sanitized record carries only code, sub-error, correlation, and status.
        Assert.Equal("invalid_grant", error.ErrorCode);
        Assert.Equal(400, error.HttpStatusCode);
        Assert.DoesNotContain("sensitive protocol detail", error.ToString(), StringComparison.Ordinal);
    }
}
