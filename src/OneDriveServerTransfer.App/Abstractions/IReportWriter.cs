namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Per-run audit report writer. Implemented in milestone M6. Reports live under
/// _TransferReport/Runs/&lt;RunId&gt; and never overwrite or append to another run's
/// files. Reports are human-readable audit output that follows docs/REPORT_SCHEMA.md;
/// they never contain secrets, tokens, temporary download URLs, employee passwords, or
/// raw Graph responses.
/// </summary>
public interface IReportWriter
{
    Task<string> CreateRunReportDirectoryAsync(
        string destinationRoot,
        string runId,
        CancellationToken cancellationToken);
}
