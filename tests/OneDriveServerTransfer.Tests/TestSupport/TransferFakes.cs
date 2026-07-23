using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Tests.TestSupport;

/// <summary>
/// Programmable download-client double. Each queued handler receives the requested
/// resume offset and destination stream and either writes bytes or throws. The URL is
/// intentionally never asserted: the engine must not depend on its value.
/// </summary>
internal sealed class StubTemporaryDownloadClient : ITemporaryDownloadClient
{
    private readonly Queue<Func<long?, Stream, CancellationToken, Task<TemporaryDownloadResult>>> _handlers = new();

    public List<long?> RequestedOffsets { get; } = [];

    public int CallCount => RequestedOffsets.Count;

    /// <summary>
    /// When set, takes precedence over the queued handlers and routes by the
    /// caller-provided item reference, so concurrent transfers stay deterministic.
    /// </summary>
    public Func<string, long?, Stream, CancellationToken, Task<TemporaryDownloadResult>>? Router { get; set; }

    public void EnqueueWrite(byte[] content, bool resumed = false) =>
        _handlers.Enqueue(async (offset, destination, ct) =>
        {
            await destination.WriteAsync(content, ct);
            return new TemporaryDownloadResult(content.Length, resumed, resumed ? 206 : 200, content.Length);
        });

    public void Enqueue(Exception exception) =>
        _handlers.Enqueue((offset, destination, ct) => Task.FromException<TemporaryDownloadResult>(exception));

    public void EnqueueHandler(Func<long?, Stream, CancellationToken, Task<TemporaryDownloadResult>> handler) =>
        _handlers.Enqueue(handler);

    public async Task<TemporaryDownloadResult> DownloadAsync(
        Uri temporaryDownloadUrl,
        string itemReference,
        Stream destination,
        long? resumeOffsetBytes,
        IProgress<long>? bytesWritten,
        CancellationToken cancellationToken)
    {
        RequestedOffsets.Add(resumeOffsetBytes);
        if (Router is not null)
        {
            return await Router(itemReference, resumeOffsetBytes, destination, cancellationToken);
        }

        if (_handlers.Count == 0)
        {
            throw new InvalidOperationException("No stubbed download behavior remains.");
        }

        return await _handlers.Dequeue()(resumeOffsetBytes, destination, cancellationToken);
    }
}

/// <summary>Programmable Graph metadata double for transfer tests.</summary>
internal sealed class FakeGraphMetadataClient : IGraphMetadataClient
{
    public List<DriveItemMetadata> ItemReads { get; } = [];

    public Func<string, string, DriveItemMetadata>? ItemHandler { get; set; }

    public int UrlFetchCount { get; private set; }

    public async IAsyncEnumerable<DriveItemMetadata> EnumerateDriveDeltaAsync(
        string driveId,
        string? deltaCheckpoint,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<DriveItemMetadata> GetItemMetadataAsync(
        string driveId, string itemId, CancellationToken cancellationToken)
    {
        if (ItemHandler is null)
        {
            throw new InvalidOperationException("ItemHandler not configured.");
        }

        var metadata = ItemHandler(driveId, itemId);
        ItemReads.Add(metadata);
        return Task.FromResult(metadata);
    }

    public Task<Uri> GetTemporaryDownloadUrlAsync(
        string driveId, string itemId, CancellationToken cancellationToken)
    {
        UrlFetchCount++;
        return Task.FromResult(new Uri($"https://download.example.test/{itemId}/{UrlFetchCount}"));
    }

    public static DriveItemMetadata MetadataFor(TransferItemRecord item, string? overrideTag = null) =>
        new(item.SourceItemId, item.ParentItemId, item.ItemName, item.SizeBytes,
            false, false, false,
            overrideTag ?? item.ETag, overrideTag is null ? item.CTag : null,
            item.CreatedUtc, item.LastModifiedUtc,
            item.SourceHashAlgorithm, item.SourceHashValue);
}

/// <summary>Creates a bound temp destination with a real state store for transfer tests.</summary>
internal sealed class TransferTestRig : IDisposable
{
    public TransferTestRig()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"odst-trf-{Guid.NewGuid():N}");
        Destination = new ResolvedDestination(RootPath);
        Directory.CreateDirectory(Destination.ContentRootPath);
        Directory.CreateDirectory(Destination.StateRootPath);
    }

    public string RootPath { get; }

    public ResolvedDestination Destination { get; }

    public SqliteTransferStateStore Store { get; private set; } = null!;

    public const string DriveId = "drive-test-1";

    public async Task<SqliteTransferStateStore> OpenStoreAsync()
    {
        var bindingStore = new SqliteDestinationBindingStore(
            new SqliteTransferStateSchemaInitializer(),
            new CapturingLogger<SqliteDestinationBindingStore>());
        await bindingStore.CreateBindingAsync(
            Destination.StateDatabasePath,
            new StoredDestinationBinding(
                "tenant-1", DriveId, "employee-1", "employee@example.test",
                "operator-1", "operator@example.test", DateTimeOffset.UtcNow),
            CancellationToken.None);

        var store = new SqliteTransferStateStore(
            new SqliteTransferStateSchemaInitializer(),
            new CapturingLogger<SqliteTransferStateStore>());
        await store.OpenAsync(Destination.StateDatabasePath, CancellationToken.None);
        Store = store;
        return store;
    }

    /// <summary>Adds the drive root item, recorded as the skipped archive container.</summary>
    public async Task AddDriveRootAsync()
    {
        var root = new TransferItemRecord(
            DriveId, "root", null, "root", null, null,
            ItemFacetClassification.Folder, null, null, null, null, null, null, null, null,
            TransferItemState.Discovered, 0, TimestampPreservationResult.NotAttempted,
            null, DateTimeOffset.UtcNow);
        await Store.ApplyRunDeltaPageAsync("setup", [root], "https://opaque/setup",
            DeltaCheckpointState.DeltaCheckpointValid, CancellationToken.None);
        await Store.UpdateItemPathsAsync("root", string.Empty, string.Empty,
            TransferItemState.Skipped, CancellationToken.None);
    }

    /// <summary>Adds one file item in the Mapped state with the given mapped path.</summary>
    public async Task<TransferItemRecord> AddMappedFileAsync(
        string itemId,
        string mappedRelativePath,
        byte[]? content,
        string? hashAlgorithm = null,
        string? hashValue = null,
        DateTimeOffset? created = null,
        DateTimeOffset? modified = null)
    {
        var item = new TransferItemRecord(
            DriveId, itemId, "root", Path.GetFileName(mappedRelativePath), null, null,
            ItemFacetClassification.File, $"etag-{itemId}", $"ctag-{itemId}",
            content?.Length ?? 0,
            created, modified,
            hashAlgorithm, hashValue, null,
            TransferItemState.Discovered, 0, TimestampPreservationResult.NotAttempted,
            null, DateTimeOffset.UtcNow);
        await Store.ApplyRunDeltaPageAsync("setup", [item], "https://opaque/setup",
            DeltaCheckpointState.DeltaCheckpointValid, CancellationToken.None);
        await Store.UpdateItemPathsAsync(itemId, mappedRelativePath, mappedRelativePath,
            TransferItemState.Mapped, CancellationToken.None);
        return (await Store.GetItemAsync(itemId, CancellationToken.None))!;
    }

    public string ContentPath(string mappedRelativePath) =>
        Path.Combine(Destination.ContentRootPath, mappedRelativePath);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
