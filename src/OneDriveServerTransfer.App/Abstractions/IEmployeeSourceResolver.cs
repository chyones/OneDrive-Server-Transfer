namespace OneDriveServerTransfer.Abstractions;

public enum EmployeeSourceMode
{
    Upn,
    OneDriveRootUrl
}

/// <summary>
/// Durable, validated identity of one employee's business OneDrive source. Tenant ID,
/// employee object ID, and drive ID are the durable binding identity; UPN and display
/// names are display and audit data only. <see cref="UserPrincipalName" /> is populated
/// for UPN-mode input; URL mode cannot reliably obtain a UPN from the approved
/// endpoints, so it remains null there.
/// </summary>
public sealed record ResolvedEmployeeSource(
    string TenantId,
    string UserObjectId,
    string? UserPrincipalName,
    string DisplayName,
    string DriveId,
    string DriveType,
    string? DriveOwnerDisplayName,
    string DriveWebUrl,
    long? QuotaTotalBytes,
    long? QuotaUsedBytes,
    long? QuotaRemainingBytes,
    EmployeeSourceMode Mode,
    bool IsTenantConfirmed);

/// <summary>
/// Resolves one employee's business OneDrive root from an employee UPN or a OneDrive
/// for Business root URL using only the approved Microsoft Graph v1.0 endpoints
/// (GRAPH-SRC-001, GRAPH-SRC-002, GRAPH-SRC-003). Implemented in M3. Scan, transfer,
/// and destination behavior do not exist here.
/// </summary>
public interface IEmployeeSourceResolver
{
    Task<ResolvedEmployeeSource> ResolveAsync(string input, CancellationToken cancellationToken);
}
