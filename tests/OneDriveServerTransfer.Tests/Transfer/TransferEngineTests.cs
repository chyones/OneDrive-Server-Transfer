using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Tests.TestSupport;
using OneDriveServerTransfer.Transfer;
using OneDriveServerTransfer.Verification;

namespace OneDriveServerTransfer.Tests.Transfer;

/// <summary>
/// Verifies the per-file transfer pipeline: .partial download and commit, range
/// resume and restart semantics, fresh-URL refetch, the five-attempt budget,
/// verification chain behavior, final-path conflict safety, and failure isolation.
/// </summary>
public class TransferEngineTests : IDisposable
{
    private readonly TransferTestRig _rig = new();
    private readonly StubTemporaryDownloadClient _downloadClient = new();
    private readonly FakeGraphMetadataClient _metadataClient = new();
    private readonly HashingService _hashing = new();

    private TransferEngine CreateEngine() =>
        new(_rig.Store, _metadataClient, _downloadClient,
            new DownloadRetryCoordinator(
                NullLogger<DownloadRetryCoordinator>.Instance,
                (_, _) => Task.CompletedTask,
                () => 0.0),
            _hashing,
            new DestinationPathGuard(NullLogger<DestinationPathGuard>.Instance),
            new CapturingLogger<TransferEngine>());

    private void StubStableMetadata(TransferItemRecord item) =>
        _metadataClient.ItemHandler = (drive, id) => FakeGraphMetadataClient.MetadataFor(item);

    [Fact]
    public async Task HappyPathCommitsVerifiedFinalFile()
    {
        await _rig.OpenStoreAsync();
        var content = "employee content"u8.ToArray();
        var quickXor = await _hashing.ComputeQuickXorHashBase64Async(new MemoryStream(content), CancellationToken.None);
        var item = await _rig.AddMappedFileAsync("f1", "docs/a.txt", content, "quickXorHash", quickXor,
            created: new DateTimeOffset(2020, 5, 1, 12, 0, 0, TimeSpan.Zero),
            modified: new DateTimeOffset(2021, 6, 2, 13, 30, 0, TimeSpan.Zero));
        StubStableMetadata(item);
        _downloadClient.EnqueueWrite(content);

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, allowOwnedReplacement: false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Completed, outcome.Status);
        var finalPath = _rig.ContentPath("docs/a.txt");
        Assert.True(File.Exists(finalPath));
        Assert.Equal(content, await File.ReadAllBytesAsync(finalPath));
        Assert.False(File.Exists(finalPath + TransferEngine.PartialSuffix));

        var persisted = await _rig.Store.GetItemAsync("f1", CancellationToken.None);
        Assert.Equal(TransferItemState.Completed, persisted!.TransferState);
        var expectedSha = await _hashing.ComputeLocalSha256HexAsync(new MemoryStream(content), CancellationToken.None);
        Assert.Equal(expectedSha, persisted.LocalSha256);
        Assert.Equal(TimestampPreservationResult.Preserved, persisted.TimestampPreservation);
        Assert.Equal(1, persisted.AttemptCount);

        // Source timestamps are preserved on the local file.
        Assert.Equal(new DateTimeOffset(2021, 6, 2, 13, 30, 0, TimeSpan.Zero).UtcDateTime,
            File.GetLastWriteTimeUtc(finalPath), precision: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ResumeAppendsThrough206WithMatchingRange()
    {
        await _rig.OpenStoreAsync();
        var content = "0123456789"u8.ToArray();
        var item = await _rig.AddMappedFileAsync("f1", "a.bin", content);
        StubStableMetadata(item);

        // A safe partial from a previous attempt exists on disk.
        var partialPath = _rig.ContentPath("a.bin" + TransferEngine.PartialSuffix);
        await File.WriteAllBytesAsync(partialPath, content[..5]);

        long? observedOffset = null;
        _downloadClient.EnqueueHandler(async (offset, destination, ct) =>
        {
            observedOffset = offset;
            destination.Position = offset!.Value;
            await destination.WriteAsync(content[5..], ct);
            return new TemporaryDownloadResult(content.Length - 5, true, 206, content.Length);
        });

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, allowOwnedReplacement: false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Completed, outcome.Status);
        Assert.Equal(5L, observedOffset);
        Assert.Equal(content, await File.ReadAllBytesAsync(_rig.ContentPath("a.bin")));
        Assert.False(File.Exists(partialPath));
    }

