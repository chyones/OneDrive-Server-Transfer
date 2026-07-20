namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// Centralized builders for user-facing authentication errors. Every error has a short
/// title, a plain-language explanation, a corrective action, and a stable reference
/// code. Messages never contain secrets, tokens, identifiers, or protocol details.
/// </summary>
public static class AuthenticationErrors
{
    public static UserFacingAuthException InvalidConfiguration(Exception? inner = null) => new(
        AuthenticationErrorCodes.InvalidConfiguration,
        "Authentication is not configured",
        "The application authentication settings are missing or invalid.",
        "Ask your administrator to provide a valid local appsettings.json with the approved tenant and application values.",
        inner);

    public static UserFacingAuthException Cancelled(Exception? inner = null) => new(
        AuthenticationErrorCodes.Cancelled,
        "Sign-in was cancelled",
        "The Microsoft sign-in window was closed before sign-in completed.",
        "Choose Sign in again to restart the Microsoft sign-in flow.",
        inner);

    public static UserFacingAuthException ConsentRequired(Exception? inner = null) => new(
        AuthenticationErrorCodes.ConsentRequired,
        "Approval is required for the application",
        "The organization has not granted the application the approved read permissions, or a previously granted approval was revoked.",
        "Ask your administrator to grant the approved delegated read permissions, then sign in again.",
        inner);

    public static UserFacingAuthException TenantMismatch(Exception? inner = null) => new(
        AuthenticationErrorCodes.TenantMismatch,
        "Wrong organization account",
        "The signed-in account belongs to a different organization than the one this application is configured for.",
        "Sign in with a work account from the configured organization.",
        inner);

    public static UserFacingAuthException GuestAccountRejected(Exception? inner = null) => new(
        AuthenticationErrorCodes.GuestAccountRejected,
        "Guest accounts cannot be used",
        "The signed-in account is a guest or external account. Only member accounts of the configured organization may be used.",
        "Sign in with a member work account of the configured organization.",
        inner);

    public static UserFacingAuthException OperatorNotAuthorized(Exception? inner = null) => new(
        AuthenticationErrorCodes.OperatorNotAuthorized,
        "This account is not an authorized transfer operator",
        "The signed-in account is not on the approved transfer-operator list for this deployment.",
        "Sign in with an authorized IT transfer account, or ask your administrator to review the approved operator list.",
        inner);

    public static UserFacingAuthException RequiredScopeMissing(Exception? inner = null) => new(
        AuthenticationErrorCodes.RequiredScopeMissing,
        "Required read permission is missing",
        "The sign-in did not grant the approved read permission needed to validate the operator.",
        "Ask your administrator to review the approved permission consent, then sign in again.",
        inner);

    public static UserFacingAuthException IdentityMismatch(Exception? inner = null) => new(
        AuthenticationErrorCodes.IdentityMismatch,
        "Sign-in could not be verified",
        "The signed-in account identity could not be confirmed consistently.",
        "Sign in again. If the problem continues, contact your administrator.",
        inner);

    public static UserFacingAuthException CacheCorrupted(Exception? inner = null) => new(
        AuthenticationErrorCodes.CacheCorrupted,
        "Saved sign-in could not be read",
        "The application sign-in cache on this device is damaged and has been cleared. No sign-in data was read from it.",
        "Sign in again to create a fresh saved sign-in.",
        inner);

    public static UserFacingAuthException SessionUnauthorized(Exception? inner = null) => new(
        AuthenticationErrorCodes.SessionUnauthorized,
        "Your sign-in session is no longer valid",
        "The service rejected the saved sign-in session.",
        "Sign in again to continue.",
        inner);

    public static UserFacingAuthException AccessForbidden(Exception? inner = null) => new(
        AuthenticationErrorCodes.AccessForbidden,
        "Access was denied",
        "The signed-in account does not have access to the requested organization resource, or a policy blocked the request.",
        "Ask your administrator to verify the account permissions and policies, then try again.",
        inner);

    public static UserFacingAuthException ReauthenticationRequired(Exception? inner = null) => new(
        AuthenticationErrorCodes.ReauthenticationRequired,
        "Please sign in again",
        "Your saved sign-in session has expired or was revoked.",
        "Choose Sign in to renew your session.",
        inner);

    public static UserFacingAuthException SignInRequired(Exception? inner = null) => new(
        AuthenticationErrorCodes.SignInRequired,
        "Sign-in is required",
        "No authorized operator is signed in.",
        "Choose Sign in and complete the Microsoft sign-in flow.",
        inner);

    public static UserFacingAuthException ServiceUnavailable(Exception? inner = null) => new(
        AuthenticationErrorCodes.ServiceUnavailable,
        "The sign-in service is unavailable",
        "The Microsoft sign-in service could not be reached.",
        "Check the network connection and try again.",
        inner);

    public static UserFacingAuthException Unexpected(Exception? inner = null) => new(
        AuthenticationErrorCodes.Unexpected,
        "Sign-in failed",
        "An unexpected error occurred during sign-in.",
        "Try again. If the problem continues, contact your administrator and quote the reference code.",
        inner);

    /// <summary>
    /// Truthful sign-out wording. It must never claim that Windows, WAM, browsers, or
    /// every Microsoft session was signed out.
    /// </summary>
    public const string SignOutDescription =
        "Signed out of this application. The application sign-in cache on this device was removed. " +
        "Other Microsoft sessions in Windows or browsers are not affected.";
}
