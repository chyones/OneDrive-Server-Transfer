namespace OneDriveServerTransfer.State;

/// <summary>
/// Approved run states defined by the binding contract. Unsupported or missing supported
/// content must produce <see cref="Incomplete" /> and can never be reported as
/// <see cref="Completed" /> or <see cref="CompletedWithWarnings" />.
/// </summary>
public enum TransferRunState
{
    InProgress,
    Completed,
    CompletedWithWarnings,
    Incomplete,
    Failed,
    Cancelled,
    Interrupted
}
