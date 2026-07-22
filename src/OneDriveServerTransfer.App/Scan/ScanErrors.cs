namespace OneDriveServerTransfer.Scan;

/// <summary>
/// Stable reference codes for user-facing scan errors. Codes are part of the
/// user-support contract and must never be renumbered.
/// </summary>
public static class ScanErrorCodes
{
    public const string OperatorSessionRequired = "SCAN-AUTH-001";
    public const string OperatorTenantMismatch = "SCAN-AUTH-002";
    public const string DeltaResetRequired = "SCAN-DELTA-001";
    public const string SourceAccessDenied = "SCAN-GRAPH-001";
    public const string SourceNotFound = "SCAN-GRAPH-002";
    public const string Throttled = "SCAN-GRAPH-003";
    public const string ServiceUnavailable = "SCAN-GRAPH-004";
    public const string UnexpectedResponse = "SCAN-GRAPH-005";
    public const string Cancelled = "SCAN-CANCEL-001";
}

/// <summary>
/// A user-facing scan error with a short title, plain-language explanation, corrective
/// action, and stable reference code. It never carries tenant IDs, drive IDs, tokens,
/// paging URLs, raw Graph responses, or protected database values in its user-facing
/// fields.
/// </summary>
public sealed class ScanException : Exception
{
    public ScanException(
        string referenceCode,
        string title,
        string explanation,
        string correctiveAction,
        Exception? innerException = null)
        : base($"{referenceCode}: {title}", innerException)
    {
        ReferenceCode = referenceCode;
        Title = title;
        Explanation = explanation;
        CorrectiveAction = correctiveAction;
    }

    public string ReferenceCode { get; }

    public string Title { get; }

    public string Explanation { get; }

    public string CorrectiveAction { get; }
}

/// <summary>Centralized builders for user-facing scan errors.</summary>
public static class ScanErrors
{
    public static ScanException OperatorSessionRequired(Exception? inner = null) => new(
        ScanErrorCodes.OperatorSessionRequired,
        "You are not signed in",
        "The scan requires an authorized signed-in transfer account, and the current session is no longer available.",
        "Sign in again with the authorized transfer account, then run the scan.",
        inner);

    public static ScanException OperatorTenantMismatch(Exception? inner = null) => new(
        ScanErrorCodes.OperatorTenantMismatch,
        "The signed-in account does not match this source",
        "The signed-in account belongs to a different organization than the employee OneDrive being scanned.",
        "Sign in with the authorized transfer account for the same organization, then run the scan again.",
        inner);

    public static ScanException DeltaResetRequired(Exception? inner = null) => new(
        ScanErrorCodes.DeltaResetRequired,
        "The scan must start over",
        "Microsoft invalidated the saved inventory position, so the partial scan cannot continue safely.",
        "Run the scan again. It will restart from the beginning automatically.",
        inner);

    public static ScanException SourceAccessDenied(Exception? inner = null) => new(
        ScanErrorCodes.SourceAccessDenied,
        "The source cannot be accessed",
        "The signed-in transfer account does not have permission to read the employee OneDrive, or access was removed.",
        "Confirm the transfer account still has access to the employee OneDrive, then run the scan again.",
        inner);

    public static ScanException SourceNotFound(Exception? inner = null) => new(
        ScanErrorCodes.SourceNotFound,
        "The source no longer exists",
        "The employee OneDrive could not be found. It may have been deleted or reprovisioned.",
        "Confirm the employee OneDrive still exists, resolve the source again, then run the scan.",
        inner);

    public static ScanException Throttled(Exception? inner = null) => new(
        ScanErrorCodes.Throttled,
        "The service is busy",
        "Microsoft 365 is limiting requests right now, so the scan could not complete.",
        "Wait a few minutes, then run the scan again.",
        inner);

    public static ScanException ServiceUnavailable(Exception? inner = null) => new(
        ScanErrorCodes.ServiceUnavailable,
        "The service is temporarily unavailable",
        "The scan could not reach Microsoft 365 or the service returned a temporary failure.",
        "Check the network connection, then run the scan again.",
        inner);

    public static ScanException UnexpectedResponse(Exception? inner = null) => new(
        ScanErrorCodes.UnexpectedResponse,
        "The scan could not read the source",
        "Microsoft returned a response the application cannot safely interpret, so the scan was stopped.",
        "Run the scan again. If the problem persists, contact IT support and quote the reference code.",
        inner);

    public static ScanException Cancelled(Exception? inner = null) => new(
        ScanErrorCodes.Cancelled,
        "The scan was cancelled",
        "The dry run was stopped before it completed, so copying remains unavailable.",
        "Run the scan again when you are ready.",
        inner);
}
