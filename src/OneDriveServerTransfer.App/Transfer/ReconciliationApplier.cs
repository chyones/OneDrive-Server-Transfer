using System.IO;
using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Inventory;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Transfer;

/// <summary>Per-pass change tracking for reconciliation.</summary>
internal sealed class ReconciliationPassTracker
{
    /// <summary>Any content-affecting change was observed during the pass.</summary>
    public bool HasChanges { get; set; }

    /// <summary>A second delta reset arrived inside one pass; the source is unstable.</summary>
    public bool ForceUnstable { get; set; }

    public List<string> RecopyIds { get; } = [];

    public List<string> DeletedBeforeCopyIds { get; } = [];

    /// <summary>
    /// Verified items that reappeared in delta without any tracked change. Their
    /// recorded local SHA-256 must revalidate before the completed state is trusted.
    /// </summary>
    public List<string> UnchangedCompletedIds { get; } = [];

    public List<RelocationCandidate> RelocationCandidates { get; } = [];
}

/// <summary>A verified item whose source name or parent changed during the run.</summary>
internal sealed record RelocationCandidate(
    string ItemId,
    string? OldMappedPath,
    ItemFacetClassification Classification,
    TransferItemState State);

/// <summary>
/// Applies reconciliation delta pages to the transfer state (contract section 7,
/// docs/GRAPH_DELTA_AND_RECONCILIATION_POLICY.md). Source identity is preserved
/// through the Drive Item ID; renames and moves update the deterministic mapping
/// transactionally and relocate only verified local content owned by the same item;
/// source deletions are recorded as tombstones and never delete local content; new
/// and changed supported files are returned to the schedulable state. Pages and their
/// opaque paging links persist atomically through the state store.
/// </summary>
internal sealed class ReconciliationApplier
{
    private const int MaxMappingRounds = 1024;

    private readonly ITransferStateStore _stateStore;
    private readonly IPathMapper _pathMapper;
    private readonly IDestinationPathGuard _pathGuard;
    private readonly IHashingService _hashingService;
    private readonly ILogger<ReconciliationApplier> _logger;

