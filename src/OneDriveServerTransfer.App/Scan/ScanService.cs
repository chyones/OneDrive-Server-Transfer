using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Inventory;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Scan;

/// <summary>
/// The mandatory dry-run scan (contract section 5). A successful scan is required
/// before copying can be enabled, and changing the source identity or the destination
/// invalidates it. The scan never downloads employee file content.
/// </summary>
public interface IScanService
{
    /// <summary>
    /// Runs the complete dry run against an already-resolved source and an already-opened
    /// destination session: revalidates the operator and the destination binding,
    /// completes the delta inventory page by page, classifies every item, applies
    /// deterministic path mapping through the persisted collision registry without
    /// writing employee content, evaluates storage reserve and destination permissions,
    /// and persists the inventory, summary, and delta checkpoint transactionally.
    /// Throws <see cref="ScanException" /> or <see cref="DestinationException" /> with a
    /// stable reference code on failure; a partial enumeration never succeeds.
    /// </summary>
    Task<ScanResult> ScanAsync(
        ResolvedEmployeeSource source,
        DestinationSession session,
        CancellationToken cancellationToken);

    /// <summary>
    /// True only when a scan succeeded for exactly this source identity (tenant,
    /// employee object, drive) and this destination. The copy orchestration must
    /// require a current scan before scheduling. An unreadable, unbound, or
    /// unsupported state database simply means no current scan exists.
    /// </summary>
    Task<bool> IsScanCurrentAsync(
        ResolvedEmployeeSource source,
        DestinationSession session,
        CancellationToken cancellationToken);
}

public sealed class ScanService : IScanService
{
    /// <summary>Safety cap on mapping-pass rounds; each round resolves one hierarchy level.</summary>
    internal const int MaxMappingRounds = 1024;

    private readonly IAuthenticationService _authenticationService;
    private readonly IDestinationBindingService _bindingService;
    private readonly IDeltaInventoryClient _deltaClient;
    private readonly ITransferStateStore _stateStore;
    private readonly SqlitePathCollisionRegistry _collisionRegistry;
    private readonly IPathMapper _pathMapper;
    private readonly IDestinationCapacityService _capacityService;
    private readonly IDestinationSecurityEvaluator _securityEvaluator;
    private readonly ILogger<ScanService> _logger;

