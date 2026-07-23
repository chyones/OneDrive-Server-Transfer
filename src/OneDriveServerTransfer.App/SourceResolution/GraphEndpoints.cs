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

    /// <summary>
    /// GRAPH-SCAN-001: initial drive delta inventory. The $select is limited to the
    /// contract-required identity, hierarchy, facet, size, time, tag, and hash fields
    /// (docs/GRAPH_DELTA_AND_RECONCILIATION_POLICY.md). Returned paging links are
    /// followed as opaque values (GRAPH-DELTA-001/002/003) and are never constructed
    /// here.
    /// </summary>
    public const string DriveRootDeltaTemplate =
        V1Base + "/drives/{0}/root/delta?$select=id,name,parentReference,size,createdDateTime,lastModifiedDateTime,eTag,cTag,file,folder,package,deleted,remoteItem";

    /// <summary>
    /// GRAPH-ITEM-001: re-read one item's metadata to revalidate source identity and
    /// stability before accepting a completed file. Same contract-required field set
    /// as the delta inventory.
    /// </summary>
    public const string DriveItemTemplate =
        V1Base + "/drives/{0}/items/{1}?$select=id,name,parentReference,size,createdDateTime,lastModifiedDateTime,eTag,cTag,file,folder,package,deleted,remoteItem";

    /// <summary>
    /// GRAPH-DL-001: obtain a fresh short-lived preauthenticated download URL through a
    /// metadata request selecting @microsoft.graph.downloadUrl. The returned URL is
    /// used immediately and is never logged, persisted, or placed in state.
    /// </summary>
    public const string DriveItemDownloadUrlTemplate =
        V1Base + "/drives/{0}/items/{1}?$select=id,@microsoft.graph.downloadUrl";
}