    public ReconciliationApplier(
        ITransferStateStore stateStore,
        IPathMapper pathMapper,
        IDestinationPathGuard pathGuard,
        IHashingService hashingService,
        ILogger<ReconciliationApplier> logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _pathMapper = pathMapper ?? throw new ArgumentNullException(nameof(pathMapper));
        _pathGuard = pathGuard ?? throw new ArgumentNullException(nameof(pathGuard));
        _hashingService = hashingService ?? throw new ArgumentNullException(nameof(hashingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Decides the per-item actions for one delta page using the pre-upsert state,
    /// then applies the page and its paging link transactionally.
    /// </summary>
    public async Task ApplyPageAsync(
        string runId,
        DeltaInventoryPage page,
        DeltaCheckpointState checkpointState,
        ReconciliationPassTracker tracker,
        CancellationToken cancellationToken)
    {
        foreach (var deltaItem in page.Items)
        {
            var existing = await _stateStore.GetItemAsync(deltaItem.ItemId, cancellationToken)
                .ConfigureAwait(false);
            ClassifyChange(deltaItem, existing, tracker);
        }

        var records = page.Items.Select(ToRecord).ToArray();
        var link = page.IsFinal ? page.DeltaLink! : page.NextLink!;
        await _stateStore.ApplyRunDeltaPageAsync(runId, records, link, checkpointState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Completes one pass after enumeration: maps new items, resets changed files for
    /// re-copy, records deleted-before-copy items as failed, and relocates verified
    /// content for renames and moves. Returns true when the pass saw no
    /// content-affecting change.
    /// </summary>
    public async Task<bool> CompletePassAsync(
        DestinationSession session,
        ReconciliationPassTracker tracker,
        ISet<string> recopyItemIds,
        List<TransferWarning> warnings,
        CancellationToken cancellationToken)
    {
        await MapPendingItemsAsync(session, warnings, cancellationToken).ConfigureAwait(false);

        foreach (var itemId in tracker.RecopyIds)
        {
            var item = await _stateStore.GetItemAsync(itemId, cancellationToken).ConfigureAwait(false);
            if (item is not null)
            {
                await RequeueForRecopyAsync(session, item, recopyItemIds, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        foreach (var itemId in tracker.DeletedBeforeCopyIds)
        {
            warnings.Add(new TransferWarning(
                TransferWarningKind.DeletedBeforeCopy,
                "A source item was deleted before it could be copied, so the archive is not complete.",
                itemId));
            await _stateStore.SetItemStateAsync(itemId, TransferItemState.Failed, cancellationToken)
                .ConfigureAwait(false);
        }

        // Before an existing completed state is trusted, the recorded local SHA-256
        // must revalidate; a mismatch means the local file cannot be validly kept and
        // the item is re-copied over its owned archive content.
        foreach (var itemId in tracker.UnchangedCompletedIds)
        {
            var item = await _stateStore.GetItemAsync(itemId, cancellationToken).ConfigureAwait(false);
            if (item is null || item.MappedRelativePath is null || item.LocalSha256 is null)
            {
                continue;
            }

            var fullPath = _pathGuard.ResolveWritableContentPath(
                session.Destination, item.MappedRelativePath);
            if (await HashMatchesAsync(fullPath, item.LocalSha256, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            _logger.LogWarning(
                "Completed content failed local hash revalidation and is re-copied; itemRef={ItemReference}",
                TransferEngine.ItemReference(itemId));
            tracker.HasChanges = true;
            tracker.RecopyIds.Add(itemId);
            await RequeueForRecopyAsync(session, item, recopyItemIds, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var candidate in tracker.RelocationCandidates
                     .OrderBy(candidate => candidate.OldMappedPath?.Length ?? 0))
        {
            await RelocateVerifiedItemAsync(session, candidate, warnings, cancellationToken)
                .ConfigureAwait(false);
        }

        return !tracker.HasChanges && !tracker.ForceUnstable;
    }

    /// <summary>
    /// Returns one item to the schedulable state for a fresh download. A partial from a
    /// different source version is never resumed: the application-owned partial is
    /// removed first.
    /// </summary>
    private async Task RequeueForRecopyAsync(
        DestinationSession session,
        TransferItemRecord item,
        ISet<string> recopyItemIds,
        CancellationToken cancellationToken)
    {
        if (item.MappedRelativePath is not null)
        {
            var partialPath = _pathGuard.ResolveWritableContentPath(
                session.Destination, item.MappedRelativePath + TransferEngine.PartialSuffix);
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }
        }

        await _stateStore.ResetItemForRecopyAsync(item.SourceItemId, cancellationToken)
            .ConfigureAwait(false);
        recopyItemIds.Add(item.SourceItemId);
    }

    private async Task<bool> HashMatchesAsync(
        string fullPath,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(fullPath))
            {
                return false;
            }

            await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var actual = await _hashingService.ComputeLocalSha256HexAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void ClassifyChange(
        DeltaInventoryItem deltaItem,
        TransferItemRecord? existing,
        ReconciliationPassTracker tracker)
    {
        var classification = ToClassification(deltaItem.Facet);

        if (classification == ItemFacetClassification.DeletedSource)
        {
            if (existing is null || existing.Classification == ItemFacetClassification.DeletedSource)
            {
                return;
            }

            tracker.HasChanges = true;
            if (existing.TransferState is TransferItemState.Completed
                or TransferItemState.Verified or TransferItemState.Skipped)
            {
                // Retained archive content: the tombstone is recorded; local content
                // is never deleted.
                return;
            }

            if (existing.Classification is ItemFacetClassification.File
                or ItemFacetClassification.Folder or ItemFacetClassification.EmptyFolder)
            {
                tracker.DeletedBeforeCopyIds.Add(existing.SourceItemId);
            }

            return;
        }

        if (existing is null)
        {
            tracker.HasChanges = true;
            return;
        }

        if (existing.Classification == ItemFacetClassification.DeletedSource)
        {
            // Drive Item ID reuse: the old content stays retained under its recorded
            // state; the new content is handled as a fresh copy of the same item ID.
            tracker.HasChanges = true;
            if (classification == ItemFacetClassification.File)
            {
                tracker.RecopyIds.Add(existing.SourceItemId);
            }

            return;
        }

        var tagChanged = !TagsEqual(existing.CTag ?? existing.ETag, deltaItem.CTag ?? deltaItem.ETag);
        var sizeChanged = existing.SizeBytes != deltaItem.SizeBytes;
        var nameChanged = !string.Equals(existing.ItemName, deltaItem.Name, StringComparison.Ordinal);
        var parentChanged = !string.Equals(existing.ParentItemId, deltaItem.ParentItemId, StringComparison.Ordinal);

        switch (classification)
        {
            case ItemFacetClassification.File:
                if (existing.TransferState is TransferItemState.Completed or TransferItemState.Verified)
                {
                    if (tagChanged || sizeChanged)
                    {
                        tracker.HasChanges = true;
                        tracker.RecopyIds.Add(existing.SourceItemId);
                    }
                    else if (nameChanged || parentChanged)
                    {
                        tracker.HasChanges = true;
                        tracker.RelocationCandidates.Add(new RelocationCandidate(
                            existing.SourceItemId, existing.MappedRelativePath,
                            existing.Classification, existing.TransferState));
                    }
                    else if (existing.TransferState == TransferItemState.Completed)
                    {
                        // The item reappeared without a tracked change (for example a
                        // metadata-only touch or a fresh re-enumeration): its local
                        // SHA-256 must revalidate before the completed state is trusted.
                        tracker.UnchangedCompletedIds.Add(existing.SourceItemId);
                    }
                }
                else if (existing.TransferState == TransferItemState.Failed && (tagChanged || sizeChanged))
                {
                    tracker.HasChanges = true;
                    tracker.RecopyIds.Add(existing.SourceItemId);
                }
                else if (tagChanged || sizeChanged || nameChanged || parentChanged)
                {
                    tracker.HasChanges = true; // pre-transfer item: remapped and rescheduled
                }

                break;

            case ItemFacetClassification.Folder:
            case ItemFacetClassification.EmptyFolder:
                if (nameChanged || parentChanged)
                {
                    tracker.HasChanges = true;
                    if (existing.TransferState is TransferItemState.Completed or TransferItemState.Verified)
                    {
                        tracker.RelocationCandidates.Add(new RelocationCandidate(
                            existing.SourceItemId, existing.MappedRelativePath,
                            existing.Classification, existing.TransferState));
                    }
                }

                break;

            default:
                // Unsupported packages, external shortcuts, and unknown facets: any new
                // or changing unsupported item keeps the run from claiming completeness.
                if (existing.Classification != classification || nameChanged || parentChanged)
                {
                    tracker.HasChanges = true;
                }

                break;
        }
    }

    /// <summary>
    /// Resolves source paths and deterministic mapped paths for items the upsert reset
    /// (new and pre-transfer items), level by level. Mirrors the scan mapping rules:
    /// the drive root is the archive container, unsupported classifications are
    /// recorded without mapping, and mapping failures fail the item safely.
    /// </summary>
    private async Task MapPendingItemsAsync(
        DestinationSession session,
        List<TransferWarning> warnings,
        CancellationToken cancellationToken)
    {
        for (var round = 0; round < MaxMappingRounds; round++)
        {
            var resolvable = await _stateStore.GetItemsAwaitingSourcePathAsync(cancellationToken)
                .ConfigureAwait(false);
            if (resolvable.Count == 0)
            {
                break;
            }

            foreach (var item in resolvable)
            {
                await MapOneItemAsync(session, item, warnings, cancellationToken).ConfigureAwait(false);
            }
        }

        var unresolved = await _stateStore.GetUnresolvedItemsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in unresolved)
        {
            warnings.Add(new TransferWarning(
                TransferWarningKind.SourceUnstable,
                "An item could not be placed because its parent folder is missing from the inventory. It is marked as failed.",
                item.ItemName));
            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, string.Empty, null, TransferItemState.Failed, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task MapOneItemAsync(
        DestinationSession session,
        TransferItemRecord item,
        List<TransferWarning> warnings,
        CancellationToken cancellationToken)
    {
        if (item.ParentItemId is null)
        {
            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, string.Empty, string.Empty, TransferItemState.Skipped, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var parent = await _stateStore.GetItemAsync(item.ParentItemId, cancellationToken)
            .ConfigureAwait(false);
        if (parent?.SourcePath is null ||
            parent.Classification is not (ItemFacetClassification.Folder or ItemFacetClassification.EmptyFolder) ||
            parent.MappedRelativePath is null)
        {
            warnings.Add(new TransferWarning(
                TransferWarningKind.SourceUnstable,
                "An item could not be placed because its parent folder is missing from the inventory. It is marked as failed.",
                item.ItemName));
            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, string.Empty, null, TransferItemState.Failed, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var sourcePath = parent.SourcePath.Length == 0
            ? item.ItemName
            : parent.SourcePath + "/" + item.ItemName;

        if (item.Classification is not (ItemFacetClassification.File or ItemFacetClassification.Folder
                or ItemFacetClassification.EmptyFolder))
        {
            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, sourcePath, null, TransferItemState.Unsupported, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        PathMapResult mapping;
        try
        {
            mapping = _pathMapper.Map(new PathMapRequest(
                parent.MappedRelativePath,
                item.ItemName,
                item.SourceItemId,
                item.Classification == ItemFacetClassification.File
                    ? MappedItemKind.File
                    : MappedItemKind.Folder));
        }
        catch (InvalidOperationException)
        {
            warnings.Add(new TransferWarning(
                TransferWarningKind.SourceUnstable,
                "An item could not be given a unique safe destination name. It is marked as failed.",
                item.ItemName));
            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, sourcePath, null, TransferItemState.Failed, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var canonicalLength = session.Destination.ContentRootPath.Length + 1 + mapping.RelativePath.Length;
        if (canonicalLength > PathMapperV1.MaxCanonicalPathUtf16Units)
        {
            warnings.Add(new TransferWarning(
                TransferWarningKind.SourceUnstable,
                "An item destination path is longer than Windows supports. It is marked as failed.",
                item.ItemName));
            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, sourcePath, null, TransferItemState.Failed, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await _stateStore.UpdateItemPathsAsync(
                item.SourceItemId, sourcePath, mapping.RelativePath, TransferItemState.Mapped, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Relocates verified local content after a source rename or move. The destination
    /// is revalidated through the guard; an occupied or unsafe new path retains the
    /// verified content at its previous location with a warning, and unrelated local
    /// content is never overwritten.
    /// </summary>
    private async Task RelocateVerifiedItemAsync(
        DestinationSession session,
        RelocationCandidate candidate,
        List<TransferWarning> warnings,
        CancellationToken cancellationToken)
    {
        var item = await _stateStore.GetItemAsync(candidate.ItemId, cancellationToken)
            .ConfigureAwait(false);
        if (item is null || candidate.OldMappedPath is null || item.MappedRelativePath is null)
        {
            return;
        }

        var parent = item.ParentItemId is null
            ? null
            : await _stateStore.GetItemAsync(item.ParentItemId, cancellationToken).ConfigureAwait(false);
        if (parent?.MappedRelativePath is null)
        {
            return; // the drive root is never relocated
        }

        PathMapResult mapping;
        try
        {
            mapping = _pathMapper.Map(new PathMapRequest(
                parent.MappedRelativePath,
                item.ItemName,
                item.SourceItemId,
                candidate.Classification == ItemFacetClassification.File
                    ? MappedItemKind.File
                    : MappedItemKind.Folder));
        }
        catch (InvalidOperationException)
        {
            warnings.Add(new TransferWarning(
                TransferWarningKind.RenameRelocationRetained,
                "A renamed item could not be given a safe destination name; its verified content stays at the previous path.",
                item.ItemName));
            return;
        }

        var newSourcePath = parent.SourcePath is { Length: > 0 } parentSource
            ? parentSource + "/" + item.ItemName
            : item.ItemName;

        if (string.Equals(mapping.RelativePath, candidate.OldMappedPath, StringComparison.Ordinal))
        {
            // The mapped name did not change; only the source metadata did.
            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, newSourcePath, candidate.OldMappedPath,
                    candidate.State, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var oldFullPath = _pathGuard.ResolveWritableContentPath(session.Destination, candidate.OldMappedPath);
        var newFullPath = _pathGuard.ResolveWritableContentPath(session.Destination, mapping.RelativePath);

        var isFolder = candidate.Classification is ItemFacetClassification.Folder
            or ItemFacetClassification.EmptyFolder;
        var targetOccupied = isFolder ? Directory.Exists(newFullPath) : File.Exists(newFullPath);
        if (targetOccupied)
        {
            // Never overwrite unrelated local content; retain the verified content.
            warnings.Add(new TransferWarning(
                TransferWarningKind.RenameRelocationRetained,
                "A renamed or moved item's destination is already in use; its verified content stays at the previous path.",
                item.ItemName));
            return;
        }

        if (isFolder)
        {
            if (Directory.Exists(oldFullPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newFullPath)!);
                Directory.Move(oldFullPath, newFullPath);
            }
            else
            {
                Directory.CreateDirectory(newFullPath);
            }

            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, newSourcePath, mapping.RelativePath,
                    candidate.State, cancellationToken)
                .ConfigureAwait(false);

            // Folder renames may not surface descendants in delta: rebuild their
            // effective paths from the stored parent relationship.
            var descendants = await _stateStore
                .GetItemsUnderMappedPathAsync(candidate.OldMappedPath, cancellationToken)
                .ConfigureAwait(false);
            foreach (var descendant in descendants)
            {
                var mappedRemainder = descendant.MappedRelativePath![candidate.OldMappedPath.Length..];
                var sourceRemainder = descendant.SourcePath is not null &&
                                      item.SourcePath is not null &&
                                      descendant.SourcePath.StartsWith(item.SourcePath, StringComparison.Ordinal)
                    ? descendant.SourcePath[item.SourcePath.Length..]
                    : null;
                await _stateStore.UpdateItemPathsAsync(
                        descendant.SourceItemId,
                        sourceRemainder is null ? descendant.SourcePath ?? newSourcePath : newSourcePath + sourceRemainder,
                        mapping.RelativePath + mappedRemainder,
                        descendant.TransferState,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else
        {
            if (!File.Exists(oldFullPath))
            {
                return; // nothing verified on disk; the recopy flow owns missing content
            }

            Directory.CreateDirectory(Path.GetDirectoryName(newFullPath)!);
            File.Move(oldFullPath, newFullPath);

            var oldPartial = oldFullPath + TransferEngine.PartialSuffix;
            if (File.Exists(oldPartial))
            {
                File.Move(oldPartial, newFullPath + TransferEngine.PartialSuffix);
            }

            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, newSourcePath, mapping.RelativePath,
                    candidate.State, cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Verified content relocated after source rename/move; itemRef={ItemReference}",
            TransferEngine.ItemReference(item.SourceItemId));
    }

    private static bool TagsEqual(string? storedTag, string? deltaTag) =>
        storedTag is null || deltaTag is null ||
        string.Equals(storedTag, deltaTag, StringComparison.Ordinal);

    private static ItemFacetClassification ToClassification(DeltaItemFacet facet) => facet switch
    {
        DeltaItemFacet.File => ItemFacetClassification.File,
        DeltaItemFacet.Folder => ItemFacetClassification.Folder,
        DeltaItemFacet.Package => ItemFacetClassification.UnsupportedPackage,
        DeltaItemFacet.Deleted => ItemFacetClassification.DeletedSource,
        DeltaItemFacet.ExternalShortcut => ItemFacetClassification.ExternalShortcut,
        _ => ItemFacetClassification.Unknown,
    };

    private static TransferItemRecord ToRecord(DeltaInventoryItem item) => new(
        DriveId: string.Empty, // the store always writes the bound drive identity
        item.ItemId,
        item.ParentItemId,
        item.Name,
        SourcePath: null,
        MappedRelativePath: null,
        ToClassification(item.Facet),
        item.ETag,
        item.CTag,
        item.SizeBytes,
        item.CreatedUtc,
        item.LastModifiedUtc,
        item.SourceHashAlgorithm,
        item.SourceHashValue,
        LocalSha256: null,
        TransferItemState.Discovered,
        AttemptCount: 0,
        TimestampPreservationResult.NotAttempted,
        ScanId: null,
        UpdatedUtc: DateTimeOffset.UtcNow);
}