    public ScanService(
        IAuthenticationService authenticationService,
        IDestinationBindingService bindingService,
        IDeltaInventoryClient deltaClient,
        ITransferStateStore stateStore,
        SqlitePathCollisionRegistry collisionRegistry,
        IPathMapper pathMapper,
        IDestinationCapacityService capacityService,
        IDestinationSecurityEvaluator securityEvaluator,
        ILogger<ScanService> logger)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _bindingService = bindingService ?? throw new ArgumentNullException(nameof(bindingService));
        _deltaClient = deltaClient ?? throw new ArgumentNullException(nameof(deltaClient));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _collisionRegistry = collisionRegistry ?? throw new ArgumentNullException(nameof(collisionRegistry));
        _pathMapper = pathMapper ?? throw new ArgumentNullException(nameof(pathMapper));
        _capacityService = capacityService ?? throw new ArgumentNullException(nameof(capacityService));
        _securityEvaluator = securityEvaluator ?? throw new ArgumentNullException(nameof(securityEvaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScanResult> ScanAsync(
        ResolvedEmployeeSource source,
        DestinationSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(session);

        var operatorIdentity = await _authenticationService
            .GetCurrentOperatorAsync(cancellationToken).ConfigureAwait(false);
        if (operatorIdentity is null)
        {
            throw ScanErrors.OperatorSessionRequired();
        }

        if (!string.Equals(operatorIdentity.TenantId, source.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Scan rejected; signed-in operator tenant does not match the source tenant.");
            throw ScanErrors.OperatorTenantMismatch();
        }

        // Revalidate the destination binding for the current source (records the audit
        // entry); the session holds the exclusive lock for the whole scan.
        await _bindingService.BindOrValidateAsync(
                session.Destination,
                new SourceBindingIdentity(
                    source.TenantId, source.DriveId, source.UserObjectId, source.UserPrincipalName),
                new Destination.OperatorIdentity(operatorIdentity.EntraObjectId, operatorIdentity.UserPrincipalName),
                cancellationToken)
            .ConfigureAwait(false);

        await _stateStore.OpenAsync(session.Destination.StateDatabasePath, cancellationToken)
            .ConfigureAwait(false);

        // A stale in-progress scan is a crashed scan: mark it interrupted so it can
        // never enable copying, then resume enumeration from the last safely
        // persisted next link when one exists.
        await _stateStore.MarkInProgressScansInterruptedAsync(cancellationToken).ConfigureAwait(false);
        _collisionRegistry.Open(session.Destination.StateDatabasePath);

        var checkpoint = await _stateStore.GetDeltaCheckpointRecordAsync(cancellationToken)
            .ConfigureAwait(false);
        var resumeLink = checkpoint?.State == DeltaCheckpointState.InitialEnumerationInProgress
            ? checkpoint.Checkpoint
            : null;

        var scan = new ScanRecord(
            ScanId: Guid.NewGuid().ToString("N"),
            source.TenantId,
            source.UserObjectId,
            source.DriveId,
            session.Destination.RootPath,
            ScanState.InProgress,
            DateTimeOffset.UtcNow,
            CompletedUtc: null,
            FileCount: 0,
            FolderCount: 0,
            EmptyFolderCount: 0,
            UnsupportedCount: 0,
            KnownBytes: 0);
        await _stateStore.BeginScanAsync(scan, cancellationToken).ConfigureAwait(false);

        try
        {
            await _deltaClient.EnumerateAsync(
                    source.DriveId,
                    resumeLink,
                    (page, pageCt) => ApplyPageAsync(scan.ScanId, page, pageCt),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DeltaCheckpointResetException exception)
        {
            // Supported 410 reset: keep the prior checkpoint, advance the persisted
            // delta state, and fail the scan so the next run starts fresh. The reset
            // location is never logged. Full re-enumeration orchestration is a
            // later-slice concern; a new scan already enumerates from the root.
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

            _logger.LogWarning("Scan stopped; the service invalidated the saved delta checkpoint.");
            throw ScanErrors.DeltaResetRequired(exception);
        }
        catch (GraphRequestException exception)
        {
            throw MapGraphFailure(exception);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Scan cancelled.");
            throw ScanErrors.Cancelled();
        }

        var warnings = await RunMappingPassAsync(session, cancellationToken).ConfigureAwait(false);

        var finalized = await _stateStore.CompleteScanAsync(scan.ScanId, cancellationToken)
            .ConfigureAwait(false);

        warnings.AddRange(EvaluateDestination(session, finalized.KnownBytes));

        var unsupported = await CollectUnsupportedAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Scan succeeded; files={FileCount}; folders={FolderCount}; unsupported={UnsupportedCount}; knownBytes={KnownBytes}; warnings={WarningCount}",
            finalized.FileCount, finalized.FolderCount, finalized.UnsupportedCount,
            finalized.KnownBytes, warnings.Count);

        return new ScanResult(
            finalized.ScanId,
            finalized.FileCount,
            finalized.FolderCount,
            finalized.EmptyFolderCount,
            finalized.UnsupportedCount,
            finalized.KnownBytes,
            unsupported,
            warnings,
            finalized.CompletedUtc ?? DateTimeOffset.UtcNow);
    }

    public async Task<bool> IsScanCurrentAsync(
        ResolvedEmployeeSource source,
        DestinationSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(session);

        ScanRecord? scan;
        try
        {
            await _stateStore.OpenAsync(session.Destination.StateDatabasePath, cancellationToken)
                .ConfigureAwait(false);
            scan = await _stateStore.GetLatestSuccessfulScanAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DestinationException)
        {
            // An unreadable, unbound, or unsupported state database means no current
            // scan exists; the gate stays closed. ScanAsync surfaces the error itself.
            return false;
        }

        if (scan is null)
        {
            return false;
        }

        return string.Equals(scan.TenantId, source.TenantId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(scan.EmployeeObjectId, source.UserObjectId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(scan.DriveId, source.DriveId, StringComparison.Ordinal) &&
               string.Equals(scan.DestinationRoot, session.Destination.RootPath, StringComparison.OrdinalIgnoreCase);
    }

    private Task ApplyPageAsync(string scanId, DeltaInventoryPage page, CancellationToken cancellationToken)
    {
        var items = page.Items.Select(ToRecord).ToArray();
        return page.IsFinal
            ? _stateStore.ApplyFinalDeltaPageAsync(scanId, items, page.DeltaLink!, cancellationToken)
            : _stateStore.ApplyDeltaPageAsync(scanId, items, page.NextLink!, cancellationToken);
    }

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

    private static ItemFacetClassification ToClassification(DeltaItemFacet facet) => facet switch
    {
        DeltaItemFacet.File => ItemFacetClassification.File,
        DeltaItemFacet.Folder => ItemFacetClassification.Folder,
        DeltaItemFacet.Package => ItemFacetClassification.UnsupportedPackage,
        DeltaItemFacet.Deleted => ItemFacetClassification.DeletedSource,
        DeltaItemFacet.ExternalShortcut => ItemFacetClassification.ExternalShortcut,
        _ => ItemFacetClassification.Unknown,
    };

    /// <summary>
    /// Resolves source paths and deterministic mapped paths level by level, reading the
    /// persisted parent rows so the complete hierarchy is never held in memory. The
    /// drive root maps to the archive container itself and is recorded as skipped.
    /// </summary>
    private async Task<List<ScanWarning>> RunMappingPassAsync(
        DestinationSession session,
        CancellationToken cancellationToken)
    {
        var warnings = new List<ScanWarning>();

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
                await MapItemAsync(session, item, warnings, cancellationToken).ConfigureAwait(false);
            }
        }

        // Anything still unresolved has a broken parent chain: fail it safely with a
        // warning instead of dropping it silently.
        var unresolved = await _stateStore.GetUnresolvedItemsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in unresolved)
        {
            warnings.Add(new ScanWarning(
                ScanWarningKind.UnresolvedParent,
                "An item could not be placed because its parent folder is missing from the inventory. It is marked as failed.",
                item.ItemName));
            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, string.Empty, null, TransferItemState.Failed, cancellationToken)
                .ConfigureAwait(false);
        }

        return warnings;
    }

    private async Task MapItemAsync(
        DestinationSession session,
        TransferItemRecord item,
        List<ScanWarning> warnings,
        CancellationToken cancellationToken)
    {
        if (item.ParentItemId is null)
        {
            // The drive root is the archive container, not copied content.
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
            // The parent is missing, is not a folder, or could not itself be placed:
            // the child can never be mapped safely and must fail instead of being
            // silently dropped or placed at the archive root.
            warnings.Add(new ScanWarning(
                ScanWarningKind.UnresolvedParent,
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

        if (item.Classification is not (ItemFacetClassification.File or ItemFacetClassification.Folder))
        {
            // Unsupported packages, external shortcuts, and unknown facets are
            // reported, never mapped, and never silently skipped.
            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, sourcePath, null, TransferItemState.Unsupported, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        PathMapResult mapping;
        try
        {
            mapping = _pathMapper.Map(new PathMapRequest(
                parent.MappedRelativePath ?? string.Empty,
                item.ItemName,
                item.SourceItemId,
                item.Classification == ItemFacetClassification.File
                    ? MappedItemKind.File
                    : MappedItemKind.Folder));
        }
        catch (InvalidOperationException)
        {
            warnings.Add(new ScanWarning(
                ScanWarningKind.PathFailure,
                "An item could not be given a unique safe destination name. It is marked as failed.",
                item.ItemName));
            await _stateStore.UpdateItemPathsAsync(
                    item.SourceItemId, sourcePath, null, TransferItemState.Failed, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (mapping.UsedCollisionSuffix)
        {
            warnings.Add(new ScanWarning(
                ScanWarningKind.PathAdjusted,
                "An item name conflicted with another item or contained characters Windows cannot store; it was given a safe name.",
                item.ItemName));
        }

        var canonicalLength = session.Destination.ContentRootPath.Length + 1 + mapping.RelativePath.Length;
        if (canonicalLength > PathMapperV1.MaxCanonicalPathUtf16Units)
        {
            warnings.Add(new ScanWarning(
                ScanWarningKind.PathFailure,
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

    private List<ScanWarning> EvaluateDestination(DestinationSession session, long knownBytes)
    {
        var warnings = new List<ScanWarning>();

        var capacity = _capacityService.CheckTotal(session.Destination.RootPath, knownBytes);
        if (!capacity.IsSufficient)
        {
            warnings.Add(new ScanWarning(
                ScanWarningKind.InsufficientStorage,
                "The destination free space does not exceed the known source size plus the required 5 GiB safety reserve.",
                null));
        }

        var assessment = _securityEvaluator.Evaluate(session.Destination);
        if (assessment.Verdict == DestinationSecurityVerdict.BroadExposureWarning)
        {
            warnings.Add(new ScanWarning(
                ScanWarningKind.BroadPermissionExposure,
                "The destination permissions allow broad access to the archive data. Restrict the destination folder permissions before copying.",
                null));
        }

        return warnings;
    }

    private async Task<IReadOnlyList<UnsupportedScanItem>> CollectUnsupportedAsync(
        CancellationToken cancellationToken)
    {
        var unsupported = new List<UnsupportedScanItem>();
        foreach (var classification in new[]
                 {
                     ItemFacetClassification.UnsupportedPackage,
                     ItemFacetClassification.ExternalShortcut,
                     ItemFacetClassification.Unknown
                 })
        {
            var items = await _stateStore.GetItemsByClassificationAsync(classification, cancellationToken)
                .ConfigureAwait(false);
            unsupported.AddRange(items.Select(item =>
                new UnsupportedScanItem(item.ItemName, item.SourcePath, classification.ToString())));
        }

        return unsupported;
    }

    private Exception MapGraphFailure(GraphRequestException exception)
    {
        var failure = exception.StatusCode switch
        {
            401 => ScanErrors.OperatorSessionRequired(),
            403 => ScanErrors.SourceAccessDenied(),
            404 => ScanErrors.SourceNotFound(),
            429 => ScanErrors.Throttled(),
            _ when exception.IsTransient => ScanErrors.ServiceUnavailable(),
            _ => ScanErrors.UnexpectedResponse(),
        };

        _logger.LogWarning(
            "Scan failed; reference={Reference}; status={Status}; code={Code}",
            failure.ReferenceCode,
            exception.StatusCode?.ToString() ?? "n/a",
            exception.GraphErrorCode ?? "n/a");
        return failure;
    }
}
