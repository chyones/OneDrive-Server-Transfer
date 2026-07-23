namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Everything the report writer needs beyond the operational state store, supplied by
/// the copy-run orchestration at run finalization. Values are display and audit data
/// only; the state store remains the operational source. <see cref="EmployeeUpn" /> is
/// null for URL-mode sources, which cannot reliably yield a UPN from the approved
/// endpoints.
/// </summary>
public sealed record RunReportRequest(
    string RunId,
    string DestinationRootPath,
    string DestinationDisplayPath,
    string OperatorUpn,
    string? EmployeeUpn,
    string SourceInputMode,
    int ReconciliationPasses,
    int StorageWarningCount);

/// <summary>
/// The generated per-run audit report set under
/// <c>SelectedDestination\_TransferReport\Runs\&lt;RunId&gt;</c>. Paths are returned so
/// the shell can offer Open Report for the exact run that produced them.
/// </summary>
public sealed record RunReportResult(
    string RunId,
    string ReportDirectoryPath,
    string SummaryJsonPath,
    string ItemsCsvPath,
    string FailedItemsCsvPath,
    string LogPath);
