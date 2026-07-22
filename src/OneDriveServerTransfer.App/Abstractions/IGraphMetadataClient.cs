namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Read-only Microsoft Graph v1.0 metadata access for the drive delta inventory, item
/// metadata re-read, and fresh temporary download URL acquisition. Implemented in
/// milestones M3 and M5. Only endpoints and delegated scopes listed in
/// docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md may be used; the beta endpoint and every
/// write permission are prohibited. Next and delta links are treated as opaque.
/// Temporary download URLs are used immediately and are never logged or persisted;
/// content bytes never flow through this client.
/// </summary>
public interface IGraphMetadataClient
{
    IAsyncEnumerable<DriveItemMetadata> EnumerateDriveDeltaAsync(
        string driveId,
        string? deltaCheckpoint,
        CancellationToken cancellationToken);

    /// <summary>
    /// Re-reads one item's metadata (GRAPH-ITEM-001) so the verification pipeline can
    /// confirm source identity and relevant metadata stability after content transfer.
    /// </summary>
    Task<DriveItemMetadata> GetItemMetadataAsync(
        string driveId,
        string itemId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtains a fresh short-lived preauthenticated download URL (GRAPH-DL-001,
    /// metadata request selecting @microsoft.graph.downloadUrl). The returned URI is a
    /// preauthenticated secret: use it immediately and never log or persist it.
    /// </summary>
    Task<Uri> GetTemporaryDownloadUrlAsync(
        string driveId,
        string itemId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Source metadata for one drive item. Graph package items (for example OneNote
/// notebooks) are surfaced with <see cref="IsPackage" /> so they can be classified as
/// unsupported; they are never copied in version 1. Source-hash information carries
/// only supported Microsoft hashes (quickXorHash preferred, then sha1Hash); the Graph
/// sha256Hash value is never used (D-038).
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
