namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// Stable reference codes for user-facing authentication errors. Codes are part of the
/// user-support contract and must never be renumbered.
/// </summary>
public static class AuthenticationErrorCodes
{
    public const string InvalidConfiguration = "AUTH-CONFIG-001";
    public const string Cancelled = "AUTH-CANCELLED-001";
    public const string ConsentRequired = "AUTH-CONSENT-001";
    public const string TenantMismatch = "AUTH-TENANT-001";
    public const string GuestAccountRejected = "AUTH-GUEST-001";
    public const string OperatorNotAuthorized = "AUTH-OPERATOR-001";
    public const string RequiredScopeMissing = "AUTH-SCOPE-001";
    public const string IdentityMismatch = "AUTH-IDENTITY-001";
    public const string CacheCorrupted = "AUTH-CACHE-001";
    public const string SessionUnauthorized = "AUTH-SESSION-401";
    public const string AccessForbidden = "AUTHZ-403";
    public const string ReauthenticationRequired = "AUTH-REAUTH-001";
    public const string SignInRequired = "AUTH-SIGNIN-001";
    public const string ServiceUnavailable = "AUTH-UNAVAILABLE-001";
    public const string Unexpected = "AUTH-UNEXPECTED-001";
}
