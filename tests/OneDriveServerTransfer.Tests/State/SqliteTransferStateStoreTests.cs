using Microsoft.Data.Sqlite;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.State;

/// <summary>
/// Verifies the M5 SQLite transfer state store: schema, integrity and version gate,
/// transactional page application with checkpoint advancement, idempotent replay,
/// scan lifecycle, summary computation, and crash-recovery helpers.
/// </summary>
public class SqliteTransferStateStoreTests : IDisposable
{
    private const string DriveId = "drive-test-1";

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"odst-m5-{Guid.NewGuid():N}", "TransferState.db");

    private static SqliteTransferStateStore CreateStore() =>
        new(new SqliteTransferStateSchemaInitializer(),
            new CapturingLogger<SqliteTransferStateStore>());

    private async Task<SqliteTransferStateStore> CreateOpenStoreAsync()
    {
        await CreateBindingAsync();
        return await OpenStoreAsync();
    }

    /// <summary>Opens a store over the existing database without recreating state.</summary>
    private async Task<SqliteTransferStateStore> OpenStoreAsync()
    {
        var store = CreateStore();
        await store.OpenAsync(_databasePath, CancellationToken.None);
        return store;
    }

    private async Task CreateBindingAsync()
    {
        var bindingStore = new SqliteDestinationBindingStore(
            new SqliteTransferStateSchemaInitializer(),
            new CapturingLogger<SqliteDestinationBindingStore>());
        await bindingStore.CreateBindingAsync(
            _databasePath,
            new StoredDestinationBinding(
                "tenant-1", DriveId, "employee-1", "employee@example.test",
                "operator-1", "operator@example.test", DateTimeOffset.UtcNow),
            CancellationToken.None);
    }

    private static TransferItemRecord Item(
        string id,
        string? parentId = null,
        string name = "item",
        ItemFacetClassification classification = ItemFacetClassification.File,
        long? size = 10) =>
        new(DriveId, id, parentId, name, null, null, classification,
            $"etag-{id}", null, size,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            "quickXorHash", "hash", null,
            TransferItemState.Discovered, 0, TimestampPreservationResult.NotAttempted,
            null, DateTimeOffset.UtcNow);

    private static ScanRecord Scan(string scanId) =>
        new(scanId, "tenant-1", "employee-1", DriveId, @"D:\Archive",
            ScanState.InProgress, DateTimeOffset.UtcNow, null, 0, 0, 0, 0, 0);

    [Fact]
    public async Task OpenInitializesM5SchemaAndReadsBoundDrive()
    {
        var store = await CreateOpenStoreAsync();

        Assert.Equal(DriveId, store.DriveId);

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        foreach (var table in new[]
                 {
                     "transfer_item", "transfer_run", "delta_checkpoint", "scan_state", "path_mapping"
                 })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            command.Parameters.AddWithValue("$name", table);
            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public async Task OpenRejectsUnsupportedFutureSchemaVersion()
    {
        await CreateBindingAsync();
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE schema_metadata SET value = '2' WHERE key = 'StateSchemaVersion';";
            await command.ExecuteNonQueryAsync();
        }

        var exception = await Assert.ThrowsAsync<DestinationException>(() =>
            CreateStore().OpenAsync(_databasePath, CancellationToken.None));
        Assert.Equal(DestinationErrorCodes.InvalidStateDatabase, exception.ReferenceCode);
    }

    [Fact]
    public async Task OpenRejectsDatabaseWithoutSourceBinding()
    {
        await new SqliteTransferStateSchemaInitializer().InitializeAsync(_databasePath, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DestinationException>(() =>
            CreateStore().OpenAsync(_databasePath, CancellationToken.None));
        Assert.Equal(DestinationErrorCodes.InvalidStateDatabase, exception.ReferenceCode);
    }

    [Fact]
    public async Task PageApplicationPersistsItemsAndNextLinkAtomicallyForCrashResume()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);

        await store.ApplyDeltaPageAsync("scan-1",
            [Item("root", null, "root", ItemFacetClassification.Folder, null), Item("f1", "root")],
            "https://opaque/next?page=2", CancellationToken.None);

        // Simulate a crash: a brand-new store instance sees exactly the committed page
        // and the resumable next link, nothing more.
        var recovered = await OpenStoreAsync();
        var checkpoint = await recovered.GetDeltaCheckpointRecordAsync(CancellationToken.None);

        Assert.NotNull(checkpoint);
        Assert.Equal("https://opaque/next?page=2", checkpoint!.Checkpoint);
        Assert.Equal(DeltaCheckpointState.InitialEnumerationInProgress, checkpoint.State);
        Assert.Equal("https://opaque/next?page=2", await recovered.GetDeltaCheckpointAsync(CancellationToken.None));

        var item = await recovered.GetItemAsync("f1", CancellationToken.None);
        Assert.NotNull(item);
        Assert.Equal("root", item!.ParentItemId);
        Assert.Equal(TransferItemState.Discovered, item.TransferState);
    }

    [Fact]
    public async Task ReplayingTheSamePageIsIdempotent()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        var page = new[] { Item("root", null, "root", ItemFacetClassification.Folder, null), Item("f1", "root") };

        await store.ApplyDeltaPageAsync("scan-1", page, "https://opaque/next", CancellationToken.None);
        await store.ApplyDeltaPageAsync("scan-1", page, "https://opaque/next", CancellationToken.None);

        var items = await store.GetItemsByStateAsync(TransferItemState.Discovered, CancellationToken.None);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task DuplicateItemOccurrencesWithinAPageLetTheLastOccurrenceWin()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);

        await store.ApplyDeltaPageAsync("scan-1",
            [Item("f1", "root", "old-name"), Item("f1", "root", "new-name", size: 99)],
            "https://opaque/next", CancellationToken.None);

        var items = await store.GetItemsByStateAsync(TransferItemState.Discovered, CancellationToken.None);
        var item = Assert.Single(items);
        Assert.Equal("new-name", item.ItemName);
        Assert.Equal(99, item.SizeBytes);
    }

    [Fact]
    public async Task FinalPagePersistsTheDeltaCheckpointAsComplete()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);

        await store.ApplyFinalDeltaPageAsync("scan-1", [], "https://opaque/delta?token=1",
            CancellationToken.None);

        var checkpoint = await store.GetDeltaCheckpointRecordAsync(CancellationToken.None);
        Assert.NotNull(checkpoint);
        Assert.Equal("https://opaque/delta?token=1", checkpoint!.Checkpoint);
        Assert.Equal(DeltaCheckpointState.InitialEnumerationComplete, checkpoint.State);
    }

    [Fact]
    public async Task FailedPageApplicationRollsBackItemsAndCheckpointTogether()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        await store.ApplyDeltaPageAsync("scan-1", [Item("f1")], "https://opaque/next?page=2",
            CancellationToken.None);

        // A trigger raising a real SQLite error mid-batch simulates a database failure
        // while applying the second item of a page.
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TRIGGER fail_f2 BEFORE INSERT ON transfer_item
                WHEN NEW.source_item_id = 'f2'
                BEGIN SELECT RAISE(ABORT, 'simulated failure'); END;
                """;
            await command.ExecuteNonQueryAsync();
        }

        var exception = await Assert.ThrowsAsync<DestinationException>(() =>
            store.ApplyDeltaPageAsync("scan-1", [Item("f2"), Item("f3")], "https://opaque/next?page=3",
                CancellationToken.None));

        Assert.Equal(DestinationErrorCodes.DestinationStateFailure, exception.ReferenceCode);

        var checkpoint = await store.GetDeltaCheckpointRecordAsync(CancellationToken.None);
        Assert.Equal("https://opaque/next?page=2", checkpoint!.Checkpoint);
        Assert.Null(await store.GetItemAsync("f2", CancellationToken.None));
        Assert.Null(await store.GetItemAsync("f3", CancellationToken.None));
        Assert.NotNull(await store.GetItemAsync("f1", CancellationToken.None));
    }

    [Fact]
    public async Task CompleteScanComputesSummaryAndAdvancesCheckpointToValid()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        await store.ApplyDeltaPageAsync("scan-1",
            [
                Item("root", null, "root", ItemFacetClassification.Folder, null),
                Item("folderA", "root", "A", ItemFacetClassification.Folder, null),
                Item("fileA", "folderA", "a.txt", size: 100),
                Item("empty", "root", "Empty", ItemFacetClassification.Folder, null),
                Item("book", "root", "Notebook", ItemFacetClassification.UnsupportedPackage, null),
                Item("gone", "root", "old.txt", ItemFacetClassification.DeletedSource, null),
            ],
            "https://opaque/next", CancellationToken.None);
        await store.ApplyFinalDeltaPageAsync("scan-1",
            [Item("fileB", "root", "b.txt", size: 50)],
            "https://opaque/delta?token=1", CancellationToken.None);

        var finalized = await store.CompleteScanAsync("scan-1", CancellationToken.None);

        Assert.Equal(ScanState.Succeeded, finalized.State);
        Assert.Equal(2, finalized.FileCount);
        Assert.Equal(150, finalized.KnownBytes);
        Assert.Equal(2, finalized.FolderCount);        // folderA and empty; root excluded
        Assert.Equal(1, finalized.EmptyFolderCount);   // folderA has a child, empty does not
        Assert.Equal(1, finalized.UnsupportedCount);   // the package; the tombstone is not unsupported
        Assert.NotNull(finalized.CompletedUtc);

        var checkpoint = await store.GetDeltaCheckpointRecordAsync(CancellationToken.None);
        Assert.Equal(DeltaCheckpointState.DeltaCheckpointValid, checkpoint!.State);

        var empty = await store.GetItemAsync("empty", CancellationToken.None);
        Assert.Equal(ItemFacetClassification.EmptyFolder, empty!.Classification);

        var latest = await store.GetLatestSuccessfulScanAsync(CancellationToken.None);
        Assert.Equal("scan-1", latest!.ScanId);
    }

    [Fact]
    public async Task StaleInProgressScansBecomeInterruptedIdempotently()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);

        Assert.Equal(1, await store.MarkInProgressScansInterruptedAsync(CancellationToken.None));
        Assert.Equal(0, await store.MarkInProgressScansInterruptedAsync(CancellationToken.None));
        Assert.Null(await store.GetLatestSuccessfulScanAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ResetInFlightItemsReturnsThemToMappedIdempotently()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        await store.ApplyFinalDeltaPageAsync("scan-1", [Item("f1"), Item("f2")],
            "https://opaque/delta", CancellationToken.None);
        await store.SetItemStateAsync("f1", TransferItemState.Downloading, CancellationToken.None);
        await store.SetItemStateAsync("f2", TransferItemState.Completed, CancellationToken.None);

        Assert.Equal(1, await store.ResetInFlightItemsAsync(CancellationToken.None));
        Assert.Equal(0, await store.ResetInFlightItemsAsync(CancellationToken.None));

        var reset = await store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal(TransferItemState.Mapped, reset!.TransferState);
        var untouched = await store.GetItemAsync("f2", CancellationToken.None);
        Assert.Equal(TransferItemState.Completed, untouched!.TransferState);

        var mapped = await store.GetItemsByStateAsync(TransferItemState.Mapped, CancellationToken.None);
        Assert.Single(mapped);
    }

    [Fact]
    public async Task UpsertPreservesLaterTransferProgressAndLocalVerificationData()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        await store.ApplyFinalDeltaPageAsync("scan-1", [Item("f1", "root", "a.txt")],
            "https://opaque/delta", CancellationToken.None);

        // Simulate later-milestone progress: completed content with a local SHA-256 and
        // resolved paths must survive a re-scan upsert of the same source item.
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE transfer_item
                SET transfer_state = 'Completed', local_sha256 = 'local-hash',
                    source_path = 'a.txt', mapped_relative_path = 'a.txt'
                WHERE drive_id = $driveId AND source_item_id = 'f1';
                """;
            command.Parameters.AddWithValue("$driveId", DriveId);
            await command.ExecuteNonQueryAsync();
        }

        await store.BeginScanAsync(Scan("scan-2"), CancellationToken.None);
        await store.ApplyFinalDeltaPageAsync("scan-2", [Item("f1", "root", "a.txt", size: 200)],
            "https://opaque/delta?token=2", CancellationToken.None);

        var item = await store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal(TransferItemState.Completed, item!.TransferState);
        Assert.Equal("local-hash", item.LocalSha256);
        Assert.Equal("a.txt", item.SourcePath);
        Assert.Equal("a.txt", item.MappedRelativePath);
        Assert.Equal(200, item.SizeBytes); // metadata still refreshes
    }

    [Fact]
    public async Task MappingPassQueriesDistinguishResolvableFromBrokenParentChains()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        await store.ApplyFinalDeltaPageAsync("scan-1",
            [
                Item("root", null, "root", ItemFacetClassification.Folder, null),
                Item("child", "root"),
                Item("orphan", "missing-parent"),
                Item("gone", "root", "old", ItemFacetClassification.DeletedSource),
            ],
            "https://opaque/delta", CancellationToken.None);

        // Initially only the root is resolvable.
        var awaiting = await store.GetItemsAwaitingSourcePathAsync(CancellationToken.None);
        Assert.Equal(["root"], awaiting.Select(item => item.SourceItemId).ToArray());

        // Once the root is resolved, the child becomes resolvable; the orphan never is.
        await store.UpdateItemPathsAsync("root", string.Empty, string.Empty,
            TransferItemState.Skipped, CancellationToken.None);
        awaiting = await store.GetItemsAwaitingSourcePathAsync(CancellationToken.None);
        Assert.Equal(["child"], awaiting.Select(item => item.SourceItemId).ToArray());

        await store.UpdateItemPathsAsync("child", "child", "child",
            TransferItemState.Mapped, CancellationToken.None);
        Assert.Empty(await store.GetItemsAwaitingSourcePathAsync(CancellationToken.None));

        var unresolved = await store.GetUnresolvedItemsAsync(CancellationToken.None);
        Assert.Equal(["orphan"], unresolved.Select(item => item.SourceItemId).ToArray());
    }

    [Fact]
    public async Task RunLifecyclePersistsStatesAndRejectsTerminalRewrite()
    {
        var store = await CreateOpenStoreAsync();

        await store.BeginRunAsync(new TransferRunRecord(
            "run-1", DriveId, "scan-1", DateTimeOffset.UtcNow, null, null), CancellationToken.None);

        var latest = await store.GetLatestRunAsync(CancellationToken.None);
        Assert.Equal("run-1", latest!.RunId);
        Assert.True(latest.IsInProgress);
        Assert.Null(latest.FinalState);

        await store.CompleteRunAsync("run-1", TransferRunState.Completed, CancellationToken.None);
        latest = await store.GetLatestRunAsync(CancellationToken.None);
        Assert.Equal(TransferRunState.Completed, latest!.FinalState);
        Assert.NotNull(latest.EndedUtc);

        // A second terminal transition for the same run is rejected.
        await Assert.ThrowsAsync<DestinationException>(() =>
            store.CompleteRunAsync("run-1", TransferRunState.Failed, CancellationToken.None));

        // InProgress itself is not a terminal state and cannot be persisted.
        await store.BeginRunAsync(new TransferRunRecord(
            "run-2", DriveId, null, DateTimeOffset.UtcNow, null, null), CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.CompleteRunAsync("run-2", TransferRunState.InProgress, CancellationToken.None));
    }

    [Fact]
    public async Task StaleInProgressRunsAreMarkedInterruptedIdempotently()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginRunAsync(new TransferRunRecord(
            "run-1", DriveId, null, DateTimeOffset.UtcNow, null, null), CancellationToken.None);
        await store.BeginRunAsync(new TransferRunRecord(
            "run-2", DriveId, null, DateTimeOffset.UtcNow, null, null), CancellationToken.None);
        await store.CompleteRunAsync("run-2", TransferRunState.Cancelled, CancellationToken.None);
        await store.BeginRunAsync(new TransferRunRecord(
            "run-3", DriveId, null, DateTimeOffset.UtcNow, null, null), CancellationToken.None);

        var interrupted = await store.MarkInProgressRunsInterruptedAsync(CancellationToken.None);
        Assert.Equal(2, interrupted);

        // Idempotent: a second open finds nothing to interrupt.
        Assert.Equal(0, await store.MarkInProgressRunsInterruptedAsync(CancellationToken.None));

        var latest = await store.GetLatestRunAsync(CancellationToken.None);
        Assert.Equal("run-3", latest!.RunId);
        Assert.Equal(TransferRunState.Interrupted, latest.FinalState);
    }

    [Fact]
    public async Task SchedulableBatchReturnsOnlyMappedFilesBounded()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        await store.ApplyFinalDeltaPageAsync("scan-1",
        [
            Item("f1", "root", "a.txt"),
            Item("f2", "root", "b.txt"),
            Item("f3", "root", "c.txt"),
            Item("d1", "root", "folder", ItemFacetClassification.Folder, null),
            Item("u1", "root", "pkg", ItemFacetClassification.UnsupportedPackage, null),
        ], "https://opaque/delta", CancellationToken.None);

        foreach (var id in new[] { "f1", "f2", "f3" })
        {
            await store.UpdateItemPathsAsync(id, id, id, TransferItemState.Mapped, CancellationToken.None);
        }

        await store.SetItemStateAsync("f3", TransferItemState.Completed, CancellationToken.None);
        await store.UpdateItemPathsAsync("d1", "d1", "d1", TransferItemState.Mapped, CancellationToken.None);

        var batch = await store.GetSchedulableFileItemsAsync(10, CancellationToken.None);
        Assert.Equal(["f1", "f2"], batch.Select(item => item.SourceItemId).ToArray());

        var bounded = await store.GetSchedulableFileItemsAsync(1, CancellationToken.None);
        Assert.Equal(["f1"], bounded.Select(item => item.SourceItemId).ToArray());
    }

    [Fact]
    public async Task AttemptBudgetIncrementsPersistently()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        await store.ApplyFinalDeltaPageAsync("scan-1", [Item("f1")], "https://opaque/delta",
            CancellationToken.None);

        Assert.Equal(1, await store.IncrementAttemptCountAsync("f1", CancellationToken.None));
        Assert.Equal(2, await store.IncrementAttemptCountAsync("f1", CancellationToken.None));

        // A reopened store sees the persisted budget: restart cannot hide attempts.
        var reopened = await OpenStoreAsync();
        Assert.Equal(3, await reopened.IncrementAttemptCountAsync("f1", CancellationToken.None));
    }

    [Fact]
    public async Task VerifiedCompletedAndRecopyTransitionsPersist()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        await store.ApplyFinalDeltaPageAsync("scan-1", [Item("f1")], "https://opaque/delta",
            CancellationToken.None);

        await store.MarkItemVerifiedAsync("f1", "local-sha", CancellationToken.None);
        var item = await store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal(TransferItemState.Verified, item!.TransferState);
        Assert.Equal("local-sha", item.LocalSha256);

        await store.MarkItemCompletedAsync("f1", TimestampPreservationResult.Preserved, CancellationToken.None);
        item = await store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal(TransferItemState.Completed, item!.TransferState);
        Assert.Equal(TimestampPreservationResult.Preserved, item.TimestampPreservation);

        await store.ResetItemForRecopyAsync("f1", CancellationToken.None);
        item = await store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal(TransferItemState.Mapped, item!.TransferState);
        Assert.Null(item.LocalSha256);
        Assert.Equal(0, item.AttemptCount);
        Assert.Equal(TimestampPreservationResult.NotAttempted, item.TimestampPreservation);
    }

    [Fact]
    public async Task CancellationTransitionsOnlyPendingItems()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        await store.ApplyFinalDeltaPageAsync("scan-1",
        [
            Item("f1"), Item("f2"), Item("f3"), Item("f4"),
        ], "https://opaque/delta", CancellationToken.None);

        await store.SetItemStateAsync("f1", TransferItemState.Mapped, CancellationToken.None);
        await store.SetItemStateAsync("f2", TransferItemState.Downloading, CancellationToken.None);
        await store.SetItemStateAsync("f3", TransferItemState.Completed, CancellationToken.None);
        await store.SetItemStateAsync("f4", TransferItemState.Failed, CancellationToken.None);

        var cancelled = await store.CancelPendingItemsAsync(CancellationToken.None);

        // f1 and f2 are cancelled; the Discovered-root-style item (none here), the
        // completed item, and the failed item keep their recorded outcome.
        Assert.Equal(2, cancelled);
        Assert.Equal(TransferItemState.Cancelled,
            (await store.GetItemAsync("f1", CancellationToken.None))!.TransferState);
        Assert.Equal(TransferItemState.Cancelled,
            (await store.GetItemAsync("f2", CancellationToken.None))!.TransferState);
        Assert.Equal(TransferItemState.Completed,
            (await store.GetItemAsync("f3", CancellationToken.None))!.TransferState);
        Assert.Equal(TransferItemState.Failed,
            (await store.GetItemAsync("f4", CancellationToken.None))!.TransferState);
    }

    [Fact]
    public async Task RunDeltaPagePersistsCheckpointStateAndPageMarker()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        await store.ApplyFinalDeltaPageAsync("scan-1", [Item("f1")], "https://opaque/delta",
            CancellationToken.None);

        await store.ApplyRunDeltaPageAsync("run-1", [Item("f2")], "https://opaque/next2",
            DeltaCheckpointState.ReconciliationInProgress, CancellationToken.None);

        var checkpoint = await store.GetDeltaCheckpointRecordAsync(CancellationToken.None);
        Assert.Equal("https://opaque/next2", checkpoint!.Checkpoint);
        Assert.Equal(DeltaCheckpointState.ReconciliationInProgress, checkpoint.State);

        var item = await store.GetItemAsync("f2", CancellationToken.None);
        Assert.Equal("run-1", item!.ScanId);
    }

    [Fact]
    public async Task FreshReenumerationMarksUntouchedItemsDeletedWithoutTouchingState()
    {
        var store = await CreateOpenStoreAsync();
        await store.BeginScanAsync(Scan("scan-1"), CancellationToken.None);
        await store.ApplyFinalDeltaPageAsync("scan-1",
        [
            Item("root", null, "root", ItemFacetClassification.Folder, null),
            Item("f1", "root", "kept.txt"),
            Item("f2", "root", "gone.txt"),
        ], "https://opaque/delta", CancellationToken.None);

        await store.MarkItemVerifiedAsync("f2", "sha", CancellationToken.None);
        await store.MarkItemCompletedAsync("f2", TimestampPreservationResult.Preserved, CancellationToken.None);

        // Fresh enumeration touches only root and f1 with the run marker.
        await store.ApplyRunDeltaPageAsync("run-9",
        [
            Item("root", null, "root", ItemFacetClassification.Folder, null),
            Item("f1", "root", "kept.txt"),
        ], "https://opaque/delta9", DeltaCheckpointState.FullReenumerationInProgress,
            CancellationToken.None);

        var marked = await store.MarkUntouchedItemsDeletedAsync("run-9", CancellationToken.None);

        Assert.Equal(1, marked);
        var gone = await store.GetItemAsync("f2", CancellationToken.None);
        Assert.Equal(ItemFacetClassification.DeletedSource, gone!.Classification);
        // Retained archive content keeps its completed outcome and local hash.
        Assert.Equal(TransferItemState.Completed, gone.TransferState);
        Assert.Equal("sha", gone.LocalSha256);

        var kept = await store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal(ItemFacetClassification.File, kept!.Classification);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(_databasePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
