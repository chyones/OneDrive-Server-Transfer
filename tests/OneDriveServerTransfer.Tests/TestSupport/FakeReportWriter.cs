using OneDriveServerTransfer.Abstractions;

namespace OneDriveServerTransfer.Tests.TestSupport;

/// <summary>
/// Report-writer double for orchestration tests. Creates real per-run directories so
/// the run log sink can open its file, and records every generation request so tests
/// can assert the finalization wiring without generating report files.
/// </summary>
internal sealed class FakeReportWriter : IReportWriter
{
    public List<(string DestinationRoot, string RunId)> CreatedDirectories { get; } = [];

    public List<RunReportRequest> GenerationRequests { get; } = [];

    /// <summary>When set, generation throws this exception (best-effort path test).</summary>
    public Exception? GenerationFailure { get; set; }

    public Task<string> CreateRunReportDirectoryAsync(
        string destinationRoot,
        string runId,
        CancellationToken cancellationToken)
    {
        CreatedDirectories.Add((destinationRoot, runId));
        var path = Path.Combine(destinationRoot, "_TransferReport", "Runs", runId);
        Directory.CreateDirectory(path);
        return Task.FromResult(path);
    }

    public Task<RunReportResult> GenerateRunReportAsync(
        RunReportRequest request,
        CancellationToken cancellationToken)
    {
        GenerationRequests.Add(request);
        if (GenerationFailure is not null)
        {
            throw GenerationFailure;
        }

        var directory = Path.Combine(request.DestinationRootPath, "_TransferReport", "Runs", request.RunId);
        return Task.FromResult(new RunReportResult(
            request.RunId,
            directory,
            Path.Combine(directory, "TransferSummary.json"),
            Path.Combine(directory, "TransferReport.csv"),
            Path.Combine(directory, "FailedFiles.csv"),
            Path.Combine(directory, "TransferLog.log")));
    }
}
