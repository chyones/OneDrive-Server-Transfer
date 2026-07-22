using System.IO;
using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Inventory;
using OneDriveServerTransfer.Scan;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Transfer;

/// <summary>
/// Copy-run orchestrator and run-state machine (contract sections 5-10). Requires a
/// current successful scan before scheduling; revalidates the signed-in operator,
/// source identity, destination binding and lock, scan currency, and storage capacity
/// before scheduling; schedules supported files through a bounded queue with a fixed
/// maximum of three simultaneous downloads; runs at most three bounded reconciliation
/// passes through the saved delta checkpoint; and persists the exact approved terminal
/// run state. Crash recovery is idempotent: stale runs become Interrupted, in-flight
/// items return to the schedulable state, and verified-but-uncommitted items are
/// revalidated against their recorded local SHA-256 before finalization.
/// </summary>
public interface ITransferOrchestrator
{
    Task<TransferRunResult> RunAsync(
        ResolvedEmployeeSource source,
        DestinationSession session,
        CancellationToken cancellationToken);
}

public sealed class TransferOrchestrator : ITransferOrchestrator
{
    /// <summary>Fixed maximum of simultaneous file downloads; never configurable.</summary>
    public const int MaxConcurrentDownloads = 3;

    /// <summary>Bounded reconciliation passes after the initial copy (contract section 7).</summary>
    public const int MaxReconciliationPasses = 3;

    private const int SchedulingBatchSize = 16;

    private readonly IAuthenticationService _authenticationService;
    private readonly IDestinationBindingService _bindingService;
    private readonly IScanService _scanService;
    private readonly ITransferStateStore _stateStore;
    private readonly IDeltaInventoryClient _deltaClient;
    private readonly ITransferEngine _engine;
    private readonly SqlitePathCollisionRegistry _collisionRegistry;
    private readonly IPathMapper _pathMapper;
    private readonly IDestinationPathGuard _pathGuard;
    private readonly IDestinationCapacityService _capacityService;
    private readonly IHashingService _hashingService;
    private readonly ILogger<TransferOrchestrator> _logger;

    private readonly HashSet<string> _recopyItemIds = new(StringComparer.Ordinal);

