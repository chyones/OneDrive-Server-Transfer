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
