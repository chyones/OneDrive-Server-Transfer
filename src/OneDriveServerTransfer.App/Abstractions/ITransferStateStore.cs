using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Application-owned SQLite operational state (decision D-016). Implemented in milestone
/// M5. SQLite is the operational authority for source binding, scan inventory, resume,
/// crash recovery, path mappings, and the delta checkpoint; CSV and JSON reports are
/// audit output only and are never used as the operational resume database. All writes
/// are transactional and durable, and every operation is idempotent so recovery after a
/// crash can safely re-run it. Tokens, temporary URLs, and employee content are never
/// stored.
/// </summary>
public interface ITransferStateStore
{
    /// <summary>
    /// Opens the state database of an already-bound destination: initializes the schema
    /// idempotently, runs the integrity check and schema/path-mapping version gate, and
    /// reads the bound source drive identity. Throws a reference-coded
    /// DestinationException when integrity or versions are unsupported.
    /// </summary>
    Task OpenAsync(string databasePath, CancellationToken cancellationToken);

    /// <summary>The bound source drive ID, available after <see cref="OpenAsync" />.</summary>
    string DriveId { get; }

    /// <summary>The persisted opaque delta checkpoint, or null when none exists.</summary>
    Task<string?> GetDeltaCheckpointAsync(CancellationToken cancellationToken);

    /// <summary>Persists the opaque delta checkpoint as the valid completed checkpoint.</summary>
    Task SaveDeltaCheckpointAsync(string deltaCheckpoint, CancellationToken cancellationToken);

    /// <summary>The checkpoint with its persisted delta state, or null when none exists.</summary>
    Task<DeltaCheckpointRecord?> GetDeltaCheckpointRecordAsync(CancellationToken cancellationToken);

    /// <summary>Persists the checkpoint and its delta state transactionally.</summary>
    Task SaveDeltaCheckpointRecordAsync(DeltaCheckpointRecord record, CancellationToken cancellationToken);

    /// <summary>Creates a new scan identity in the <see cref="ScanState.InProgress" /> state.</summary>
    Task BeginScanAsync(ScanRecord scan, CancellationToken cancellationToken);

    /// <summary>
    /// Marks every stale in-progress scan as <see cref="ScanState.Interrupted" />.
    /// Idempotent; returns the number of scans transitioned.
    /// </summary>
    Task<int> MarkInProgressScansInterruptedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies one non-final delta page transactionally: upserts the items by Drive Item
    /// ID (last occurrence wins) and persists the opaque next link as
    /// <see cref="DeltaCheckpointState.InitialEnumerationInProgress" /> in the same
    /// transaction, so a crash can never skip an applied page or advance past
    /// unapplied data.
    /// </summary>
    Task ApplyDeltaPageAsync(
        string scanId,
        IReadOnlyList<TransferItemRecord> items,
        string nextLink,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies the final delta page transactionally: upserts the items and persists the
    /// opaque delta link as <see cref="DeltaCheckpointState.InitialEnumerationComplete" />.
    /// </summary>
    Task ApplyFinalDeltaPageAsync(
        string scanId,
        IReadOnlyList<TransferItemRecord> items,
        string deltaLink,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finalizes a successful scan transactionally: reclassifies childless folders as
    /// empty folders, computes and stores the scan summary, marks the scan
    /// <see cref="ScanState.Succeeded" />, and advances the checkpoint to
    /// <see cref="DeltaCheckpointState.DeltaCheckpointValid" />. Returns the finalized
    /// scan record.
    /// </summary>
    Task<ScanRecord> CompleteScanAsync(string scanId, CancellationToken cancellationToken);

    /// <summary>The most recent successful scan for the bound drive, or null.</summary>
    Task<ScanRecord?> GetLatestSuccessfulScanAsync(CancellationToken cancellationToken);

    /// <summary>One item by source Drive Item ID, or null.</summary>
    Task<TransferItemRecord?> GetItemAsync(string sourceItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Non-deleted items whose source path is not yet resolved and whose parent path is
    /// already resolved (or which are the drive root). Used by the iterative scan
    /// mapping pass; each returned batch resolves at least one more hierarchy level.
    /// </summary>
    Task<IReadOnlyList<TransferItemRecord>> GetItemsAwaitingSourcePathAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Non-deleted items whose source path could never be resolved because their parent
    /// chain is broken. These must fail safely and can never be copied.
    /// </summary>
    Task<IReadOnlyList<TransferItemRecord>> GetUnresolvedItemsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records the deterministic source path, mapped relative path, and transfer state
    /// for one item. Idempotent for identical inputs.
    /// </summary>
    Task UpdateItemPathsAsync(
        string sourceItemId,
        string sourcePath,
        string? mappedRelativePath,
        TransferItemState transferState,
        CancellationToken cancellationToken);

    /// <summary>All items of the bound drive in one approved transfer state.</summary>
    Task<IReadOnlyList<TransferItemRecord>> GetItemsByStateAsync(
        TransferItemState state,
        CancellationToken cancellationToken);

    /// <summary>All items of the bound drive with one facet classification.</summary>
    Task<IReadOnlyList<TransferItemRecord>> GetItemsByClassificationAsync(
        ItemFacetClassification classification,
        CancellationToken cancellationToken);

    /// <summary>Sets the transfer state of one item. Idempotent for identical inputs.</summary>
    Task SetItemStateAsync(
        string sourceItemId,
        TransferItemState state,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resets in-flight (<see cref="TransferItemState.Downloading" />) items back to the
    /// schedulable <see cref="TransferItemState.Mapped" /> state after an interruption.
    /// Idempotent; returns the number of items reset.
    /// </summary>
    Task<int> ResetInFlightItemsAsync(CancellationToken cancellationToken);
}
