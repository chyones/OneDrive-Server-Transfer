using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Transfer;

/// <summary>
/// Per-file transfer pipeline (contract sections 8 and 9). Streams one source file
/// through a fresh temporary download URL into a guard-resolved <c>.partial</c> file,
/// resumes only on valid 206 range metadata, restarts from byte zero otherwise,
/// verifies the content (size, source metadata stability, supported Microsoft source
/// hash when available, streaming local SHA-256), commits the final name, applies
/// source timestamps, and persists every state transition transactionally. One file's
/// failure never throws past this pipeline: it is recorded and surfaced as an outcome
/// so unrelated files continue when safe. Temporary URLs are never logged or
/// persisted; log statements use a short non-reversible item reference.
/// </summary>
public interface ITransferEngine
{
    /// <summary>
    /// Transfers one mapped file item end to end. <paramref name="allowOwnedReplacement" />
    /// is true only when the orchestrator proved the existing final-path content belongs
    /// to this same source item (a re-copy after a source content change); unrelated
    /// existing content is never overwritten.
    /// </summary>
    Task<FileTransferOutcome> TransferFileAsync(
        TransferItemRecord item,
        ResolvedDestination destination,
        bool allowOwnedReplacement,
        IProgress<long>? bytesWritten,
        CancellationToken cancellationToken);
}

public sealed class TransferEngine : ITransferEngine
{
    /// <summary>Application-owned suffix for the temporary partial file.</summary>
    public const string PartialSuffix = ".partial";

    private readonly ITransferStateStore _stateStore;
    private readonly IGraphMetadataClient _metadataClient;
    private readonly ITemporaryDownloadClient _downloadClient;
    private readonly DownloadRetryCoordinator _retryCoordinator;
    private readonly IHashingService _hashingService;
    private readonly IDestinationPathGuard _pathGuard;
    private readonly ILogger<TransferEngine> _logger;

