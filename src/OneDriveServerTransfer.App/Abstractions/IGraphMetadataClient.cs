namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Read-only Microsoft Graph v1.0 metadata access for the drive delta inventory and item
/// metadata. Implemented in milestones M3 and M5. Only endpoints and delegated scopes
/// listed in docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md may be used; the beta endpoint and
/// every write permission are prohibited. Next and delta links are treated as opaque.
/// Temporary download URLs never flow through this client.
/// </summary>
public interface IGraphMetadataClient
{
    IAsyncEnumerable<DriveItemMetadata> EnumerateDriveDeltaAsync(
        string driveId,
        string? deltaCheckpoint,
        CancellationToken cancellationToken);

    Task<DriveItemMetadata> GetItemMetadataAsync(
        string driveId,
        string itemId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Source metadata for one drive item. Graph package items (for example OneNote
/// notebooks) are surfaced with <see cref="IsPackage" /> so they can be classified as
/// unsupported; they are never copied in version 1.
/// </summary>
public sealed record DriveItemMetadata(
    string ItemId,
    string? ParentItemId,
    string Name,
    long? SizeBytes,
    bool IsFolder,
    bool IsPackage,
    bool IsDeleted,
    string? ETag,
    string? CTag,
    DateTimeOffset? CreatedUtc,
    DateTimeOffset? LastModifiedUtc,
    string? SourceHashAlgorithm,
    string? SourceHashValue);
