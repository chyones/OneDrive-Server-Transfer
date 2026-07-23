using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Inventory;
using OneDriveServerTransfer.Reporting;
using OneDriveServerTransfer.Scan;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Tests.TestSupport;
using OneDriveServerTransfer.Transfer;
using OneDriveServerTransfer.Verification;

using OperatorIdentity = OneDriveServerTransfer.Abstractions.OperatorIdentity;

namespace OneDriveServerTransfer.Tests.Transfer;

/// <summary>
/// Verifies the copy orchestrator and run-state machine: preflight gates, fixed
/// concurrency, disk-reserve scheduling stop, cancellation, reconciliation passes,
/// rename/move/delete handling, 410 reset, crash recovery, and the exact terminal
/// run states.
/// </summary>
public class TransferOrchestratorTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string OperatorObjectId = "operator-1";

    private readonly TransferTestRig _rig = new();
    private readonly StubTemporaryDownloadClient _downloadClient = new();
    private readonly FakeGraphMetadataClient _metadataClient = new();
    private readonly FakeDeltaInventoryClient _deltaClient = new();
    private readonly FakeAuthenticationService _authenticationService = new();
    private readonly FakeScanService _scanService = new();
    private readonly ScriptedDriveSpaceProvider _spaceProvider = new();
    private readonly HashingService _hashing = new();
    private readonly FakeReportWriter _reportWriter = new();
    private readonly RunReportLogSink _runLogSink = new();

    public TransferOrchestratorTests()
    {
        _authenticationService.SetSignedInOperator(new OperatorIdentity(
            OperatorObjectId, "operator@example.test", "Test Operator", TenantId));
        _scanService.IsCurrent = true;
        _spaceProvider.FreeBytes = long.MaxValue / 4; // ample space by default
    }

    private async Task SetupStoreAsync()
    {
        await _rig.OpenStoreAsync();
        await _rig.AddDriveRootAsync();
    }

    private ResolvedEmployeeSource Source() =>
        new(TenantId, "employee-1", "employee@example.test", "Employee One",
            TransferTestRig.DriveId, "business", "Employee One",
            "https://tenant-my.sharepoint.com/personal/employee", null, null, null,
            EmployeeSourceMode.Upn, IsTenantConfirmed: true);

    private TransferOrchestrator CreateOrchestrator() =>
        new(
            _authenticationService,
            new FakeDestinationBindingService(),
            _scanService,
            _rig.Store,
            _deltaClient,
            CreateEngine(),
            new SqlitePathCollisionRegistry(new SqliteTransferStateSchemaInitializer()),
            new PathMapperV1(new InMemoryPathCollisionRegistry()),
            new DestinationPathGuard(NullLogger<DestinationPathGuard>.Instance),
            new DestinationCapacityService(_spaceProvider),
            _hashing,
            _reportWriter,
            _runLogSink,
            new CapturingLogger<TransferOrchestrator>());

    private TransferEngine CreateEngine() =>
        new(_rig.Store, _metadataClient, _downloadClient,
            new DownloadRetryCoordinator(
                NullLogger<DownloadRetryCoordinator>.Instance,
                (_, _) => Task.CompletedTask,
                () => 0.0),
            _hashing,
            new DestinationPathGuard(NullLogger<DestinationPathGuard>.Instance),
            new CapturingLogger<TransferEngine>());

    private DestinationSession Session() => DestinationSessionFactory.Create(_rig.RootPath);

    /// <summary>Routes metadata re-reads to the current stored record.</summary>
    private void StubMetadataFromStore() =>
        _metadataClient.ItemHandler = (drive, id) =>
            FakeGraphMetadataClient.MetadataFor(
                _rig.Store.GetItemAsync(id, CancellationToken.None).GetAwaiter().GetResult()!);

    /// <summary>Serves one fixed content for every download request of any item.</summary>
    private void ServeContent(byte[] content) =>
        _downloadClient.Router = async (reference, offset, destination, ct) =>
        {
            var slice = offset is > 0 ? content[(int)offset.Value..] : content;
            if (offset is > 0)
            {
                destination.Position = offset.Value;
            }

            await destination.WriteAsync(slice, ct);
            return new TemporaryDownloadResult(slice.Length, offset is > 0, offset is > 0 ? 206 : 200,
                content.Length);
        };

    /// <summary>Queues empty final reconciliation pages (stable source).</summary>
    private void StubStableDelta(int pages = 4)
    {
        for (var i = 0; i < pages; i++)
        {
            _deltaClient.EnqueuePage(new DeltaInventoryPage([], NextLink: null,
                DeltaLink: $"https://opaque/delta-{Guid.NewGuid():N}"));
        }
    }

    private static string RefOf(string itemId) => TransferEngine.ItemReference(itemId);

    [Fact]
    public async Task CopyWithoutCurrentScanFailsTheRun()
    {
        await SetupStoreAsync();
        _scanService.IsCurrent = false;

        var exception = await Assert.ThrowsAsync<TransferException>(() =>
            CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None));

        Assert.Equal(TransferErrorCodes.ScanNotCurrent, exception.ReferenceCode);
        var run = await _rig.Store.GetLatestRunAsync(CancellationToken.None);
        Assert.Equal(TransferRunState.Failed, run!.FinalState);
    }

    [Fact]
    public async Task CopyWithoutSignedInOperatorIsRejectedBeforeRunCreation()
    {
        await SetupStoreAsync();
        _authenticationService.GetCurrentOperatorHandler = _ => Task.FromResult<OperatorIdentity?>(null);

        var exception = await Assert.ThrowsAsync<TransferException>(() =>
            CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None));

        Assert.Equal(TransferErrorCodes.OperatorSessionRequired, exception.ReferenceCode);
        Assert.Null(await _rig.Store.GetLatestRunAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CopyWithForeignTenantOperatorIsRejected()
    {
        await SetupStoreAsync();
        _authenticationService.SetSignedInOperator(new OperatorIdentity(
            OperatorObjectId, "operator@example.test", "Test Operator", "other-tenant"));

        var exception = await Assert.ThrowsAsync<TransferException>(() =>
            CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None));

        Assert.Equal(TransferErrorCodes.OperatorTenantMismatch, exception.ReferenceCode);
    }

    [Fact]
    public async Task PreflightStorageViolationFailsTheRun()
    {
        await SetupStoreAsync();
        await _rig.AddMappedFileAsync("f1", "a.bin", new byte[10]);
        _spaceProvider.FreeBytes = DestinationCapacityService.ReserveBytes; // never sufficient

        var exception = await Assert.ThrowsAsync<TransferException>(() =>
            CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None));

        Assert.Equal(TransferErrorCodes.InsufficientStorage, exception.ReferenceCode);
        var run = await _rig.Store.GetLatestRunAsync(CancellationToken.None);
        Assert.Equal(TransferRunState.Failed, run!.FinalState);
    }

    [Fact]
    public async Task HappyPathCompletesFilesAndFoldersWithStableSource()
    {
        await SetupStoreAsync();
        var content = "file content"u8.ToArray();
        var file = await _rig.AddMappedFileAsync("f1", "folder/a.txt", content);
        await AddMappedFolderAsync("d1", "folder");
        StubMetadataFromStore();
        ServeContent(content);
        StubStableDelta();

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);
        Assert.True(result.SourceStable);
        Assert.Equal(content, await File.ReadAllBytesAsync(_rig.ContentPath("folder/a.txt")));
        Assert.True(Directory.Exists(_rig.ContentPath("folder")));
        Assert.Equal(TransferItemState.Completed,
            (await _rig.Store.GetItemAsync("d1", CancellationToken.None))!.TransferState);

        var checkpoint = await _rig.Store.GetDeltaCheckpointRecordAsync(CancellationToken.None);
        Assert.Equal(DeltaCheckpointState.SourceStable, checkpoint!.State);
    }

    [Fact]
    public async Task DownloadsReachExactlyTheFixedConcurrencyOfThree()
    {
        await SetupStoreAsync();
        var content = new byte[128];
        for (var i = 0; i < 10; i++)
        {
            await _rig.AddMappedFileAsync($"f{i}", $"file{i}.bin", content);
        }

        StubMetadataFromStore();
        StubStableDelta();

        // Deterministic concurrency proof: every worker blocks inside the download
        // stub until three are simultaneously present. If the fixed cap were higher,
        // a fourth would enter; if it were lower, the gate would time out.
        var inside = 0;
        var maxInside = 0;
        var threeInside = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _downloadClient.Router = async (reference, offset, destination, ct) =>
        {
            var current = Interlocked.Increment(ref inside);
            int observed;
            while ((observed = maxInside) < current &&
                   Interlocked.CompareExchange(ref maxInside, current, observed) != observed)
            {
            }

            if (current == TransferOrchestrator.MaxConcurrentDownloads)
            {
                threeInside.TrySetResult();
            }

            try
            {
                await threeInside.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await destination.WriteAsync(content, ct);
                return new TemporaryDownloadResult(content.Length, false, 200, content.Length);
            }
            finally
            {
                Interlocked.Decrement(ref inside);
            }
        };

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);
        Assert.Equal(TransferOrchestrator.MaxConcurrentDownloads, maxInside);
    }

    [Fact]
    public async Task PerFileFailureDoesNotStopUnrelatedFiles()
    {
        await SetupStoreAsync();
        var content = "ok"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "bad.bin", content);
        await _rig.AddMappedFileAsync("f2", "good.bin", content);
        StubMetadataFromStore();
        StubStableDelta();

        _downloadClient.Router = (reference, offset, destination, ct) =>
            reference == RefOf("f1")
                ? Task.FromException<TemporaryDownloadResult>(
                    new TemporaryDownloadException(TemporaryDownloadFailureKind.Permanent, 400))
                : WriteResult(content, destination, ct);

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Incomplete, result.FinalState);
        Assert.Equal(TransferItemState.Failed,
            (await _rig.Store.GetItemAsync("f1", CancellationToken.None))!.TransferState);
        Assert.Equal(TransferItemState.Completed,
            (await _rig.Store.GetItemAsync("f2", CancellationToken.None))!.TransferState);
        Assert.True(File.Exists(_rig.ContentPath("good.bin")));
    }

    [Fact]
    public async Task DiskReserveViolationStopsSchedulingAndNeverCompletes()
    {
        await SetupStoreAsync();
        var content = new byte[10];
        await _rig.AddMappedFileAsync("f1", "a.bin", content);
        await _rig.AddMappedFileAsync("f2", "b.bin", content);
        StubMetadataFromStore();
        ServeContent(content);
        StubStableDelta();

        // Preflight passes; the per-file check passes for the first file and then
        // keeps violating the reserve for every later file.
        var reserve = DestinationCapacityService.ReserveBytes;
        _spaceProvider.Script(
            20 + reserve + 1,  // CheckTotal over the remaining 20 bytes
            10 + reserve + 1,  // CheckFile: first file
            reserve);          // every later check fails (sticky)

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Incomplete, result.FinalState);
        Assert.Contains(result.Warnings, w => w.Kind == TransferWarningKind.DiskReserveStop);
        Assert.Equal(TransferItemState.Mapped,
            (await _rig.Store.GetItemAsync("f2", CancellationToken.None))!.TransferState);
        // Completed work is preserved.
        Assert.True(File.Exists(_rig.ContentPath("a.bin")));
    }

    [Fact]
    public async Task CancellationPreservesCompletedFilesAndSafePartials()
    {
        await SetupStoreAsync();
        var doneContent = "already done"u8.ToArray();
        var done = await _rig.AddMappedFileAsync("f0", "done.bin", doneContent);
        await File.WriteAllBytesAsync(_rig.ContentPath("done.bin"), doneContent);
        var doneSha = await _hashing.ComputeLocalSha256HexAsync(
            new MemoryStream(doneContent), CancellationToken.None);
        await _rig.Store.MarkItemVerifiedAsync("f0", doneSha, CancellationToken.None);
        await _rig.Store.MarkItemCompletedAsync("f0", TimestampPreservationResult.Preserved,
            CancellationToken.None);

        var content = new byte[64];
        await _rig.AddMappedFileAsync("f1", "a.bin", content);
        await _rig.AddMappedFileAsync("f2", "b.bin", content);
        StubMetadataFromStore();

        using var cts = new CancellationTokenSource();
        var startedCount = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _downloadClient.Router = async (reference, offset, destination, ct) =>
        {
            if (Interlocked.Increment(ref startedCount) == 2)
            {
                bothStarted.TrySetResult();
            }

            await Task.Delay(Timeout.Infinite, ct); // blocks until cancellation
            throw new InvalidOperationException("unreachable");
        };

        var runTask = CreateOrchestrator().RunAsync(Source(), Session(), cts.Token);
        TransferRunResult result;
        try
        {
            await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            // Even if the gate never trips, the run is always cancelled: a regression
            // fails the test instead of hanging the run.
            cts.Cancel();
        }

        result = await runTask;

        Assert.Equal(TransferRunState.Cancelled, result.FinalState);
        // Completed work is preserved; in-flight items are cancelled, never failed.
        Assert.Equal(doneContent, await File.ReadAllBytesAsync(_rig.ContentPath("done.bin")));
        Assert.Equal(TransferItemState.Completed,
            (await _rig.Store.GetItemAsync("f0", CancellationToken.None))!.TransferState);
        Assert.Equal(TransferItemState.Cancelled,
            (await _rig.Store.GetItemAsync("f1", CancellationToken.None))!.TransferState);
        Assert.Equal(TransferItemState.Cancelled,
            (await _rig.Store.GetItemAsync("f2", CancellationToken.None))!.TransferState);
    }

    [Fact]
    public async Task ReconciliationRecopiesChangedContentOverOwnedArchiveFile()
    {
        await SetupStoreAsync();
        var oldContent = "old"u8.ToArray();
        var newContent = "new-version"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "a.txt", oldContent);
        StubMetadataFromStore();

        // Initial download serves the old version; the recopy serves the new one.
        var downloads = new Queue<byte[]>([oldContent, newContent]);
        _downloadClient.Router = async (reference, offset, destination, ct) =>
            await WriteResult(downloads.Dequeue(), destination, ct);

        _deltaClient.EnqueuePage(new DeltaInventoryPage(
            [DeltaItem("f1", "root", "a.txt", newContent.Length, "etag-v2", "ctag-v2")],
            NextLink: null, DeltaLink: "https://opaque/delta-v2"));
        StubStableDelta();

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);
        Assert.Equal(newContent, await File.ReadAllBytesAsync(_rig.ContentPath("a.txt")));
    }

    [Fact]
    public async Task UnstableSourceAfterThreePassesProducesIncomplete()
    {
        await SetupStoreAsync();
        var content = "x"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "a.txt", content);
        StubMetadataFromStore();
        ServeContent(content);

        for (var pass = 0; pass < 3; pass++)
        {
            _deltaClient.EnqueuePage(new DeltaInventoryPage(
                [DeltaItem("f1", "root", "a.txt", content.Length, $"etag-v{pass}", $"ctag-v{pass}")],
                NextLink: null, DeltaLink: $"https://opaque/delta-p{pass}"));
        }

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Incomplete, result.FinalState);
        Assert.False(result.SourceStable);
        Assert.Contains(result.Warnings, w => w.Kind == TransferWarningKind.SourceUnstable);
        var checkpoint = await _rig.Store.GetDeltaCheckpointRecordAsync(CancellationToken.None);
        Assert.Equal(DeltaCheckpointState.SourceUnstable, checkpoint!.State);
    }

    [Fact]
    public async Task RenameOfCompletedFileRelocatesVerifiedContentWithoutDuplicate()
    {
        await SetupStoreAsync();
        var content = "renamed"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "old.txt", content);
        StubMetadataFromStore();
        ServeContent(content);

        // Same identity, size, and change tag; only the name changed.
        _deltaClient.EnqueuePage(new DeltaInventoryPage(
            [DeltaItem("f1", "root", "new.txt", content.Length, "etag-f1", "ctag-f1")],
            NextLink: null, DeltaLink: "https://opaque/delta-ren"));
        StubStableDelta();

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);
        Assert.False(File.Exists(_rig.ContentPath("old.txt")));
        Assert.Equal(content, await File.ReadAllBytesAsync(_rig.ContentPath("new.txt")));
        var item = await _rig.Store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal("new.txt", item!.MappedRelativePath);
        Assert.Equal(TransferItemState.Completed, item.TransferState);
    }

    [Fact]
    public async Task FolderRenameRebuildsDescendantPathsWithoutDuplicates()
    {
        await SetupStoreAsync();
        var content = "child"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "oldfolder\\child.txt", content);
        await AddMappedFolderAsync("d1", "oldfolder");
        StubMetadataFromStore();
        ServeContent(content);

        // The folder was renamed at the source; the child does not appear in delta.
        _deltaClient.EnqueuePage(new DeltaInventoryPage(
            [new DeltaInventoryItem("d1", "root", "newfolder", null, DeltaItemFacet.Folder,
                null, null, null, null, null, null)],
            NextLink: null, DeltaLink: "https://opaque/delta-fold"));
        StubStableDelta();

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);
        var child = await _rig.Store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal("newfolder\\child.txt", child!.MappedRelativePath);
        Assert.Equal(TransferItemState.Completed, child.TransferState);
        var folder = await _rig.Store.GetItemAsync("d1", CancellationToken.None);
        Assert.Equal("newfolder", folder!.MappedRelativePath);

        // Mapped paths use Windows separators by design; the physical relocation is
        // verified on Windows (CI). State-level outcomes hold on every platform.
        if (OperatingSystem.IsWindows())
        {
            Assert.True(File.Exists(Path.Combine(
                _rig.Destination.ContentRootPath, "newfolder", "child.txt")));
            Assert.False(Directory.Exists(_rig.ContentPath("oldfolder")));
        }
    }

    [Fact]
    public async Task MoveOfCompletedFileRelocatesVerifiedContent()
    {
        await SetupStoreAsync();
        var content = "moved"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "a.txt", content);
        await AddMappedFolderAsync("d1", "sub");
        StubMetadataFromStore();
        ServeContent(content);

        // The file moved under the folder d1 (same identity, size, and tag).
        _deltaClient.EnqueuePage(new DeltaInventoryPage(
            [DeltaItem("f1", "d1", "a.txt", content.Length, "etag-f1", "ctag-f1")],
            NextLink: null, DeltaLink: "https://opaque/delta-move"));
        StubStableDelta();

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);
        Assert.False(File.Exists(_rig.ContentPath("a.txt")));
        var moved = await _rig.Store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal(TransferItemState.Completed, moved!.TransferState);
        Assert.True(File.Exists(Path.Combine(
            _rig.Destination.ContentRootPath, moved.MappedRelativePath!)));
        Assert.Equal("sub\\a.txt", moved.MappedRelativePath);
    }

    [Fact]
    public async Task SourceDeletionNeverDeletesCopiedLocalContent()
    {
        await SetupStoreAsync();
        var content = "archived"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "a.txt", content);
        StubMetadataFromStore();
        ServeContent(content);

        _deltaClient.EnqueuePage(new DeltaInventoryPage(
            [new DeltaInventoryItem("f1", "root", "a.txt", null, DeltaItemFacet.Deleted,
                null, null, null, null, null, null)],
            NextLink: null, DeltaLink: "https://opaque/delta-del"));
        StubStableDelta();

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);
        Assert.True(File.Exists(_rig.ContentPath("a.txt")));
        var tombstone = await _rig.Store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal(ItemFacetClassification.DeletedSource, tombstone!.Classification);
        Assert.Equal(TransferItemState.Completed, tombstone.TransferState);
    }

    [Fact]
    public async Task SourceDeletionOfFailedItemIsRecordedAndNeverCopied()
    {
        await SetupStoreAsync();
        var content = "data"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "gone.txt", content);
        StubMetadataFromStore();

        _downloadClient.Router = (reference, offset, destination, ct) =>
            Task.FromException<TemporaryDownloadResult>(
                new TemporaryDownloadException(TemporaryDownloadFailureKind.Permanent, 400));

        // The source deletes the item after the copy attempt failed.
        _deltaClient.EnqueuePage(new DeltaInventoryPage(
            [new DeltaInventoryItem("f1", "root", "gone.txt", null, DeltaItemFacet.Deleted,
                null, null, null, null, null, null)],
            NextLink: null, DeltaLink: "https://opaque/delta-del"));
        StubStableDelta();

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Incomplete, result.FinalState);
        var tombstone = await _rig.Store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal(ItemFacetClassification.DeletedSource, tombstone!.Classification);
        Assert.Equal(TransferItemState.Failed, tombstone.TransferState);
        Assert.False(File.Exists(_rig.ContentPath("gone.txt")));
        Assert.Contains(result.Warnings, w => w.Kind == TransferWarningKind.DeletedBeforeCopy);
    }

    [Fact]
    public async Task UnsupportedPackageInDeltaProducesIncomplete()
    {
        await SetupStoreAsync();
        _deltaClient.EnqueuePage(new DeltaInventoryPage(
            [new DeltaInventoryItem("pkg1", "root", "Notebook", null, DeltaItemFacet.Package,
                null, null, null, null, null, null)],
            NextLink: null, DeltaLink: "https://opaque/delta-pkg"));
        StubStableDelta();

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Incomplete, result.FinalState);
        Assert.Equal(TransferItemState.Unsupported,
            (await _rig.Store.GetItemAsync("pkg1", CancellationToken.None))!.TransferState);
    }

    [Fact]
    public async Task CheckpointResetStartsFreshEnumerationWithoutStateReset()
    {
        await SetupStoreAsync();
        var content = "archived"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "a.txt", content);
        StubMetadataFromStore();
        ServeContent(content);

        // Pass 1: the checkpoint is invalidated (410) before any page; the fresh
        // enumeration location returns the live inventory (root plus the file), and
        // later passes from the new delta link are stable.
        _deltaClient.Failure = new DeltaCheckpointResetException(
            new Uri("https://graph.example.test/fresh-enumeration"));
        _deltaClient.FailAfterPages = 0;
        _deltaClient.ClearFailureAfterThrow = true;
        _deltaClient.EnqueuePage(new DeltaInventoryPage(
            [
                new DeltaInventoryItem("root", null, "root", null, DeltaItemFacet.Folder,
                    null, null, null, null, null, null),
                DeltaItem("f1", "root", "a.txt", content.Length, "etag-f1", "ctag-f1"),
            ],
            NextLink: null, DeltaLink: "https://opaque/delta-fresh"));
        StubStableDelta();

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);
        Assert.True(File.Exists(_rig.ContentPath("a.txt")));
        Assert.Equal(TransferItemState.Completed,
            (await _rig.Store.GetItemAsync("f1", CancellationToken.None))!.TransferState);
        // The opaque fresh-enumeration location was followed after the reset.
        Assert.Contains("https://graph.example.test/fresh-enumeration", _deltaClient.ResumeLinks);
    }

    [Fact]
    public async Task StaleRunAndInFlightItemsRecoverIdempotently()
    {
        await SetupStoreAsync();
        var content = "recovered"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "a.bin", content);
        StubMetadataFromStore();
        ServeContent(content);
        StubStableDelta();

        // Simulate a crash: an in-progress run and an in-flight download.
        await _rig.Store.BeginRunAsync(new TransferRunRecord(
            "crashed-run", TransferTestRig.DriveId, null, DateTimeOffset.UtcNow, null, null),
            CancellationToken.None);
        await _rig.Store.SetItemStateAsync("f1", TransferItemState.Downloading, CancellationToken.None);

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);
        Assert.Equal(content, await File.ReadAllBytesAsync(_rig.ContentPath("a.bin")));

        // Recovery is idempotent: a second run finds nothing left to repair.
        StubStableDelta();
        var second = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);
        Assert.Equal(TransferRunState.Completed, second.FinalState);
    }

    [Fact]
    public async Task VerifiedItemCrashRecoveryFinalizesOnlyHashProvenContent()
    {
        await SetupStoreAsync();
        var content = "verified bytes"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "a.bin", content);
        StubMetadataFromStore();
        StubStableDelta();

        // Simulate a crash between verification and commit: Verified state with the
        // recorded local SHA-256 and the partial still on disk.
        var partialPath = _rig.ContentPath("a.bin" + TransferEngine.PartialSuffix);
        await File.WriteAllBytesAsync(partialPath, content);
        var sha = await _hashing.ComputeLocalSha256HexAsync(new MemoryStream(content), CancellationToken.None);
        await _rig.Store.MarkItemVerifiedAsync("f1", sha, CancellationToken.None);

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);
        Assert.True(File.Exists(_rig.ContentPath("a.bin")));
        Assert.False(File.Exists(partialPath));
        Assert.Equal(0, _downloadClient.CallCount); // no re-download needed
    }

    [Fact]
    public async Task UnsupportedTimestampForcesCompletedWithWarnings()
    {
        await SetupStoreAsync();
        var content = "data"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "a.bin", content,
            created: new DateTimeOffset(100, 1, 1, 0, 0, 0, TimeSpan.Zero),
            modified: new DateTimeOffset(100, 1, 1, 0, 0, 0, TimeSpan.Zero));
        StubMetadataFromStore();
        ServeContent(content);
        StubStableDelta();

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        // Year-100 source timestamps cannot be represented as Windows file times; the
        // verified bytes are unaffected and only the warning remains.
        Assert.Equal(TransferRunState.CompletedWithWarnings, result.FinalState);
        Assert.Equal(TimestampPreservationResult.UnsupportedValue,
            (await _rig.Store.GetItemAsync("f1", CancellationToken.None))!.TimestampPreservation);
    }

    [Fact]
    public async Task RunFinalizationCreatesPerRunReportDirectoryAndGeneratesReport()
    {
        await SetupStoreAsync();
        var content = "file content"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "folder/a.txt", content);
        await AddMappedFolderAsync("d1", "folder");
        StubMetadataFromStore();
        ServeContent(content);
        StubStableDelta();

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);

        var created = Assert.Single(_reportWriter.CreatedDirectories);
        Assert.Equal(_rig.RootPath, created.DestinationRoot);
        Assert.Equal(result.RunId, created.RunId);

        var reportDirectory = Path.Combine(
            _rig.Destination.StateRootPath, "Runs", result.RunId);
        Assert.True(Directory.Exists(reportDirectory));
        // The run log sink opened TransferLog.log at run start and closed it at run end.
        Assert.True(File.Exists(Path.Combine(reportDirectory, "TransferLog.log")));
        Assert.Null(_runLogSink.ActiveLogPath);

        var request = Assert.Single(_reportWriter.GenerationRequests);
        Assert.Equal(result.RunId, request.RunId);
        Assert.Equal("operator@example.test", request.OperatorUpn);
        Assert.Equal("employee@example.test", request.EmployeeUpn);
        Assert.Equal(EmployeeSourceMode.Upn.ToString(), request.SourceInputMode);
        Assert.Equal(1, request.ReconciliationPasses);
        Assert.Equal(0, request.StorageWarningCount);
    }

    [Fact]
    public async Task FailedRunStillGeneratesReport()
    {
        await SetupStoreAsync();
        await _rig.AddMappedFileAsync("f1", "a.bin", new byte[16]);
        _spaceProvider.FreeBytes = 0; // total-capacity precheck fails the run

        var exception = await Assert.ThrowsAsync<TransferException>(() =>
            CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None));

        Assert.Equal(TransferErrorCodes.InsufficientStorage, exception.ReferenceCode);
        var request = Assert.Single(_reportWriter.GenerationRequests);
        Assert.Equal(_reportWriter.CreatedDirectories.Single().RunId, request.RunId);
        Assert.Null(_runLogSink.ActiveLogPath);
    }

    [Fact]
    public async Task ReportGenerationFailureNeverChangesTheRunOutcome()
    {
        await SetupStoreAsync();
        var content = "file content"u8.ToArray();
        await _rig.AddMappedFileAsync("f1", "a.txt", content);
        StubMetadataFromStore();
        ServeContent(content);
        StubStableDelta();
        _reportWriter.GenerationFailure = new InvalidOperationException("disk full");

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), CancellationToken.None);

        Assert.Equal(TransferRunState.Completed, result.FinalState);
        Assert.Single(_reportWriter.GenerationRequests);
        Assert.Null(_runLogSink.ActiveLogPath);
    }

    [Fact]
    public async Task CancelledRunStillGeneratesReport()
    {
        await SetupStoreAsync();
        var content = new byte[64];
        await _rig.AddMappedFileAsync("f1", "a.bin", content);
        StubMetadataFromStore();
        using var cts = new CancellationTokenSource();
        _downloadClient.Router = async (reference, offset, destination, ct) =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return new TemporaryDownloadResult(0, false, 200, 0);
        };

        var result = await CreateOrchestrator().RunAsync(Source(), Session(), cts.Token);

        Assert.Equal(TransferRunState.Cancelled, result.FinalState);
        Assert.Single(_reportWriter.GenerationRequests);
        Assert.Null(_runLogSink.ActiveLogPath);
    }

    private static async Task<TemporaryDownloadResult> WriteResult(
        byte[] content, Stream destination, CancellationToken cancellationToken)
    {
        await destination.WriteAsync(content, cancellationToken);
        return new TemporaryDownloadResult(content.Length, false, 200, content.Length);
    }

    private async Task AddMappedFolderAsync(string itemId, string mappedRelativePath)
    {
        var item = new TransferItemRecord(
            TransferTestRig.DriveId, itemId, "root", mappedRelativePath, null, null,
            ItemFacetClassification.Folder, null, null, null, null, null, null, null, null,
            TransferItemState.Discovered, 0, TimestampPreservationResult.NotAttempted,
            null, DateTimeOffset.UtcNow);
        await _rig.Store.ApplyRunDeltaPageAsync("setup", [item], "https://opaque/setup",
            DeltaCheckpointState.DeltaCheckpointValid, CancellationToken.None);
        await _rig.Store.UpdateItemPathsAsync(itemId, mappedRelativePath, mappedRelativePath,
            TransferItemState.Mapped, CancellationToken.None);
    }

    private static DeltaInventoryItem DeltaItem(
        string id, string parent, string name, long size, string etag, string ctag) =>
        new(id, parent, name, size, DeltaItemFacet.File, etag, ctag,
            null, null, null, null);

    public void Dispose() => _rig.Dispose();

    /// <summary>Scan gate double; scan behavior itself is covered by slice-1 tests.</summary>
    private sealed class FakeScanService : IScanService
    {
        public bool IsCurrent { get; set; }

        public Task<ScanResult> ScanAsync(
            ResolvedEmployeeSource source, DestinationSession session, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsScanCurrentAsync(
            ResolvedEmployeeSource source, DestinationSession session, CancellationToken cancellationToken) =>
            Task.FromResult(IsCurrent);
    }

    /// <summary>
    /// Free-space double returning scripted values in call order; the last scripted
    /// value repeats so later checks stay deterministic.
    /// </summary>
    private sealed class ScriptedDriveSpaceProvider : IDriveSpaceProvider
    {
        private readonly Queue<long> _script = new();
        private long _last;

        public long FreeBytes { get; set; }

        public void Script(params long[] values)
        {
            foreach (var value in values)
            {
                _script.Enqueue(value);
            }
        }

        public long GetAvailableFreeSpaceBytes(string destinationRootPath)
        {
            if (_script.Count > 0)
            {
                _last = _script.Dequeue();
                return _last;
            }

            return _script.Count == 0 && _last == 0 ? FreeBytes : _last;
        }
    }
}
