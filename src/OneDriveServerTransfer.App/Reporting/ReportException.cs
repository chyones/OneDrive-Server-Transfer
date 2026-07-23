namespace OneDriveServerTransfer.Reporting;

/// <summary>
/// A report-generation failure with a stable reference code. Report failures never
/// change the persisted run outcome; the orchestration logs them and continues.
/// </summary>
public sealed class ReportException : Exception
{
    public ReportException(string referenceCode, string message, Exception? innerException = null)
        : base($"{referenceCode}: {message}", innerException)
    {
        ReferenceCode = referenceCode;
    }

    public string ReferenceCode { get; }
}

/// <summary>Stable reference codes for report-generation failures.</summary>
public static class ReportErrorCodes
{
    /// <summary>The per-run report directory already exists.</summary>
    public const string ReportDirectoryExists = "RPT-DIR-001";

    /// <summary>The run has no record in the operational state store.</summary>
    public const string RunNotFound = "RPT-RUN-001";

    /// <summary>The run has not reached a terminal state, so no final report exists.</summary>
    public const string RunNotTerminal = "RPT-RUN-002";

    /// <summary>A report file for the run already exists and is never overwritten.</summary>
    public const string ReportFileExists = "RPT-FILE-001";
}
