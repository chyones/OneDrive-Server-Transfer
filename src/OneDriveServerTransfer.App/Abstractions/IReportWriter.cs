namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Per-run audit report writer. Reports live under
/// _TransferReport/Runs/&lt;RunId&gt; and never overwrite or append to another run's
/// files. Reports are human-readable audit output that follows docs/REPORT_SCHEMA.md;
/// they never contain secrets, tokens, temporary download URLs, employee passwords, or
/// raw Graph responses.
/// </summary>
public interface IReportWriter
{
    /// <summary>
    /// Creates the unique report directory for one run. Fails clearly when the
    /// directory already exists: a later run must never overwrite or append to another
    /// run's files.
    /// </summary>
    Task<string> CreateRunReportDirectoryAsync(
        string destinationRoot,
        string runId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Generates the full report set (TransferSummary.json, TransferReport.csv,
    /// FailedFiles.csv) for one run that reached a terminal state, reading item, run,
    /// and scan records from the operational state store. Fails clearly when the run
    /// is unknown or not terminal, or when any report file already exists.
    /// </summary>
    Task<RunReportResult> GenerateRunReportAsync(
        RunReportRequest request,
        CancellationToken cancellationToken);
}
