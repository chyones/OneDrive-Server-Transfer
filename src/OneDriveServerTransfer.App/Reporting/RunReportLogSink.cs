using System.Globalization;
using System.IO;
using OneDriveServerTransfer.Authentication;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace OneDriveServerTransfer.Reporting;

/// <summary>
/// The per-run technical log sink behind TransferLog.log (docs/REPORT_SCHEMA.md). The
/// copy-run orchestration opens the sink on the run's report directory at run start
/// and closes it at run end, so the run's Serilog events land in that run's protected
/// report directory. Only one run can be active at a time; the destination lock
/// already guarantees this for real runs.
///
/// The log lives inside _TransferReport and may contain protected identifiers, but
/// never tokens, temporary download URLs, or raw Graph responses: producers sanitize
/// before logging, and this sink applies the authentication redaction helper to every
/// rendered event as defense-in-depth.
/// </summary>
public sealed class RunReportLogSink : ILogEventSink
{
    private const string OutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    private static readonly MessageTemplateTextFormatter Formatter =
        new(OutputTemplate, CultureInfo.InvariantCulture);

    private readonly object _gate = new();
    private StreamWriter? _writer;

    /// <summary>The log file currently receiving events, or null between runs.</summary>
    public string? ActiveLogPath { get; private set; }

    /// <summary>
    /// Starts a run log at the given path. Fails when a run log is already open or the
    /// file already exists: a run log is never appended to or overwritten.
    /// </summary>
    public void BeginRun(string logPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);

        lock (_gate)
        {
            if (_writer is not null)
            {
                throw new ReportException(
                    ReportErrorCodes.ReportFileExists,
                    "A run log is already active; only one copy run can write a log at a time.");
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(logPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // FileMode.CreateNew: an existing TransferLog.log belongs to another run
            // and must never be overwritten or appended to.
            var stream = new FileStream(logPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            try
            {
                _writer = new StreamWriter(stream, CsvReportWriter.Utf8) { AutoFlush = true };
            }
            catch
            {
                stream.Dispose();
                throw;
            }

            ActiveLogPath = logPath;
        }
    }

    /// <summary>Closes the active run log, if any. Idempotent.</summary>
    public void EndRun()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
            ActiveLogPath = null;
        }
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        lock (_gate)
        {
            if (_writer is null)
            {
                return;
            }

            var buffer = new StringWriter(CultureInfo.InvariantCulture);
            Formatter.Format(logEvent, buffer);
            _writer.WriteLine(AuthErrorSanitizer.RedactSensitiveText(buffer.ToString().TrimEnd()));
        }
    }
}
