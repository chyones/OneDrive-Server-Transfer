namespace OneDriveServerTransfer.Transfer;

/// <summary>
/// Point-in-time copy-run progress snapshot for the single-window UI (contract
/// section 12). Counts are advisory display data only; the operational state store
/// remains the source of truth and progress reporting never changes the run outcome.
/// Item names are employee content names; the snapshot never carries drive item IDs,
/// tenant IDs, URLs, or other protected identifiers. <see cref="TotalKnownBytes" />
/// is null while the total is unknown, so the UI shows indeterminate progress and
/// never fabricates a percentage.
/// </summary>
public sealed record TransferProgress(
    string Operation,
    string? CurrentItemName,
    string? ActivityMessage,
    long DiscoveredCount,
    long CompletedCount,
    long SkippedCount,
    long UnsupportedCount,
    long FailedCount,
    long? TotalKnownBytes,
    long DownloadedBytes);
