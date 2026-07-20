namespace OneDriveServerTransfer.SourceResolution;

/// <summary>
/// Deployment configuration for employee source resolution. The tenant OneDrive host
/// is required for OneDrive-root URL mode and must use the tenant's real
/// *-my.sharepoint.com host; the committed example file carries a placeholder only.
/// </summary>
public sealed class SourceResolutionOptions
{
    public const string SectionName = "SourceResolution";

    /// <summary>
    /// The configured tenant OneDrive host, for example contoso-my.sharepoint.com.
    /// URL-mode inputs are accepted only on this exact host.
    /// </summary>
    public string TenantOneDriveHost { get; set; } = string.Empty;
}
