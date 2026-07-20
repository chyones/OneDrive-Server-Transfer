namespace OneDriveServerTransfer.SourceResolution;

/// <summary>
/// The complete Microsoft Graph endpoint inventory used by this build. Every entry is
/// an approved v1.0 endpoint from docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md. Do not add
/// an endpoint without updating the matrix and obtaining owner approval.
/// </summary>
public static class GraphEndpoints
{
    public const string V1Base = "https://graph.microsoft.com/v1.0";

    /// <summary>GRAPH-AUTH-001: signed-in operator profile.</summary>
    public const string Me = V1Base + "/me?$select=id,userPrincipalName,displayName";

    /// <summary>GRAPH-SRC-001: employee default drive from UPN.</summary>
    public const string UserDriveTemplate =
        V1Base + "/users/{0}/drive?$select=id,driveType,webUrl,owner,quota";

    /// <summary>GRAPH-SRC-002: personal site from OneDrive root URL.</summary>
    public const string SiteByPathTemplate =
        V1Base + "/sites/{0}:/{1}?$select=id,webUrl,isPersonalSite,siteCollection";

    /// <summary>GRAPH-SRC-003: default drive from validated personal site.</summary>
    public const string SiteDriveTemplate =
        V1Base + "/sites/{0}/drive?$select=id,driveType,webUrl,owner,quota";
}
