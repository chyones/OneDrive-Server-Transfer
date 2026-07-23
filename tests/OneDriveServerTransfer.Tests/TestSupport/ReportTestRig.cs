using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Reporting;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Tests.TestSupport;

/// <summary>
/// Creates a bound temp destination with a real state store plus deterministic
/// scan/run/item seeding helpers for report-generation tests. Items are seeded
/// through the real store transitions so every report row reflects operational state.
/// </summary>
internal sealed class ReportTestRig : IDisposable
{
    public const string DriveId = "drive-test-1";
    public const string OperatorUpn = "operator@example.test";
    public const string EmployeeUpn = "employee@example.test";

    public ReportTestRig()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"odst-rpt-{Guid.NewGuid():N}");
        Destination = new ResolvedDestination(RootPath);
        Directory.CreateDirectory(Destination.ContentRootPath);
        Directory.CreateDirectory(Destination.StateRootPath);
    }

    public string RootPath { get; }

    public ResolvedDestination Destination { get; }

    public SqliteTransferStateStore Store { get; private set; } = null!;

    public async Task<SqliteTransferStateStore> OpenStoreAsync()
    {
        var bindingStore = new SqliteDestinationBindingStore(
            new SqliteTransferStateSchemaInitializer(),
            NullLogger<SqliteDestinationBindingStore>.Instance);
        await bindingStore.CreateBindingAsync(
            Destination.StateDatabasePath,
            new StoredDestinationBinding(
                "tenant-1", DriveId, "employee-1", EmployeeUpn,
                "operator-1", OperatorUpn, DateTimeOffset.UtcNow),
            CancellationToken.None);

        var store = new SqliteTransferStateStore(
            new SqliteTransferStateSchemaInitializer(),
            NullLogger<SqliteTransferStateStore>.Instance);
        await store.OpenAsync(Destination.StateDatabasePath, CancellationToken.None);
        Store = store;
        return store;
    }

    public ReportWriter CreateWriter() =>
        new(Store, NullLogger<ReportWriter>.Instance);

    /// <summary>Runs a real scan lifecycle over the seeded items and finalizes it.</summary>
    public async Task<ScanRecord> SeedScanAsync(
        string scanId,
        IReadOnlyList<TransferItemRecord> items)
    {
        await Store.BeginScanAsync(
            new ScanRecord(scanId, "tenant-1", "employee-1", DriveId, RootPath,
                ScanState.InProgress, DateTimeOffset.UtcNow, null, 0, 0, 0, 0, 0),
            CancellationToken.None);
        await Store.ApplyFinalDeltaPageAsync(scanId, items, "https://opaque/delta",
            CancellationToken.None);
        return await Store.CompleteScanAsync(scanId, CancellationToken.None);
    }

    /// <summary>Creates a run and persists the given terminal state.</summary>
    public async Task<TransferRunRecord> SeedTerminalRunAsync(
        string runId,
        string? scanId,
        TransferRunState finalState)
    {
        await Store.BeginRunAsync(
            new TransferRunRecord(runId, DriveId, scanId, DateTimeOffset.UtcNow, null, null),
            CancellationToken.None);
        await Store.CompleteRunAsync(runId, finalState, CancellationToken.None);
        return (await Store.GetRunAsync(runId, CancellationToken.None))!;
    }

    /// <summary>The drive root row, recorded as the skipped archive container.</summary>
    public TransferItemRecord DriveRoot() =>
        Item("root", parentId: null, name: "root", classification: ItemFacetClassification.Folder,
            size: null);

    /// <summary>Deterministic item builder for page application.</summary>
    public TransferItemRecord Item(
        string id,
        string? parentId = "root",
        string name = "item",
        ItemFacetClassification classification = ItemFacetClassification.File,
        long? size = 10) =>
        new(DriveId, id, parentId, name, null, null, classification,
            $"etag-{id}", $"ctag-{id}", size,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            "quickXorHash", $"sourcehash-{id}", null,
            TransferItemState.Discovered, 0, TimestampPreservationResult.NotAttempted,
            null, DateTimeOffset.UtcNow);

    /// <summary>Marks a seeded file as downloaded, verified, and completed.</summary>
    public async Task CompleteFileAsync(
        string itemId,
        string sourcePath,
        string mappedRelativePath,
        string localSha256,
        TimestampPreservationResult timestampResult = TimestampPreservationResult.Preserved)
    {
        await Store.UpdateItemPathsAsync(itemId, sourcePath, mappedRelativePath,
            TransferItemState.Mapped, CancellationToken.None);
        await Store.IncrementAttemptCountAsync(itemId, CancellationToken.None);
        await Store.MarkItemVerifiedAsync(itemId, localSha256, CancellationToken.None);
        await Store.MarkItemCompletedAsync(itemId, timestampResult, CancellationToken.None);
    }

    /// <summary>Marks a seeded item failed at path mapping (no mapped path).</summary>
    public async Task FailAtMappingAsync(string itemId, string sourcePath) =>
        await Store.UpdateItemPathsAsync(itemId, sourcePath, null,
            TransferItemState.Failed, CancellationToken.None);

    /// <summary>Marks a seeded item failed after mapping (download-stage failure).</summary>
    public async Task FailAfterMappingAsync(string itemId, string sourcePath, string mappedRelativePath)
    {
        await Store.UpdateItemPathsAsync(itemId, sourcePath, mappedRelativePath,
            TransferItemState.Mapped, CancellationToken.None);
        await Store.SetItemStateAsync(itemId, TransferItemState.Failed, CancellationToken.None);
    }

    /// <summary>Records a seeded item as never-supported content.</summary>
    public async Task MarkUnsupportedAsync(string itemId, string sourcePath) =>
        await Store.UpdateItemPathsAsync(itemId, sourcePath, null,
            TransferItemState.Unsupported, CancellationToken.None);

    /// <summary>Records the drive root as the skipped archive container.</summary>
    public async Task SkipDriveRootAsync() =>
        await Store.UpdateItemPathsAsync("root", string.Empty, string.Empty,
            TransferItemState.Skipped, CancellationToken.None);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
