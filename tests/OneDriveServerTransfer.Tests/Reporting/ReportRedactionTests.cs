using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Reporting;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Reporting;

/// <summary>
/// The report redaction guard (docs/REPORT_SCHEMA.md, "Error redaction" and
/// docs/EVIDENCE_POLICY.md "Security"): token-like strings, authorization headers,
/// temporary download URLs, raw Graph response bodies, and stack traces must never
/// reach the CSV or JSON report outputs, and untrusted source values must be
/// formula-injection neutralized and correctly escaped.
/// </summary>
public class ReportRedactionTests : IDisposable
{
    private const string Jwt =
        "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.dBjftJeZ4CVPmB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    private const string TemporaryUrl =
        "https://contoso-my.sharepoint.com/personal/e/_layouts/15/download.aspx" +
        "?UniqueId=abc123&AuthToken=tempsupersecret";

    private readonly ReportTestRig _rig = new();

    public ReportRedactionTests() => _rig.OpenStoreAsync().GetAwaiter().GetResult();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizerKeepsEmptyMessagesEmpty(string? input) =>
        Assert.Equal(string.Empty, ReportTextSanitizer.SanitizeErrorMessage(input));

    [Fact]
    public void SanitizerRedactsTokensBearerAndTemporaryDownloadUrls()
    {
        var sanitized = ReportTextSanitizer.SanitizeErrorMessage(
            $"download failed for {TemporaryUrl} with {Jwt} and Bearer abcdef1234567890token");

        Assert.DoesNotContain("tempsupersecret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthToken", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain(Jwt, sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("abcdef1234567890token", sanitized, StringComparison.Ordinal);
        Assert.Contains("[redacted]", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizerRedactsJsonCredentialFieldsFromGraphLikeBodies()
    {
        var sanitized = ReportTextSanitizer.SanitizeErrorMessage(
            "Graph error {\"error\":{\"code\":\"itemNotFound\"},\"access_token\":\"tok-abc-123\"}");

        Assert.DoesNotContain("tok-abc-123", sanitized, StringComparison.Ordinal);
        Assert.Contains("[redacted]", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizerCollapsesMultiLinePayloadsAndStackTracesToOneLine()
    {
        var sanitized = ReportTextSanitizer.SanitizeErrorMessage(
            "The download failed.\n   at OneDrive.Http.Send()\n   at OneDrive.Run()\r\n{\"raw\":\"body\"}");

        Assert.Equal("The download failed.", sanitized);
        Assert.DoesNotContain('\n', sanitized);
        Assert.DoesNotContain('\r', sanitized);
    }

    [Fact]
    public void ErrorMessageCellNeverCarriesForbiddenValuesIntoTheCsv()
    {
        var run = new TransferRunRecord("run-1", ReportTestRig.DriveId, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TransferRunState.Failed);
        var request = new RunReportRequest("run-1", _rig.RootPath, _rig.RootPath,
            ReportTestRig.OperatorUpn, ReportTestRig.EmployeeUpn, "Upn", 1, 0);
        var item = new TransferItemRecord(
            ReportTestRig.DriveId, "f1", "root", "a.txt", "a.txt", "a.txt",
            ItemFacetClassification.File, null, null, 5, null, null, null, null, null,
            TransferItemState.Failed, 1, TimestampPreservationResult.NotAttempted,
            null, DateTimeOffset.UtcNow);

        var row = ReportWriter.BuildRow(run, request, item,
            errorCode: "TRF-DL-001",
            errorMessage: $"failed {TemporaryUrl}\n   at Stack.Trace()\n{Jwt}");

        var writer = new StringWriter();
        CsvReportWriter.WriteRow(writer, row);
        var csv = writer.ToString();

        Assert.DoesNotContain("tempsupersecret", csv, StringComparison.Ordinal);
        Assert.DoesNotContain(Jwt, csv, StringComparison.Ordinal);
        Assert.DoesNotContain("at Stack.Trace()", csv, StringComparison.Ordinal);
        Assert.Equal("TRF-DL-001", row[16]);
    }

    [Fact]
    public async Task UntrustedSourceNamesAreNeutralizedAndEscapedEndToEnd()
    {
        var items = new[]
        {
            _rig.DriveRoot(),
            _rig.Item("f1", name: "=HYPERLINK(\"http://evil.test\",\"click\").txt", size: 3),
            _rig.Item("f2", name: "comma, name.txt", size: 4),
            _rig.Item("f3", name: "quote\"name.txt", size: 5),
        };
        await _rig.SeedScanAsync("scan-1", items);
        await _rig.SkipDriveRootAsync();
        await _rig.CompleteFileAsync("f1",
            "=HYPERLINK(\"http://evil.test\",\"click\").txt",
            "_x0052_namesafe.txt", "sha-1");
        await _rig.CompleteFileAsync("f2", "comma, name.txt", "comma, name.txt", "sha-2");
        await _rig.CompleteFileAsync("f3", "quote\"name.txt", "quote\"name.txt", "sha-3");
        await _rig.SeedTerminalRunAsync("run-1", "scan-1", TransferRunState.Completed);

        var result = await _rig.CreateWriter().GenerateRunReportAsync(
            new RunReportRequest("run-1", _rig.RootPath, _rig.RootPath,
                ReportTestRig.OperatorUpn, ReportTestRig.EmployeeUpn, "Upn", 1, 0),
            CancellationToken.None);

        var csv = await File.ReadAllTextAsync(result.ItemsCsvPath);

        // The formula-leading source path is apostrophe-neutralized and quote-escaped.
        Assert.Contains(
            "\"'=HYPERLINK(\"\"http://evil.test\"\",\"\"click\"\").txt\"",
            csv, StringComparison.Ordinal);
        // Comma and quote names are RFC 4180 escaped.
        Assert.Contains("\"comma, name.txt\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"quote\"\"name.txt\"", csv, StringComparison.Ordinal);
        // No cell begins a line with a formula trigger from untrusted content.
        foreach (var line in csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.False(line.StartsWith("=", StringComparison.Ordinal));
            Assert.False(line.StartsWith("@", StringComparison.Ordinal));
        }
    }

    public void Dispose() => _rig.Dispose();
}
