namespace OneDriveServerTransfer.Scan;

/// <summary>Kind of one non-fatal scan warning.</summary>
public enum ScanWarningKind
{
    /// <summary>A source name required deterministic encoding or a collision suffix.</summary>
    PathAdjusted,

    /// <summary>An item cannot be mapped to a safe destination path and is marked failed.</summary>
    PathFailure,

    /// <summary>An item's parent chain could not be resolved; it is marked failed.</summary>
    UnresolvedParent,

    /// <summary>Destination free space does not exceed known bytes plus the 5 GiB reserve.</summary>
    InsufficientStorage,

    /// <summary>Destination permissions expose archive data broadly.</summary>
    BroadPermissionExposure
}

/// <summary>One non-fatal scan warning for the preflight summary.</summary>
public sealed record ScanWarning(ScanWarningKind Kind, string Message, string? ItemName);

/// <summary>One unsupported source item (package, external shortcut, or unknown facet).</summary>
public sealed record UnsupportedScanItem(string Name, string? SourcePath, string Classification);

/// <summary>
/// Structured result of a successful mandatory dry run (contract section 5). It is
/// in-memory data for the UI; it never claims that any content was copied.
/// </summary>
public sealed record ScanResult(
    string ScanId,
    long FileCount,
    long FolderCount,
    long EmptyFolderCount,
    long UnsupportedCount,
    long KnownSourceBytes,
    IReadOnlyList<UnsupportedScanItem> UnsupportedItems,
    IReadOnlyList<ScanWarning> Warnings,
    DateTimeOffset CompletedUtc);