    [Fact]
    public async Task InvalidRangeMetadataRestartsFromByteZero()
    {
        await _rig.OpenStoreAsync();
        var content = "0123456789"u8.ToArray();
        var item = await _rig.AddMappedFileAsync("f1", "a.bin", content);
        StubStableMetadata(item);
        await File.WriteAllBytesAsync(_rig.ContentPath("a.bin" + TransferEngine.PartialSuffix), content[..5]);

        _downloadClient.Enqueue(new TemporaryDownloadException(
            TemporaryDownloadFailureKind.InvalidRangeMetadata, 206));
        _downloadClient.EnqueueWrite(content);

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, allowOwnedReplacement: false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Completed, outcome.Status);
        Assert.Equal(content, await File.ReadAllBytesAsync(_rig.ContentPath("a.bin")));
        // First attempt requested a resume; the restart attempt requested a full GET.
        Assert.Equal(5L, _downloadClient.RequestedOffsets[0]);
        Assert.Null(_downloadClient.RequestedOffsets[1]);
    }

    [Fact]
    public async Task RangeNotSatisfiableRevalidatesAndRestartsFromZero()
    {
        await _rig.OpenStoreAsync();
        var content = "0123456789"u8.ToArray();
        var item = await _rig.AddMappedFileAsync("f1", "a.bin", content);
        StubStableMetadata(item);
        await File.WriteAllBytesAsync(_rig.ContentPath("a.bin" + TransferEngine.PartialSuffix), content[..5]);

        _downloadClient.Enqueue(new TemporaryDownloadException(
            TemporaryDownloadFailureKind.RangeNotSatisfiable, 416));
        _downloadClient.EnqueueWrite(content);

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, allowOwnedReplacement: false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Completed, outcome.Status);
        Assert.Equal(content, await File.ReadAllBytesAsync(_rig.ContentPath("a.bin")));
    }

    [Fact]
    public async Task ExpiredUrlTriggersFreshUrlAcquisitionOnNextAttempt()
    {
        await _rig.OpenStoreAsync();
        var content = "data"u8.ToArray();
        var item = await _rig.AddMappedFileAsync("f1", "a.bin", content);
        StubStableMetadata(item);

        _downloadClient.Enqueue(new TemporaryDownloadException(
            TemporaryDownloadFailureKind.Transient, 403)); // expired preauthenticated URL
        _downloadClient.EnqueueWrite(content);

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, allowOwnedReplacement: false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Completed, outcome.Status);
        Assert.Equal(2, _metadataClient.UrlFetchCount); // fresh URL per attempt
    }

    [Fact]
    public async Task AttemptBudgetExhaustionFailsOnlyThisFile()
    {
        await _rig.OpenStoreAsync();
        var content = "data"u8.ToArray();
        var failing = await _rig.AddMappedFileAsync("f1", "bad.bin", content);
        var succeeding = await _rig.AddMappedFileAsync("f2", "good.bin", content);
        StubStableMetadata(failing);
        _downloadClient.Enqueue(new TemporaryDownloadException(TemporaryDownloadFailureKind.Transient, 503));
        _downloadClient.Enqueue(new TemporaryDownloadException(TemporaryDownloadFailureKind.Transient, 503));
        _downloadClient.Enqueue(new TemporaryDownloadException(TemporaryDownloadFailureKind.Transient, 503));
        _downloadClient.Enqueue(new TemporaryDownloadException(TemporaryDownloadFailureKind.Transient, 503));
        _downloadClient.Enqueue(new TemporaryDownloadException(TemporaryDownloadFailureKind.Transient, 503));

        var engine = CreateEngine();
        var failedOutcome = await engine.TransferFileAsync(
            failing, _rig.Destination, false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Failed, failedOutcome.Status);
        Assert.Equal(FileTransferFailure.DownloadExhausted, failedOutcome.Failure);
        Assert.Equal(5, (await _rig.Store.GetItemAsync("f1", CancellationToken.None))!.AttemptCount);
        Assert.Equal(TransferItemState.Failed,
            (await _rig.Store.GetItemAsync("f1", CancellationToken.None))!.TransferState);

        // The unrelated file is unaffected.
        StubStableMetadata(succeeding);
        _downloadClient.EnqueueWrite(content);
        var okOutcome = await engine.TransferFileAsync(
            succeeding, _rig.Destination, false, null, CancellationToken.None);
        Assert.Equal(FileTransferStatus.Completed, okOutcome.Status);
    }

    [Fact]
    public async Task AttemptBudgetIsHonoredAcrossRestart()
    {
        await _rig.OpenStoreAsync();
        var content = "data"u8.ToArray();
        var item = await _rig.AddMappedFileAsync("f1", "a.bin", content);
        StubStableMetadata(item);

        // Four earlier attempts persisted before the crash.
        for (var i = 0; i < 4; i++)
        {
            await _rig.Store.IncrementAttemptCountAsync("f1", CancellationToken.None);
        }

        _downloadClient.Enqueue(new TemporaryDownloadException(TemporaryDownloadFailureKind.Transient, 503));
        _downloadClient.EnqueueWrite(content); // would succeed, but the budget is spent

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Failed, outcome.Status);
        Assert.Equal(5, (await _rig.Store.GetItemAsync("f1", CancellationToken.None))!.AttemptCount);
    }

    [Fact]
    public async Task SourceChangedDuringTransferIsNotFinalized()
    {
        await _rig.OpenStoreAsync();
        var content = "old version"u8.ToArray();
        var item = await _rig.AddMappedFileAsync("f1", "a.txt", content);
        // The re-read proves the source changed (new change tag).
        _metadataClient.ItemHandler = (drive, id) =>
            FakeGraphMetadataClient.MetadataFor(item, overrideTag: "etag-new-version");
        _downloadClient.EnqueueWrite(content);

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Failed, outcome.Status);
        Assert.Equal(FileTransferFailure.SourceChangedDuringTransfer, outcome.Failure);
        Assert.False(File.Exists(_rig.ContentPath("a.txt"))); // never finalized
        Assert.Equal(TransferItemState.Failed,
            (await _rig.Store.GetItemAsync("f1", CancellationToken.None))!.TransferState);
    }

    [Fact]
    public async Task SourceHashMismatchFailsTheItem()
    {
        await _rig.OpenStoreAsync();
        var content = "real content"u8.ToArray();
        var item = await _rig.AddMappedFileAsync(
            "f1", "a.txt", content, "quickXorHash", "AAAAAAAAAAAAAAAAAAAAAAAAAAA=");
        StubStableMetadata(item);
        _downloadClient.EnqueueWrite(content);

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Failed, outcome.Status);
        Assert.Equal(FileTransferFailure.SourceHashMismatch, outcome.Failure);
        Assert.False(File.Exists(_rig.ContentPath("a.txt")));
    }

    [Fact]
    public async Task MissingSourceHashCompletesWithoutClaimingVerification()
    {
        await _rig.OpenStoreAsync();
        var content = "content without hash"u8.ToArray();
        var item = await _rig.AddMappedFileAsync("f1", "a.txt", content);
        StubStableMetadata(item); // no source hash in scanned or fresh metadata
        _downloadClient.EnqueueWrite(content);

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Completed, outcome.Status);
        var persisted = await _rig.Store.GetItemAsync("f1", CancellationToken.None);
        Assert.Null(persisted!.SourceHashAlgorithm); // no source-hash claim recorded
        Assert.NotNull(persisted.LocalSha256);       // local SHA-256 recorded separately
    }

    [Fact]
    public async Task UnrelatedFinalPathContentIsNeverOverwritten()
    {
        await _rig.OpenStoreAsync();
        var content = "new content"u8.ToArray();
        var item = await _rig.AddMappedFileAsync("f1", "a.txt", content);
        StubStableMetadata(item);
        _downloadClient.EnqueueWrite(content);

        var finalPath = _rig.ContentPath("a.txt");
        await File.WriteAllTextAsync(finalPath, "unrelated local content");

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, allowOwnedReplacement: false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Failed, outcome.Status);
        Assert.Equal(FileTransferFailure.FinalPathConflict, outcome.Failure);
        Assert.Equal("unrelated local content", await File.ReadAllTextAsync(finalPath));
        // The verified partial is preserved for diagnosis, never adopted silently.
        Assert.True(File.Exists(finalPath + TransferEngine.PartialSuffix));
    }

    [Fact]
    public async Task OwnedReplacementIsAllowedForRecopyOfSameItem()
    {
        await _rig.OpenStoreAsync();
        var oldContent = "old version"u8.ToArray();
        var newContent = "new version"u8.ToArray();
        var item = await _rig.AddMappedFileAsync("f1", "a.txt", newContent);
        StubStableMetadata(item);
        _downloadClient.EnqueueWrite(newContent);

        var finalPath = _rig.ContentPath("a.txt");
        await File.WriteAllBytesAsync(finalPath, oldContent);

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, allowOwnedReplacement: true, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Completed, outcome.Status);
        Assert.Equal(newContent, await File.ReadAllBytesAsync(finalPath));
    }

    [Fact]
    public async Task OvershootResponseFailsPermanentlyWithoutConcatenation()
    {
        await _rig.OpenStoreAsync();
        var item = await _rig.AddMappedFileAsync("f1", "a.bin", new byte[4]);
        StubStableMetadata(item);
        _downloadClient.EnqueueWrite(new byte[10]); // more than the expected size

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Failed, outcome.Status);
        Assert.Equal(FileTransferFailure.DownloadExhausted, outcome.Failure);
        Assert.Equal(1, _downloadClient.CallCount); // permanent: no retry
    }

    [Fact]
    public async Task CancellationPropagatesAndKeepsPartial()
    {
        await _rig.OpenStoreAsync();
        var content = "0123456789"u8.ToArray();
        var item = await _rig.AddMappedFileAsync("f1", "a.bin", content);
        StubStableMetadata(item);

        using var cts = new CancellationTokenSource();
        _downloadClient.EnqueueHandler((offset, destination, ct) =>
        {
            cts.Cancel();
            return Task.FromException<TemporaryDownloadResult>(new OperationCanceledException(ct));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateEngine().TransferFileAsync(
            item, _rig.Destination, false, null, cts.Token));

        Assert.Equal(TransferItemState.Downloading,
            (await _rig.Store.GetItemAsync("f1", CancellationToken.None))!.TransferState);
    }

    [Fact]
    public async Task TemporaryUrlIsNeverPersistedInState()
    {
        await _rig.OpenStoreAsync();
        var content = "employee content"u8.ToArray();
        var item = await _rig.AddMappedFileAsync("f1", "a.txt", content);
        StubStableMetadata(item);
        _downloadClient.EnqueueWrite(content);

        var outcome = await CreateEngine().TransferFileAsync(
            item, _rig.Destination, allowOwnedReplacement: false, null, CancellationToken.None);

        Assert.Equal(FileTransferStatus.Completed, outcome.Status);
        Assert.True(_metadataClient.UrlFetchCount > 0);

        // The state database must never contain the temporary download URL.
        var databaseBytes = await File.ReadAllBytesAsync(_rig.Destination.StateDatabasePath);
        var databaseText = System.Text.Encoding.ASCII.GetString(databaseBytes);
        Assert.DoesNotContain("download.example.test", databaseText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TemporaryUrlIsNeverPersistedInStateAcrossRepeatedRuns()
    {
        // Stability proof for the Windows file-lock timing flake: the database byte
        // read immediately after a transfer must never race a retained connection
        // handle. Repeated with fresh rigs and no pool clearing between iterations.
        for (var iteration = 0; iteration < 10; iteration++)
        {
            using var rig = new TransferTestRig();
            var downloadClient = new StubTemporaryDownloadClient();
            var metadataClient = new FakeGraphMetadataClient();
            var hashing = new HashingService();

            await rig.OpenStoreAsync();
            var content = "employee content"u8.ToArray();
            var item = await rig.AddMappedFileAsync("f1", "a.txt", content);
            metadataClient.ItemHandler = (_, _) => FakeGraphMetadataClient.MetadataFor(item);
            downloadClient.EnqueueWrite(content);

            var engine = new TransferEngine(
                rig.Store, metadataClient, downloadClient,
                new DownloadRetryCoordinator(
                    NullLogger<DownloadRetryCoordinator>.Instance,
                    (_, _) => Task.CompletedTask,
                    () => 0.0),
                hashing,
                new DestinationPathGuard(NullLogger<DestinationPathGuard>.Instance),
                new CapturingLogger<TransferEngine>());

            var outcome = await engine.TransferFileAsync(
                item, rig.Destination, allowOwnedReplacement: false, null, CancellationToken.None);

            Assert.Equal(FileTransferStatus.Completed, outcome.Status);

            var databaseBytes = await File.ReadAllBytesAsync(rig.Destination.StateDatabasePath);
            var databaseText = System.Text.Encoding.ASCII.GetString(databaseBytes);
            Assert.DoesNotContain("download.example.test", databaseText, StringComparison.Ordinal);
        }
    }

    public void Dispose() => _rig.Dispose();
}
