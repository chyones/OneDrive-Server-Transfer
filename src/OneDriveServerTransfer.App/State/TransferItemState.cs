namespace OneDriveServerTransfer.State;

/// <summary>
/// Approved per-item transfer states defined by the binding contract.
/// </summary>
public enum TransferItemState
{
    Discovered,
    Mapped,
    Downloading,
    Verified,
    Completed,
    Skipped,
    Unsupported,
    Failed,
    Cancelled
}
