namespace OneDriveServerTransfer.State;

/// <summary>
/// Facet classification of one inventoried source item (contract section 10). Package
/// items, external shortcuts, and unknown facets are unsupported content; deleted
/// tombstones are recorded without any local deletion semantics.
/// </summary>
public enum ItemFacetClassification
{
    File,
    Folder,
    EmptyFolder,
    UnsupportedPackage,
    DeletedSource,
    ExternalShortcut,
    Unknown
}

/// <summary>Result of preserving source timestamps on the local item.</summary>
public enum TimestampPreservationResult
{
    NotAttempted,
    Preserved,
    Failed,
    UnsupportedValue
}

/// <summary>
/// Approved delta checkpoint states (docs/GRAPH_DELTA_AND_RECONCILIATION_POLICY.md).
/// Transitions are persisted so a restart always knows how the checkpoint may be used.
/// </summary>
public enum DeltaCheckpointState
{
    NoCheckpoint,
    InitialEnumerationInProgress,
    InitialEnumerationComplete,
    DeltaCheckpointValid,
    DeltaCheckpointResetRequired,
    FullReenumerationInProgress,
    ReconciliationInProgress,
    SourceStable,
    SourceUnstable,
    DeltaFailed
}

/// <summary>Lifecycle of one dry-run scan identity.</summary>
public enum ScanState
{
    InProgress,
    Succeeded,

    /// <summary>A stale in-progress scan after a crash; never enables copying.</summary>
    Interrupted
}

/// <summary>
/// One inventoried source item row. Source-hash information holds only supported
/// Microsoft hashes (sha1Hash or quickXorHash, never the Graph sha256Hash); the local
/// SHA-256 stays null until content verification runs. Deleted-source tombstones keep
/// no source or mapped path and never trigger local deletion.
/// </summary>
public sealed record TransferItemRecord(
    string DriveId,
    string SourceItemId,
    string? ParentItemId,
    string ItemName,
    string? SourcePath,
    string? MappedRelativePath,
    ItemFacetClassification Classification,
    string? ETag,
    string? CTag,
    long? SizeBytes,
    DateTimeOffset? CreatedUtc,
    DateTimeOffset? LastModifiedUtc,
    string? SourceHashAlgorithm,
    string? SourceHashValue,
    string? LocalSha256,
    TransferItemState TransferState,
    int AttemptCount,
    TimestampPreservationResult TimestampPreservation,
    string? ScanId,
    DateTimeOffset UpdatedUtc);

/// <summary>The persisted opaque delta checkpoint for the bound source drive.</summary>
public sealed record DeltaCheckpointRecord(
    string DriveId,
    string Checkpoint,
    DeltaCheckpointState State,
    DateTimeOffset UpdatedUtc);

/// <summary>
/// One copy run (contract section 10). A null <see cref="FinalState" /> means the run
/// is <see cref="TransferRunState.InProgress" />; terminal states are stored as the
/// exact approved enum names. A run left without an orderly terminal transition is
/// marked <see cref="TransferRunState.Interrupted" /> on the next open.
/// </summary>
public sealed record TransferRunRecord(
    string RunId,
    string DriveId,
    string? ScanId,
    DateTimeOffset StartedUtc,
    DateTimeOffset? EndedUtc,
    TransferRunState? FinalState)
{
    /// <summary>True while the run has no persisted terminal state.</summary>
    public bool IsInProgress => FinalState is null;
}

/// <summary>One dry-run scan identity and its persisted summary.</summary>
public sealed record ScanRecord(
    string ScanId,
    string TenantId,
    string EmployeeObjectId,
    string DriveId,
    string DestinationRoot,
    ScanState State,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc,
    long FileCount,
    long FolderCount,
    long EmptyFolderCount,
    long UnsupportedCount,
    long KnownBytes);
