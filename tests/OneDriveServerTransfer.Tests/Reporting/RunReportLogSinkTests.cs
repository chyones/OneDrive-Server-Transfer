using OneDriveServerTransfer.Reporting;
using Serilog;
using Serilog.Core;

namespace OneDriveServerTransfer.Tests.Reporting;

/// <summary>
/// Verifies the per-run technical log sink: one active run at a time, events land in
/// that run's TransferLog.log, the file is never overwritten or appended, and token,
/// temporary-URL, and credential material is redacted from every rendered event.
/// </summary>
public class RunReportLogSinkTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"odst-log-{Guid.NewGuid():N}");

    private string LogPath => Path.Combine(_directory, "TransferLog.log");

    [Fact]
    public void RunEventsLandInTheRunLogFile()
    {
        var sink = new RunReportLogSink();
        using (var logger = CreateLogger(sink))
        {
            sink.BeginRun(LogPath);
            logger.Information("Run started; state={State}", "InProgress");
            logger.Warning("A {Kind} warning", "storage");
            sink.EndRun();
        }

        var content = File.ReadAllText(LogPath);
        Assert.Contains("Run started; state=InProgress", content, StringComparison.Ordinal);
        Assert.Contains("[INF]", content, StringComparison.Ordinal);
        Assert.Contains("[WRN]", content, StringComparison.Ordinal);
        Assert.Contains("A storage warning", content, StringComparison.Ordinal);
        Assert.Null(sink.ActiveLogPath);
    }

    [Fact]
    public void EventsAreDroppedBetweenRuns()
    {
        var sink = new RunReportLogSink();
        using var logger = CreateLogger(sink);
        logger.Information("before any run");

        sink.BeginRun(LogPath);
        logger.Information("during the run");
        sink.EndRun();
        logger.Information("after the run");

        var content = File.ReadAllText(LogPath);
        Assert.DoesNotContain("before any run", content, StringComparison.Ordinal);
        Assert.Contains("during the run", content, StringComparison.Ordinal);
        Assert.DoesNotContain("after the run", content, StringComparison.Ordinal);
    }

    [Fact]
    public void BeginRunWhileAnotherRunIsActiveFailsClearly()
    {
        var sink = new RunReportLogSink();
        sink.BeginRun(LogPath);
        try
        {
            var exception = Assert.Throws<ReportException>(() =>
                sink.BeginRun(Path.Combine(_directory, "second.log")));
            Assert.Equal(ReportErrorCodes.ReportFileExists, exception.ReferenceCode);
        }
        finally
        {
            sink.EndRun();
        }
    }

    [Fact]
    public void ExistingLogFileIsNeverOverwrittenOrAppended()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(LogPath, "previous run content");

        var sink = new RunReportLogSink();
        Assert.Throws<IOException>(() => sink.BeginRun(LogPath));
        Assert.Equal("previous run content", File.ReadAllText(LogPath));
    }

    [Fact]
    public void EndRunIsIdempotent()
    {
        var sink = new RunReportLogSink();
        sink.EndRun();
        sink.BeginRun(LogPath);
        sink.EndRun();
        sink.EndRun();

        Assert.Null(sink.ActiveLogPath);
    }

    [Fact]
    public void TokensTemporaryUrlsAndCredentialsAreRedactedFromEveryEvent()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.dBjftJeZ4CVPmB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string temporaryUrl =
            "https://contoso-my.sharepoint.com/personal/e/_layouts/15/download.aspx" +
            "?UniqueId=abc123&AuthToken=tempsupersecret";
        const string bearer = "Bearer abcdef1234567890token";

        var sink = new RunReportLogSink();
        using (var logger = CreateLogger(sink))
        {
            sink.BeginRun(LogPath);
            logger.Error("failure {Detail} {Url} {Auth}", jwt, temporaryUrl, bearer);
            logger.Error("assignment access_token=secretvalue123 happened");
            sink.EndRun();
        }

        var content = File.ReadAllText(LogPath);
        Assert.DoesNotContain(jwt, content, StringComparison.Ordinal);
        Assert.DoesNotContain("tempsupersecret", content, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthToken", content, StringComparison.Ordinal);
        Assert.DoesNotContain(bearer, content, StringComparison.Ordinal);
        Assert.DoesNotContain("secretvalue123", content, StringComparison.Ordinal);
        Assert.Contains("[redacted]", content, StringComparison.Ordinal);
    }

    [Fact]
    public void LogFileIsUtf8AndPreservesUnicode()
    {
        var sink = new RunReportLogSink();
        using (var logger = CreateLogger(sink))
        {
            sink.BeginRun(LogPath);
            logger.Information("نسخ الملف {Name}", "تقرير.txt");
            sink.EndRun();
        }

        var bytes = File.ReadAllBytes(LogPath);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Contains("تقرير.txt", File.ReadAllText(LogPath), StringComparison.Ordinal);
    }

    private static Logger CreateLogger(RunReportLogSink sink) =>
        new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(sink)
            .CreateLogger();

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
