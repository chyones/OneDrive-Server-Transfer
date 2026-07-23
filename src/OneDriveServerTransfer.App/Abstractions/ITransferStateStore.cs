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

    /// <summary>Creates a new copy run in the in-progress state (null final state).</summary>
    Task BeginRunAsync(TransferRunRecord run, CancellationToken cancellationToken);

    /// <summary>One run by its stable run identifier, or null.</summary>
    Task<TransferRunRecord?> GetRunAsync(string runId, CancellationToken cancellationToken);

    /// <summary>One scan by its stable scan identifier, or null.</summary>
    Task<ScanRecord?> GetScanAsync(string scanId, CancellationToken cancellationToken);

    /// <summary>
    /// All items of the bound drive in stable insertion order. Report generation is the
    /// intended caller: it materializes the hierarchy once per run to build the audit
    /// files. Scheduling paths must keep using the bounded batch accessors instead.
    /// </summary>
    Task<IReadOnlyList<TransferItemRecord>> GetAllItemsAsync(CancellationToken cancellationToken);

    /// <summary>The most recently started run for the bound drive, or null.</summary>
    Task<TransferRunRecord?> GetLatestRunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks every run left without an orderly terminal transition as
    /// <see cref="TransferRunState.Interrupted" />. Idempotent; returns the number of
    /// runs transitioned. Runs on open so a crashed run stays eligible for validated
    /// resume and is never mistaken for a live one.
    /// </summary>
    Task<int> MarkInProgressRunsInterruptedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persists the terminal state of one run with its end timestamp. Terminal states
    /// only; a second terminal transition for the same run is rejected so run history
    /// is never rewritten.
    /// </summary>
    Task CompleteRunAsync(string runId, TransferRunState finalState, CancellationToken cancellationToken);

    /// <summary>
    /// Up to <paramref name="maxCount" /> supported file items in the schedulable
    /// <see cref="TransferItemState.Mapped" /> state, in stable insertion order. The
    /// bounded batch feeds the download queue so the complete hierarchy is never
    /// materialized for scheduling.
    /// </summary>
    Task<IReadOnlyList<TransferItemRecord>> GetSchedulableFileItemsAsync(
        int maxCount,
        CancellationToken cancellationToken);

    /// <summary>
    /// Increments the persisted per-file attempt budget and returns the new value, so a
    /// restart cannot create an unbounded hidden retry loop.
    /// </summary>
    Task<int> IncrementAttemptCountAsync(string sourceItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Transactionally records the local SHA-256 and the
    /// <see cref="TransferItemState.Verified" /> state for one item whose content has
    /// been downloaded and verified but whose final path is not yet committed.
    /// </summary>
    Task MarkItemVerifiedAsync(
        string sourceItemId,
        string localSha256,
        CancellationToken cancellationToken);

    /// <summary>
    /// Transactionally records the <see cref="TransferItemState.Completed" /> state and
    /// the timestamp-preservation result for one item whose final path is committed.
    /// </summary>
    Task MarkItemCompletedAsync(
        string sourceItemId,
        TimestampPreservationResult timestampResult,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns one item to the schedulable state for a content re-copy after the source
    /// version changed: clears the local hash and timestamp result and restarts the
    /// per-file attempt budget for the new source version.
    /// </summary>
    Task ResetItemForRecopyAsync(string sourceItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks every item still in a non-terminal pre-completion state
    /// (<see cref="TransferItemState.Discovered" />, <see cref="TransferItemState.Mapped" />,
    /// <see cref="TransferItemState.Downloading" />, <see cref="TransferItemState.Verified" />)
    /// as <see cref="TransferItemState.Cancelled" />. Completed, skipped, unsupported,
    /// and failed items keep their recorded outcome. Returns the number transitioned.
    /// </summary>
    Task<int> CancelPendingItemsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies one reconciliation or fresh re-enumeration delta page transactionally:
    /// upserts the items by Drive Item ID and persists the opaque paging link with the
    /// given checkpoint state in the same transaction. The run ID is recorded as the
    /// page marker so a fresh re-enumeration can identify items the source no longer
    /// returns.
    /// </summary>
    Task ApplyRunDeltaPageAsync(
        string runId,
        IReadOnlyList<TransferItemRecord> items,
        string pagingLink,
        DeltaCheckpointState checkpointState,
        CancellationToken cancellationToken);

    /// <summary>
    /// After a completed fresh re-enumeration, marks every non-tombstone item the new
    /// inventory did not touch (page marker differs from <paramref name="runId" />) as
    /// a deleted-source record. Local archive content is never deleted; verified items
    /// keep their completed state so retained content stays distinguishable.
    /// Returns the number of items marked.
    /// </summary>
    Task<int> MarkUntouchedItemsDeletedAsync(string runId, CancellationToken cancellationToken);

    /// <summary>
    /// All items whose mapped relative path sits under the given mapped directory
    /// prefix (the folder's own mapped path). Used to update descendant mappings
    /// transactionally when a verified folder is relocated after a rename or move.
    /// </summary>
    Task<IReadOnlyList<TransferItemRecord>> GetItemsUnderMappedPathAsync(
        string mappedPathPrefix,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sum of the expected sizes of supported file items still needing transfer
    /// (Discovered, Mapped, or Downloading). Bounded SQL aggregation for the
    /// pre-scheduling capacity evaluation.
    /// </summary>
    Task<long> GetRemainingFileBytesAsync(CancellationToken cancellationToken);
}
