namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// The exact delegated scope set approved by docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md.
/// No write permission and no application permission may ever be added here.
/// </summary>
public static class ApprovedScopes
{
    public static readonly IReadOnlyCollection<string> Delegated =
    [
        "User.Read",
        "Files.Read.All",
        "Sites.Read.All",
        "offline_access",
        "openid",
        "profile"
    ];

    /// <summary>
    /// The minimum scope required for operator validation in M2.
    /// </summary>
    public const string OperatorRead = "User.Read";
}
