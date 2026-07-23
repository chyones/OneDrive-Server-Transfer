using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Reporting;

/// <summary>
/// Generates the per-run audit report set defined by docs/REPORT_SCHEMA.md from the
/// operational SQLite state (the reports are audit output only, never a resume
/// database). Every run gets its own directory under
/// <c>_TransferReport\Runs\&lt;RunId&gt;</c>; report files are created with
/// <see cref="FileMode.CreateNew" /> so a later run or a repeated generation can never
/// overwrite or append to another run's files.
///
/// Per-item error codes, error messages, and per-item start timestamps are not
/// persisted by state schema version 1, so those report columns are emitted empty
/// rather than fabricated; the sanitization path for error text is implemented and
/// tested so any future per-item error text passes through it.
/// </summary>
public sealed class ReportWriter : IReportWriter
{
    public const int ReportSchemaVersion = 1;
    public const string RunsDirectoryName = "Runs";
    public const string SummaryFileName = "TransferSummary.json";
    public const string ItemsFileName = "TransferReport.csv";
    public const string FailedItemsFileName = "FailedFiles.csv";
    public const string LogFileName = "TransferLog.log";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ITransferStateStore _stateStore;
    private readonly ILogger<ReportWriter> _logger;

    public ReportWriter(ITransferStateStore stateStore, ILogger<ReportWriter> logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>The canonical report directory of one run under the destination.</summary>
    public static string GetRunReportDirectoryPath(string destinationRoot, string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        ValidateRunId(runId);
        return Path.Combine(destinationRoot, ResolvedDestination.StateDirectoryName, RunsDirectoryName, runId);
    }

    public Task<string> CreateRunReportDirectoryAsync(
        string destinationRoot,
        string runId,
        CancellationToken cancellationToken)
    {
        var reportDirectory = GetRunReportDirectoryPath(destinationRoot, runId);
        if (Directory.Exists(reportDirectory))
        {
            // A later run must never overwrite or append to another run's files.
            throw new ReportException(
                ReportErrorCodes.ReportDirectoryExists,
                "A report directory already exists for this run identifier; report files are never overwritten.");
        }

        Directory.CreateDirectory(reportDirectory);
        return Task.FromResult(reportDirectory);
    }

    public async Task<RunReportResult> GenerateRunReportAsync(
        RunReportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var run = await _stateStore.GetRunAsync(request.RunId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            throw new ReportException(
                ReportErrorCodes.RunNotFound,
                "The run has no record in the operational state store.");
        }

        if (run.FinalState is null)
        {
            throw new ReportException(
                ReportErrorCodes.RunNotTerminal,
                "The run has not reached a terminal state, so no final report can be generated.");
        }

        var scan = run.ScanId is null
            ? null
            : await _stateStore.GetScanAsync(run.ScanId, cancellationToken).ConfigureAwait(false);
        var items = await _stateStore.GetAllItemsAsync(cancellationToken).ConfigureAwait(false);

        var reportDirectory = GetRunReportDirectoryPath(request.DestinationRootPath, run.RunId);
        if (!Directory.Exists(reportDirectory))
        {
            // Runs recorded before reporting existed have no directory yet; creating it
            // now can never overwrite another run's files because the path is unique.
            Directory.CreateDirectory(reportDirectory);
        }

        var rows = items.Select(item => BuildRow(run, request, item)).ToArray();
        var summaryPath = Path.Combine(reportDirectory, SummaryFileName);
        var itemsPath = Path.Combine(reportDirectory, ItemsFileName);
        var failedPath = Path.Combine(reportDirectory, FailedItemsFileName);

        await WriteCsvAsync(itemsPath, rows, cancellationToken).ConfigureAwait(false);
        var failedRows = rows
            .Where((_, index) => items[index].TransferState is
                TransferItemState.Failed or TransferItemState.Unsupported)
            .ToArray();
        await WriteCsvAsync(failedPath, failedRows, cancellationToken).ConfigureAwait(false);
        await WriteSummaryAsync(summaryPath, BuildSummary(run, request, scan, items), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Run report generated; run={RunIdReference}; state={FinalState}; items={ItemCount}",
            run.RunId, run.FinalState, items.Count);

        return new RunReportResult(
            run.RunId,
            reportDirectory,
            summaryPath,
            itemsPath,
            failedPath,
            Path.Combine(reportDirectory, LogFileName));
    }

    private static string?[] BuildRow(
        TransferRunRecord run,
        RunReportRequest request,
        TransferItemRecord item) =>
        BuildRow(run, request, item, errorCode: null, errorMessage: null);

    /// <summary>
    /// Builds one schema-version-1 row. State schema version 1 persists no per-item
    /// error code, message, or start timestamp, so the current callers leave them null
    /// and the columns stay empty instead of carrying fabricated values. Any supplied
    /// error message is sanitized before it can reach a report cell.
    /// </summary>
    internal static string?[] BuildRow(
        TransferRunRecord run,
        RunReportRequest request,
        TransferItemRecord item,
        string? errorCode,
        string? errorMessage) =>
    [
        ReportSchemaVersion.ToString(CultureInfo.InvariantCulture),
        run.RunId,
        request.OperatorUpn,
        request.EmployeeUpn ?? string.Empty,
        item.DriveId,
        item.SourceItemId,
        item.SourcePath ?? string.Empty,
        // Destination-relative under OneDriveData, never a full server path.
        item.MappedRelativePath ?? string.Empty,
        MapItemType(item.Classification),
        item.Classification is ItemFacetClassification.File
            ? item.SizeBytes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty,
        item.TransferState.ToString(),
        item.AttemptCount.ToString(CultureInfo.InvariantCulture),
        item.SourceHashAlgorithm ?? string.Empty,
        item.SourceHashValue ?? string.Empty,
        item.LocalSha256 ?? string.Empty,
        item.TimestampPreservation.ToString(),
        errorCode ?? string.Empty,
        ReportTextSanitizer.SanitizeErrorMessage(errorMessage),
        string.Empty,
        IsTerminal(item.TransferState) ? FormatUtc(item.UpdatedUtc) : string.Empty,
    ];

    private static TransferSummaryDto BuildSummary(
        TransferRunRecord run,
        RunReportRequest request,
        ScanRecord? scan,
        IReadOnlyList<TransferItemRecord> items)
    {
        var finalState = run.FinalState!.Value;
        return new TransferSummaryDto
        {
            ReportSchemaVersion = ReportWriter.ReportSchemaVersion,
            RunId = run.RunId,
            OperatorUPN = request.OperatorUpn,
            EmployeeUPN = request.EmployeeUpn,
            SourceInputMode = request.SourceInputMode,
            SourceDriveId = run.DriveId,
            DestinationDisplayPath = request.DestinationDisplayPath,
            ScanCompletedAtUtc = scan?.CompletedUtc,
            CopyStartedAtUtc = run.StartedUtc,
            CopyCompletedAtUtc = run.EndedUtc,
            DiscoveredCount = scan is null
                ? items.Count(item => item.Classification is not ItemFacetClassification.DeletedSource)
                : scan.FileCount + scan.FolderCount + scan.EmptyFolderCount + scan.UnsupportedCount,
            FileCount = scan?.FileCount
                ?? items.Count(item => item.Classification is ItemFacetClassification.File),
            FolderCount = scan is null
                ? items.Count(item => item.Classification is
                    ItemFacetClassification.Folder or ItemFacetClassification.EmptyFolder)
                : scan.FolderCount + scan.EmptyFolderCount,
            UnsupportedCount = items.Count(item => item.TransferState is TransferItemState.Unsupported),
            CompletedCount = items.Count(item => item.TransferState is TransferItemState.Completed),
            SkippedCount = items.Count(item => item.TransferState is TransferItemState.Skipped),
            FailedCount = items.Count(item => item.TransferState is TransferItemState.Failed),
            KnownSourceBytes = scan?.KnownBytes
                ?? items.Where(item => item.Classification is ItemFacetClassification.File)
                    .Sum(item => item.SizeBytes ?? 0),
            DownloadedBytes = items
                .Where(item => item.Classification is ItemFacetClassification.File &&
                               item.TransferState is TransferItemState.Completed)
                .Sum(item => item.SizeBytes ?? 0),
            PathWarningCount = CountPathWarnings(items),
            TimestampWarningCount = items.Count(item =>
                item.TimestampPreservation is TimestampPreservationResult.Failed
                    or TimestampPreservationResult.UnsupportedValue),
            StorageWarningCount = request.StorageWarningCount,
            ReconciliationPasses = request.ReconciliationPasses,
            FinalRunState = finalState.ToString(),
            IsArchiveComplete = finalState is TransferRunState.Completed
                or TransferRunState.CompletedWithWarnings,
        };
    }

    /// <summary>
    /// Deterministic path-warning count from operational state: items that failed
    /// during path mapping (failed with no mapped path) plus items whose mapped leaf
    /// name differs from the source name (a safe-name adjustment was applied).
    /// </summary>
    private static int CountPathWarnings(IReadOnlyList<TransferItemRecord> items)
    {
        var count = 0;
        foreach (var item in items)
        {
            if (item.Classification is ItemFacetClassification.DeletedSource)
            {
                continue;
            }

            if (item.TransferState is TransferItemState.Failed && item.MappedRelativePath is null)
            {
                count++;
                continue;
            }

            if (item.MappedRelativePath is { Length: > 0 } mapped &&
                !string.Equals(LeafName(mapped), item.ItemName, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static string LeafName(string mappedRelativePath)
    {
        var separator = mappedRelativePath.LastIndexOfAny(['\\', '/']);
        return separator < 0 ? mappedRelativePath : mappedRelativePath[(separator + 1)..];
    }

    private static string MapItemType(ItemFacetClassification classification) =>
        classification switch
        {
            ItemFacetClassification.File => "File",
            ItemFacetClassification.Folder => "Folder",
            ItemFacetClassification.EmptyFolder => "Folder",
            ItemFacetClassification.UnsupportedPackage => "Package",
            ItemFacetClassification.ExternalShortcut => "ExternalShortcut",
            ItemFacetClassification.Unknown => "Unknown",
            ItemFacetClassification.DeletedSource => "DeletedSource",
            _ => "Unknown",
        };

    private static bool IsTerminal(TransferItemState state) =>
        state is TransferItemState.Completed or TransferItemState.Skipped
            or TransferItemState.Unsupported or TransferItemState.Failed
            or TransferItemState.Cancelled;

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static void ValidateRunId(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            runId.IndexOfAny(['/', '\\']) >= 0 ||
            runId is "." or "..")
        {
            throw new ReportException(
                ReportErrorCodes.RunNotFound,
                "The run identifier is not a safe single path component.");
        }
    }

    private static async Task WriteCsvAsync(
        string path,
        IReadOnlyList<string?[]> rows,
        CancellationToken cancellationToken)
    {
        await using var writer = CreateReportFile(path);
        CsvReportWriter.WriteRow(writer, CsvReportWriter.ItemsHeader.Split(','));
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CsvReportWriter.WriteRow(writer, row);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteSummaryAsync(
        string path,
        TransferSummaryDto summary,
        CancellationToken cancellationToken)
    {
        await using var writer = CreateReportFile(path);
        var json = JsonSerializer.Serialize(summary, JsonOptions);
        await writer.WriteAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static StreamWriter CreateReportFile(string path)
    {
        try
        {
            return new StreamWriter(
                new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
                CsvReportWriter.Utf8);
        }
        catch (IOException exception)
        {
            throw new ReportException(
                ReportErrorCodes.ReportFileExists,
                "A report file already exists for this run; report files are never overwritten.",
                exception);
        }
    }

    /// <summary>
    /// Summary DTO with the exact schema field names and order (docs/REPORT_SCHEMA.md).
    /// Nullable timestamps and the URL-mode employee UPN serialize as JSON null.
    /// </summary>
    private sealed class TransferSummaryDto
    {
        public required int ReportSchemaVersion { get; init; }
        public required string RunId { get; init; }
        public required string OperatorUPN { get; init; }
        public required string? EmployeeUPN { get; init; }
        public required string SourceInputMode { get; init; }
        public required string SourceDriveId { get; init; }
        public required string DestinationDisplayPath { get; init; }
        public required DateTimeOffset? ScanCompletedAtUtc { get; init; }
        public required DateTimeOffset CopyStartedAtUtc { get; init; }
        public required DateTimeOffset? CopyCompletedAtUtc { get; init; }
        public required long DiscoveredCount { get; init; }
        public required long FileCount { get; init; }
        public required long FolderCount { get; init; }
        public required long UnsupportedCount { get; init; }
        public required long CompletedCount { get; init; }
        public required long SkippedCount { get; init; }
        public required long FailedCount { get; init; }
        public required long KnownSourceBytes { get; init; }
        public required long DownloadedBytes { get; init; }
        public required int PathWarningCount { get; init; }
        public required int TimestampWarningCount { get; init; }
        public required int StorageWarningCount { get; init; }
        public required int ReconciliationPasses { get; init; }
        public required string FinalRunState { get; init; }
        public required bool IsArchiveComplete { get; init; }
    }
}
