using System.Text.Json;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Reporting;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Reporting;

/// <summary>
/// Verifies per-run audit report generation against docs/REPORT_SCHEMA.md: exact CSV
/// header and row shape from operational state, FailedFiles filtering, summary field
/// presence and the IsArchiveComplete rule, per-run isolation, UTF-8 encoding, and
/// clear failures for unknown or non-terminal runs and existing report files.
/// </summary>
public class ReportWriterTests : IDisposable
{
    private static readonly string[] RequiredSummaryFields =
    [
        "ReportSchemaVersion", "RunId", "OperatorUPN", "EmployeeUPN", "SourceInputMode",
        "SourceDriveId", "DestinationDisplayPath", "ScanCompletedAtUtc", "CopyStartedAtUtc",
        "CopyCompletedAtUtc", "DiscoveredCount", "FileCount", "FolderCount",
        "UnsupportedCount", "CompletedCount", "SkippedCount", "FailedCount",
        "KnownSourceBytes", "DownloadedBytes", "PathWarningCount", "TimestampWarningCount",
        "StorageWarningCount", "ReconciliationPasses", "FinalRunState", "IsArchiveComplete",
    ];

    private readonly ReportTestRig _rig = new();

    public ReportWriterTests() => _rig.OpenStoreAsync().GetAwaiter().GetResult();

    private RunReportRequest Request(string runId, int reconciliationPasses = 1, int storageWarnings = 0) =>
        new(runId, _rig.RootPath, _rig.RootPath,
            ReportTestRig.OperatorUpn, ReportTestRig.EmployeeUpn,
            "Upn", reconciliationPasses, storageWarnings);