    public TransferOrchestrator(
        IAuthenticationService authenticationService,
        IDestinationBindingService bindingService,
        IScanService scanService,
        ITransferStateStore stateStore,
        IDeltaInventoryClient deltaClient,
        ITransferEngine engine,
        SqlitePathCollisionRegistry collisionRegistry,
        IPathMapper pathMapper,
        IDestinationPathGuard pathGuard,
        IDestinationCapacityService capacityService,
        IHashingService hashingService,
        ILogger<TransferOrchestrator> logger)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _bindingService = bindingService ?? throw new ArgumentNullException(nameof(bindingService));
        _scanService = scanService ?? throw new ArgumentNullException(nameof(scanService));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _deltaClient = deltaClient ?? throw new ArgumentNullException(nameof(deltaClient));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _collisionRegistry = collisionRegistry ?? throw new ArgumentNullException(nameof(collisionRegistry));
        _pathMapper = pathMapper ?? throw new ArgumentNullException(nameof(pathMapper));
        _pathGuard = pathGuard ?? throw new ArgumentNullException(nameof(pathGuard));
        _capacityService = capacityService ?? throw new ArgumentNullException(nameof(capacityService));
        _hashingService = hashingService ?? throw new ArgumentNullException(nameof(hashingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TransferRunResult> RunAsync(
        ResolvedEmployeeSource source,
        DestinationSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(session);
        _recopyItemIds.Clear();

        // Revalidate the signed-in operator before any state transition.
        var operatorIdentity = await _authenticationService
            .GetCurrentOperatorAsync(cancellationToken).ConfigureAwait(false);
        if (operatorIdentity is null)
        {
            throw TransferErrors.OperatorSessionRequired();
        }

        if (!string.Equals(operatorIdentity.TenantId, source.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw TransferErrors.OperatorTenantMismatch();
        }

        await _stateStore.OpenAsync(session.Destination.StateDatabasePath, cancellationToken)
            .ConfigureAwait(false);

        // Idempotent crash recovery: a previous in-progress run becomes Interrupted,
        // in-flight items return to the schedulable state, and verified items are
        // revalidated against their recorded local SHA-256 before reuse.
        await _stateStore.MarkInProgressRunsInterruptedAsync(cancellationToken).ConfigureAwait(false);
        await _stateStore.ResetInFlightItemsAsync(cancellationToken).ConfigureAwait(false);
        _collisionRegistry.Open(session.Destination.StateDatabasePath);
        await RecoverVerifiedItemsAsync(session, cancellationToken).ConfigureAwait(false);

        var latestScan = await _stateStore.GetLatestSuccessfulScanAsync(cancellationToken)
            .ConfigureAwait(false);
        var run = new TransferRunRecord(
            Guid.NewGuid().ToString("N"),
            _stateStore.DriveId,
            latestScan?.ScanId,
            DateTimeOffset.UtcNow,
            EndedUtc: null,
            FinalState: null);
        await _stateStore.BeginRunAsync(run, cancellationToken).ConfigureAwait(false);

        var warnings = new List<TransferWarning>();

        try
        {
            // Destination binding revalidation (records the operator audit entry);
            // the session holds the exclusive lock for the whole run.
            await _bindingService.BindOrValidateAsync(
                    session.Destination,
                    new SourceBindingIdentity(
                        source.TenantId, source.DriveId, source.UserObjectId, source.UserPrincipalName),
                    new Destination.OperatorIdentity(
                        operatorIdentity.EntraObjectId, operatorIdentity.UserPrincipalName),
                    cancellationToken)
                .ConfigureAwait(false);

            // The copy requires a current successful scan for this exact source and
            // destination; a missing or stale scan fails the run.
            if (!await _scanService.IsScanCurrentAsync(source, session, cancellationToken)
                    .ConfigureAwait(false))
            {
                throw await FailRunAsync(run.RunId, TransferErrors.ScanNotCurrent(), cancellationToken)
                    .ConfigureAwait(false);
            }

            // Storage capacity precheck over the known remaining bytes plus reserve.
            var remainingBytes = await _stateStore.GetRemainingFileBytesAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!_capacityService.CheckTotal(session.Destination.RootPath, remainingBytes).IsSufficient)
            {
                throw await FailRunAsync(run.RunId, TransferErrors.InsufficientStorage(), cancellationToken)
                    .ConfigureAwait(false);
            }

            await CreatePendingFoldersAsync(session, cancellationToken).ConfigureAwait(false);

            var diskStop = await ScheduleMappedFilesAsync(session, cancellationToken)
                .ConfigureAwait(false);
            if (diskStop)
            {
                warnings.Add(new TransferWarning(
                    TransferWarningKind.DiskReserveStop,
                    "Destination free space reached the safety reserve; scheduling stopped safely. Completed files and valid partials are preserved.",
                    null));
            }

            var stable = await ReconcileAsync(run.RunId, session, warnings, cancellationToken)
                .ConfigureAwait(false);
            if (!stable)
            {
                warnings.Add(new TransferWarning(
                    TransferWarningKind.SourceUnstable,
                    "The source kept changing during the copy, so the archive could not be confirmed complete.",
                    null));
            }

            await ApplyFolderTimestampsAsync(session, warnings, cancellationToken).ConfigureAwait(false);

            var finalState = await EvaluateFinalStateAsync(stable && !diskStop, cancellationToken)
                .ConfigureAwait(false);
            await _stateStore.CompleteRunAsync(run.RunId, finalState, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Run finished; state={FinalState}; stable={SourceStable}",
                finalState, stable);
            return await BuildResultAsync(run.RunId, finalState, stable, warnings, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stop scheduling, preserve completed files and safe partials, and persist
            // the cancelled states. Cancellation is never converted into Failed.
            await _stateStore.CancelPendingItemsAsync(CancellationToken.None).ConfigureAwait(false);
            await _stateStore.CompleteRunAsync(run.RunId, TransferRunState.Cancelled, CancellationToken.None)
                .ConfigureAwait(false);
            _logger.LogInformation("Run cancelled; completed files and safe partials are preserved.");
            return await BuildResultAsync(
                    run.RunId, TransferRunState.Cancelled, SourceStable: false, warnings, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (TransferException)
        {
            throw; // the run was already transitioned by FailRunAsync where applicable
        }
        catch (GraphRequestException exception)
        {
            var error = exception.StatusCode switch
            {
                401 => TransferErrors.OperatorSessionRequired(),
                403 => TransferErrors.SourceAccessDenied(),
                404 => TransferErrors.SourceNotFound(),
                429 => TransferErrors.Throttled(),
                _ when exception.IsTransient => TransferErrors.ServiceUnavailable(),
                _ => TransferErrors.UnexpectedResponse(),
            };
            throw await FailRunAsync(run.RunId, error, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Bounded scheduling: pages of Mapped file items feed at most
    /// <see cref="MaxConcurrentDownloads" /> workers. Before each file the per-file
    /// capacity check (expected size plus the fixed reserve) runs; a violation stops
    /// new scheduling safely and lets in-flight files finish. One file's failure never
    /// stops unrelated files.
    /// </summary>
    private async Task<bool> ScheduleMappedFilesAsync(
        DestinationSession session,
        CancellationToken cancellationToken)
    {
        var diskStop = false;
        using var throttler = new SemaphoreSlim(MaxConcurrentDownloads);
        var running = new List<Task>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await _stateStore
                .GetSchedulableFileItemsAsync(SchedulingBatchSize, cancellationToken)
                .ConfigureAwait(false);
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var item in batch)
            {
                if (diskStop)
                {
                    break;
                }

                var capacity = _capacityService.CheckFile(
                    session.Destination.RootPath, item.SizeBytes ?? 0);
                if (!capacity.IsSufficient)
                {
                    diskStop = true;
                    _logger.LogWarning(
                        "Scheduling stopped; free space {FreeBytes} does not exceed the next file plus reserve",
                        capacity.AvailableFreeSpaceBytes);
                    break;
                }

                // Mark before starting the worker so the next batch never re-reads it.
                await _stateStore.SetItemStateAsync(
                        item.SourceItemId, TransferItemState.Downloading, cancellationToken)
                    .ConfigureAwait(false);

                await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
                running.Add(Task.Run(
                    async () =>
                    {
                        try
                        {
                            await _engine.TransferFileAsync(
                                    item,
                                    session.Destination,
                                    _recopyItemIds.Contains(item.SourceItemId),
                                    bytesWritten: null,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    },
                    CancellationToken.None));
            }
        }

        // In-flight workers finish (or observe cancellation) before the state is read.
        await Task.WhenAll(running).ConfigureAwait(false);
        return diskStop;
    }

    /// <summary>
    /// At most three bounded reconciliation passes through the saved delta checkpoint.
    /// Each pass applies changes transactionally, schedules newly supported work, and
    /// must reach a new checkpoint; a pass with no content-affecting change is stable.
    /// A supported 410 reset starts a fresh enumeration without resetting state or
    /// deleting archived files.
    /// </summary>
    private async Task<bool> ReconcileAsync(
        string runId,
        DestinationSession session,
        List<TransferWarning> warnings,
        CancellationToken cancellationToken)
    {
        var applier = new ReconciliationApplier(
            _stateStore, _pathMapper, _pathGuard, _hashingService,
            new LoggerAdapter<ReconciliationApplier>(_logger));

        for (var pass = 1; pass <= MaxReconciliationPasses; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var checkpoint = await _stateStore.GetDeltaCheckpointRecordAsync(cancellationToken)
                .ConfigureAwait(false);
            if (checkpoint is null)
            {
                // Without a checkpoint the source can never be confirmed stable.
                return false;
            }

            var tracker = new ReconciliationPassTracker();
            try
            {
                await _deltaClient.EnumerateAsync(
                        _stateStore.DriveId,
                        checkpoint.Checkpoint,
                        (page, pageCt) => applier.ApplyPageAsync(
                            runId, page, DeltaCheckpointState.ReconciliationInProgress, tracker, pageCt),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (DeltaCheckpointResetException reset)
            {
                await HandleCheckpointResetAsync(runId, reset, applier, tracker, cancellationToken)
                    .ConfigureAwait(false);
            }

            var passStable = await applier
                .CompletePassAsync(session, tracker, _recopyItemIds, warnings, cancellationToken)
                .ConfigureAwait(false);

            await CreatePendingFoldersAsync(session, cancellationToken).ConfigureAwait(false);
            var diskStop = await ScheduleMappedFilesAsync(session, cancellationToken)
                .ConfigureAwait(false);
            if (diskStop)
            {
                warnings.Add(new TransferWarning(
                    TransferWarningKind.DiskReserveStop,
                    "Destination free space reached the safety reserve; scheduling stopped safely. Completed files and valid partials are preserved.",
                    null));
            }

            if (passStable && !diskStop)
            {
                await SaveCheckpointStateAsync(DeltaCheckpointState.SourceStable, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
        }

        await SaveCheckpointStateAsync(DeltaCheckpointState.SourceUnstable, cancellationToken)
            .ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Supported 410 reset (GRAPH-DELTA-003): keep the prior checkpoint and state,
    /// follow the opaque fresh-enumeration location, rebuild the inventory without
    /// deleting archived files, and mark items the source no longer returns as
    /// deleted-source records. A second reset inside the same pass makes the source
    /// unstable.
    /// </summary>
    private async Task HandleCheckpointResetAsync(
        string runId,
        DeltaCheckpointResetException reset,
        ReconciliationApplier applier,
        ReconciliationPassTracker tracker,
        CancellationToken cancellationToken)
    {
        var checkpoint = await _stateStore.GetDeltaCheckpointRecordAsync(cancellationToken)
            .ConfigureAwait(false);
        if (checkpoint is not null)
        {
            await _stateStore.SaveDeltaCheckpointRecordAsync(
                checkpoint with
                {
                    State = DeltaCheckpointState.DeltaCheckpointResetRequired,
                    UpdatedUtc = DateTimeOffset.UtcNow
                },
                cancellationToken).ConfigureAwait(false);
        }

        _logger.LogWarning("Delta checkpoint was reset by the service; starting a fresh enumeration.");
        tracker.HasChanges = true;

        try
        {
            await _deltaClient.EnumerateAsync(
                    _stateStore.DriveId,
                    reset.FreshEnumerationLocation.OriginalString,
                    (page, pageCt) => applier.ApplyPageAsync(
                        runId, page, DeltaCheckpointState.FullReenumerationInProgress, tracker, pageCt),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DeltaCheckpointResetException)
        {
            // Two resets inside one bounded pass: the source cannot be stabilized here.
            tracker.ForceUnstable = true;
            return;
        }

        await _stateStore.MarkUntouchedItemsDeletedAsync(runId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Revalidates items whose state reached Verified before a crash: the recorded
    /// local SHA-256 must recompute over the committed file (already finalized) or the
    /// preserved partial (finalized now); anything else returns to the schedulable
    /// state without trusting unverified content.
    /// </summary>
    private async Task RecoverVerifiedItemsAsync(
        DestinationSession session,
        CancellationToken cancellationToken)
    {
        var verified = await _stateStore.GetItemsByStateAsync(
            TransferItemState.Verified, cancellationToken).ConfigureAwait(false);

        foreach (var item in verified)
        {
            if (item.MappedRelativePath is null || item.LocalSha256 is null)
            {
                await _stateStore.ResetItemForRecopyAsync(item.SourceItemId, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            var finalPath = _pathGuard.ResolveWritableContentPath(
                session.Destination, item.MappedRelativePath);
            var partialPath = _pathGuard.ResolveWritableContentPath(
                session.Destination, item.MappedRelativePath + TransferEngine.PartialSuffix);

            if (File.Exists(finalPath) &&
                await HashMatchesAsync(finalPath, item.LocalSha256, cancellationToken).ConfigureAwait(false))
            {
                var timestamps = ApplyTimestamps(finalPath, item.CreatedUtc, item.LastModifiedUtc);
                await _stateStore.MarkItemCompletedAsync(item.SourceItemId, timestamps, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (File.Exists(partialPath) &&
                await HashMatchesAsync(partialPath, item.LocalSha256, cancellationToken).ConfigureAwait(false))
            {
                if (File.Exists(finalPath))
                {
                    // The final path holds unverified content; never adopt or overwrite
                    // it silently. Keep the partial and fail the item for diagnosis.
                    await _stateStore.SetItemStateAsync(
                            item.SourceItemId, TransferItemState.Failed, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                File.Move(partialPath, finalPath);
                var timestamps = ApplyTimestamps(finalPath, item.CreatedUtc, item.LastModifiedUtc);
                await _stateStore.MarkItemCompletedAsync(item.SourceItemId, timestamps, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            // Neither the final file nor the partial matches the recorded hash: do not
            // trust unverified content. An existing final file is owned by this item's
            // recorded state, so a verified re-copy may replace it.
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }

            if (File.Exists(finalPath))
            {
                _recopyItemIds.Add(item.SourceItemId);
            }

            await _stateStore.ResetItemForRecopyAsync(item.SourceItemId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task CreatePendingFoldersAsync(
        DestinationSession session,
        CancellationToken cancellationToken)
    {
        foreach (var classification in new[]
                 {
                     ItemFacetClassification.Folder,
                     ItemFacetClassification.EmptyFolder
                 })
        {
            var folders = await _stateStore
                .GetItemsByClassificationAsync(classification, cancellationToken)
                .ConfigureAwait(false);
            foreach (var folder in folders)
            {
                if (folder.TransferState != TransferItemState.Mapped || folder.MappedRelativePath is null)
                {
                    continue;
                }

                var fullPath = _pathGuard.ResolveWritableContentPath(
                    session.Destination, folder.MappedRelativePath);
                Directory.CreateDirectory(fullPath);
                await _stateStore.MarkItemCompletedAsync(
                        folder.SourceItemId, TimestampPreservationResult.NotAttempted, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Applies source timestamps to completed folders after child processing, deepest
    /// first. Timestamp failure never invalidates content; it is recorded and forces
    /// CompletedWithWarnings when no content is missing.
    /// </summary>
    private async Task ApplyFolderTimestampsAsync(
        DestinationSession session,
        List<TransferWarning> warnings,
        CancellationToken cancellationToken)
    {
        var folders = new List<TransferItemRecord>();
        foreach (var classification in new[]
                 {
                     ItemFacetClassification.Folder,
                     ItemFacetClassification.EmptyFolder
                 })
        {
            folders.AddRange(await _stateStore
                .GetItemsByClassificationAsync(classification, cancellationToken)
                .ConfigureAwait(false));
        }

        foreach (var folder in folders
                     .Where(folder => folder.TransferState == TransferItemState.Completed &&
                                      folder.MappedRelativePath is { Length: > 0 })
                     .OrderByDescending(folder => folder.MappedRelativePath!.Length))
        {
            var fullPath = Path.Combine(session.Destination.ContentRootPath, folder.MappedRelativePath!);
            var result = ApplyDirectoryTimestamps(fullPath, folder.CreatedUtc, folder.LastModifiedUtc);
            if (result is TimestampPreservationResult.Failed or TimestampPreservationResult.UnsupportedValue)
            {
                warnings.Add(new TransferWarning(
                    TransferWarningKind.TimestampPreservation,
                    "A folder's source timestamps could not be preserved. The folder content is unaffected.",
                    folder.ItemName));
            }

            await _stateStore.MarkItemCompletedAsync(folder.SourceItemId, result, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<TransferRunState> EvaluateFinalStateAsync(
        bool stable,
        CancellationToken cancellationToken)
    {
        var failed = await _stateStore.GetItemsByStateAsync(TransferItemState.Failed, cancellationToken)
            .ConfigureAwait(false);
        var unsupported = await _stateStore.GetItemsByStateAsync(TransferItemState.Unsupported, cancellationToken)
            .ConfigureAwait(false);
        var pending = (await _stateStore.GetItemsByStateAsync(TransferItemState.Discovered, cancellationToken)
                .ConfigureAwait(false)).Count +
            (await _stateStore.GetItemsByStateAsync(TransferItemState.Mapped, cancellationToken)
                .ConfigureAwait(false)).Count +
            (await _stateStore.GetItemsByStateAsync(TransferItemState.Downloading, cancellationToken)
                .ConfigureAwait(false)).Count +
            (await _stateStore.GetItemsByStateAsync(TransferItemState.Verified, cancellationToken)
                .ConfigureAwait(false)).Count;

        // Any failed supported item, any unsupported item, unfinished work, or an
        // unstable source means the archive is not complete.
        if (!stable || pending > 0 || unsupported.Count > 0 || failed.Count > 0)
        {
            return TransferRunState.Incomplete;
        }

        var completed = await _stateStore.GetItemsByStateAsync(
            TransferItemState.Completed, cancellationToken).ConfigureAwait(false);
        var timestampWarnings = completed.Any(item =>
            item.TimestampPreservation is TimestampPreservationResult.Failed
                or TimestampPreservationResult.UnsupportedValue);

        return timestampWarnings
            ? TransferRunState.CompletedWithWarnings
            : TransferRunState.Completed;
    }

    private async Task<TransferRunResult> BuildResultAsync(
        string runId,
        TransferRunState finalState,
        bool SourceStable,
        List<TransferWarning> warnings,
        CancellationToken cancellationToken)
    {
        var completed = (await _stateStore.GetItemsByStateAsync(TransferItemState.Completed, cancellationToken)
            .ConfigureAwait(false)).Count;
        var skipped = (await _stateStore.GetItemsByStateAsync(TransferItemState.Skipped, cancellationToken)
            .ConfigureAwait(false)).Count;
        var failed = (await _stateStore.GetItemsByStateAsync(TransferItemState.Failed, cancellationToken)
            .ConfigureAwait(false)).Count;
        var unsupported = (await _stateStore.GetItemsByStateAsync(TransferItemState.Unsupported, cancellationToken)
            .ConfigureAwait(false)).Count;

        return new TransferRunResult(
            runId, finalState, completed, skipped, failed, unsupported, SourceStable, warnings);
    }

    private async Task SaveCheckpointStateAsync(
        DeltaCheckpointState state,
        CancellationToken cancellationToken)
    {
        var checkpoint = await _stateStore.GetDeltaCheckpointRecordAsync(cancellationToken)
            .ConfigureAwait(false);
        if (checkpoint is not null)
        {
            await _stateStore.SaveDeltaCheckpointRecordAsync(
                checkpoint with { State = state, UpdatedUtc = DateTimeOffset.UtcNow },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Exception> FailRunAsync(
        string runId,
        TransferException error,
        CancellationToken cancellationToken)
    {
        try
        {
            await _stateStore.CompleteRunAsync(runId, TransferRunState.Failed, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Failed run state could not be persisted; error={Error}", exception.Message);
        }

        _logger.LogWarning("Run failed; code={ReferenceCode}", error.ReferenceCode);
        return error;
    }

    private async Task<bool> HashMatchesAsync(
        string path,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var actual = await _hashingService.ComputeLocalSha256HexAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private TimestampPreservationResult ApplyTimestamps(
        string fullPath,
        DateTimeOffset? createdUtc,
        DateTimeOffset? lastModifiedUtc)
    {
        if (createdUtc is null && lastModifiedUtc is null)
        {
            return TimestampPreservationResult.NotAttempted;
        }

        try
        {
            if (createdUtc is { } created)
            {
                File.SetCreationTimeUtc(fullPath, created.UtcDateTime);
            }

            if (lastModifiedUtc is { } modified)
            {
                File.SetLastWriteTimeUtc(fullPath, modified.UtcDateTime);
            }

            return TimestampPreservationResult.Preserved;
        }
        catch (ArgumentOutOfRangeException)
        {
            return TimestampPreservationResult.UnsupportedValue;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return TimestampPreservationResult.Failed;
        }
    }

    private TimestampPreservationResult ApplyDirectoryTimestamps(
        string fullPath,
        DateTimeOffset? createdUtc,
        DateTimeOffset? lastModifiedUtc)
    {
        if (createdUtc is null && lastModifiedUtc is null)
        {
            return TimestampPreservationResult.NotAttempted;
        }

        try
        {
            if (createdUtc is { } created)
            {
                Directory.SetCreationTimeUtc(fullPath, created.UtcDateTime);
            }

            if (lastModifiedUtc is { } modified)
            {
                Directory.SetLastWriteTimeUtc(fullPath, modified.UtcDateTime);
            }

            return TimestampPreservationResult.Preserved;
        }
        catch (ArgumentOutOfRangeException)
        {
            return TimestampPreservationResult.UnsupportedValue;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return TimestampPreservationResult.Failed;
        }
    }

    /// <summary>Adapts the orchestrator logger category for the internal applier.</summary>
    private sealed class LoggerAdapter<T>(ILogger inner) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
