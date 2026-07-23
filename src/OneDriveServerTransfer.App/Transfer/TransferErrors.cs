namespace OneDriveServerTransfer.Transfer;

/// <summary>
/// Stable reference codes for user-facing transfer errors. Codes are part of the
/// user-support contract and must never be renumbered.
/// </summary>
public static class TransferErrorCodes
{
    public const string ScanNotCurrent = "TRF-GATE-001";
    public const string OperatorSessionRequired = "TRF-GATE-002";
    public const string OperatorTenantMismatch = "TRF-GATE-003";
    public const string SourceAccessDenied = "TRF-SRC-001";
    public const string SourceNotFound = "TRF-SRC-002";
    public const string ServiceUnavailable = "TRF-SRC-003";
    public const string Throttled = "TRF-SRC-004";
    public const string UnexpectedResponse = "TRF-SRC-005";
    public const string InsufficientStorage = "TRF-DISK-001";
}

/// <summary>
/// A user-facing transfer error with a short title, plain-language explanation,
/// corrective action, and stable reference code. It never carries tenant IDs, drive
/// IDs, tokens, temporary download URLs, employee content, or raw Graph responses in
/// its user-facing fields.
/// </summary>
public sealed class TransferException : Exception
{
    public TransferException(
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

/// <summary>Centralized builders for user-facing transfer errors.</summary>
public static class TransferErrors
{
    public static TransferException ScanNotCurrent(Exception? inner = null) => new(
        TransferErrorCodes.ScanNotCurrent,
        "A current scan is required before copying",
        "The copy can only start after a successful scan of the current employee source and destination. The previous scan is missing or no longer current.",
        "Run the scan again, review the summary, and then start the copy.",
        inner);

    public static TransferException OperatorSessionRequired(Exception? inner = null) => new(
        TransferErrorCodes.OperatorSessionRequired,
        "Sign-in is required",
        "The transfer operator session expired or is no longer valid, so the copy cannot continue safely.",
        "Sign in again with the authorized transfer account and retry the operation.",
        inner);

    public static TransferException OperatorTenantMismatch(Exception? inner = null) => new(
        TransferErrorCodes.OperatorTenantMismatch,
        "The signed-in account does not match this archive",
        "The signed-in operator belongs to a different organization than the employee source this destination is bound to.",
        "Sign in with the authorized transfer account for the correct organization.",
        inner);

    public static TransferException SourceAccessDenied(Exception? inner = null) => new(
        TransferErrorCodes.SourceAccessDenied,
        "Access to the employee OneDrive was denied",
        "Microsoft 365 refused read access to the employee OneDrive. The transfer account may no longer have the required access.",
        "Confirm the transfer account still has access to the employee OneDrive, then try again.",
        inner);

    public static TransferException SourceNotFound(Exception? inner = null) => new(
        TransferErrorCodes.SourceNotFound,
        "The employee OneDrive could not be found",
        "Microsoft 365 no longer returns the employee OneDrive this destination is bound to.",
        "Confirm the employee OneDrive still exists and that the transfer account has access, then run a new scan.",
        inner);

    public static TransferException ServiceUnavailable(Exception? inner = null) => new(
        TransferErrorCodes.ServiceUnavailable,
        "Microsoft 365 is temporarily unavailable",
        "The copy stopped because the service could not be reached reliably.",
        "Wait a few minutes and retry the operation. Completed files are kept.",
        inner);

    public static TransferException Throttled(Exception? inner = null) => new(
        TransferErrorCodes.Throttled,
        "Microsoft 365 is limiting requests",
        "The copy stopped because the service asked the application to slow down for an extended period.",
        "Wait and retry the operation later. Completed files are kept.",
        inner);

    public static TransferException UnexpectedResponse(Exception? inner = null) => new(
        TransferErrorCodes.UnexpectedResponse,
        "The service returned an unexpected response",
        "The copy stopped because Microsoft 365 returned a response the application cannot safely use.",
        "Try again. If the problem persists, contact IT support and quote the reference code.",
        inner);

    public static TransferException InsufficientStorage(Exception? inner = null) => new(
        TransferErrorCodes.InsufficientStorage,
        "The destination does not have enough free space",
        "The destination free space does not exceed the remaining source size plus the required 5 GiB safety reserve.",
        "Free up space on the destination drive or choose a larger destination, then retry. Completed files are kept.",
        inner);
}