    public TransferEngine(
        ITransferStateStore stateStore,
        IGraphMetadataClient metadataClient,
        ITemporaryDownloadClient downloadClient,
        DownloadRetryCoordinator retryCoordinator,
        IHashingService hashingService,
        IDestinationPathGuard pathGuard,
        ILogger<TransferEngine> logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _metadataClient = metadataClient ?? throw new ArgumentNullException(nameof(metadataClient));
        _downloadClient = downloadClient ?? throw new ArgumentNullException(nameof(downloadClient));
        _retryCoordinator = retryCoordinator ?? throw new ArgumentNullException(nameof(retryCoordinator));
        _hashingService = hashingService ?? throw new ArgumentNullException(nameof(hashingService));
        _pathGuard = pathGuard ?? throw new ArgumentNullException(nameof(pathGuard));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FileTransferOutcome> TransferFileAsync(
        TransferItemRecord item,
        ResolvedDestination destination,
        bool allowOwnedReplacement,
        IProgress<long>? bytesWritten,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(destination);

        if (item.MappedRelativePath is null || item.SizeBytes is not { } expectedSize)
        {
            // A file without a mapped path or a known size can never be verified.
            await _stateStore.SetItemStateAsync(item.SourceItemId, TransferItemState.Failed, cancellationToken)
                .ConfigureAwait(false);
            return FileTransferOutcome.Failed(FileTransferFailure.MetadataUnavailable);
        }

        var itemReference = ItemReference(item.SourceItemId);

        try
        {
            await _stateStore.SetItemStateAsync(item.SourceItemId, TransferItemState.Downloading, cancellationToken)
                .ConfigureAwait(false);

            var partialRelative = item.MappedRelativePath + PartialSuffix;
            var partialPath = _pathGuard.ResolveWritableContentPath(destination, partialRelative);
            var finalPath = _pathGuard.ResolveWritableContentPath(destination, item.MappedRelativePath);

            await DownloadWithRetryAsync(item, itemReference, partialPath,
                expectedSize, bytesWritten, cancellationToken).ConfigureAwait(false);

            var fresh = await ReReadAndVerifyStabilityAsync(item, expectedSize, cancellationToken)
                .ConfigureAwait(false);

            await VerifySourceHashAsync(item, fresh, partialPath, cancellationToken)
                .ConfigureAwait(false);

            // Crash-safe ordering: the verified state and local SHA-256 are durable
            // before the final path is committed, so a crash between replacement and
            // completion is recoverable without trusting unverified content.
            string localSha256;
            await using (var partialRead = OpenRead(partialPath))
            {
                localSha256 = await _hashingService.ComputeLocalSha256HexAsync(partialRead, cancellationToken)
                    .ConfigureAwait(false);
            }

            await _stateStore.MarkItemVerifiedAsync(item.SourceItemId, localSha256, cancellationToken)
                .ConfigureAwait(false);

            CommitFinal(destination, partialPath, finalPath, allowOwnedReplacement);

            var timestampResult = ApplyTimestamps(finalPath,
                fresh.CreatedUtc ?? item.CreatedUtc,
                fresh.LastModifiedUtc ?? item.LastModifiedUtc);

            await _stateStore.MarkItemCompletedAsync(item.SourceItemId, timestampResult, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "File transferred; itemRef={ItemReference}; bytes={Bytes}; timestamps={TimestampResult}",
                itemReference, expectedSize, timestampResult);
            return FileTransferOutcome.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The item stays in Downloading; the orchestrator records cancellation.
            throw;
        }
        catch (SourceChangedDuringTransferException exception)
        {
            _logger.LogInformation(
                "Source changed during transfer; itemRef={ItemReference}; the next reconciliation pass re-copies the current version",
                itemReference);
            await SafeSetFailedAsync(item.SourceItemId, exception);
            return FileTransferOutcome.Failed(FileTransferFailure.SourceChangedDuringTransfer);
        }
        catch (FinalPathConflictException exception)
        {
            _logger.LogWarning(
                "Final path holds unrelated content; itemRef={ItemReference}; the verified partial is preserved",
                itemReference);
            await SafeSetFailedAsync(item.SourceItemId, exception);
            return FileTransferOutcome.Failed(FileTransferFailure.FinalPathConflict);
        }
        catch (SourceHashMismatchException exception)
        {
            _logger.LogWarning("Source hash verification failed; itemRef={ItemReference}", itemReference);
            await SafeSetFailedAsync(item.SourceItemId, exception);
            return FileTransferOutcome.Failed(FileTransferFailure.SourceHashMismatch);
        }
        catch (TemporaryDownloadException exception)
        {
            _logger.LogWarning(
                "Download attempts exhausted; itemRef={ItemReference}; kind={Kind}; status={Status}",
                itemReference, exception.Kind, exception.StatusCode?.ToString() ?? "n/a");
            await SafeSetFailedAsync(item.SourceItemId, exception);
            return FileTransferOutcome.Failed(FileTransferFailure.DownloadExhausted);
        }
        catch (GraphRequestException exception)
        {
            _logger.LogWarning(
                "Graph metadata failure during transfer; itemRef={ItemReference}; status={Status}",
                itemReference, exception.StatusCode?.ToString() ?? "n/a");
            await SafeSetFailedAsync(item.SourceItemId, exception);
            return FileTransferOutcome.Failed(FileTransferFailure.DownloadExhausted);
        }
        catch (IOException exception)
        {
            _logger.LogWarning("Local storage failure during transfer; itemRef={ItemReference}", itemReference);
            await SafeSetFailedAsync(item.SourceItemId, exception);
            return FileTransferOutcome.Failed(FileTransferFailure.LocalStorageFailure);
        }
    }

    private async Task DownloadWithRetryAsync(
        TransferItemRecord item,
        string itemReference,
        string partialPath,
        long expectedSize,
        IProgress<long>? bytesWritten,
        CancellationToken cancellationToken)
    {
        await _retryCoordinator.ExecuteAsync(
            RetryCategory.TemporaryDownload,
            async attemptCt =>
            {
                // The per-file attempt budget is persisted, so a restart cannot create
                // an unbounded hidden retry loop. The budget is checked before the
                // attempt is counted: a refused attempt performs no work.
                var current = await _stateStore.GetItemAsync(item.SourceItemId, attemptCt)
                    .ConfigureAwait(false);
                if (current is null || current.AttemptCount >= DownloadRetryCoordinator.MaxAttempts)
                {
                    throw new TemporaryDownloadException(TemporaryDownloadFailureKind.Permanent, null);
                }

                await _stateStore.IncrementAttemptCountAsync(item.SourceItemId, attemptCt)
                    .ConfigureAwait(false);

                Directory.CreateDirectory(Path.GetDirectoryName(partialPath)!);
                await using var stream = new FileStream(
                    partialPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

                var partialLength = stream.Length;
                if (partialLength > expectedSize)
                {
                    // A partial longer than the expected size is unsafe: restart.
                    stream.SetLength(0);
                    partialLength = 0;
                }

                if (partialLength == expectedSize)
                {
                    // A complete partial from a previous attempt (or a crash before
                    // verification) needs no further download; verification decides.
                    return 0;
                }

                // Every attempt obtains a fresh temporary URL through Graph; expired
                // URLs are never retried. Graph retries stay with the Graph channel.
                var url = await _metadataClient
                    .GetTemporaryDownloadUrlAsync(item.DriveId, item.SourceItemId, attemptCt)
                    .ConfigureAwait(false);

                TemporaryDownloadResult result;
                try
                {
                    result = await _downloadClient.DownloadAsync(
                        url, itemReference, stream,
                        partialLength > 0 ? partialLength : null,
                        bytesWritten, attemptCt).ConfigureAwait(false);
                }
                catch (TemporaryDownloadException exception) when (
                    exception.Kind is TemporaryDownloadFailureKind.InvalidRangeMetadata
                        or TemporaryDownloadFailureKind.RangeNotSatisfiable)
                {
                    // Policy: revalidate the partial and restart from byte zero inside
                    // the remaining attempt budget.
                    stream.SetLength(0);
                    throw new TemporaryDownloadException(
                        TemporaryDownloadFailureKind.Transient,
                        exception.StatusCode, exception.RetryAfter, exception);
                }

                var total = (result.ResumedFromOffset ? partialLength : 0) + result.BytesWritten;
                if (total < expectedSize)
                {
                    // Incomplete response; the safe partial allows a range resume.
                    throw new TemporaryDownloadException(
                        TemporaryDownloadFailureKind.Transient, result.StatusCode,
                        innerException: new IOException(
                            $"Incomplete transfer: {total} of {expectedSize} bytes."));
                }

                if (total > expectedSize)
                {
                    // Bytes from a different source version must never be concatenated.
                    throw new TemporaryDownloadException(
                        TemporaryDownloadFailureKind.Permanent, result.StatusCode,
                        innerException: new IOException(
                            $"Transfer exceeded the expected size: {total} of {expectedSize} bytes."));
                }

                return 0;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Abstractions.DriveItemMetadata> ReReadAndVerifyStabilityAsync(
        TransferItemRecord item,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        var fresh = await _metadataClient
            .GetItemMetadataAsync(item.DriveId, item.SourceItemId, cancellationToken)
            .ConfigureAwait(false);

        var storedTag = item.CTag ?? item.ETag;
        var freshTag = fresh.CTag ?? fresh.ETag;

        var stable = !fresh.IsDeleted &&
                     !fresh.IsFolder &&
                     !fresh.IsPackage &&
                     fresh.SizeBytes == expectedSize &&
                     (storedTag is null || freshTag is null ||
                      string.Equals(storedTag, freshTag, StringComparison.Ordinal));

        if (!stable)
        {
            // The content changed during transfer: do not finalize the scanned
            // version. The reconciliation pass re-copies the latest source state.
            throw new SourceChangedDuringTransferException();
        }

        return fresh;
    }

    private async Task VerifySourceHashAsync(
        TransferItemRecord item,
        Abstractions.DriveItemMetadata fresh,
        string partialPath,
        CancellationToken cancellationToken)
    {
        // Prefer the freshest metadata hash; fall back to the scanned hash. When no
        // comparable Microsoft hash exists the file proceeds on metadata-and-size
        // verification only; source cryptographic verification is never claimed (the
        // record simply keeps a null source hash).
        var algorithm = fresh.SourceHashAlgorithm ?? item.SourceHashAlgorithm;
        var expected = fresh.SourceHashValue ?? item.SourceHashValue;
        if (algorithm is null || expected is null)
        {
            return;
        }

        bool verified;
        await using (var partialRead = OpenRead(partialPath))
        {
            verified = await _hashingService
                .VerifySourceHashAsync(partialRead, algorithm, expected, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!verified)
        {
            throw new SourceHashMismatchException();
        }
    }

    private void CommitFinal(
        ResolvedDestination destination,
        string partialPath,
        string finalPath,
        bool allowOwnedReplacement)
    {
        // Revalidate containment immediately before finalization.
        var validatedFinal = _pathGuard.ResolveWritableContentPath(
            destination, RelativeToContentRoot(destination, finalPath));
        var validatedPartial = _pathGuard.ResolveWritableContentPath(
            destination, RelativeToContentRoot(destination, partialPath));

        Directory.CreateDirectory(Path.GetDirectoryName(validatedFinal)!);

        if (File.Exists(validatedFinal) && !allowOwnedReplacement)
        {
            // Never overwrite unrelated local content; keep the verified partial.
            throw new FinalPathConflictException();
        }

        File.Move(validatedPartial, validatedFinal, overwrite: allowOwnedReplacement);
    }

    private static TimestampPreservationResult ApplyTimestamps(
        string finalPath,
        DateTimeOffset? createdUtc,
        DateTimeOffset? lastModifiedUtc) =>
        TimestampPreservation.ApplyToFile(finalPath, createdUtc, lastModifiedUtc);

    private static string RelativeToContentRoot(ResolvedDestination destination, string fullPath) =>
        Path.GetRelativePath(destination.ContentRootPath, fullPath);

    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

    /// <summary>Short non-reversible log reference; the raw item ID is never logged.</summary>
    internal static string ItemReference(string sourceItemId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceItemId)))[..8];

    private async Task SafeSetFailedAsync(string sourceItemId, Exception cause)
    {
        try
        {
            await _stateStore.SetItemStateAsync(sourceItemId, TransferItemState.Failed, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Failed state could not be persisted; error={Error}", exception.Message);
        }
    }

    private sealed class SourceChangedDuringTransferException : Exception;

    private sealed class SourceHashMismatchException : Exception;

    private sealed class FinalPathConflictException : Exception;
}