    [Fact]
    public async Task GeneratesTheExactReportSetForATerminalRun()
    {
        await SeedMixedArchiveAsync("scan-1");
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Incomplete);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            Request("run-1"), CancellationToken.None);

        var expectedDirectory = Path.Combine(
            _rig.Destination.StateRootPath, "Runs", "run-1");
        Assert.Equal(expectedDirectory, result.ReportDirectoryPath);
        Assert.Equal("run-1", result.RunId);
        Assert.True(File.Exists(result.SummaryJsonPath));
        Assert.True(File.Exists(result.ItemsCsvPath));
        Assert.True(File.Exists(result.FailedItemsCsvPath));
        Assert.Equal(Path.Combine(expectedDirectory, "TransferSummary.json"), result.SummaryJsonPath);
        Assert.Equal(Path.Combine(expectedDirectory, "TransferReport.csv"), result.ItemsCsvPath);
        Assert.Equal(Path.Combine(expectedDirectory, "FailedFiles.csv"), result.FailedItemsCsvPath);
        Assert.Equal(Path.Combine(expectedDirectory, "TransferLog.log"), result.LogPath);
    }

    [Fact]
    public async Task ItemsCsvHasExactHeaderAndOneRowPerItemFromState()
    {
        await SeedMixedArchiveAsync("scan-1");
        var run = await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Incomplete);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            Request("run-1"), CancellationToken.None);

        var lines = (await File.ReadAllLinesAsync(result.ItemsCsvPath))
            .Where(line => line.Length > 0).ToArray();
        Assert.Equal(CsvReportWriter.ItemsHeader, lines[0]);
        // Header plus one row per seeded item (root, file, folder, package, failed).
        Assert.Equal(6, lines.Length);

        var fileRow = lines.Single(line => line.Contains("a.txt", StringComparison.Ordinal));
        var columns = fileRow.Split(',');
        Assert.Equal("1", columns[0]); // ReportSchemaVersion
        Assert.Equal("run-1", columns[1]);
        Assert.Equal(ReportTestRig.OperatorUpn, columns[2]);
        Assert.Equal(ReportTestRig.EmployeeUpn, columns[3]);
        Assert.Equal(ReportTestRig.DriveId, columns[4]);
        Assert.Equal("f1", columns[5]);
        Assert.Equal("folder/a.txt", columns[6]); // SourcePath
        Assert.Equal("folder/a.txt", columns[7]); // LocalPath, destination-relative
        Assert.Equal("File", columns[8]);
        Assert.Equal("12", columns[9]); // SizeBytes
        Assert.Equal("Completed", columns[10]);
        Assert.Equal("1", columns[11]); // AttemptCount
        Assert.Equal("quickXorHash", columns[12]);
        Assert.Equal("sourcehash-f1", columns[13]);
        Assert.Equal("localsha-f1", columns[14]);
        Assert.Equal("Preserved", columns[15]);
        Assert.Equal(string.Empty, columns[16]); // ErrorCode: not persisted in state v1
        Assert.Equal(string.Empty, columns[17]); // ErrorMessage
        Assert.Equal(string.Empty, columns[18]); // StartedAtUtc: not persisted in state v1
        Assert.True(DateTimeOffset.TryParse(columns[19], out _)); // CompletedAtUtc

        Assert.Equal(run.DriveId, columns[4]);
    }

    [Fact]
    public async Task FolderRowsHaveEmptySizeAndRootRowIsSkipped()
    {
        await SeedMixedArchiveAsync("scan-1");
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Incomplete);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            Request("run-1"), CancellationToken.None);

        var lines = await File.ReadAllLinesAsync(result.ItemsCsvPath);
        var folderRow = lines.Single(line => line.Contains(",d1,"));
        var columns = folderRow.Split(',');
        Assert.Equal("Folder", columns[8]);
        Assert.Equal(string.Empty, columns[9]); // SizeBytes empty for folders

        var rootRow = lines.Single(line => line.Contains(",root,"));
        Assert.Equal("Skipped", rootRow.Split(',')[10]);
    }

    [Fact]
    public async Task LocalPathIsNeverAFullServerPath()
    {
        await SeedMixedArchiveAsync("scan-1");
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Incomplete);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            Request("run-1"), CancellationToken.None);

        var content = await File.ReadAllTextAsync(result.ItemsCsvPath);
        Assert.DoesNotContain(_rig.RootPath, content, StringComparison.Ordinal);
        Assert.DoesNotContain(_rig.Destination.ContentRootPath, content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedFilesContainsOnlyFailedAndUnsupportedRowsWithSameSchema()
    {
        await SeedMixedArchiveAsync("scan-1");
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Incomplete);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            Request("run-1"), CancellationToken.None);

        var lines = (await File.ReadAllLinesAsync(result.FailedItemsCsvPath))
            .Where(line => line.Length > 0).ToArray();
        Assert.Equal(CsvReportWriter.ItemsHeader, lines[0]);
        // One Failed file and one Unsupported package; the same column order.
        Assert.Equal(3, lines.Length);
        Assert.Contains(lines, line => line.Split(',')[10] is "Failed");
        Assert.Contains(lines, line => line.Split(',')[10] is "Unsupported");
        Assert.DoesNotContain(lines, line => line.Split(',')[10] is "Completed" or "Skipped");
    }

    [Fact]
    public async Task SummaryContainsEveryRequiredSchemaField()
    {
        await SeedMixedArchiveAsync("scan-1");
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Incomplete);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            Request("run-1", reconciliationPasses: 2, storageWarnings: 1), CancellationToken.None);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(result.SummaryJsonPath));
        var root = document.RootElement;
        foreach (var field in RequiredSummaryFields)
        {
            Assert.True(root.TryGetProperty(field, out _), $"Summary is missing '{field}'.");
        }

        Assert.Equal(1, root.GetProperty("ReportSchemaVersion").GetInt32());
        Assert.Equal("run-1", root.GetProperty("RunId").GetString());
        Assert.Equal(ReportTestRig.OperatorUpn, root.GetProperty("OperatorUPN").GetString());
        Assert.Equal(ReportTestRig.EmployeeUpn, root.GetProperty("EmployeeUPN").GetString());
        Assert.Equal("Upn", root.GetProperty("SourceInputMode").GetString());
        Assert.Equal(ReportTestRig.DriveId, root.GetProperty("SourceDriveId").GetString());
        Assert.Equal(_rig.RootPath, root.GetProperty("DestinationDisplayPath").GetString());
        Assert.Equal(JsonValueKind.String, root.GetProperty("ScanCompletedAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("CopyStartedAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("CopyCompletedAtUtc").ValueKind);
        Assert.Equal("Incomplete", root.GetProperty("FinalRunState").GetString());
        Assert.False(root.GetProperty("IsArchiveComplete").GetBoolean());
        Assert.Equal(2, root.GetProperty("ReconciliationPasses").GetInt32());
        Assert.Equal(1, root.GetProperty("StorageWarningCount").GetInt32());
    }

    [Fact]
    public async Task SummaryCountsComeFromOperationalStateAndScanRecord()
    {
        await SeedMixedArchiveAsync("scan-1");
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Incomplete);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            Request("run-1"), CancellationToken.None);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(result.SummaryJsonPath));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("CompletedCount").GetInt64()); // file and folder
        Assert.Equal(1, root.GetProperty("SkippedCount").GetInt64()); // the drive root
        Assert.Equal(1, root.GetProperty("FailedCount").GetInt64());
        Assert.Equal(1, root.GetProperty("UnsupportedCount").GetInt64());
        Assert.Equal(12, root.GetProperty("DownloadedBytes").GetInt64()); // completed file bytes
        // Scan-record discovery values: file, folder, and the package, 22 known bytes.
        Assert.Equal(2, root.GetProperty("FileCount").GetInt64());
        Assert.Equal(1, root.GetProperty("FolderCount").GetInt64());
        Assert.Equal(4, root.GetProperty("DiscoveredCount").GetInt64());
        Assert.Equal(22, root.GetProperty("KnownSourceBytes").GetInt64());
        // One path warning: the failed item never got a mapped path.
        Assert.Equal(1, root.GetProperty("PathWarningCount").GetInt32());
        Assert.Equal(0, root.GetProperty("TimestampWarningCount").GetInt32());
    }

    [Theory]
    [InlineData(TransferRunState.Completed, true)]
    [InlineData(TransferRunState.CompletedWithWarnings, true)]
    [InlineData(TransferRunState.Incomplete, false)]
    [InlineData(TransferRunState.Failed, false)]
    [InlineData(TransferRunState.Cancelled, false)]
    [InlineData(TransferRunState.Interrupted, false)]
    public async Task IsArchiveCompleteFollowsTheFinalRunStateExactly(
        TransferRunState finalState,
        bool expectedComplete)
    {
        await _rig.SeedScanAsync("scan-1", [_rig.DriveRoot()]);
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", finalState);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            Request("run-1"), CancellationToken.None);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(result.SummaryJsonPath));
        Assert.Equal(finalState.ToString(), document.RootElement.GetProperty("FinalRunState").GetString());
        Assert.Equal(expectedComplete, document.RootElement.GetProperty("IsArchiveComplete").GetBoolean());
    }

    [Fact]
    public async Task TimestampWarningsCountFailedAndUnsupportedPreservation()
    {
        var items = new[]
        {
            _rig.DriveRoot(),
            _rig.Item("f1", name: "a.bin", size: 4),
            _rig.Item("f2", name: "b.bin", size: 4),
        };
        await _rig.SeedScanAsync("scan-1", items);
        await _rig.SkipDriveRootAsync();
        await _rig.CompleteFileAsync("f1", "a.bin", "a.bin", "sha-a",
            TimestampPreservationResult.UnsupportedValue);
        await _rig.CompleteFileAsync("f2", "b.bin", "b.bin", "sha-b",
            TimestampPreservationResult.Preserved);
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.CompletedWithWarnings);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            Request("run-1"), CancellationToken.None);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(result.SummaryJsonPath));
        Assert.Equal(1, document.RootElement.GetProperty("TimestampWarningCount").GetInt32());
        Assert.True(document.RootElement.GetProperty("IsArchiveComplete").GetBoolean());
    }

    [Fact]
    public async Task GenerationForUnknownRunFailsClearly()
    {
        var exception = await Assert.ThrowsAsync<ReportException>(() =>
            _rig.CreateWriter().GenerateRunReportAsync(Request("run-missing"), CancellationToken.None));
        Assert.Equal(ReportErrorCodes.RunNotFound, exception.ReferenceCode);
    }

    [Fact]
    public async Task GenerationForNonTerminalRunFailsClearly()
    {
        await _rig.Store.BeginRunAsync(
            new TransferRunRecord("run-live", ReportTestRig.DriveId, null,
                DateTimeOffset.UtcNow, null, null),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ReportException>(() =>
            _rig.CreateWriter().GenerateRunReportAsync(Request("run-live"), CancellationToken.None));
        Assert.Equal(ReportErrorCodes.RunNotTerminal, exception.ReferenceCode);
    }

    [Fact]
    public async Task SecondGenerationForTheSameRunNeverOverwritesFiles()
    {
        await SeedMixedArchiveAsync("scan-1");
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Incomplete);
        var writer = _rig.CreateWriter();

        var first = await writer.GenerateRunReportAsync(Request("run-1"), CancellationToken.None);
        var firstContent = await File.ReadAllTextAsync(first.ItemsCsvPath);

        var exception = await Assert.ThrowsAsync<ReportException>(() =>
            writer.GenerateRunReportAsync(Request("run-1"), CancellationToken.None));
        Assert.Equal(ReportErrorCodes.ReportFileExists, exception.ReferenceCode);
        Assert.Equal(firstContent, await File.ReadAllTextAsync(first.ItemsCsvPath));
    }

    [Fact]
    public async Task TwoRunsNeverShareOrOverwriteReportFiles()
    {
        await SeedMixedArchiveAsync("scan-1");
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Incomplete);
        await _rig.SeedTerminalRunAsync("run-2", "scan-1", TransferRunState.Completed);
        var writer = _rig.CreateWriter();

        var first = await writer.GenerateRunReportAsync(Request("run-1"), CancellationToken.None);
        var firstCsv = await File.ReadAllTextAsync(first.ItemsCsvPath);
        var firstSummary = await File.ReadAllTextAsync(first.SummaryJsonPath);

        var second = await writer.GenerateRunReportAsync(Request("run-2"), CancellationToken.None);

        Assert.NotEqual(first.ReportDirectoryPath, second.ReportDirectoryPath);
        Assert.Equal(firstCsv, await File.ReadAllTextAsync(first.ItemsCsvPath));
        Assert.Equal(firstSummary, await File.ReadAllTextAsync(first.SummaryJsonPath));
        Assert.Contains("run-2", await File.ReadAllTextAsync(second.ItemsCsvPath), StringComparison.Ordinal);
        Assert.DoesNotContain("run-1", await File.ReadAllTextAsync(second.ItemsCsvPath),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRunReportDirectoryFailsClearlyWhenItAlreadyExists()
    {
        var writer = _rig.CreateWriter();
        await writer.CreateRunReportDirectoryAsync(_rig.RootPath, "run-1", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ReportException>(() =>
            writer.CreateRunReportDirectoryAsync(_rig.RootPath, "run-1", CancellationToken.None));
        Assert.Equal(ReportErrorCodes.ReportDirectoryExists, exception.ReferenceCode);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    public async Task UnsafeRunIdIsRejected(string runId)
    {
        await Assert.ThrowsAsync<ReportException>(() =>
            _rig.CreateWriter().CreateRunReportDirectoryAsync(_rig.RootPath, runId, CancellationToken.None));
    }

    [Fact]
    public async Task ReportsAreUtf8WithoutBomAndPreserveUnicodeNames()
    {
        var arabicName = "تقرير الملف.txt";
        var items = new[]
        {
            _rig.DriveRoot(),
            _rig.Item("f1", name: arabicName, size: 7),
        };
        await _rig.SeedScanAsync("scan-1", items);
        await _rig.SkipDriveRootAsync();
        await _rig.CompleteFileAsync("f1", arabicName, arabicName, "sha-ar");
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Completed);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            Request("run-1"), CancellationToken.None);

        foreach (var path in new[] { result.ItemsCsvPath, result.FailedItemsCsvPath, result.SummaryJsonPath })
        {
            var bytes = await File.ReadAllBytesAsync(path);
            Assert.False(
                bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                $"{Path.GetFileName(path)} must be UTF-8 without a BOM.");
        }

        var csv = await File.ReadAllTextAsync(result.ItemsCsvPath);
        Assert.Contains(arabicName, csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunWithoutScanRecordStillProducesASummaryFromState()
    {
        var items = new[] { _rig.DriveRoot(), _rig.Item("f1", name: "a.bin", size: 5) };
        await _rig.SeedScanAsync("scan-1", items);
        await _rig.SkipDriveRootAsync();
        await _rig.CompleteFileAsync("f1", "a.bin", "a.bin", "sha-a");
        // No scan id on the run: the summary falls back to state-derived values.
        await _rig.SeedTerminalRunAsync("run-1", scanId: null, TransferRunState.Completed);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            Request("run-1"), CancellationToken.None);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(result.SummaryJsonPath));
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Null, root.GetProperty("ScanCompletedAtUtc").ValueKind);
        Assert.Equal(1, root.GetProperty("FileCount").GetInt64());
        Assert.Equal(1, root.GetProperty("FolderCount").GetInt64());
        Assert.Equal(5, root.GetProperty("KnownSourceBytes").GetInt64());
        Assert.True(root.GetProperty("IsArchiveComplete").GetBoolean());
    }

    [Fact]
    public async Task UrlModeSourceWritesNullEmployeeUpnInSummaryAndEmptyInCsv()
    {
        await _rig.SeedScanAsync("scan-1", [_rig.DriveRoot()]);
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Completed);
        var request = Request("run-1") with { EmployeeUpn = null, SourceInputMode = "OneDriveRootUrl" };

        var result = await _rig.CreateWriter().GenerateRunReportAsync(request, CancellationToken.None);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(result.SummaryJsonPath));
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("EmployeeUPN").ValueKind);
        var csvLines = await File.ReadAllLinesAsync(result.ItemsCsvPath);
        Assert.Equal(string.Empty, csvLines[1].Split(',')[3]);
    }

    /// <summary>
    /// Seeds a scan with the drive root, one completed file (12 bytes), one completed
    /// folder, one unsupported package (10 bytes), and one path-failed file.
    /// </summary>
    private async Task SeedMixedArchiveAsync(string scanId)
    {
        var items = new[]
        {
            _rig.DriveRoot(),
            _rig.Item("f1", parentId: "d1", name: "a.txt", size: 12),
            _rig.Item("d1", name: "folder", classification: ItemFacetClassification.Folder, size: null),
            _rig.Item("p1", name: "note.one", classification: ItemFacetClassification.UnsupportedPackage,
                size: 10),
            _rig.Item("f2", name: "broken file.txt", size: 10),
        };
        await _rig.SeedScanAsync(scanId, items);
        await _rig.SkipDriveRootAsync();
        await _rig.CompleteFileAsync("f1", "folder/a.txt", "folder/a.txt", "localsha-f1");
        await _rig.Store.UpdateItemPathsAsync("d1", "folder", "folder",
            TransferItemState.Mapped, CancellationToken.None);
        await _rig.Store.MarkItemCompletedAsync("d1",
            TimestampPreservationResult.Preserved, CancellationToken.None);
        await _rig.MarkUnsupportedAsync("p1", "note.one");
        await _rig.FailAtMappingAsync("f2", "broken file.txt");
    }

    public void Dispose() => _rig.Dispose();
}
