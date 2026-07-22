using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Transfer;

/// <summary>Terminal outcome of one file's transfer pipeline.</summary>
public enum FileTransferStatus
{
    /// <summary>Downloaded, verified, committed, and recorded.</summary>
    Completed,

    /// <summary>Recorded as failed; unrelated files continue when safe.</summary>
    Failed,

    /// <summary>Stopped by operator cancellation.</summary>
    Cancelled
}

/// <summary>Machine-readable failure classification for one failed file.</summary>
public enum FileTransferFailure
{
    None,
    DownloadExhausted,
    SourceChangedDuringTransfer,
    SourceHashMismatch,
    MetadataUnavailable,
    FinalPathConflict,
    LocalStorageFailure,
    Unexpected
}

/// <summary>Outcome of one file's transfer pipeline.</summary>
public sealed record FileTransferOutcome(FileTransferStatus Status, FileTransferFailure Failure)
{
    public static readonly FileTransferOutcome Completed =
        new(FileTransferStatus.Completed, FileTransferFailure.None);

    public static readonly FileTransferOutcome Cancelled =
        new(FileTransferStatus.Cancelled, FileTransferFailure.None);

    public static FileTransferOutcome Failed(FileTransferFailure failure) =>
        new(FileTransferStatus.Failed, failure);
}

/// <summary>Kind of one non-fatal run warning.</summary>
public enum TransferWarningKind
{
    /// <summary>A source timestamp could not be preserved; verified bytes are unaffected.</summary>
    TimestampPreservation,

    /// <summary>Free space fell below the reserve; new scheduling stopped safely.</summary>
    DiskReserveStop,

    /// <summary>The source did not stabilize within the bounded reconciliation passes.</summary>
    SourceUnstable,

    /// <summary>A supported source item was deleted before it could be copied.</summary>
    DeletedBeforeCopy,

    /// <summary>
    /// A renamed or moved item's verified local content could not be relocated safely
    /// and was retained at its previous path.
    /// </summary>
    RenameRelocationRetained
}

/// <summary>One non-fatal run warning for the final result.</summary>
public sealed record TransferWarning(TransferWarningKind Kind, string Message, string? ItemName);

/// <summary>
/// Structured result of one copy run. The terminal state follows the binding run-state
/// rules exactly; <see cref="TransferRunState.Incomplete" /> always means the local
/// archive is not complete.
/// </summary>
public sealed record TransferRunResult(
    string RunId,
    TransferRunState FinalState,
    long CompletedCount,
    long SkippedCount,
    long FailedCount,
    long UnsupportedCount,
    bool SourceStable,
    IReadOnlyList<TransferWarning> Warnings);
