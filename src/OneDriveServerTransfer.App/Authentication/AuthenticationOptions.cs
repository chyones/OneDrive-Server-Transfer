namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// Authentication deployment configuration. Real tenant values are supplied through the
/// local, non-committed appsettings.json; the committed example file carries
/// placeholders only.
/// </summary>
public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    /// <summary>The configured Microsoft Entra tenant (directory) ID as a GUID.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>The single-tenant public-client application (client) ID as a GUID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Optional allowlist of authorized transfer-account Microsoft Entra object IDs.
    /// When at least one entry is configured, every other account is rejected.
    /// </summary>
    public string[] AuthorizedOperatorObjectIds { get; set; } = [];

    /// <summary>Default state of the remember-sign-in checkbox.</summary>
    public bool RememberSignInDefault { get; set; } = true;

    /// <summary>
    /// Public-client redirect URI for the system-browser fallback. The approved value is
    /// http://localhost unless a separately approved change is recorded.
    /// </summary>
    public string RedirectUri { get; set; } = "http://localhost";
}
